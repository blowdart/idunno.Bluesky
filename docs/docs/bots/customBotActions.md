# Writing a custom bot action

In [Writing a durable bot](durableBotSdk.md), we used the built-in reply action. But your bot might need to do something else: save matching posts in your application's database, for example.

Let's write a bot that watches for `#beans` and saves the matching post text to a separate SQLite database. The policy decides what to save, and a custom action handler does the saving. We'll also make retries safe when the save succeeds but the bot stops before recording that success.

You don't need to inherit from `BotAction`. Instead, define your own payload and use `BotActions.Create()` to package it as a durable intent.

[!include[Untrusted data warning](../includes/untrusted-data-warning.md)]

## Before you start

Start with the ASP.NET Core application and configuration from [Writing a durable bot](durableBotSdk.md). This example uses the `idunno.Bluesky.BotSdk` and `idunno.Bluesky.BotSdk.SQLite` packages.

The SQLite provider brings in `Microsoft.Data.Sqlite`, which we'll also use for the application's matching-post database. Keep both database files on persistent local storage. The bot's outbox and the application's saved matches are separate stores, even though both use SQLite.

Put the following classes in separate files in your project. The examples use the namespace `CustomActionBot`.

## Define the payload

The payload contains the post text chosen by the policy. The source post URI and event sequence are already part of the `BotAction` envelope, so we don't need to duplicate them.

Create `SaveMatchPayload.cs`:

```csharp
namespace CustomActionBot;

public sealed record SaveMatchPayload(string Text);
```

The runtime persists the payload before dispatching it. A handler must use that saved payload, not fetch the post again and substitute newer text.

Next, create `CustomActionJsonContext.cs`:

```csharp
using System.Text.Json.Serialization;

namespace CustomActionBot;

[JsonSerializable(typeof(SaveMatchPayload))]
internal partial class CustomActionJsonContext : JsonSerializerContext
{
}
```

This registers the payload with System.Text.Json's source generator. We pass the generated metadata explicitly when serializing and deserializing, rather than relying on reflection. See [JSON source generation, AOT and trimming](../sourceGeneration.md).

## Write a policy that queues the action

Create `SaveMatchPolicy.cs`:

```csharp
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Jetstream;
using idunno.AtProto.Repo;
using idunno.Bluesky;
using idunno.Bluesky.BotSdk;

namespace CustomActionBot;

public sealed class SaveMatchPolicy : IBotPolicy
{
    public IReadOnlyList<BotAction> Evaluate(
        JetstreamEvent item,
        DateTimeOffset startedAt)
    {
        // A covering archive segment can include events from before initialization.
        if (item.WitnessedAt is not DateTimeOffset witnessedAt || witnessedAt < startedAt)
        {
            return [];
        }

        // Match only new posts with the information needed for a durable action.
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

        Post post = commitEvent.Commit.Record.Value.Deserialize<Post>(
            BlueskyJsonSerializerOptions.Options)
            ?? throw new JsonException("The post record is null.");

        if (!post.Text.Contains("#beans", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        StrongReference source = new(
            new AtUri($"at://{commitEvent.Did}/app.bsky.feed.post/{commitEvent.Commit.RKey}"),
            cid);

        // Return an intent, not a database write. The runtime saves this payload
        // and stream progress together before a sender can execute the action.
        return [BotActions.Create(
            kind: SaveMatchHandler.ActionKind,
            source: source,
            sequence: sequence,
            payload: new SaveMatchPayload(post.Text),
            metadata: CustomActionJsonContext.Default.SaveMatchPayload,
            actionKey: "default",
            payloadVersion: 1)];
    }
}
```

The policy uses the server's witnessed time to exclude events before the durable `startedAt` boundary, including earlier events in a covering archive segment. This is not the author's `Post.CreatedAt`, which may be old or chosen by the author.

The policy deliberately lets malformed JSON raise `JsonException` rather than silently ignoring it. The reader records that exception in its skip audit and logs it before committing progress. Other exceptions, including invalid action arguments, stop processing without committing the event; do not throw `JsonException` for policy configuration or programming errors.

### Action kinds, keys, and multiple actions

`ActionKind` identifies the handler's operation. The runtime selects a handler using the action kind **and payload version**. `actionKey` does not select a handler or check the post's text; it distinguishes separate actions of the same kind for the same source post. The `#beans` trigger is checked by `post.Text.Contains()` in the policy.

The runtime's durable action identity combines **the source post URI, action kind, and action key**. Both the kind and key are case-sensitive, non-blank strings of at most 128 characters. The event sequence tracks ingestion progress; it is not an additional part of that action identity.

Our policy returns one `"save-match"` action with the key `"default"`. A different source post gets its own action even though it uses the same kind and key. If the original post is delivered again, durable progress suppresses already-processed events, and the storage provider's action identity prevents a second outbox entry with the same source, kind, and key. The sender retries the saved entry with its original payload and record key rather than generating a new intent. `"default"` is simply a conventional label, and is also the factory's default when you omit `actionKey`.

