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
| Administrator | Moderator permissions plus team/binding/token management and maintenance authorization |

Authority is persisted by **DID**, never handle. `LabelTeamService` verifies a matching DID document;
enrollment by handle also verifies the handle in both directions. Enrollment is an administrator's
grant, not proof that a new member has signed in. Members authenticate independently.

Choose one existing SQLite, PostgreSQL or MySQL provider. Team state, bindings, token replay state,
revocations, administrative audit and receipts live in that provider's issuer-bound database.
Schema version four upgrades versions one, two and three additively through `InitializeAsync`.
Back up all tables together and stop older application versions before upgrading.

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

WebApplication app = builder.Build();

// Omit this middleware when the application itself terminates HTTPS.
app.UseLabelerAdministrationForwardedHeaders(options =>
{
    options.KnownProxies.Add(IPAddress.Parse("10.0.0.10"));
    options.KnownProxies.Add(IPAddress.Parse("10.0.0.11"));
    options.ForwardLimit = 2;
});

// Read once at startup; changing this setting requires a host restart.
app.MapLabelerAdministration(
    remoteEnabled: app.Configuration.GetValue("Labeler:Administration:RemoteEnabled", defaultValue: true));
```

The signing key and hardened identity-resolution HTTP client are caller-owned. Configure outbound
network/DNS restrictions, timeouts and cancellation for identity resolution. Registration requires
one policy and the same provider instance for `ILabelStore` and `ILabelAdministrationStore`; there
is no permissive fallback. Initialize storage before starting hosted services.

`MapBlueskyLabeler()` remains independently registered, public and read-only. Administrative
registration does not make public label distribution require sign-in.

Configure admission/rate limiting in the hosting application before administrative handlers.
Verification and required denial audit consume outbound requests and durable transactions, so
bound unauthenticated traffic and monitor audit growth. Administrative routes require HTTPS.
Requests that do not arrive over direct HTTPS or a verified trusted-proxy HTTPS chain are audited
and rejected with `403 Forbidden` before service-token authentication.
When HTTPS terminates at a reverse proxy, call `UseLabelerAdministrationForwardedHeaders` before
mapping endpoints and configure every trusted hop using ASP.NET Core's `KnownProxies` and
`KnownIPNetworks`. Set `ForwardLimit` to the trusted chain length. The middleware processes paired
`X-Forwarded-For` and `X-Forwarded-Proto` entries with header symmetry required; requests from
unlisted proxies remain HTTP even if they send those headers.
Restrict direct network access to the application so clients cannot bypass the trusted proxy chain.
Both forwarded headers must have matching entries. Configure each trusted proxy to discard untrusted
incoming forwarding headers and emit accurate address/protocol pairs. Multiple proxy addresses or
networks can be added to the options.

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

The document declares the permitted scope superset; request only needed method-bound permissions
at sign-in. Add `rpc:<administrative-method>?aud=<encoded-DID#service>` per method. The member's PDS
checks the requested service token's `lxm` permission, not a scope naming `getServiceAuth` itself.
Metadata can supply a name/logo, but an authorization server need not display untrusted branding.

After OAuth establishes the expected account DID and authoritative PDS/authorization-server
relationship, call `GetServiceAuth` using the new `AtProtoServiceReference` overload. Request the
**exact** audience `did:web:labeler.example#labeler_admin` and exact `lxm`. `GetServiceAuth` requests
a 15-minute token by default; specify a different positive `expiry` when acquiring the token. The
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
(15 minutes by default), five-second clock
tolerance, and `jti`. Provider admission uses database time for persisted expiry and consumes replay
identity durably across replicas. A retry obtains a **new service token** but retains the original
application operation ID.

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
read queries may include `reason`.
Responses contain `succeeded`, classified `error`/`message`, and the applicable bounded result.
HTTPS distinguishes invalid authentication (401), insufficient live authority (403), malformed
input (400), and dependency/audit or response-size unavailability (503). Local responses use the
same result envelope and are bounded to 4 MiB, enough for the maximum 1,000-member team while
keeping all transport responses finite.

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
coordinator outcomes. These are authorization/audit contracts, **not** a rotation command, lease,
drain mechanism or cross-host coordinator. A future maintenance integration must recheck authority
at its own commit boundary and coordinate every writer; an earlier intent is not a reusable grant.

Team mutations retain same-intent receipts by operation ID. On `OutcomeUnknown`, recover using that
ID and the current authorized account; do not invent a new operation. Maintenance audit events are
append-only observations, not an idempotent maintenance execution ledger. Keep clocks synchronized,
protect database credentials and backups, and treat raw provider/setup/key helpers as trusted
infrastructure unavailable to remote callers.

Protocol references: [OAuth](https://atproto.com/specs/oauth),
[permissions](https://atproto.com/specs/permission), [XRPC](https://atproto.com/specs/xrpc), and
the [inspected reference getServiceAuth route](https://github.com/bluesky-social/atproto/blob/57c85d5ea03583dae3ae8d1f51106210dcad315b/packages/pds/src/api/com/atproto/server/getServiceAuth.ts).
