# Standalone labeler deployment

`idunno.Bluesky.Labeler.Host` is the canonical deployable ASP.NET Core application for the public label distribution
endpoints, authenticated remote administration, and OS-protected local administration.
It supports SQLite, PostgreSQL and MySQL storage, environment-supplied keys by default, an opt-in PEM key ring, and
configuration-driven issuance policy. Its configuration is deliberately external to the
image: no database password, account credential or private key is baked into the application.

The project lives in `src\idunno.Bluesky.Labeler.Host`, targets .NET 10, and is published as the
labeler container image, not a NuGet library. The independent SDK libraries remain available
for custom hosts. This application hosts the administration backend but does not include a web UI.
Build the image from the repository root with
`docker build --file src\idunno.Bluesky.Labeler.Host\Dockerfile --tag idunno-bluesky-labeler .`.

## Required configuration

Set the following configuration values through environment variables, a protected secret
provider, or a host-owned configuration file:

| Key | Purpose |
|---|---|
| `Labeler:IssuerDid` | The DID bound to the initialized label database |
| `Labeler:PublicEndpoint` | The externally published HTTPS origin; never inferred from `Host` |
| `Labeler:DatabaseProvider` | `postgresql` (default), or explicit `sqlite` or `mysql` |
| `Labeler:ConnectionString` | Persistent SQLite file or dedicated PostgreSQL/MySQL database |
| `Labeler:SigningKeyProvider` | `environment` (default) or explicit opt-in `pem`; configure only the selected provider |
| `Labeler:SigningKeyDirectory` | PEM provider: read-only directory containing one `<key-id>.pem` per preinstalled key; defaults to `/run/labeler-keys` |
| `Labeler:ActiveKeyPath` | PEM provider: writable persistent file containing the active key ID; defaults to `/data/active-key` |
| `Labeler:SigningKeys:<index>:Id` | Environment provider: stable, case-sensitive key ID using ASCII letters, digits, dots, underscores or hyphens |
| `Labeler:SigningKeys:<index>:PrivateKeyBase64` | Environment provider: separate protected variable per base64-encoded private PEM |
| `Labeler:InitialSigningKeyId` | Environment provider: explicit initial key ID, used only when the database has no selection |
| `Labeler:AdministrationAudience` | Exact `did:...#service-fragment` audience for service tokens |
| `Labeler:DeclaredLabels` | Comma-separated declared label values |
| `Labeler:AllowAccountLabels` | Whether the issuance policy permits account labels |
| `Labeler:AllowRecordLabels` | Whether the issuance policy permits record labels |
| `LabelerAdministration:RemoteEnabled` | Whether HTTPS administration is enabled; defaults to `false` |
| `Labeler:TrustedProxies` | Optional array of exact proxy IP addresses trusted to forward client IP and HTTPS scheme |
| `Labeler:ForwardLimit` | Positive maximum forwarding hops; defaults to `1` when trusted proxies are configured |

The host also configures a protected local endpoint. Its default is the Windows named pipe
`idunno-labeler-admin` or the Linux Unix socket `/run/labeler/admin.sock`. The socket and
all parent directories must be protected from replacement by other users. Remote administration
requires exact-audience method-bound service authentication and live team authorization; local
administration requires the service process's OS identity to have an explicit active team binding.
Neither localhost nor container placement is an authorization boundary.

### Native OAuth and TLS forwarding

The host serves `/native-client-metadata.json` publicly without authentication or redirects.
Its client ID is the configured `Labeler:PublicEndpoint` origin plus that path, never the
incoming `Host` header. The native public-client document declares `http://127.0.0.1/` as
its desktop loopback callback, with no fixed port, and the method-bound administrative scope
superset for `Labeler:AdministrationAudience`, plus `identity:*` for PLC identity publication
and recovery. Only those PLC commands request identity mutation permission. Use this HTTPS metadata URL as the CLI's
`--client-id`; the CLI requests only the method it is executing. The headless host does not
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
dotnet run --project src\idunno.Bluesky.Labeler.Host -- `
  --bootstrap-admin did:plc:administrator `
  --bootstrap-operation-id initial-team-setup
