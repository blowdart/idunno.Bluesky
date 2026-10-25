# Standalone labeler host

This sample hosts public label queries and subscriptions, remote service-authenticated
administration, and protected local administration against one of the SQLite, PostgreSQL,
or MySQL label stores. It contains no live account, database, or signing-key secrets.

See the [labeler deployment guide](../../docs/docs/labeller/deployment.md) for environment
configuration, initial local administrator bootstrap, key installation, container deployment,
health checks, image verification, and update/recovery procedures.

For a local build, configure the required `Labeler:*` settings through environment variables
or reloadable JSON configuration, then run:

```powershell
dotnet run --project samples\Samples.LabelerHost
```

The host requires an issuer DID, explicit HTTPS public origin, database provider and connection,
an installed signing-key PEM directory with a persisted active-key ID, a DID service audience,
and declared label policy. Missing values fail at startup rather than enabling a permissive
default.
