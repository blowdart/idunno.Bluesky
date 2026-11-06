# idunno.Bluesky.Labeler.PostgreSQL

## About

PostgreSQL persistence for standalone AT Protocol labelers.

## Key Features

* Durable labeler storage in a dedicated PostgreSQL database
* PostgreSQL 17 or later with fsync enabled
* Packaged database schema and a local development container script
* Issuer-bound initialization through the shared storage contracts

## Version History

A full [version history](https://github.com/blowdart/idunno.Bluesky/blob/main/CHANGELOG.md) can be found on the project's
[GitHub](https://github.com/blowdart/idunno.Bluesky/) repository.

## How to Use

Register `services.AddPostgreSqlLabelStorage(connectionString)`, resolve `ILabelStore`, and call
`InitializeAsync(issuer)`. PostgreSQL 17 or later with fsync enabled is required.
Use a dedicated database and verified TLS in production.

`schema.sql` is included in this package. For local development use
`.artifacts\Start-PostgreSqlLabelerContainer.ps1`, which uses a named Docker data volume
and returns an Npgsql connection string. It is not a production deployment script.

## Related Packages

* [idunno.Bluesky.Labeler](https://www.nuget.org/packages/idunno.Bluesky.Labeler) provides shared issuance and storage contracts.
* [idunno.Bluesky.AspNet.Labeler](https://www.nuget.org/packages/idunno.Bluesky.AspNet.Labeler) provides ASP.NET Core hosting.
* [idunno.Bluesky.Labeler.SQLite](https://www.nuget.org/packages/idunno.Bluesky.Labeler.SQLite) provides an alternative SQLite storage provider.
* [idunno.Bluesky.Labeler.MySQL](https://www.nuget.org/packages/idunno.Bluesky.Labeler.MySQL) provides an alternative MySQL storage provider.

## Documentation

See the [persistence guide](https://bluesky.idunno.dev/docs/labeler/persistence.html) for setup,
upgrades, concurrency, monitoring and consistent backup/restore.
