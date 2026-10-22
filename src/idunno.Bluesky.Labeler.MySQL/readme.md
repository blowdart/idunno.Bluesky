# idunno.Bluesky.Labeler.MySQL

Register `services.AddMySqlLabelStorage(connectionString)`, resolve `ILabelStore`, and call
`InitializeAsync(issuer)`. MySQL 8.4 or later, InnoDB, innodb_flush_log_at_trx_commit=1
and sync_binlog=1 are required. MariaDB is not a verified substitute.
Use a dedicated database and verified TLS in production.

`schema.sql` is included in this package. For local development use
`.artifacts\Start-MySqlLabelerContainer.ps1`, which uses a named Docker data volume
and returns a MySqlConnector connection string. It is not a production deployment script.

See the [persistence guide](https://bluesky.idunno.dev/docs/labeller/persistence.html) for setup,
upgrades, concurrency, monitoring and consistent backup/restore.
