# idunno.Bluesky.Labeler.PostgreSQL

Register `services.AddPostgreSqlLabelStorage(connectionString)`, resolve `ILabelStore`, and call
`InitializeAsync(issuer)`. PostgreSQL 17 or later with fsync enabled is required.
Use a dedicated database and verified TLS in production.

`schema.sql` is included in this package. For local development use
`.artifacts\Start-PostgreSqlLabelerContainer.ps1`, which uses a named Docker data volume
and returns an Npgsql connection string. It is not a production deployment script.

See the [persistence guide](https://bluesky.idunno.dev/docs/labeller/persistence.html) for setup,
upgrades, concurrency, monitoring and consistent backup/restore.