A policy can return multiple actions in the list. Different kinds can share `"default"` because the kind is part of the identity. For example, after the same trigger check, you could save the match **and** like the post by replacing the return statement with:

```csharp
return [
    BotActions.Create(
        kind: SaveMatchHandler.ActionKind,
        source: source,
        sequence: sequence,
        payload: new SaveMatchPayload(post.Text),
        metadata: CustomActionJsonContext.Default.SaveMatchPayload,
        actionKey: "default",
        payloadVersion: 1),
    BotActions.Like(source, sequence, actionKey: "default")
];
```

These have identities `(source, "save-match", "default")` and `(source, "like", "default")`. The runtime validates both and saves the batch together with stream progress in one transaction. Dispatch and completion are independent: saving the match and publishing the like are not an atomic pair, and either action may need retries.

If you want **two actions of the same kind** for one post, give them different stable keys. For example, a custom notification handler could receive two intents with kind `"notify"`: one keyed `"email"` and one keyed `"sms"`, each with a complete payload identifying its destination and message. Both are dispatched to the same handler version, but are tracked and reconciled separately.

Choose keys from the logical purpose of the action, not a random value or the current time. Reusing a source/kind/key does not request another execution or replace its saved payload. Changing the key creates a different identity and can cause another effect if that intent is ingested; do not change keys merely to retry existing work.

Outbox deduplication alone cannot prevent duplicated effects in another system. The handler below also uses the source/kind/key as its database primary key and checks the saved payload when reconciling, so an uncertain attempt can be recovered safely.

## Implement the handler

An `IBotActionHandler` has three responsibilities:

| Method | What it does |
| --- | --- |
| `Validate` | Checks the saved payload without making network calls or changing storage. |
| `ReconcileAsync` | Looks for an existing effect. Returns a confirmed result if it exists, or `null` only when execution is safe. |
| `ExecuteAsync` | Performs the effect after the runtime has durably marked the attempt uncertain. |

For our example, the effect is a row in an application-owned table. Its primary key uses the same source URI, action kind, and action key as the durable intent. An insert can therefore be retried without adding a second row.

Create `SaveMatchHandler.cs`:

```csharp
using System.Text.Json;

using idunno.Bluesky.BotSdk;

using Microsoft.Data.Sqlite;

namespace CustomActionBot;

public sealed class SaveMatchHandler(string connectionString) : IBotActionHandler
{
    public const string ActionKind = "save-match";

    public string Kind => ActionKind;
    public int PayloadVersion => 1;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS saved_matches (
                source_uri TEXT NOT NULL,
                action_kind TEXT NOT NULL,
                action_key TEXT NOT NULL,
                payload_version INTEGER NOT NULL,
                payload TEXT NOT NULL,
                PRIMARY KEY (source_uri, action_kind, action_key)
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public void Validate(BotAction action)
    {
        if (action.ActionKind != Kind || action.PayloadVersion != PayloadVersion)
        {
            throw new InvalidOperationException("Unsupported saved-match format.");
        }

        SaveMatchPayload payload = JsonSerializer.Deserialize(
            action.RecordJson,
            CustomActionJsonContext.Default.SaveMatchPayload)
            ?? throw new JsonException("The saved-match payload is null.");

        ArgumentException.ThrowIfNullOrWhiteSpace(payload.Text);
    }

    public async Task<BotActionResult?> ReconcileAsync(
        BotAction action,
        IBotActionRemote remote,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = new(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT payload_version, payload FROM saved_matches
            WHERE source_uri = $source AND action_kind = $kind AND action_key = $key;
            """;
        AddIdentityParameters(command, action);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            // This is an authoritative database read, not an eventually
            // consistent cache. The unique key also protects the subsequent insert.
            return null;
        }

        // An existing row must represent this exact saved intent.
        // A conflicting payload is not evidence that our action succeeded.
        if (reader.GetInt32(0) != action.PayloadVersion ||
            reader.GetString(1) != action.RecordJson)
        {
            throw new InvalidOperationException("The existing match has a different payload.");
        }

        return new BotActionResult(true, null, "Matching post is saved.");
    }

    public async Task<BotActionResult> ExecuteAsync(
        BotAction action,
        IBotActionRemote remote,
        CancellationToken cancellationToken)
    {
        await using (SqliteConnection connection = new(connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO saved_matches
                    (source_uri, action_kind, action_key, payload_version, payload)
                VALUES ($source, $kind, $key, $version, $payload)
                ON CONFLICT (source_uri, action_kind, action_key) DO NOTHING;
                """;
            AddIdentityParameters(command, action);
            command.Parameters.AddWithValue("$version", action.PayloadVersion);
            command.Parameters.AddWithValue("$payload", action.RecordJson);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        // Check the row even after a conflict; never overwrite a different intent.
        BotActionResult result = await ReconcileAsync(action, remote, cancellationToken)
            ?? throw new InvalidOperationException("The saved match could not be confirmed.");

        return result;
    }

    private static void AddIdentityParameters(SqliteCommand command, BotAction action)
    {
        command.Parameters.AddWithValue("$source", action.SourceUri.ToString());
        command.Parameters.AddWithValue("$kind", action.ActionKind);
        command.Parameters.AddWithValue("$key", action.ActionKey);
    }
}
```

