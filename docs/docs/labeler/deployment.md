# Standalone labeler deployment

`idunno.Bluesky.Labeler.Host` is the provider-neutral, non-packable shared canonical hosting library.
Thin `idunno.Bluesky.Labeler.Host.PostgreSQL` and `idunno.Bluesky.Labeler.Host.MySQL` applications
compose it with the existing label-storage and OAuth provider packages. No new NuGet packages
are introduced. Both applications host public label distribution, authenticated remote administration,
OS-protected local administration and an [optional Xenon web UI](ui.md), disabled by default.
Database servers remain external; neither image bundles a database or falls back to SQLite.
Environment-supplied signing keys are the default, with an opt-in PEM key ring and configuration-driven
issuance policy. No database password, account credential or private key is baked into an image.

All three projects target .NET 10. Build the provider-neutral base and then the desired variant
from the repository root:

```powershell
docker build --build-arg SOURCE_REVISION_ID=$(git rev-parse HEAD) --file src\idunno.Bluesky.Labeler.Host\Dockerfile --tag idunno-bluesky-labeler-base .
docker build --build-arg LABELER_BASE=idunno-bluesky-labeler-base --build-arg SOURCE_REVISION_ID=$(git rev-parse HEAD) --file src\idunno.Bluesky.Labeler.Host.PostgreSQL\Dockerfile --tag idunno-bluesky-labeler-postgresql .
```

Use the MySQL project's Dockerfile and the `idunno-bluesky-labeler-mysql` tag for MySQL.
Release builds pin the base by digest rather than a mutable tag.
The source revision is embedded in the Xenon UI footer as the build identifier.
The image also includes the matching Linux CLI in `/opt/labeler-cli`, which is on
`PATH`. Run `docker exec <container> labeler-admin --help` for its commands. The
CLI runs as the container's configured user; existing local identity authorization
and remote-administration restrictions still apply.

### Trimming and Native AOT support

The canonical host ships untrimmed and does not support trimmed or Native AOT
deployment. It includes the Xenon and authentication Razor Pages UI packages;
Razor Pages does not support trimming or Native AOT. Disabling the UI through
runtime configuration does not remove those build-time dependencies.

This is an architectural support boundary, not a pending trimming experiment.
The host remains excluded from the supported-library trimming/AOT matrix in
`trimTest.ps1`. That exclusion does not extend to independently supported SDK
libraries or the trimmed CLI. A separately built headless host would require its
own evaluation and is not part of the canonical host's supported configurations.

The host serves `/robots.txt` anonymously as plain text with `User-agent: *` and
`Disallow: /`, asking crawlers not to crawl any path. This is advisory and does not
replace authentication or access controls.

## Metrics collection

Both canonical variants support independently opt-in Prometheus scraping and OTLP
metrics export. Neither exporter is enabled by default. They collect the persistence,
HTTP distribution, moderation, authentication, AT Protocol HTTP client, directory
and DID/handle-cache meters, plus built-in ASP.NET Core, Kestrel and HTTP client
measurements. Selecting a meter does not enable its optional feature. See the
[metrics reference](../metrics.md) for instrument names and units.

Prometheus uses a separate Kestrel listener that serves only `/metrics`; the public
labeler listener does not map a metrics route. Enable it with:

```text
LabelerTelemetry__Prometheus__Enabled=true
LabelerTelemetry__Prometheus__Address=127.0.0.1
LabelerTelemetry__Prometheus__Port=9464
```

Scrape `http://127.0.0.1:9464/metrics` from the same network namespace. The address
must be a literal IPv4 or IPv6 address, and the port must be between 1 and 65535.
For a collector in another container on a private Docker network, explicitly bind
to `0.0.0.0` and let that collector scrape `<labeler-container>:9464`. Do not
publish this port publicly or route it through the public reverse proxy. If a
host-local collector needs a published port, bind the Docker publication to
loopback, for example `-p 127.0.0.1:9464:9464`, with the container listener bound
to `0.0.0.0`. The scrape endpoint has no authentication or TLS; restrict access
with private networking and firewall rules. A listener bind failure fails host
startup rather than silently dropping telemetry. It stops with the main host.

OTLP pushes metrics rather than exposing an OTLP ingestion endpoint. Enable it
and configure an external collector, for example:

