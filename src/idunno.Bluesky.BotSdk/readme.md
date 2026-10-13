# idunno.Bluesky.BotSdk

## About

A hosted durable event-to-action runtime for [Bluesky](https://bsky.social/) bots on the [AT Protocol](https://atproto.com/).

## Key Features

* Sequential Jetstream v2 live streaming with automatic archive snapshot replay fallback.
* Transactional outbox pattern committing stream checkpoints and action intents atomically.
* Durable actions for replies, likes, reposts, bookmarks, list memberships, and custom application handlers.
* Stable action identities and safe reconciliation of uncertain remote writes.
* Distributed database lease fencing and conservative rate-limit accounting.
* Memory-only operational health endpoints (`/health/live`, `/health/dependencies`) and durable-state Prometheus gauges (`/metrics`).
* Pluggable storage providers (SQLite, PostgreSQL, MySQL) plus an in-memory testing provider.

Trimming is supported for applications targeting .NET 9.0 or later.

## Version History

A full [version history](https://github.com/blowdart/idunno.Bluesky/blob/main/CHANGELOG.md) can be found on the project's
[GitHub](https://github.com/blowdart/idunno.Bluesky/) repository.

## How to Use

Implement `IBotPolicy` to evaluate incoming Jetstream events and return actions to execute, then register the bot using the fluent builder:

```c#
builder.Services
    .AddBlueskyBot<MyBotPolicy>(
        builder.Configuration.GetSection(BotOptions.Bot),
        httpUserAgent: "MyBot/1.0")
    .WithSqliteStorage(connectionString);

var app = builder.Build();

app.MapBlueskyBotHealthEndpoints();
app.MapBlueskyBotMetricsEndpoint();

app.Run();
```

Delivery is at least once. Choose exactly one relational storage provider package for production deployments:
`idunno.Bluesky.BotSdk.SQLite`, `idunno.Bluesky.BotSdk.PostgreSQL`, or `idunno.Bluesky.BotSdk.MySQL`.
For development and unit testing, `.WithInMemoryStorage()` is available in this package.

## Related Packages

* [idunno.Bluesky.BotSdk.SQLite](https://www.nuget.org/packages/idunno.Bluesky.BotSdk.SQLite) which provides SQLite storage for this package.
* [idunno.Bluesky.BotSdk.PostgreSQL](https://www.nuget.org/packages/idunno.Bluesky.BotSdk.PostgreSQL) which provides PostgreSQL storage for this package.
* [idunno.Bluesky.BotSdk.MySQL](https://www.nuget.org/packages/idunno.Bluesky.BotSdk.MySQL) which provides MySQL storage for this package.
* [idunno.Bluesky](https://www.nuget.org/packages/idunno.Bluesky) for interacting with the [Bluesky social network](https://docs.bsky.app/).

## Documentation
[Documentation](https://bluesky.idunno.dev/) is available, including API references.

See the [durable bot guide](https://bluesky.idunno.dev/docs/bots/durableBotSdk.html) for setup instructions, recovery behavior, built-in actions, and operational endpoints.