We use SQL parameters, not string interpolation, for values originating from posts or action identities. Each operation owns its connection; the singleton handler does not share a connection or mutable per-action state.

The example expects rows to remain available for reconciliation. Don't delete or rewrite them while pending or uncertain actions could still refer to them.

## Register and run the bot

Replace `Program.cs` with:

```csharp
using CustomActionBot;

using idunno.Bluesky.BotSdk;
using idunno.Bluesky.BotSdk.DataProviders;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

SaveMatchHandler handler = new("Data Source=matches.db");
await handler.InitializeAsync(CancellationToken.None);

builder.Services
    .AddBlueskyBot<SaveMatchPolicy>(
        builder.Configuration.GetSection(BotOptions.Bot),
        httpUserAgent: "CustomActionBot/1.0")
    .WithSqliteStorage("Data Source=bot.db")
    .AddActionHandler(handler);

WebApplication app = builder.Build();
app.MapBlueskyBotHealthEndpoints();
app.MapBlueskyBotMetricsEndpoint();
app.Run();
```

We register the initialized handler instance. If your handler takes DI-registered dependencies instead, use `.AddActionHandler<YourHandler>()`. Handlers are singletons: keep per-action state in local variables, and ensure dependencies are safe for singleton use.

Run with `dotnet run`. With `Bot:DeveloperMode` set to `true`, the runtime validates and marks actions simulated; it does **not** call `ReconcileAsync` or `ExecuteAsync`, so `saved_matches` stays empty.

To publish, stop the bot, set `Bot:DeveloperMode` to `false`, and configure a **separate empty runtime database**, for example `Data Source=bot-live.db`. Relational replay state is bound to its original mode; reusing `bot.db` stops during state restoration rather than converting simulated work into live actions. Retain the development database, and do not edit its mode or delete its progress. Previously simulated actions are terminal and are not automatically replayed. The separate application `saved_matches` database can remain unchanged.

Configure the DID, handle, app password, archive API key, and encryption key as described in the durable bot guide. The current sender authenticates a Bluesky session even for a custom handler that only uses an application database.

## What happens after a crash?

Suppose the insert succeeds, but the bot stops before marking the outbox action complete. On restart, that action is still uncertain. `ReconcileAsync` reads the existing row, checks that its payload matches, and reports success without inserting again.

If the bot stops before the insert, reconciliation finds no row and execution can proceed. The unique constraint also handles a race between that read and an insert: another attempt cannot create a second row under the same identity.

This makes **this database effect** idempotent. It does not make arbitrary custom actions exactly-once, and it does not create a transaction spanning the bot outbox and another database or a PDS.

## Errors, external services, and upgrades

Return `BotActionResult(true, ...)` only for confirmed success. A result with `Succeeded = false` is a terminal rejection, not a request to retry. Never turn a timeout, unavailable dependency, or unknown write outcome into a successful result or `null` reconciliation result.

The runtime schedules retries for `HttpRequestException`, `TimeoutException`, eligible cancellation failures, and retryable `RemoteFailureException` instances. Arbitrary exceptions from your own database client are not automatically classified as transient by the bot's storage provider. In this example, an application SQLite failure propagates and stops the host; fix the cause and restart to reconcile retained work. A production handler can classify known transient failures explicitly, but must not label every exception retryable or hide damaged data.

For a remote service, use its authoritative lookup and idempotency facilities. An eventually consistent "not found" response may not prove that a previous write failed. If duplicate prevention cannot be established, document that limitation rather than promising exactly-once delivery.

For custom Bluesky operations, use `remote.Agent`, which has the runtime's protected, rate-accounted transport. Do not dispose the agent or replace its credentials. For other services, inject an appropriately secured client and implement that service's rate limiting, timeouts, and reconciliation; Bluesky's accounting does not automatically cover it. Never use an untrusted post to choose an arbitrary outbound URL.

Action kinds, keys, payload versions, and payloads are persisted contracts. When introducing version 2, retain a separate version-1 handler for queued work and register both. Do not change the meaning of an existing kind/key to force retries to do something different. An unregistered kind/version causes an explicit failure rather than silently discarding work.

Before deploying your handler, test successful execution, repeated execution, reconciliation after an uncertain write, mismatched saved payloads, dependency failures, cancellation, and upgrades with older queued versions. Also test that developer mode does not invoke your handler's execution or reconciliation methods.
