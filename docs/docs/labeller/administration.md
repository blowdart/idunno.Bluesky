# Administrative authentication and teams

The Bean Bureau's public label queries don't establish who may issue labels. Administration is a
separate, opt-in boundary, shared by remote HTTPS and protected local IPC. Neither a successful
Bluesky sign-in nor being on the labeller's machine grants a role.

These are SDK services and adapters, not a CLI, moderation UI or runnable host. Those applications
must consume this boundary rather than construct actor DIDs from request fields or expose raw storage.
The `dev.idunno.labeler.admin.*` methods are this library's administration protocol, not Ozone or
standard AT Protocol moderation endpoints.

## Roles and persistence

| Role | Permission |
|---|---|
| Viewer | Inspect team, administrative audit, operation receipts and publication progress |
| Moderator | Viewer permissions plus issue and negate labels |
| Administrator | Moderator permissions plus team/binding management and maintenance authorization |

Authority is persisted by **DID**, never handle. `LabelTeamService` verifies a matching DID document;
enrollment by handle also verifies the handle in both directions. Enrollment is an administrator's
grant, not proof that a new member has signed in. Members authenticate independently.

Choose one existing SQLite, PostgreSQL or MySQL provider. Team state, bindings, token replay state,
revocations, administrative audit and receipts live in that provider's issuer-bound database.
Schema version six upgrades versions one through five additively through `InitializeAsync`.
Initialization also installs the administrative token-expiry index on existing version-four
databases so cleanup can select expired replay entries without scanning all retained tokens.
Version five adds durable coordinated maintenance and signing-key rotation state. Version six
adds an indexed subject/value identity for bounded decision lookups during retirement; interrupted
backfills resume without changing signed history. Back up all tables together and stop older
application versions before upgrading.

Team changes, last-administrator checks and label commit fences use the same provider serialization
lock. Concurrent removal, suspension or demotion cannot eliminate the last active administrator.
Membership and binding generations prevent a removed/re-enrolled member or replaced binding from
reviving previously admitted invocation evidence.

## Register the shared boundary

After registering storage and an `ILabelIssuancePolicyProvider`, opt in explicitly:

```csharp
AtProtoServiceReference audience = new(
    new Did("did:web:labeler.example"), "labeler_admin");

builder.Services.AddLabelerAdministration(
    audience: audience,
    signingKey: labelSigningKey,
    httpClient: identityHttpClient);

// Optional: a protected local administration endpoint.
builder.Services.AddLabelerLocalAdministration(
    OperatingSystem.IsWindows() ? "bean-bureau-admin" : "/run/bean-bureau/admin.sock");

// Remote administration is disabled by default and follows reloadable configuration.
builder.Services.Configure<LabelerAdministrationOptions>(
    builder.Configuration.GetSection("Labeler:Administration"));

WebApplication app = builder.Build();

// Omit this middleware when the application itself terminates HTTPS.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    RequireHeaderSymmetry = true,
    ForwardLimit = 2,
    KnownProxies = { IPAddress.Parse("10.0.0.10"), IPAddress.Parse("10.0.0.11") }
});

app.MapLabelerAdministration();
```

The signing key and hardened identity-resolution HTTP client are caller-owned. Configure outbound
network/DNS restrictions, timeouts and cancellation for identity resolution. Registration requires
one policy and the same provider instance for `ILabelStore` and `ILabelAdministrationStore`; there
is no permissive fallback. Initialize storage before starting hosted services.
Remote administration endpoints opt out of a host's ASP.NET authorization fallback policy because
they perform their own service-token authentication and live team authorization. This endpoint
metadata does not grant anonymous access: missing or invalid service tokens are still rejected.

`MapBlueskyLabeler()` remains independently registered, public and read-only. Administrative
registration does not make public label distribution require sign-in.
Remote administration is disabled unless `Labeler:Administration:RemoteEnabled` is true. Because
the mapped routes read monitored options on each request, configuration reload can enable or disable
them without restarting; disabled requests receive 404. This setting never affects protected local
administration or team membership and does not bypass authentication or authorization.