```text
LabelerTelemetry__Otlp__Enabled=true
LabelerTelemetry__Otlp__Endpoint=http://collector:4318/v1/metrics
LabelerTelemetry__Otlp__Protocol=http/protobuf
OTEL_SERVICE_NAME=my-labeler
```

`LabelerTelemetry:Otlp:Endpoint` and `Protocol` override the exporter's standard
`OTEL_EXPORTER_OTLP_*` environment settings. If omitted, the exporter uses those
settings and its defaults (gRPC at `http://localhost:4317`). The explicit protocol
accepts `grpc` or `http/protobuf`; for HTTP/protobuf use the full `/v1/metrics`
URL. Use HTTPS when telemetry crosses a trusted-network boundary. Exporter headers
and timeouts can use the standard OpenTelemetry environment settings; keep headers
in protected configuration, not source control. `OTEL_METRIC_EXPORT_INTERVAL`
controls periodic export. The default service name is
`idunno.Bluesky.Labeler.Host`, overridable with `OTEL_SERVICE_NAME`.

These exporters belong to the non-packable canonical host, not to the persistence
or hosting NuGet packages. The official OpenTelemetry Prometheus ASP.NET Core
exporter is currently prerelease; the host pins the exporter release matching its
OpenTelemetry SDK. No database server or telemetry collector is bundled.

## Required configuration

When Xenon is enabled in either canonical variant, the host also uses
`Labeler:ConnectionString` for website identities, refresh locks, single-use OAuth
login state and the append-only `labeler_data_protection_keys` key ring.
Authentication tables are initialized additively before serving requests.
The Data Protection application name is stable and bound to the configured public
origin, so signed-in sessions survive container replacement at that origin.
Changing the origin intentionally changes the protection boundary.

Identity and OAuth state payloads are protected with Data Protection. The database
key ring is not separately encrypted by default: database access and backups must
be restricted as secrets, because access to both keys and payloads permits
decryption. Back up these tables together with the labeler database; do not delete
old keys or revocation records. Existing sessions lost by the earlier ephemeral
configuration require one new sign-in. PostgreSQL and MySQL use their respective existing
OAuth stores; refresh locks and single-use correlation state remain database-coordinated.

Set the following configuration values through environment variables, a protected secret
provider, or a host-owned configuration file:

| Key | Purpose |
|---|---|
| `Labeler:IssuerDid` | The DID bound to the initialized label database |
| `Labeler:PublicEndpoint` | The externally published HTTPS origin; never inferred from `Host` |
| `Labeler:DatabaseProvider` | Optional compatibility check: must match the chosen variant (`postgresql` or `mysql`); it cannot change providers |
| `Labeler:ConnectionString` | Dedicated external PostgreSQL or MySQL database, matching the executable/image |
| `Labeler:SigningKeyProvider` | `environment` (default) or explicit opt-in `pem`; configure only the selected provider |
| `Labeler:SigningKeyDirectory` | PEM provider: read-only directory containing one `<key-id>.pem` per preinstalled key; defaults to `/run/labeler-keys` |
| `Labeler:ActiveKeyPath` | PEM provider: writable persistent file containing the active key ID; defaults to `/data/active-key` |
| `Labeler:SigningKeys:<index>:Id` | Environment provider: stable, case-sensitive key ID using ASCII letters, digits, dots, underscores or hyphens |
| `Labeler:SigningKeys:<index>:PrivateKeyBase64` | Environment provider: separate protected variable per base64-encoded private PEM |
| `Labeler:InitialSigningKeyId` | Environment provider: explicit initial key ID, used only when the database has no selection |
| `Labeler:AdministrationAudience` | Exact `did:...#labeler_admin` audience for administration service tokens; the host derives the matching `#labeler_issuance` audience from the same DID |
| `Labeler:DeclaredLabels` | Comma-separated declared label values |
| `Labeler:AllowAccountLabels` | Whether the issuance policy permits account labels |
| `Labeler:AllowRecordLabels` | Whether the issuance policy permits record labels |
| `LabelerAdministration:RemoteMode` | `Disabled` (default), `Enabled`, or `WebUiControlled`; overrides the legacy Boolean |
| `LabelerAdministration:RemoteEnabled` | Legacy fallback only when `RemoteMode` is absent; defaults to `false` |
| `LabelerOzone:Enabled` | Startup opt-in to `MapOzoneCompatibility()` discovery and labeler-focused administration; defaults to `false` and does not enable remote administration |
| `LabelerOzone:AllowedOrigins` | Exact Ozone UI HTTP(S) origin array, including non-default ports and without trailing slashes; defaults to empty (no cross-origin access). For a local UI, set `LabelerOzone__AllowedOrigins__0=http://127.0.0.1:3000`. CORS is automatic and limited to mapped Ozone routes; changes require a restart |
| `LabelerModeration:Enabled` | Optional Xenon report/case/policy backend; defaults to `false` |
| `LabelerModeration:VerificationFreshness` | Persisted declaration-observation lifetime; defaults to `00:30:00`; active policies are checked in the background every five minutes |
| `Labeler:TrustedProxies` | Optional array of exact proxy IP addresses trusted to forward client IP and HTTPS scheme |
| `Labeler:ForwardLimit` | Positive maximum forwarding hops; defaults to `1` when trusted proxies are configured |

