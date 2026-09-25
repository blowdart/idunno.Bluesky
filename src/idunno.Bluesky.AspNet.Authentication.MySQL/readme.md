# idunno.Bluesky.AspNet.Authentication.MySQL

## About

A MySQL implementation of the backing stores for the [idunno.Bluesky.AspNet.Authentication](https://github.com/blowdart/idunno.Bluesky/tree/main/src/idunno.Bluesky.AspNet.Authentication) library.

## Version History

A full [version history](https://github.com/blowdart/idunno.Bluesky/blob/main/CHANGELOG.md) can be found on the project's
[GitHub](https://github.com/blowdart/idunno.Bluesky/) repository.

## How to Use

Create the required tables by running the `schema.sql` file included in the package, then configure the authentication options with stores that use your MySQL connection string.

```c#
string connectionString = builder.Configuration.GetConnectionString("BlueskyAuthentication")!;

builder.Services
    .AddAuthentication()
    .AddBluesky(options =>
    {
        options.IdentityStore = new MySqlIdentityStore(connectionString);
        options.CorrelationCache = new MySqlCorrelationStateCache(connectionString);
    });
```

The identity store uses a seven-day sliding expiration by default. Refresh locks expire after 90 seconds, and correlation state expires after 15 minutes. Alternate lifetimes can be supplied to the constructors.

Expired rows are deleted by the store which wrote them, at most once every five minutes per store instance, so the tables stay bounded without an operator scheduling anything. Pass `expiredEntrySweepInterval` to change how often that happens, or `TimeSpan.Zero` to turn it off and reclaim the rows yourself. Expiry is enforced on read either way, so an unswept row authenticates nobody.

## Documentation
[Documentation](https://bluesky.idunno.dev/) is available, including API references.
