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

The Visual Studio launch profile uses the `Development` environment and loopback-only
`https://localhost:50656` and `http://localhost:50657` endpoints. HTTPS requires the ASP.NET
development certificate. Automatic browser launch is disabled until a landing page is available.
The profile contains no secrets; the deployment-specific settings below are still required.

The checked-in `appsettings.json` contains only non-secret defaults: PostgreSQL,
environment-backed signing keys, disabled account/record issuance and remote administration,
no trusted proxies, a forwarding limit of one, and standard logging levels. Environment
variables override these settings.

Supply these deployment-specific environment variables separately:

| Variable | Purpose |
|---|---|
| `Labeler__IssuerDid` | The labeler's issuer DID |
| `Labeler__PublicEndpoint` | The public HTTPS origin |
| `Labeler__ConnectionString` | The protected PostgreSQL connection string |
| `Labeler__AdministrationAudience` | The exact DID and service-fragment audience |
| `Labeler__DeclaredLabels` | Comma-separated permitted label values |
| `Labeler__InitialSigningKeyId` | The key ID used only to initialize database selection |
| `Labeler__SigningKeys__<index>__Id` | A stable key ID |
| `Labeler__SigningKeys__<index>__PrivateKeyBase64` | The protected base64-encoded private PEM for that ID |

Explicitly set `Labeler__AllowAccountLabels=true` and/or `Labeler__AllowRecordLabels=true`
for the target types your policy permits. Remote administration remains disabled unless
`LabelerAdministration__RemoteEnabled=true`; enabling it does not bypass authentication
or team authorization. Do not add private keys or database credentials to `appsettings.json`.

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

Optional [Xenon moderation](../../docs/docs/labeller/moderation.md) is disabled by
default. Set `LabelerModeration__Enabled=true` to add standard independent-labeler
reports and shared case/appeal/policy administration. The same database, keys,
roles and maintenance coordination are used. Persisted independently verified
policy then replaces configuration-backed issuance permissions; declaration drift
or stale/unavailable verification closes reports and new issuance, not distribution
or safe withdrawal. Remote administration remains independently opt-in.

Build the container from the repository root:

```powershell
docker build --file src\idunno.Bluesky.Labeler.Host\Dockerfile --tag idunno-bluesky-labeler .
```