```

This one-shot command initializes storage, verifies the DID/handle and records the initial
OS binding. It exits without starting the HTTP host. Do not put bootstrap parameters in a
service's ordinary startup arguments or configuration.

## Key installation and rotation

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
The PEM key ring loads the preinstalled keys once and atomically replaces only the active-ID file
when an authorized rotation completes. Private keys never travel over remote administration.

Use the CLI's `rotation` commands to check status, begin maintenance, verify PLC publication
or apply the did:web snippet, re-sign bounded batches, and complete the operation. Every live
host must acknowledge draining, and signing hosts must acknowledge replacement-key activation.
Keyless distribution replicas remain keyless. A host that cannot acknowledge
draining must be independently verified as stopped and explicitly confirmed by an operator.
Never treat a timeout as proof that a process stopped. If a host restarts during maintenance,
it reads the durable operation, remains unavailable, and participates in the remaining
acknowledgements. Do not manually delete maintenance rows or reopen a partial rotation.

### Volume-free hosting on DigitalOcean App Platform

Choose `Labeler__SigningKeyProvider=environment`, use an external PostgreSQL or MySQL database,
and store its connection string and each private key in App Platform's **encrypted environment
variables**. Do not use ephemeral SQLite storage. For example, configure these variable names:

```text
Labeler__SigningKeyProvider=environment
Labeler__DatabaseProvider=postgresql
Labeler__ConnectionString=<encrypted external database connection string>
Labeler__SigningKeys__0__Id=original
Labeler__SigningKeys__0__PrivateKeyBase64=<encrypted base64-encoded private PEM>
Labeler__InitialSigningKeyId=original
```

Base64 only transports the PEM safely as one value; it is not encryption. Protect the original
key, deployment configuration and access to the running process. Do not put secret values in
source, image layers, command-line arguments or diagnostic output. The provider imports keys
into memory without creating private-key files.

On first setup the configured initial identity and its public key are transactionally bound
to the database's issuer and stream. Later starts use the database selection, ignoring a stale
initial ID. A missing selected key or a different private key under the same ID prevents startup;
there is no fallback. The database stores only the key ID and public binding, never the private
key. Schema version eight adds this selection without guessing one for existing deployments.
When switching an existing PEM deployment, explicitly seed its current key ID and matching
private key; a persisted rotation must agree with that ID and, after completion, its public key.

For rotation, add a second encrypted variable (for example,
`Labeler__SigningKeys__1__PrivateKeyBase64`) and its `Id`, then redeploy through App Platform so
every signing host has both keys **before beginning rotation**. Keep the original variable during
that rollout. Use the existing authorized rotation workflow; selection changes are permitted
only after verified publication, completed re-signing and universal draining. Shared persistence
does not substitute for each signing host's activation acknowledgement. A restart resumes from
the database's selection and maintenance checkpoint. After rotation completes and every signing
host has activated the replacement, the old private key can be removed in a further deployment.
Do not recycle a key ID with different key material.

Back up the database and protected secret configuration together, and restore a matching set.
Do not reset selection rows to recover from a missing secret. App Platform redeployments replace
images/configuration rather than mutating a running container. Configure the platform's HTTP
readiness check to use `/health/labeler/ready` on the internal port; do not assume it consumes the
image's Docker health status.

This provider removes the signing-key volume requirement, not other setup requirements.
The local socket may remain on the ephemeral filesystem without a shared mount. Perform the
explicit initial administrator bootstrap under the correct OS identity through a supported
setup execution path; retain the existing authenticated remote-administration requirements.
Configure explicitly trusted proxy forwarding for the actual deployment topology rather than
trusting arbitrary forwarded headers. The public origin, issuer, audience and policy settings
are still required.

## Run the GHCR image

Release tags publish the standalone `linux/amd64` image to the public package
`ghcr.io/blowdart/idunno-bluesky-labeler`. The release includes
`idunno.Bluesky.Labeler.image-digest.txt`, which contains the immutable digest reference
produced by that workflow. Verify the release artifact before deployment and pin the image
by digest rather than using a mutable tag:

Before the first image release, a repository maintainer must set the GHCR container package's
visibility to **public** in the package settings. GitHub Actions verifies anonymous access to
the pushed image before uploading its digest; it does not attempt to change package visibility
through an unsupported API.

```text
gh attestation verify idunno.Bluesky.Labeler.image-digest.txt --repo blowdart/idunno.Bluesky
docker pull ghcr.io/blowdart/idunno-bluesky-labeler@sha256:<release-digest>
```

The image runs as UID 1654, listens on port 8080 and defaults to PostgreSQL, as does a direct
run of the application. Supply `Labeler__ConnectionString` through protected configuration;
the image contains no default connection string or database credentials. When using SQLite
and opt-in PEM keys, set `Labeler__DatabaseProvider=sqlite`,
`Labeler__ConnectionString=Data Source=/data/labels.db`, and `Labeler__SigningKeyProvider=pem`,
mount `/data` as durable storage, the key directory read-only, and a
service-owned mode-`0700` socket directory at `/run/labeler`. A bind mount replaces the image's
directory permissions: set its ownership to UID 1654 and mode to `0700` on the host too.
Supply the required identity, policy and
administration audience settings separately. For remote databases, set
`Labeler__DatabaseProvider` and `Labeler__ConnectionString` using a secret provider. A
container deployment must retain its external database, configuration, key material and active
key state when replacing the image. Environment-backed deployments use the shared database and
encrypted platform variables instead of key/data mounts.

The image exposes `/health/labeler/live` and `/health/labeler/ready` through the standard
ASP.NET Core health-check middleware. Liveness does not require database access; readiness
verifies the publisher and durable store. Its Docker `HEALTHCHECK` probes readiness on
`http://127.0.0.1:8080/health/labeler/ready` every 30 seconds, with a 30-second startup grace
period, a 10-second process timeout and three failures before marking the container unhealthy.
It runs `dotnet idunno.Bluesky.Labeler.Host.dll --health-check <URL>` as the container user, without
starting another host or loading configuration, keys or database connections. The probe accepts
only HTTP 200, does not follow redirects or use an outbound HTTP proxy, and has a five-second
HTTP timeout. No curl installation is required.

