# idunno.Bluesky.BotSdk.SQLite

## About

A SQLite storage provider for the [idunno.Bluesky.BotSdk](https://github.com/blowdart/idunno.Bluesky/tree/main/src/idunno.Bluesky.BotSdk) durable bot runtime.

## Version History

A full [version history](https://github.com/blowdart/idunno.Bluesky/blob/main/CHANGELOG.md) can be found on the project's
[GitHub](https://github.com/blowdart/idunno.Bluesky/) repository.

## How to Use

Configure your bot with SQLite storage using a local connection string. Database tables are created and migrated automatically at startup.

```c#
string connectionString = builder.Configuration.GetConnectionString("Bot") ?? "Data Source=bot.db";

builder.Services
    .AddBlueskyBot<MyBotPolicy>(
        builder.Configuration.GetSection(BotOptions.Bot),
        httpUserAgent: "MyBot/1.0")
    .WithSqliteStorage(connectionString);
```

Use a persistent local disk for application storage on a single host. In-memory SQLite databases, network filesystems, and ephemeral container storage cannot provide durable shared bot state.

## Related Packages

* [idunno.Bluesky.BotSdk](https://www.nuget.org/packages/idunno.Bluesky.BotSdk) which provides the core durable bot runtime.
* [idunno.Bluesky.BotSdk.PostgreSQL](https://www.nuget.org/packages/idunno.Bluesky.BotSdk.PostgreSQL) which provides PostgreSQL storage for the Bot SDK.
* [idunno.Bluesky.BotSdk.MySQL](https://www.nuget.org/packages/idunno.Bluesky.BotSdk.MySQL) which provides MySQL storage for the Bot SDK.
* [idunno.Bluesky](https://www.nuget.org/packages/idunno.Bluesky) for interacting with the [Bluesky social network](https://docs.bsky.app/).

## Documentation
[Documentation](https://bluesky.idunno.dev/) is available, including API references.

See the [durable bot guide](https://bluesky.idunno.dev/docs/bots/durableBotSdk.html) for setup instructions and configuration details.
