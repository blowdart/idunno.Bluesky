# Writing a durable bot

In the [watcher bot tutorial](watcherBot.md), we wrote a bot that listens to the live Jetstream and reacts to posts in real time. But what happens if your application crashes while replying to a post? What if the network drops right after you send an HTTP request to the PDS, or your service restarts while catching up on missed events? You don't want your bot to post duplicate replies, lose its place, or get banned by rate limiters.

The `idunno.Bluesky.BotSdk` library provides a durable bot runtime. It listens to the Jetstream v2 event stream, saves complete checkpoints and queued actions to a database, protects against duplicate posts, handles rate limiting and retries, and catches up smoothly after restarts.

For this example, we're going to write a simple reply bot that watches for posts containing `#hello` and posts a friendly reply.

> [!NOTE]
> You must already have created an account for your bot to run as, and generated an [app password](https://bsky.app/settings/app-passwords) for that account. If you plan to replay past events from the Jetstream archive when recovering from downtime, you will also need an [archive API key](https://bsky.network/account#api-keys-section-heading).

[!include[Untrusted data warning](../includes/untrusted-data-warning.md)]

## Prerequisites

Before starting, ensure you have a supported [.NET SDK](https://dotnet.microsoft.com/download) installed (.NET 8.0, 9.0, or 10.0). You can check your installed version by running:

```PowerShell
dotnet --version
```

## Create a .NET project and add the NuGet packages

Let's start by creating an ASP.NET Core project for our bot and adding the `idunno.Bluesky.BotSdk` packages. We use the ASP.NET Core Web host so the bot can expose health and metrics endpoints for monitoring.

# [Command Line](#tab/commandLine)

1. In a console window, run the following commands:
   ```PowerShell
   dotnet new web -n SimpleReplyBot
   cd SimpleReplyBot
   dotnet add package idunno.Bluesky.BotSdk
   dotnet add package idunno.Bluesky.BotSdk.SQLite
   ```

# [Visual Studio](#tab/visualStudio)

1. Create a new project by opening the **File** menu and choosing **New ▶ Project**.
1. In the **Create a new project** dialog, select **C#** as the language, choose **ASP.NET Core Empty** as the project template, and click **Next**.
1. In the **Configure your new project** dialog, name the project `SimpleReplyBot` and click **Next**.
1. In the **Additional information** dialog, choose **.NET 8.0** or newer, uncheck "Do not use top-level statements", and click **Create**.
1. Under the **Project** menu, select **Manage NuGet Packages**, switch to the **Browse** tab, search for and install `idunno.Bluesky.BotSdk` and `idunno.Bluesky.BotSdk.SQLite`.
1. Close the **Manage NuGet Packages** tab.

# [Visual Studio Code](#tab/vsCode)

1. Open the Command Palette (**Ctrl + Shift + P**), search for and select **.NET: New Project...**.
1. Select **ASP.NET Core Empty**.
1. Choose the folder where you want to save the project.
1. Name the project `SimpleReplyBot` and press **Enter**.
1. Open the integrated terminal in Visual Studio Code and add the packages:
   ```PowerShell
   dotnet add package idunno.Bluesky.BotSdk
   dotnet add package idunno.Bluesky.BotSdk.SQLite
   ```

***

### Choosing a storage provider

The runtime supports several storage providers depending on your hosting environment:

| Provider package | Registration method | Description |
| --- | --- | --- |
| `idunno.Bluesky.BotSdk.SQLite` | `.WithSqliteStorage(connectionString)` | Uses a local SQLite database file. Great for single-instance bots running on a persistent disk or VM. |
| `idunno.Bluesky.BotSdk.PostgreSQL` | `.WithPostgreSqlStorage(connectionString)` | Production-grade PostgreSQL storage. Recommended for multi-container and cloud deployments. |
| `idunno.Bluesky.BotSdk.MySQL` | `.WithMySqlStorage(connectionString)` | Production-grade MySQL (version 8.4+) storage. |
| Core package | `.WithInMemoryStorage()` | In-memory storage for development and unit testing. |

> [!WARNING]
> The in-memory provider (`.WithInMemoryStorage()`) keeps everything in RAM. It logs a warning at startup and loses all state when the application stops. It is useful for testing, but should not be used in production.

## How the durable runtime works

The durable runtime splits bot operations into two distinct steps:

1. **Ingestion and policy evaluation**: A reader worker streams events from the Jetstream. Whenever an event matches your policy, the policy returns one or more *action intents* (such as a reply, like, or repost). The runtime saves these intents and your stream progress into the database in a single transaction before advancing.
2. **Action dispatch**: A separate sender worker picks up pending actions from the database outbox, checks rate limits, and sends them to the network. If a network call fails or times out, the action remains in the database to be retried safely.

Because your policy never makes network calls directly, a network failure or sudden crash will never cause your bot to lose track of what it was doing.

### Live streaming and automatic archive replay fallback

When your bot starts up:

1. **Live stream first**: The bot connects directly to the live WebSocket Jetstream feed and evaluates incoming events in real time.
2. **Short disconnections**: If the bot restarts or briefly loses connection, it reconnects to the live stream starting from its last committed sequence number. As long as the missed events are still within the Jetstream server's live buffer (usually a few minutes), streaming resumes with zero missed events.
3. **Automatic replay fallback**: If the bot was offline longer than the live buffer window, the live connection returns `CursorTooOld`. Instead of failing or skipping missed history, the runtime **automatically falls back to Jetstream archive replay**:
   - It queries the Jetstream v2 archive service for sealed snapshot segments covering the downtime.
   - It replays and evaluates events sequentially and transactionally, advancing `ProcessedThrough` progress as each batch is saved.
   - Once it catches up to the live tip, it automatically switches back to the live Jetstream feed!

All of this happens automatically without any manual intervention.

## Writing a bot policy

Your bot logic lives in a class that implements `IBotPolicy`. The policy has a single method, `Evaluate()`, which evaluates each incoming `JetstreamEvent` and returns a list of `BotAction` objects to execute.

Let's create `SimpleReplyBotPolicy.cs`:

```csharp
using idunno.AtProto;
using idunno.AtProto.Jetstream;
using idunno.AtProto.Repo;
using idunno.Bluesky;
using idunno.Bluesky.BotSdk;
using idunno.Bluesky.Feed;

namespace SimpleReplyBot;

public sealed class SimpleReplyBotPolicy : IBotPolicy
{
    public IReadOnlyList<BotAction> Evaluate(JetstreamEvent item, DateTimeOffset startedAt)
    {
        // Only inspect new post creations from the Jetstream
        if (item is not JetstreamCommitEvent
            {
                Sequence: { } sequence,
                Commit:
                {
                    Operation: JetstreamCommitOperation.Create,
                    Record: not null,
                    Cid: { } cid
                }
            } commitEvent ||
            commitEvent.Commit.Collection != "app.bsky.feed.post")
        {
            return [];
        }

        // Deserialize the post record from the event
        Post? post = commitEvent.Commit.Record.Value.Deserialize<Post>(BlueskyJsonSerializerOptions.Options);
        if (post is null)
        {
            return [];
        }

        // Check if the post contains our trigger word
        if (!post.Text.Contains("#hello", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        // Identify the post we are replying to
        StrongReference sourcePost = new(
            new AtUri($"at://{commitEvent.Did}/app.bsky.feed.post/{commitEvent.Commit.RKey}"),
            cid);

        // Set up the reply thread references (preserve the existing root if in a thread)
        ReplyReferences replyReferences = post.Reply is not null
            ? new ReplyReferences(root: post.Reply.Root, parent: sourcePost)
            : new ReplyReferences(root: sourcePost, parent: sourcePost);

        // Create the reply post
        Post reply = new(
            text: "Hello! Thanks for saying hi on Bluesky! 👋",
            createdAt: DateTimeOffset.UtcNow,
            reply: replyReferences);

        // Build and return the durable reply action
        return [BotActions.Reply(sourcePost, sequence, reply)];
    }
}
```

Notice a few key details:
- **No network calls**: The policy does not call the Bluesky API. It simply inspects the post and returns the intent.
- **Thread continuity**: We check `post.Reply` to preserve the root of an existing conversation so our reply appears properly nested.
- **Factory helper**: `BotActions.Reply()` bundles the target post, the event sequence number, and the reply post into a durable `BotAction`.

### Other built-in actions

The SDK provides factory methods in `BotActions` for common Bluesky operations:
- `BotActions.Reply(source, sequence, reply)`: Replies to a post.
- `BotActions.Like(source, sequence)`: Likes a post.
- `BotActions.Repost(source, sequence)`: Reposts a post.
- `BotActions.Bookmark(source, sequence)`: Saves a private bookmark.
- `BotActions.AddAuthorToList(source, sequence, listUri)`: Adds the post's author to an existing bot-owned curation list.
- `BotActions.Create<TPayload>(kind, source, sequence, payload, metadata)`: Enqueues a custom application-defined action for your own registered `IBotActionHandler`.

For a complete payload, policy, handler, and registration example, see [Writing a custom bot action](customBotActions.md). It also explains how to reconcile uncertain attempts and keep retries duplicate-safe.

## Configuring the bot

Next, let's configure your bot settings. Add the following to your `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Bot": "Data Source=bot.db"
  },
  "Bot": {
    "DeveloperMode": true,
    "Did": "did:plc:your-bot-did",
    "Handle": "your-bot.bsky.social",
    "JetstreamService": "wss://jetstream.us-west.bsky.network"
  }
}
```

> [!TIP]
> `DeveloperMode` is enabled by default. In developer mode, the runtime logs and simulates outbound actions without actually publishing records to Bluesky. This is great for testing your bot against live posts safely! Set `"DeveloperMode": false` when you're ready to post live.

### Storing secrets safely

Sensitive credentials should not be checked into source control. Use the [.NET User Secrets tool](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) during development:

```PowerShell
dotnet user-secrets init
dotnet user-secrets set "Bot:AppPassword" "<your-app-password>"
dotnet user-secrets set "Bot:ArchiveApiKey" "<your-archive-api-key>"
dotnet user-secrets set "Bot:CredentialEncryptionKey" "<base64-32-byte-key>"
```

> [!NOTE]
> `CredentialEncryptionKey` is a base64-encoded 32-byte key used to encrypt cached authentication tokens in the database. You can generate one in PowerShell using:
> ```PowerShell
> $key = New-Object byte[] 32
> $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
> try {
>     $rng.GetBytes($key)
>     [Convert]::ToBase64String($key)
> }
> finally {
>     $rng.Dispose()
>     [Array]::Clear($key, 0, $key.Length)
> }
> ```

Store the generated key in User Secrets during development or a deployment secret store in production. Do not put the app password, archive API key, or encryption key in tracked configuration files. Keep the encryption key stable: replacing it without migrating stored credentials makes those credentials unreadable.

### Recovering from an encryption key change

If the encryption key no longer matches the stored credentials, the next attempt to restore them fails cryptographic authentication. The sender logs the failure, marks itself unhealthy, and throws; the host stops under the SDK's configured background-service exception policy. It does not automatically discard the credentials or log in with the app password, because the same failure could indicate corrupted storage or tampering.

Restoring the original key is the simplest recovery. If it is unavailable, you can deliberately clear the saved credentials and let the bot create a fresh session:

1. Stop every bot instance using this database, and pause any automatic restarts. Back up the database before changing it. If the key changed unexpectedly, investigate possible configuration errors or tampering before proceeding.
2. Confirm you are connected to the affected bot's database. Clear **only** the `credentials` column in its `bot_state` row using your database administration tool.

   For SQLite or MySQL:

   ```sql
   UPDATE bot_state SET credentials = NULL WHERE id = 1;
   ```

   For PostgreSQL (which uses a Boolean singleton ID):

   ```sql
   UPDATE bot_state SET credentials = NULL WHERE id = TRUE;
   ```

   Confirm that exactly one row was updated. Do not delete the `bot_state` row, queued actions, checkpoints, ownership records, or rate-limit accounting.
3. Configure the new encryption key and a valid `Bot:Handle` and `Bot:AppPassword` for the existing `Bot:Did`. Keep the bot's identity, stream settings, and developer-mode setting unchanged.
4. Restart the bot and monitor the sender logs. When authentication is next needed, the absence of saved credentials causes a fresh login. The runtime verifies the account's DID and encrypts the new refresh credentials with the configured key.

This preserves queued actions and stream progress, but clearing local credentials does not revoke the old remote session. If credentials may have been compromised, also revoke the affected app password through Bluesky's account settings and configure a replacement.

The in-memory provider loses its saved credentials when the process stops, so changing its key between runs does not require this database recovery.

## Hosting and running the bot

We configure the bot using standard .NET configuration and the fluent `BlueskyBotBuilder`.

Here is `Program.cs`:

```csharp
using idunno.Bluesky.BotSdk;
using idunno.Bluesky.BotSdk.DataProviders;
using SimpleReplyBot;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Register the bot runtime and storage fluently:
string connectionString = builder.Configuration.GetConnectionString("Bot")
    ?? "Data Source=bot.db";