Docker Compose inherits this check from the image. If you change the container's listening port
or use HTTPS-only binding, override the check with the actual internal readiness URL, for example:

```yaml
healthcheck:
  test: ["CMD", "dotnet", "idunno.Bluesky.Labeler.Host.dll", "--health-check", "http://127.0.0.1:8081/health/labeler/ready"]
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
Configure WebSocket
forwarding, TLS termination, idle timeouts, graceful shutdown and readiness-based traffic
draining at the platform boundary. A filesystem Unix socket can be shared with a host-side
CLI only when the deployment platform supports a protected bind-mounted socket directory and
matching service UID; otherwise use the authenticated HTTPS remote transport.

## Safe updates and rollback

For Docker Compose, use an image variable and keep every stateful path outside the container:

```yaml
services:
  labeler:
    image: ${LABELER_IMAGE:?Set an immutable release digest}
    ports:
      - "8080:8080"
    stop_grace_period: 90s
    environment:
      Labeler__DatabaseProvider: sqlite
      Labeler__ConnectionString: Data Source=/data/labels.db
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
  --image ghcr.io/blowdart/idunno-bluesky-labeler@sha256:<new-digest> \
  --previous-image ghcr.io/blowdart/idunno-bluesky-labeler@sha256:<current-digest> \
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

The CLI ZIPs are self-contained per-platform releases. Verify their GitHub release attestation
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

* **SQLite:** use a persistent local file on storage with reliable locking; do not place the
  database on an unsupported network filesystem.
* **PostgreSQL:** use a dedicated PostgreSQL 17+ database and a connection secret scoped to the
  labeler. Preserve transactions, migrations and backups together.
* **MySQL:** use a dedicated MySQL 8.4+ database with compatible transaction and collation
  settings. Use the provider's supported backup and restore process.

For all providers, test a restore and a rolling replacement against a copy before a production
upgrade. A deployment image does not include or own your data, TLS certificates, signing keys,
identity records or decision policy.
