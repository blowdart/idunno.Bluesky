# idunno.Bluesky.Labeler.Host

The canonical deployable labeler application hosts public label queries and subscriptions, remote service-authenticated
administration, and protected local administration against one of the SQLite, PostgreSQL,
or MySQL label stores. It contains no live account, database, or signing-key secrets.

See the [labeler deployment guide](../../docs/docs/labeller/deployment.md) for environment
configuration, initial local administrator bootstrap, key installation, container deployment,
health checks, image verification, and update/recovery procedures.

For a local build, configure the required `Labeler:*` settings through environment variables
or reloadable JSON configuration, then run:

```powershell
dotnet run --project src\idunno.Bluesky.Labeler.Host
```

The host defaults to PostgreSQL for both direct runs and containers. Supply
`Labeler__ConnectionString` through protected environment configuration; no database credentials
or fallback SQLite connection string are built into the image. SQLite and MySQL remain explicit
alternatives through `Labeler__DatabaseProvider=sqlite` or `mysql`.

The host requires an issuer DID, explicit HTTPS public origin, database connection,
separately configured environment keys with database-backed selection by default, a DID service audience,
and declared label policy. Missing values fail at startup rather than enabling a permissive
default.

Supply the current key and any retained previous key through separate indexed environment variables:

```text
Labeler__SigningKeys__0__Id=current-key-id
Labeler__SigningKeys__0__PrivateKeyBase64=<encrypted base64-encoded private PEM>
Labeler__SigningKeys__1__Id=previous-key-id
Labeler__SigningKeys__1__PrivateKeyBase64=<encrypted base64-encoded previous private PEM>
Labeler__InitialSigningKeyId=current-key-id
```

The previous-key entry is optional. Indices are not key roles: the database selects the active
key, and `InitialSigningKeyId` is used only for first setup, never to bypass coordinated rotation.
Preserve stable IDs when changing entry order. The environment provider is the default; no
signing-key files or volumes are required. Supply each
`Labeler__SigningKeys__<index>__PrivateKeyBase64` through the platform's encrypted environment
settings, with a corresponding `Id`. The deployment guide describes first setup and rotation.
For filesystem-backed deployments only, explicitly set `Labeler__SigningKeyProvider=pem`
and configure the PEM directory and persistent active-key ID file.

The application targets .NET 10 and is distributed as the labeler container image rather than a
NuGet library. SDK consumers can continue to compose their own hosts using the independent
labeler libraries. This host does not include a moderation web UI.

Build the container from the repository root:

```powershell
docker build --file src\idunno.Bluesky.Labeler.Host\Dockerfile --tag idunno-bluesky-labeler .
```
