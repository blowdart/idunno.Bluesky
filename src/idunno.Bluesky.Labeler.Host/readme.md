# idunno.Bluesky.Labeler.Host

The non-packable, provider-neutral canonical hosting library supplies reusable extensions over
the existing registration and mapping helpers. Thin PostgreSQL and MySQL applications select
their existing label-storage and OAuth packages; no new NuGet packages are published.
The base container supplies only ASP.NET runtime infrastructure and the matching CLI.
It contains no canonical executable, database provider, fallback store or secrets.

See the [labeler deployment guide](../../docs/docs/labeler/deployment.md) for environment
configuration, initial local administrator bootstrap, key installation, container deployment,
health checks, image verification, and update/recovery procedures.

For a local build, configure the required `Labeler:*` settings through environment variables
or reloadable JSON configuration, then run:

```powershell
dotnet run --project src\idunno.Bluesky.Labeler.Host.PostgreSQL
```

Select `idunno.Bluesky.Labeler.Host.MySQL` for MySQL. The executable projects' Visual Studio launch profiles use the `Development` environment and loopback-only
`https://localhost:50656` and `http://localhost:50657` endpoints. HTTPS requires the ASP.NET
development certificate. Automatic browser launch is disabled until a landing page is available.
The profile contains no secrets; the deployment-specific settings below are still required.

The shared `appsettings.json` contains only non-secret defaults, with no database selection:
environment-backed signing keys, disabled account/record issuance and remote administration,
no trusted proxies, a forwarding limit of one, and standard logging levels. Environment
variables override these settings.

Supply these deployment-specific environment variables separately:

| Variable | Purpose |
|---|---|
| `Labeler__IssuerDid` | The labeler's issuer DID |
| `Labeler__PublicEndpoint` | The public HTTPS origin |
| `Labeler__ConnectionString` | The protected connection string for the selected external engine |
| `Labeler__AdministrationAudience` | The labeler's exact `did:...#labeler_admin` service audience; the host derives `did:...#labeler_issuance` for agent issuance routes |
| `Labeler__DeclaredLabels` | Comma-separated permitted label values |
| `Labeler__InitialSigningKeyId` | The key ID used only to initialize database selection |
| `Labeler__SigningKeys__<index>__Id` | A stable key ID |
| `Labeler__SigningKeys__<index>__PrivateKeyBase64` | The protected base64-encoded private PEM for that ID |

Explicitly set `Labeler__AllowAccountLabels=true` and/or `Labeler__AllowRecordLabels=true`
for the target types your policy permits. Remote administration defaults to disabled. Set
`LabelerAdministration__RemoteMode=Enabled` to make authenticated remote CLI administration
available, or `WebUiControlled` to let only the current labeler owner enable or disable it from Xenon.
When `RemoteMode` is configured it takes precedence over legacy
`LabelerAdministration__RemoteEnabled`; when omitted, the old Boolean remains supported.
Neither setting bypasses authentication or team authorization. Protected local administration
remains available in every mode. Do not add private keys or database credentials to `appsettings.json`.

Choose the PostgreSQL or MySQL executable/image explicitly. Supply
`Labeler__ConnectionString` through protected environment configuration; no database credentials
or fallback SQLite connection string are built into either image. `Labeler__DatabaseProvider`,
if supplied, must match the selected variant; it cannot switch engines.

The host requires an issuer DID, explicit HTTPS public origin, database connection,
separately configured environment keys with database-backed selection by default, an administration DID service audience,
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

All host projects target .NET 10 and remain non-packable. Custom applications can project-reference
`LabelerApplicationExtensions.RunAsync`, or register a provider and call
`AddCanonicalBlueskyLabeler`, `InitializeCanonicalBlueskyLabelerAsync` and
`MapCanonicalBlueskyLabeler`. The individual extensions retain caller ownership of the HTTP
client and signing-key ring. Variant extensions include durable OAuth identities, correlation state
and origin-bound Data Protection keys/revocations when the Xenon UI is enabled.

Optional [Xenon moderation](../../docs/docs/labeler/moderation.md) is disabled by
default. Set `LabelerModeration__Enabled=true` to add standard independent-labeler
reports and shared case/appeal/policy administration. The same database, keys,
roles and maintenance coordination are used. Persisted independently verified
policy then replaces configuration-backed issuance permissions; declaration drift
or stale/unavailable verification closes reports and new issuance, not distribution
or safe withdrawal. Remote administration remains independently opt-in.

Build the container from the repository root:

```powershell
docker build --build-arg SOURCE_REVISION_ID=$(git rev-parse HEAD) --file src\idunno.Bluesky.Labeler.Host\Dockerfile --tag idunno-bluesky-labeler-base .
docker build --build-arg LABELER_BASE=idunno-bluesky-labeler-base --build-arg SOURCE_REVISION_ID=$(git rev-parse HEAD) --file src\idunno.Bluesky.Labeler.Host.PostgreSQL\Dockerfile --tag idunno-bluesky-labeler-postgresql .
```

Use the MySQL project's Dockerfile for MySQL. The base image has no inherited health check;
custom derived images must provide their own readiness probe. Stable and independently optional
prerelease publications sign all three images with keyless Cosign, attest their digests and OCI
subjects, and verify anonymous access. Prerelease publication never moves stable or latest tags.
