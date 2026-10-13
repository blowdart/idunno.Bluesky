# Samples.BotSdk

A sample demonstrating how to build a durable Bluesky bot using `idunno.Bluesky.BotSdk`.

This sample connects to the live Jetstream event stream, evaluates public posts for the `#beans` hashtag using `BeansBotPolicy`, and simulates replying with the bean emoji "🫘". It defaults to **developer mode** (`DeveloperMode = true`) and uses the **in-memory storage provider** (`.WithInMemoryStorage()`).

## Key Concepts

* **Durable Architecture**: Bot logic lives in an `IBotPolicy`. Policies are purely evaluative and never make network calls directly. When a post matches the trigger, the policy returns an action intent (`BotActions.Reply`), which the runtime commits to storage alongside stream progress before dispatching to Bluesky.
* **Developer Mode**: `DeveloperMode = true` logs and simulates replies without publishing them. The bot still listens to the live Jetstream.
* **In-Memory Storage**: The `.WithInMemoryStorage()` provider keeps state in RAM for easy local experimentation without needing to set up an external database.
* **Operational Monitoring**: Memory-only health endpoints (`/health/live`, `/health/dependencies`) report worker liveness and tracked dependency waits. `/metrics` exposes Prometheus gauges for the durable processed sequence, action counts by status, and due retries, not throughput or latency counters.

> [!WARNING]
> Setting `Bot:DeveloperMode` to `false` enables actual replies to Bluesky whenever someone posts `#beans`. Use a dedicated bot account when enabling publishing.
>
> Additionally, the in-memory provider loses all state when the application stops. For production bots, use a persistent relational provider such as `idunno.Bluesky.BotSdk.SQLite`, `idunno.Bluesky.BotSdk.PostgreSQL`, or `idunno.Bluesky.BotSdk.MySQL`.

## Prerequisites

1. A dedicated Bluesky account for your bot.
2. A Bluesky [App Password](https://bsky.app/settings/app-passwords) for the bot account, required only when publishing real replies.
3. An [archive API key](https://bsky.network/account#api-keys-section-heading) for Jetstream archive access.
4. A base64-encoded 32-byte encryption key for encrypting cached authentication tokens, required only when publishing real replies. You can generate one in PowerShell:
   ```PowerShell
   $key = New-Object byte[] 32
   $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
   try {
       $rng.GetBytes($key)
       [Convert]::ToBase64String($key)
   }
   finally {
       $rng.Dispose()
       [Array]::Clear($key, 0, $key.Length)
   }
   ```

## Configuration

Supply credentials using [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) during development or environment variables populated from a deployment secret store. Keep `appsettings.json` for non-secret settings; never put app passwords, archive API keys, or encryption keys in tracked configuration files.

Store the generated encryption key securely. For persistent storage providers, keep it stable: replacing it without migrating stored credentials makes those credentials unreadable.

When the sender next tries to restore those credentials, cryptographic authentication fails. The sender logs the failure, marks itself unhealthy, and throws, causing the host to stop under the SDK's configured background-service exception policy. It does not silently discard the credentials or fall back to a fresh login. See [recovering from an encryption key change](../../docs/docs/bots/durableBotSdk.md#recovering-from-an-encryption-key-change) for recovery instructions. This sample's in-memory provider loses credentials when it stops, so changing its key between runs does not require database recovery.

### Option 1: Using .NET User Secrets (Recommended for development)

From the `samples/Samples.BotSdk` folder:

```PowerShell
dotnet user-secrets init
dotnet user-secrets set "Bot:Did" "did:plc:your-bot-did"
dotnet user-secrets set "Bot:Handle" "your-bot.bsky.social"
dotnet user-secrets set "Bot:AppPassword" "xxxx-xxxx-xxxx-xxxx"
dotnet user-secrets set "Bot:ArchiveApiKey" "your-archive-api-key"
dotnet user-secrets set "Bot:CredentialEncryptionKey" "your-base64-32-byte-key"
```

### Option 2: Using Environment Variables

Set the following environment variables before launching:

* `_BlueskyHandle`: Your bot's handle (e.g. `your-bot.bsky.social`).
* `_BlueskyPassword`: Your bot's app password.
* `_BlueskyDid`: Your bot's DID (e.g. `did:plc:...`).
* `_BlueskyArchiveApiKey`: Your archive API key.
* `_BlueskyCredentialEncryptionKey`: Your base64-encoded 32-byte encryption key.

Developer mode requires only the bot's DID and archive API key. The handle, app password, and credential encryption key are needed when publishing real replies.

To explicitly enable publishing, set `"DeveloperMode": false` in the `Bot` section of configuration, or run:

```PowerShell
dotnet run --project samples\Samples.BotSdk -- --Bot:DeveloperMode=false
```

## Running the Sample

Run the project from the repository root:

```PowerShell
dotnet run --project samples\Samples.BotSdk
```

When started, the application will:
1. Log an in-memory provider startup warning (Event ID 420).
2. Connect to the live Jetstream WebSocket stream.
3. Listen for posts containing `#beans` and log simulated `🫘` replies without publishing them.
4. Serve health and metrics endpoints on `http://localhost:58264` (the HTTP port in this sample's launch profile, unless you override the URLs).

To test the health endpoint while running:
```PowerShell
curl http://localhost:58264/health/live
```
