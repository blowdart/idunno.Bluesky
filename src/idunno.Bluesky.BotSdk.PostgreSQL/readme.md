# idunno.Bluesky.BotSdk.PostgreSQL

## About

A PostgreSQL storage provider for the [idunno.Bluesky.BotSdk](https://github.com/blowdart/idunno.Bluesky/tree/main/src/idunno.Bluesky.BotSdk) durable bot runtime.

## Version History

A full [version history](https://github.com/blowdart/idunno.Bluesky/blob/main/CHANGELOG.md) can be found on the project's
[GitHub](https://github.com/blowdart/idunno.Bluesky/) repository.

## How to Use

Configure your bot with PostgreSQL storage using your connection string. Database tables are created and migrated automatically at startup, using database time and transactional lease checks to fence competing processes.

```c#
string connectionString = builder.Configuration.GetConnectionString("Bot")!;

builder.Services
    .AddBlueskyBot<MyBotPolicy>(
        builder.Configuration.GetSection(BotOptions.Bot),
        httpUserAgent: "MyBot/1.0")
    .WithPostgreSqlStorage(connectionString);
```

Use a dedicated database and verified TLS in production environments.

## Related Packages

* [idunno.Bluesky.BotSdk](https://www.nuget.org/packages/idunno.Bluesky.BotSdk) which provides the core durable bot runtime.
* [idunno.Bluesky.BotSdk.SQLite](https://www.nuget.org/packages/idunno.Bluesky.BotSdk.SQLite) which provides SQLite storage for the Bot SDK.
* [idunno.Bluesky.BotSdk.MySQL](https://www.nuget.org/packages/idunno.Bluesky.BotSdk.MySQL) which provides MySQL storage for the Bot SDK.
* [idunno.Bluesky](https://www.nuget.org/packages/idunno.Bluesky) for interacting with the [Bluesky social network](https://docs.bsky.app/).

## Documentation
[Documentation](https://bluesky.idunno.dev/) is available, including API references.

See the [durable bot guide](https://bluesky.idunno.dev/docs/bots/durableBotSdk.html) for setup instructions and configuration details.
