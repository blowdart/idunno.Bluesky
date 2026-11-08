# idunno.Bluesky.Labeler.SQLite

## About

SQLite persistence for standalone AT Protocol labelers.

## Key Features

* Durable labeler storage in a dedicated local database file
* WAL, FULL synchronization and bounded immediate write transactions
* Packaged database schema and a new-database creation script
* Issuer-bound initialization through the shared storage contracts

## Version History

A full [version history](https://github.com/blowdart/idunno.Bluesky/blob/main/CHANGELOG.md) can be found on the project's
[GitHub](https://github.com/blowdart/idunno.Bluesky/) repository.

## How to Use

Register `services.AddSqliteLabelStorage("Data Source=labels.db")`, resolve `ILabelStore`, and call
`InitializeAsync(issuer)`. Use a dedicated persistent local file, never a network filesystem.
The provider requires WAL, FULL synchronization and bounded immediate write transactions.

`schema.sql` is included in this package. `New-LabelDatabase.ps1 -OutputDirectory <directory>`
creates a new database through the SQLite CLI without overwriting an existing database.
Run `InitializeAsync` to bind that database to its issuer before use.

## Related Packages

* [idunno.Bluesky.Labeler](https://www.nuget.org/packages/idunno.Bluesky.Labeler) provides shared issuance and storage contracts.
* [idunno.Bluesky.AspNet.Labeler](https://www.nuget.org/packages/idunno.Bluesky.AspNet.Labeler) provides ASP.NET Core hosting.
* [idunno.Bluesky.Labeler.PostgreSQL](https://www.nuget.org/packages/idunno.Bluesky.Labeler.PostgreSQL) provides an alternative PostgreSQL storage provider.
* [idunno.Bluesky.Labeler.MySQL](https://www.nuget.org/packages/idunno.Bluesky.Labeler.MySQL) provides an alternative MySQL storage provider.

## Documentation

See the [persistence guide](https://bluesky.idunno.dev/docs/labeler/persistence.html) for setup,
upgrades, concurrency, monitoring and consistent backup/restore.
