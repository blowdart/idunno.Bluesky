# idunno.Bluesky.AspNet.Authentication.SQLite

## About

A SQLite implementation of the backing stores for the [idunno.Bluesky.AspNet.Authentication](https://github.com/blowdart/idunno.Bluesky/tree/main/src/idunno.Bluesky.AspNet.Authentication) library.

## Version History

A full [version history](https://github.com/blowdart/idunno.Bluesky/blob/main/CHANGELOG.md) can be found on the project's
[GitHub](https://github.com/blowdart/idunno.Bluesky/) repository.

## How to Use

```c#
string connectionString = builder.Configuration.GetConnectionString("BlueskyAuthentication")!;

builder.Services
    .AddAuthentication()
    .AddBluesky(options =>
    {
        options.IdentityStore = new SqliteIdentityStore(connectionString);
        options.CorrelationCache = new SqliteCorrelationStateCache(connectionString);
    });
```

The identity store uses a seven-day sliding expiration by default. Refresh locks expire after 90 seconds, and correlation state expires after 15 minutes. Alternate lifetimes can be supplied to the constructors.

Use a file-backed database for application storage. In-memory SQLite databases do not persist across the separate connections used for store operations.

You can create the required tables by running the `schema.sql` file included in the package, then configure the authentication options with stores that use your SQLite connection string.

The package also includes a PowerShell script that creates a new database and applies the schema. The database is created in the current directory unless `-OutputDirectory` is specified.

```powershell
.\New-AuthenticationDatabase.ps1 -OutputDirectory C:\data
```

The script requires the `sqlite3` command-line tool on `PATH` and creates `idunno.Bluesky.AspNet.Authentication.db`. It refuses to overwrite an existing database.

You can install the latest version of the SQLite command-line tool via winget

```powershell
winget install -e --id SQLite.SQLite
```

## Documentation
[Documentation](https://bluesky.idunno.dev/) is available, including API references.