builder.Services.AddBlueskyBot<SimpleReplyBotPolicy>(
    builder.Configuration.GetSection(BotOptions.Bot),
    httpUserAgent: "SimpleReplyBot/1.0")
    .WithSqliteStorage(connectionString);

// Or configure options using a delegate:
// builder.Services.AddBlueskyBot<SimpleReplyBotPolicy>(
//     options =>
//     {
//         options.Did = "did:plc:your-bot-did";
//         options.Handle = "your-bot.bsky.social";
//     },
//     httpUserAgent: "SimpleReplyBot/1.0")
//     .WithSqliteStorage(connectionString);

WebApplication app = builder.Build();

// Map operational health and metrics endpoints
app.MapBlueskyBotHealthEndpoints();
app.MapBlueskyBotMetricsEndpoint();

app.Run();
```

Run the application:

```PowerShell
dotnet run
```

When your bot starts, the database tables will be created automatically. The bot will acquire an ownership lease, connect to the Jetstream, and start evaluating posts.

## Operational features and reliability

### Duplicate protection and delivery guarantees

When interacting with a remote network like Bluesky, an operation might succeed on the server even if the network connection dropped before the response reached your bot.

To prevent sending duplicate replies:
- The runtime uses deterministic Record Keys (TIDs) for generated posts, likes, and reposts.
- Actions are marked *uncertain* in the database right before sending. If a connection drops, the bot reconciles with the server using the fixed record key before attempting to recreate it.
- Delivery is *at least once*, and state transitions ensure that events at or below your committed progress are not re-processed.

### Monitoring your bot

The runtime provides endpoints you can wire up to monitoring tools:

- **`/health/live`**: Reports overall process and worker liveness. Returns HTTP 200 when healthy, or HTTP 503 if a worker has failed.
- **`/health/dependencies`**: Details the health of external dependencies (such as database connectivity).
- **`/metrics`**: Emits Prometheus-formatted metrics, including:
  - `bluesky_bot_processed_through_sequence`: The highest Jetstream sequence number durably committed.
  - `bluesky_bot_actions`: The count of actions grouped by status (`pending`, `uncertain`, `sent`, `simulated`, `not-sent`).
  - `bluesky_bot_retry_due_actions`: The count of actions currently scheduled for retry.

### Rate limiting and cooldowns

The runtime enforces conservative outbound rate limits to prevent your bot from exceeding Bluesky's API budgets. If the server signals that a rate limit has been hit, the sender automatically pauses and schedules retries according to the server's cooldown.

For more details on low-level Jetstream replay and snapshot checkpoints, see [Network replay and snapshots](../jetstreamReplay.md).
