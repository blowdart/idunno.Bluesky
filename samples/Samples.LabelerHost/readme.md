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
either an installed PEM directory with a persisted active-key ID or separately configured
environment keys with database-backed selection, a DID service audience,
and declared label policy. Missing values fail at startup rather than enabling a permissive
default.

For volume-free hosting, select `Labeler__SigningKeyProvider=environment` and supply each
`Labeler__SigningKeys__<index>__PrivateKeyBase64` through the platform's encrypted environment
settings, with a corresponding `Id`. The deployment guide describes first setup and rotation.
