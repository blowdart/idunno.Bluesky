# idunno.Bluesky.Labeler.SQLite

Register `services.AddSqliteLabelStorage("Data Source=labels.db")`, resolve `ILabelStore`, and call
`InitializeAsync(issuer)`. Use a dedicated persistent local file, never a network filesystem.
The provider requires WAL, FULL synchronization and bounded immediate write transactions.

`schema.sql` is included in this package. `New-LabelDatabase.ps1 -OutputDirectory <directory>`
creates a new database through the SQLite CLI without overwriting an existing database.
Run `InitializeAsync` to bind that database to its issuer before use.

See the [persistence guide](https://bluesky.idunno.dev/docs/labeller/persistence.html) for setup,
upgrades, concurrency, monitoring and consistent backup/restore.
