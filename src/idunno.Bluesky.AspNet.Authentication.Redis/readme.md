# idunno.Bluesky.AspNet.Authentication.Redis

## About

A Redis implementation of the backing stores for the [idunno.Bluesky.AspNet.Authentication](https://github.com/blowdart/idunno.Bluesky/tree/main/src/idunno.Bluesky.AspNet.Authentication) library.

## Version History

A full [version history](https://github.com/blowdart/idunno.Bluesky/blob/main/CHANGELOG.md) can be found on the project's
[GitHub](https://github.com/blowdart/idunno.Bluesky/) repository.

## How to Use

Create and reuse a single `ConnectionMultiplexer`, then configure the authentication options with Redis-backed stores.

```c#
ConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(
    builder.Configuration.GetConnectionString("Redis")!);

builder.Services
    .AddAuthentication()
    .AddBluesky(options =>
    {
        options.IdentityStore = new RedisIdentityStore(redis);
        options.CorrelationCache = new RedisCorrelationStateCache(redis);
    });
```

An optional Redis database number and instance name can isolate stores which share a Redis server. The identity store uses a seven-day sliding expiration by default. Refresh locks expire after 90 seconds, and correlation state expires after 15 minutes. Alternate lifetimes can be supplied to the constructors.

The application owns the `IConnectionMultiplexer` and must dispose it when the application shuts down.

## Documentation

[Documentation](https://bluesky.idunno.dev/) is available, including API references.