The host also configures a protected local endpoint. Its default is the Windows named pipe
`idunno-labeler-admin` or the Linux Unix socket `/run/labeler/admin.sock`. The socket and
all parent directories must be protected from replacement by other users. Remote administration
requires exact-audience method-bound service authentication and live team authorization; local
administration requires the service process's OS identity to have an explicit active team binding.
Neither localhost nor container placement is an authorization boundary.

Enabling Ozone opts into [limited compatibility](administration.md#optional-ozone-compatibility),
not a complete Ozone implementation. Use Xenon/native views as the primary moderation
interface. Unresolved URI-only records can be absent from Ozone event/status views
without a visible warning in stock Ozone; those views must not be the sole inventory
of outstanding moderation work. This is an accepted limitation of the supported subset.

`RemoteMode=Disabled` keeps remote CLI routes unavailable; `Enabled` allows them subject to those
existing checks. `WebUiControlled` additionally requires the durable switch to be enabled by the
current owner in Xenon. The owner is initially the DID that completed the initial OS-authorized bootstrap,
or an active Administrator to whom the current owner explicitly transferred ownership,
not any Administrator. The durable switch defaults off, is transactionally audited, and does not
affect protected local administration. An explicitly configured `RemoteMode` always takes
precedence over `RemoteEnabled`. If `RemoteMode` is omitted, the old Boolean continues to apply,
so existing deployments are not silently enabled.

### Native OAuth and TLS forwarding

The host serves `/native-client-metadata.json` publicly without authentication or redirects.
Its client ID is the configured `Labeler:PublicEndpoint` origin plus that path, never the
incoming `Host` header. The native public-client document declares `http://127.0.0.1/` as
its desktop loopback callback, with no fixed port, and the method-bound administrative scope
superset for `Labeler:AdministrationAudience`, plus `identity:*` for PLC identity publication
and recovery, and create/update permission for `app.bsky.labeler.service` publication.
Only PLC commands request identity mutation permission. Policy publication uses a separate
labeler-account sign-in with only its selected declaration write action. Use this HTTPS metadata URL as the CLI's
`--oauth-client-id`; ordinary commands request only their method, and policy publication requests
each publication/recovery method separately as needed. The headless host does not
run the desktop callback listener or distribute an OAuth client secret.

For TLS termination at a reverse proxy, configure the proxy's **actual immediate peer IP**
in `Labeler:TrustedProxies`, for example `Labeler__TrustedProxies__0=172.20.0.2`, and keep the
container HTTP port private to that proxy network. The proxy must replace, not append to
untrusted client-supplied headers, and forward both `X-Forwarded-For` and
`X-Forwarded-Proto: https`. Set `Labeler__ForwardLimit` to the exact supported hop count when
multiple trusted proxies are involved, and list every trusted proxy IP. No forwarding headers
are processed when the list is empty; arbitrary client addresses never become trusted.
Without a TLS-terminating proxy, configure Kestrel HTTPS directly. Do not enable the
ASP.NET Core environment shortcut that trusts all forwarded headers.

Identity resolution uses the existing `idunno.Security.Ssrf` connection-time DNS/IP protection,
with redirects disabled; private and loopback issuer endpoints are not allowed.

Initialize the store before serving traffic. The application calls `InitializeAsync` at startup,
so its database identity must have schema migration privileges. For deployments that separate
database setup from serving credentials, run initialization as a deliberate setup step and
restrict serving credentials to the required reads and writes. Back up the complete database
before upgrades and verify the release's schema compatibility notes.

Bootstrap the first administrator only from an explicitly invoked setup process running as the
OS identity that will use local administration:

```powershell
dotnet run --project src\idunno.Bluesky.Labeler.Host.PostgreSQL -- `
  --bootstrap-admin did:plc:administrator `
  --bootstrap-operation-id initial-team-setup
```

This one-shot command initializes storage, verifies the DID/handle and records the initial
OS binding. It exits without starting the HTTP host. Do not put bootstrap parameters in a
service's ordinary startup arguments or configuration. Select the MySQL project for a MySQL deployment.

## Custom applications and derived images

Custom .NET 10 applications can reference the non-packable shared host project and reuse
`AddCanonicalBlueskyLabeler`, `InitializeCanonicalBlueskyLabelerAsync` and
`MapCanonicalBlueskyLabeler` extensions, built on the existing registration and mapping helpers.
Register exactly one storage provider first. With Xenon enabled, register exactly one
`ILabelerHostAuthenticationPersistence` integration and durable OAuth/Data Protection services.
The variant extensions `AddPostgreSqlCanonicalLabelerStorage` and `AddMySqlCanonicalLabelerStorage`
provide that wiring through project references, without another builder or NuGet package.

`LabelerApplicationExtensions.RunAsync(args, configureStorage)` reuses the complete canonical
configuration, bootstrap, key/HTTP-client ownership and shutdown lifecycle. Applications
composing the individual extensions own their hardened identity client and signing-key ring;
keep both alive until the application is disposed, and pass the same ring during registration
and initialization. Identity, feature flags and signing role are captured at registration.
For independently selected behavior, applications can instead compose the independent provider,
`AddBlueskyLabeler`, optional `AddLabelerModeration`, and
`AddLabelerAdministration` services. Initialize the shared store and optional
policy gate before serving, using the same issuer, hardened caller-owned HTTP
client and protected signing-key ring. Bootstrap deliberately through the
existing setup boundary, never automatically from an anonymous request.

Map `MapBlueskyLabeler()`, `MapLabelerModeration()` when enabled, and protected
administration. `MapBlueskyLabeler()` already maps liveness and readiness; mapping
another endpoint at the same paths creates ambiguous routes. Preserve WebSocket
middleware, explicit trusted-proxy configuration and remote administration's
disabled-by-default setting.

Custom decisioning consumes `LabelCaseDecision` and shared issuance rather than
appending separately signed labels or inventing caller DIDs. Keep runtime
mutations on the shared protected administration adapters so they participate
in host admission/draining and key rotation. A custom worker that owns signing
must also honor the existing durable host-role, draining and key-activation
contracts; sharing a database alone is not a drain acknowledgment. Do not expose
raw trusted infrastructure services as anonymous HTTP handlers.

The provider-neutral base image supplies the non-root ASP.NET runtime, matching CLI, port and writable
directory layout, with no database provider or canonical executable. Its default command is
`labeler-admin --help`; it is not a running labeler. A derived image can add a framework-dependent custom
publish directory without copying secrets or replacing that infrastructure:

```dockerfile
ARG LABELER_BASE
FROM ${LABELER_BASE}
COPY --chown=1654:1654 publish/ /app/custom/
ENTRYPOINT ["dotnet", "/app/custom/CustomLabeler.dll"]
```

Publish the custom application for the image's .NET version and architecture,
including its dependencies and native runtime assets, into `publish/`.
Set `LABELER_BASE` at build time to the verified immutable
`ghcr.io/blowdart/idunno-bluesky-labeler-base@sha256:<release-digest>` reference.
The base has no inherited health check. Supply a probe for the custom application's actual
readiness endpoint, without loading another host, keys or database connections.
Retain UID 1654, private networking and externally supplied database/key configuration.
Production deployments must use real discovery and independent policy verification.
No trimmed-host support is claimed.

## Key installation and rotation

[Optional Xenon moderation](moderation.md) is enabled with
`LabelerModeration__Enabled=true`. Its immutable, independently verified policy
replaces configuration-backed issuance permissions; `Labeler:DeclaredLabels`
and `Labeler:Allow*Labels` do not override that persisted policy. A new gate is
fail-closed until explicit policy publication and verification. The public report
route does not require enabling remote administration.

The canonical host defaults to environment-backed keys. Supply the current key and, when
needed, a retained previous key as separate indexed variables:

```text
Labeler__SigningKeys__0__Id=current-key-id
Labeler__SigningKeys__0__PrivateKeyBase64=<encrypted base64-encoded private PEM>
Labeler__SigningKeys__1__Id=previous-key-id
Labeler__SigningKeys__1__PrivateKeyBase64=<encrypted base64-encoded previous private PEM>
Labeler__InitialSigningKeyId=current-key-id
```

The previous-key entry is optional; omit both of its variables when not needed. These indices
do not assign active/previous roles. Keep each key's stable ID across redeployments; the database
selects the active key, and `InitialSigningKeyId` only seeds a new database. A deployment still
using the previous key remains on that key until the authorized rotation completes. Add
replacement keys before beginning rotation rather than changing configuration to switch keys.
Never retain private keys longer than the rotation/recovery procedure requires.

For filesystem-backed installations, explicitly set `Labeler__SigningKeyProvider=pem` and
install private keys on each host before starting or rotating it. Use opaque filenames such as
`active.pem` and `replacement.pem`; the filename stem is the key ID. Mount the key directory
read-only and the active-key file on writable persistent storage. Protect PEM contents with
the host's secret-management permissions (for Unix, mode `0600` and an owner-only directory).
The PEM key ring loads the preinstalled keys once. On first setup it seeds database selection
from the active-ID file; thereafter the database selection is authoritative, just as for
environment-backed keys. Startup validates the selected private key against its persisted
public key and repairs a stale active-ID file. Authorized activation updates the database
before atomically updating that local file; a failed file update prevents acknowledgement
and can be recovered on restart or retry. Private keys never travel over remote administration.

To switch between environment and PEM providers, install the same stable key IDs and matching
private keys, including any keys needed by an in-progress rotation. PEM still requires a readable
active-ID file, but its value cannot override an existing database selection. Missing or
mismatched database-selected keys prevent startup. Do not reset selection rows when switching providers.

Use the CLI's `rotation` commands to check status, begin maintenance, verify PLC publication
or apply the did:web snippet, re-sign bounded batches, and complete the operation. Every live
host must acknowledge draining, and signing hosts must acknowledge replacement-key activation.
Keyless distribution replicas remain keyless. A host that cannot acknowledge
draining must be independently verified as stopped and explicitly confirmed by an operator.
Never treat a timeout as proof that a process stopped. If a host restarts during maintenance,
it reads the durable operation, remains unavailable, and participates in the remaining
acknowledgements. Do not manually delete maintenance rows or reopen a partial rotation.

## Run the GHCR image

Stable release tags publish three `linux/amd64` images:

| Package | Purpose | Digest file |
|---|---|---|
| `ghcr.io/blowdart/idunno-bluesky-labeler-base` | Provider-neutral infrastructure for custom derived applications | `idunno.Bluesky.Labeler.base.image-digest.txt` |
| `ghcr.io/blowdart/idunno-bluesky-labeler-postgresql` | Canonical PostgreSQL host | `idunno.Bluesky.Labeler.postgresql.image-digest.txt` |
| `ghcr.io/blowdart/idunno-bluesky-labeler-mysql` | Canonical MySQL host | `idunno.Bluesky.Labeler.mysql.image-digest.txt` |

The `labeler-image-digests` workflow artifact contains the immutable references. Images carry
Buildx provenance and SBOMs, GitHub attestations and mandatory keyless Cosign signatures.
Verify the digest file, OCI attestation and signature before deployment; pin the image by digest.
Before the first publication, a repository maintainer must set all three GHCR packages'
visibility to **public** in the package settings. GitHub Actions verifies anonymous access to
the pushed image before uploading its digest; it does not attempt to change package visibility
through an unsupported API.

The manual prerelease workflow independently offers `Sign NuGet packages`, `Publish Prerelease` (NuGet)
and `Publish signed prerelease container images`. Container publication does not require NuGet
publication or NuGet signing: it builds the checked-out source and always signs all three images.
Prerelease tags are `vMAJOR.MINOR.PATCH-prerelease-<run-id>-<attempt>-<short-sha>`.
They never move stable version or `latest` tags. Stable tags remain exactly `vMAJOR.MINOR.PATCH`.

```text
gh attestation verify idunno.Bluesky.Labeler.postgresql.image-digest.txt --repo blowdart/idunno.Bluesky
gh attestation verify oci://ghcr.io/blowdart/idunno-bluesky-labeler-postgresql@sha256:<release-digest> --repo blowdart/idunno.Bluesky
cosign verify ghcr.io/blowdart/idunno-bluesky-labeler-postgresql@sha256:<release-digest> --certificate-oidc-issuer https://token.actions.githubusercontent.com --certificate-identity https://github.com/blowdart/idunno.Bluesky/.github/workflows/publish-labeler-images.yml@refs/tags/v9.0.0
docker pull ghcr.io/blowdart/idunno-bluesky-labeler-postgresql@sha256:<release-digest>
```

Replace the identity's release tag with the exact published version; for prereleases, use
`publish-labeler-images.yml@refs/heads/version/vMAJOR.MINOR.PATCH`. Fulcio identifies the reusable
signing workflow, not its caller. Select the matching MySQL image/file when appropriate.

Both canonical images run as UID 1654 and listen on port 8080. Supply `Labeler__ConnectionString`
for the chosen external engine through protected configuration; neither image contains a default
connection string, database credentials or an alternative provider. With opt-in PEM keys, set
`Labeler__SigningKeyProvider=pem`, mount the key directory read-only and the active-key file on durable
storage, and use a service-owned mode-`0700` socket directory at `/run/labeler`. A bind mount replaces the image's
directory permissions: set its ownership to UID 1654 and mode to `0700` on the host too.
Supply the required identity, policy and
administration audience settings separately. For remote databases, set
`Labeler__ConnectionString` using a secret provider. A
container deployment must retain its external database, configuration, key material and active
key state when replacing the image. Environment-backed deployments use the shared database and
encrypted platform variables instead of key/data mounts.

The image exposes `/health/labeler/live` and `/health/labeler/ready` through the standard
ASP.NET Core health-check middleware. Liveness does not require database access; readiness
verifies the publisher and durable store. Its Docker `HEALTHCHECK` probes readiness on
`http://127.0.0.1:8080/health/labeler/ready` every 30 seconds, with a 30-second startup grace
period, a 10-second process timeout and three failures before marking the container unhealthy.
It runs `dotnet idunno.Bluesky.Labeler.Host.PostgreSQL.dll --health-check <URL>` (or the MySQL DLL) as the container user, without
starting another host or loading configuration, keys or database connections. The probe accepts
only HTTP 200, does not follow redirects or use an outbound HTTP proxy, and has a five-second
HTTP timeout. No curl installation is required.

Docker Compose inherits this check from the image. If you change the container's listening port
or use HTTPS-only binding, override the check with the actual internal readiness URL, for example:

```yaml
healthcheck:
  test: ["CMD", "dotnet", "idunno.Bluesky.Labeler.Host.PostgreSQL.dll", "--health-check", "http://127.0.0.1:8081/health/labeler/ready"]
  interval: 30s
  timeout: 10s
  start_period: 30s
  retries: 3
```

An HTTPS probe requires a certificate trusted by the container and valid for the probe hostname.
Published host ports do not change the internal probe URL. Maintenance and draining deliberately
make readiness fail; do not configure automatic restarts solely on failed readiness. Docker's
health status does not itself restart a container or remove it from traffic. Configure your
platform to use readiness for routing and `/health/labeler/live` for separate liveness checks.
The public ingress must accept incoming secure WebSocket connections at
`wss://<labeler-host>/xrpc/com.atproto.label.subscribeLabels` and forward them to the
application; outbound WebSocket connectivity alone is insufficient. Verify subscription
delivery, idle-connection behavior and cursor-based reconnect/replay through the public
endpoint, including after redeployment.
Configure WebSocket forwarding, TLS termination, idle timeouts, graceful shutdown and readiness-based traffic
draining at the platform boundary. A filesystem Unix socket can be shared with a host-side
CLI only when the deployment platform supports a protected bind-mounted socket directory and
matching service UID; otherwise use the authenticated HTTPS remote transport.

## Safe updates and rollback

Schema sixteen adds the escalated case state and upgrades the case-status check
in each relational provider. Stop all older writers before initialization and
take a verified whole-database backup. A schema-fifteen host rejects the upgraded
database: retaining the old image alone is not a rollback plan. Use a compatible
host or restore the pre-upgrade backup, accounting for any writes after deployment.

For Docker Compose, use an image variable and keep every stateful path outside the container:

```yaml
services:
  labeler:
    image: ${LABELER_IMAGE:?Set an immutable release digest}
    ports:
      - "8080:8080"
    stop_grace_period: 90s
    environment:
      Labeler__ConnectionString: ${LABELER_CONNECTION:?Set a protected external PostgreSQL connection}
      Labeler__SigningKeyProvider: pem
    volumes:
      - /srv/labeler/data:/data
      - /srv/labeler/keys:/run/labeler-keys:ro
      - /srv/labeler/run:/run/labeler
```

Set `LABELER_IMAGE` to the current immutable digest and retain it for rollback. Before an update,
back up the database, active-key state and configuration, verify the target image digest and
schema compatibility, and configure a `stop_grace_period` long enough for the host to drain
requests and subscriptions. The CLI's Compose updater requires explicit backup and compatibility
confirmations, pulls both image digests first, writes a temporary image-only override, and
recreates only the selected service:

```text
labeler-admin service-update \
  --compose-file ./compose.yml \
  --service labeler \
  --image ghcr.io/blowdart/idunno-bluesky-labeler-postgresql@sha256:<new-digest> \
  --previous-image ghcr.io/blowdart/idunno-bluesky-labeler-postgresql@sha256:<current-digest> \
  --ready-url https://labeler.example/health/labeler/ready \
  --backup-confirmed \
  --schema-compatible \
  --drain-seconds 90
```

The command prompts before updating; use `--yes` only in a controlled script. It never edits the
Compose project, modifies a running container, removes volumes or changes the database backup.
After recreation it polls the readiness endpoint for up to the configured timeout and redeploys
the previous digest if readiness fails. Verify the restored service's readiness before retrying.
If the command is interrupted during deployment, inspect `docker compose ps`, the running image
digest and readiness endpoint before taking action. Never run an older binary against a database
already migrated by a newer release.
Select the matching MySQL image for MySQL; changing image variants does not migrate databases.
The updater also accepts legacy monolithic digests for compatible rollback, but never the base
image as a service. Assess provider and schema compatibility explicitly.

The CLI ZIPs contain trimmed, self-contained single-file executables for each supported platform;
they bundle the runtime and do not require a separate .NET installation. Verify their GitHub release attestation
before first installation or replacement. Keep configuration and secrets outside the executable
directory. For a supported direct installation, `labeler-admin update --tag v9.0.1` performs that
attestation verification, checks the staged executable, and atomically replaces it after the
current process exits while retaining a sibling backup. The updater does not alter a labeler
service, configuration or data.

Container updates are supported for Docker Compose deployments only. DigitalOcean App Platform,
Azure App Service and other orchestrators can run the public image only when their plan supports
the required WebSocket behavior, persistent external storage, long-lived connections, health
checks and graceful replacement. Select and roll back images through that platform's deployment
mechanism; do not assume the CLI can safely redeploy an arbitrary provider-managed service.

## Provider examples

* **SQLite (custom hosts only):** compose the existing SQLite package explicitly, using a persistent
  local file on storage with reliable locking; do not place it on an unsupported network filesystem.
* **PostgreSQL:** use a dedicated PostgreSQL 17+ database and a connection secret scoped to the
  labeler. Preserve transactions, migrations and backups together.
* **MySQL:** use a dedicated MySQL 8.4+ database with compatible transaction and collation
  settings. Use the provider's supported backup and restore process.

For all providers, test a restore and a rolling replacement against a copy before a production
upgrade. A deployment image does not include or own your data, TLS certificates, signing keys,
identity records or decision policy.