Configure admission/rate limiting in the hosting application before administrative handlers.
Verification and required denial audit consume outbound requests and durable transactions, so
bound unauthenticated traffic and monitor audit growth. Administrative routes require HTTPS:
requests for which `HttpRequest.IsHttps` is `false` are audited and rejected with `403 Forbidden`
before service-token authentication.
When the application terminates TLS itself, no additional configuration is needed. When HTTPS
terminates at a reverse proxy, call ASP.NET Core's `UseForwardedHeaders` before mapping endpoints,
as shown above, and list every trusted hop in `KnownProxies` or `KnownIPNetworks`. Set
`ForwardLimit` to the trusted chain length. ASP.NET Core only trusts forwarded headers from listed
proxies, so requests from unlisted peers remain HTTP even if they send those headers; ASP.NET Core
trusts loopback proxies by default unless the lists are cleared.
Restrict direct network access to the application so clients cannot bypass the trusted proxy chain.
Configure each trusted proxy to discard untrusted incoming forwarding headers and emit accurate
address/protocol pairs. See
[Configure ASP.NET Core to work with proxy servers and load balancers](https://learn.microsoft.com/aspnet/core/host-and-deploy/proxy-load-balancer).

Set `Labeler:Administration:RemoteEnabled` to `false` in host configuration to omit all remote
administration routes at startup while leaving a configured local pipe/socket available. It defaults
to `true` when the host explicitly maps administration, preserving the existing opt-in behavior.
Changes take effect after restarting the host.

## Deliberate, browserless setup and local use

Bootstrap from an OS-authorized setup process using the configured service provider:

```csharp
LabelServiceResult<LabelAdministrationReceipt> setup =
    await app.Services.BootstrapLocalAdministratorAsync(
        administrator: new Handle("admin.example"),
        operationId: "initial-team-setup",
        cancellationToken: cancellationToken);

if (!setup.Succeeded)
{
    throw new InvalidOperationException(setup.Error.Message);
}
```

This verifies the configured account and atomically creates the first Administrator, irreversible
bootstrap marker, initial OS binding and audit receipt. Keep the same operation ID for an uncertain
retry. A conflicting bootstrap fails; restarting or removing members never reopens it. There is no
HTTP bootstrap, first-caller-wins rule or emergency re-bootstrap backdoor.

On a root-only Linux VPS, running this setup as root explicitly binds `uid:0` to the administrator
DID. This is an operator delegation, **not** a claim that root completed Bluesky OAuth.
No browser or local callback server is needed on the VPS. Shared root cannot identify individual
human operators; audit retains both the delegated DID and the authenticated OS identity.

Linux x64/arm64 uses a filesystem Unix socket, kernel `SO_PEERCRED`, a service-owned mode-0700
directory and mode-0600 socket. Ancestors must be trusted directories, not symlinks; a root-owned
sticky temporary directory is permitted. Use `/run/...`, not a symlinked `/var/run/...` path.
Socket paths must be canonical absolute filenames, without `.` or `..` segments, redundant
separators or a trailing separator; ambiguous paths are rejected rather than normalized.
Windows uses a protected current-service-SID named-pipe ACL with network identities denied; clients
verify the server owner and use a local-only pipe connection. Unsupported platforms fail closed.
The supplied local transport admits the service's own UID/SID, not arbitrary local users.

The transport establishes the OS peer; the database must additionally contain its active explicit
binding. `localhost`, TCP connections, forwarded headers and a claimed UID/SID never substitute.
Bindings for another service identity become usable only on a host running under that identity.
Verify ownership and remove a stale socket deliberately before restart; the listener never
silently replaces an existing endpoint.

The reusable local client sends bounded, length-prefixed JSON over IPC, one invocation per connection:

```csharp
string response = await LabelerLocalAdministration.SendLocalCommandAsync(
    endpoint: localEndpoint,
    command: """{"method":"getTeam"}""",
    cancellationToken: cancellationToken);
```

Every invocation rechecks the current binding and team role. Revoke/replace a binding through
`changeTeam`; never edit database rows. Test another usable credential before revoking your final
local binding. The last-administrator invariant protects membership, not the availability of a
particular administrator's PDS or OS credential.

## Remote use from an administrator's desktop

Use a **production native public OAuth client**, with its metadata document hosted at a configured
HTTPS URL on the labeller. The browser and existing local callback listener run on the desktop,
not on the headless server. No client secret is distributed with the application.

The metadata middleware in `idunno.Bluesky.AspNet.Authentication` can publish an independently
configured native document alongside a web client's document:

```csharp
string audience = Uri.EscapeDataString("did:web:labeler.example#labeler_admin");
OAuthOptions native = new()
{
    ClientId = "https://labeler.example/native-client-metadata.json",
    ReturnUri = new Uri("http://127.0.0.1/oauth/callback"),
    ClientName = "Bean Bureau administration",
    ClientUri = new Uri("https://labeler.example/"),
    Scopes = ["atproto", $"rpc:dev.idunno.labeler.admin.getTeam?aud={audience}"]
};

app.UseBlueskyOAuthClientMetadata(
    native,
    new BlueskyOAuthClientMetadataOptions
    {
        ApplicationType = BlueskyOAuthClientApplicationType.Native
    });
```

Use the `idunno.AtProto.Authentication` and `idunno.Bluesky.AspNet.Authentication` namespaces.
Publish the document before authentication middleware. A callback without a declared port permits
the native client to supply its actual ephemeral loopback port. Publishing metadata does not start
the callback listener. Use the existing OAuth/PAR/PKCE/DPoP and callback libraries in desktop adapters.
Production loopback callbacks do **not** relax remote PDS/authorization-server HTTPS or SSRF rules.
Do not use the development `http://localhost` client ID for production.

`Samples.LabelerHost` already publishes `/native-client-metadata.json` with the administrative
scope superset and its actual desktop callback path, `http://127.0.0.1/`. Use that endpoint
directly with the CLI. See [deployment](deployment.md#native-oauth-and-tls-forwarding) for
explicit trusted-proxy configuration when HTTPS terminates before the container.

The document declares the permitted scope superset; request only needed method-bound permissions
at sign-in. Add `rpc:<administrative-method>?aud=<encoded-DID#service>` per method. The member's PDS
checks the requested service token's `lxm` permission, not a scope naming `getServiceAuth` itself.
Metadata can supply a name/logo, but an authorization server need not display untrusted branding.

After OAuth establishes the expected account DID and authoritative PDS/authorization-server
relationship, call `GetServiceAuth` using the new `AtProtoServiceReference` overload. Request the
**exact** audience `did:web:labeler.example#labeler_admin` and exact `lxm`. `GetServiceAuth` requests
a token expiring in 14 minutes 55 seconds by default, leaving five seconds of client/PDS clock-skew
headroom below the verifier maximum; specify a different positive `expiry` when acquiring the token. The
labeler accepts up to 15 minutes by default; set `maximumServiceTokenLifetime` when registering
administration to change that limit. This is currently a registration API setting, not an
`appsettings` binding; binding it from host configuration is planned for a later configuration-file
integration. Present only that service JWT as an HTTPS Bearer token to:

```text
https://labeler.example/xrpc/dev.idunno.labeler.admin.getTeam
```

PDS OAuth access/refresh tokens, DPoP proofs and bare-DID service audiences are not labeler credentials.
The reference PDS's generic XRPC proxy currently drops the service fragment; that is not this flow.
If a PDS cannot grant the granular scopes or mint exact-audience tokens, report incompatibility;
do not broaden scopes or weaken audience verification. Reference-source support is not a claim that
every hosted or self-hosted PDS deployment has rolled it out.

The verifier checks bounded JWT shape, duplicate/ambiguous claims, `typ`, ES256/P-256 or
ES256K/secp256k1 signature, the account's repository `#atproto` key and matching DID document/controller,
exact scalar audience and `lxm`, integer times, a lifetime no greater than the configured maximum
(15 minutes by default), optional integer `nbf` (not-before) preceding expiry, five-second clock
tolerance, and `jti`. Provider admission uses database time for persisted expiry and consumes replay
identity durably across replicas. A retry obtains a **new service token** but retains the original
application operation ID.
Lifetime and not-before checks run both before and after DID resolution; a token whose `nbf`
is more than five seconds in the future is rejected rather than admitted early.

## Commands, revocation and audit

GET methods are `getTeam`, `getAudit`, `getOperation`, and `getPublicationState`; other methods use
POST with JSON. Prefix each with `dev.idunno.labeler.admin.` on HTTPS. Local JSON includes `method`.
POST bodies may omit `method` or match the route exactly. Unknown JSON fields and null required
strings are rejected; actor identity is never accepted from the body.

`enroll` takes `target` (DID or verified handle), `role`, `operationId` and `reason`.
`changeTeam` takes a DID `target`, numeric `action`, `operationId`, `reason`, and any action-specific
`role` or `credential`. Roles are Viewer=0, Moderator=1, Administrator=2. Team actions are Enroll=0
(use `enroll`), SetRole=1, Suspend=2, Reinstate=3, Remove=4, BindLocal=5, RevokeLocal=6.
Binding credentials are `uid:<decimal>` or `sid:<SID>`. Service tokens are short-lived and
single-use; the provider records consumed issuer/`jti` pairs until expiry and rejects replay.
Suspend or remove a member to revoke their team access. Do not put raw credentials in reasons or logs.

`issueLabel` and `negateLabel` use `operationId`, `target`, `value`, `reason`, and optional `cid`,
`expiresAt` (issue only), and `correlationId`. The shared issuance service still enforces policy,
target verification, lifecycle and semantic retries. `getOperation` requires `operationId`;
`getAudit` accepts `afterSequence` and `limit` (1-16), bounding the maximum serialized audit
response below the local transport's 1 MiB frame limit even when reasons require JSON escaping;
read queries may include `reason`. Both HTTP and local read commands use
`Administrative inspection.` when the reason is omitted or blank; mutation commands still require
an explicit nonblank reason.
Successful audit reads do not append audit events to their own feed, so a traversal can reach an
empty page when other administrative activity is idle, including with `limit=1`. Denied audit reads
remain durably audited. Pages are live, not a fixed snapshot; concurrent administrative activity can
add new events while a traversal is in progress.
Responses contain `succeeded`, classified `error`/`message`, and the applicable bounded result.
HTTPS distinguishes invalid authentication (401), insufficient live authority (403), malformed
input (400), and dependency/audit or response-size unavailability (503). Local responses use the
same result envelope and are bounded to 4 MiB, enough for the maximum 1,000-member team while
keeping all transport responses finite.
Local transports consume the bounded command frame before replying to an unbound or revoked
OS peer, but never deserialize or dispatch that peer's command. Invalid frame lengths are
rejected and recorded as unattributed denial audit entries without stopping the listener.
Audit persistence failures are logged without accepting the command.

Member suspension/removal invalidates all active invocation evidence and removes local bindings.
Reinstatement does not restore removed bindings. The provider retains service-token replay entries
through their expiry plus clock skew. Up to 1,000 member tombstones and 1,000 bindings are
supported; capacity exhaustion is explicit, never silent eviction.

Service JWTs do not identify an upstream desktop OAuth session. Revoke an individual desktop's
OAuth session at its PDS; suspend or remove a team member to block that DID's labeler access immediately.
These changes do not erase previously committed labels, receipts or audit. A label transaction that
wins serialization before a membership change may finish; one reaching the fence afterwards cannot commit,
including no-op/recovery decisions. Successful label/no-op audit links the operation ID, DID and OS
identity where applicable. Required denial audit failures fail closed.

`authorizeMaintenance` records an administrator's authorized intent with `operationId` and `reason`.
`ILabelAdministrationStore.AuditMaintenanceAsync` distinguishes intent, completed and failed
coordinator outcomes. These audit events are distinct from the durable maintenance lease and
cross-host rotation coordinator described below.

The `retire` CLI commands use this shared maintenance authorization and audit path together with
the durable lifecycle checkpoints:

```text
labeler-admin retire begin --operation-id retire-start --reason "Close the service" --local-endpoint /run/bean-bureau/admin.sock
labeler-admin retire advance --operation-id retire-batch --reason "Negate retained active labels" --local-endpoint /run/bean-bureau/admin.sock
labeler-admin retire grace --days 30 --operation-id retire-grace --reason "Allow consumers to catch up" --local-endpoint /run/bean-bureau/admin.sock
labeler-admin retire complete --operation-id retire-complete --reason "Grace period elapsed" --local-endpoint /run/bean-bureau/admin.sock
labeler-admin retire purge --limit 100 --operation-id retire-purge --reason "Approved local cleanup" --local-endpoint /run/bean-bureau/admin.sock
```

`begin` permanently closes ordinary issuance. Repeat `advance` until status reports `Distributing`;
each call publishes pending labels and processes one bounded page, appending signed negations and
the enumeration checkpoint atomically. It negates only the current unexpired positive decisions,
preserving CID scope, and skips existing negations and expired decisions. Start the grace period only
after status shows all issued labels published. `complete` is rejected before the database-clock
deadline. `purge` requires separate confirmation and removes a bounded local batch; it does not destroy
the signing key or backups. Retirement does not stop distribution automatically.

Team mutations retain same-intent receipts by operation ID. On `OutcomeUnknown`, recover using that
ID and the current authorized account; do not invent a new operation. Maintenance audit events are
append-only observations, not an idempotent maintenance execution ledger. Keep clocks synchronized,
protect database credentials and backups, and treat raw provider/setup/key helpers as trusted
infrastructure unavailable to remote callers.

Protocol references: [OAuth](https://atproto.com/specs/oauth),
[permissions](https://atproto.com/specs/permission), [XRPC](https://atproto.com/specs/xrpc), and
the [inspected reference getServiceAuth route](https://github.com/bluesky-social/atproto/blob/57c85d5ea03583dae3ae8d1f51106210dcad315b/packages/pds/src/api/com/atproto/server/getServiceAuth.ts).

## Administration CLI

The self-contained `idunno.Bluesky.Labeler.Cli` executable is published for Windows x64, Linux x64
and Linux arm64. Each invocation selects exactly one transport. Local mode uses the configured
protected named pipe or Unix socket and must run under the OS identity explicitly bound to a live
team member. Remote mode opens the user's browser for native OAuth, obtains a method-bound service
token from the user's PDS, and calls the labeler's HTTPS administration endpoint. The CLI does not
store OAuth credentials or accept a private label-signing key.

Download the ZIP matching the operating system and architecture from the GitHub release, then verify
its build provenance before extracting or running it:

```text
gh attestation verify idunno.Bluesky.Labeler.Cli-linux-x64.zip --repo blowdart/idunno.Bluesky
```

Keep the release tag and artifact digest with deployment records. Replace a directly installed CLI only after verifying the new release. The `update` command
downloads one exact stable release, verifies its GitHub artifact attestation with the installed
GitHub CLI (`gh`), checks the archive SHA-256 and staged executable, and schedules an atomic
replacement after the current process exits. It preserves the prior executable as a sibling backup;
restore that file manually if an interrupted helper leaves the installation unusable.

```text
labeler-admin update --tag v9.0.1
```

The update requires a self-contained installation from the Windows x64, Linux x64 or Linux arm64
release ZIP, GitHub CLI authentication/verification support, and write access to the executable's
directory. It does not update a `dotnet tool`, package-manager installation or renamed launcher.
The command asks for confirmation; `--yes` is only for an explicitly controlled script.

Examples below use the executable name `labeler-admin`; substitute the published executable path
and the endpoint configured by the host.

Read team, publication and lifecycle status, audit, or an operation receipt locally:

```text
labeler-admin team --local-endpoint /run/bean-bureau/admin.sock
labeler-admin status --local-endpoint /run/bean-bureau/admin.sock
labeler-admin audit --after-sequence 100 --limit 16 --local-endpoint /run/bean-bureau/admin.sock
labeler-admin operation --operation-id label-op-123 --local-endpoint /run/bean-bureau/admin.sock
```

The status response includes issuer and stream identity, revision, issued/published sequence
boundaries, replay floor, lifecycle phase, retirement checkpoints, and the grace deadline when set.

Use the configured Windows pipe name instead of the Unix socket path on Windows. Remote commands
require the labeler's HTTPS origin, exact service reference, administrator handle, and the HTTPS
URL of the registered native OAuth client metadata. Use a metadata document published by the
labeler host; do not substitute a development client ID.

```text
labeler-admin team --admin-url https://labeler.example --audience "did:web:labeler.example#labeler_admin" --handle admin.example --client-id https://labeler.example/native-client-metadata.json
labeler-admin operation --operation-id label-op-123 --admin-url https://labeler.example --audience "did:web:labeler.example#labeler_admin" --handle admin.example --client-id https://labeler.example/native-client-metadata.json
```

Issue a label for an account, profile record, post, list, or feed generator. A record URI is kept a
record target; use `--cid` to pin the operation to one record version. Negation uses the same target
and value and does not accept an expiry:

```text
labeler-admin issue did:plc:subject spam --operation-id issue-001 --reason "Manual review" --local-endpoint /run/bean-bureau/admin.sock
labeler-admin issue at://did:plc:subject/app.bsky.actor.profile/self impersonation --cid bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4 --operation-id issue-002 --reason "Profile review" --local-endpoint /run/bean-bureau/admin.sock
labeler-admin negate at://did:plc:subject/app.bsky.feed.post/record-key spam --operation-id negate-001 --reason "Appeal accepted" --local-endpoint /run/bean-bureau/admin.sock
labeler-admin enroll did:plc:new-member --role Moderator --operation-id enroll-001 --reason "Moderator onboarding" --local-endpoint /run/bean-bureau/admin.sock
labeler-admin change-team did:plc:member --action SetRole --role Viewer --operation-id role-001 --reason "Role review" --local-endpoint /run/bean-bureau/admin.sock
labeler-admin change-team did:plc:member --action BindLocal --credential uid:1001 --operation-id bind-001 --reason "Grant local access" --local-endpoint /run/bean-bureau/admin.sock
```

Issue and negation, enrollment, and team changes require an interactive confirmation. Use `--yes`
only in an explicitly controlled script; the operation ID makes a retry recoverable, not harmless
when changed to a new ID. Mutation reasons are required and written to the shared audit trail.
Responses are JSON on standard output. Exit codes are 0 for success, 2 for invalid input or declined
confirmation, 3 for transport/setup errors, 4 for authentication or authorization failure, 5 for
service failure, and 130 for cancellation.

The CLI exposes shared team, audit, publication/lifecycle state, operation receipts, issuance,
negation, enrollment, team changes, resumable retirement, and coordinated signing-key rotation.
Rotation uses durable maintenance shared by all hosts; all participating hosts must drain and
acknowledge the replacement key before distribution reopens. `rotation recover-plc` checks the
current DID document before resubmitting a saved signed operation.

The `service-update` command is limited to Docker Compose deployments using the published GHCR
image. It requires the target and previous images as immutable digests, the Compose service name,
the public readiness URL, and explicit confirmations that an external backup exists and the
schema transition is compatible. It pulls both images before replacement, writes an image-only
temporary Compose override, stops the host with a bounded grace period (90 seconds by default),
recreates only the selected service, checks readiness, and redeploys the previous digest if the
new service fails readiness. It never edits the operator's Compose file, enters a running
container, removes volumes, or updates provider-managed services. Cancellation or process
interruption can leave deployment state uncertain; inspect `docker compose ps`, the configured
image digest and readiness endpoint before retrying.
