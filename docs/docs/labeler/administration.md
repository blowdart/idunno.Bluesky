# Administrative authentication and teams

For installation, access-control configuration, command options and worked examples,
see the [labeler administration CLI reference](cli.md).

The Bean Bureau's public label queries don't establish who may issue labels. Administration is a
separate, opt-in boundary, shared by remote HTTPS and protected local IPC. Neither a successful
Bluesky sign-in nor being on the labeller's machine grants a role.

These are SDK services and adapters, not a CLI, moderation UI or runnable host. Those applications
must consume this boundary rather than construct actor DIDs from request fields or expose raw storage.
The `dev.idunno.labeler.admin.*` methods are this library's administration protocol, not Ozone or
standard AT Protocol moderation endpoints.

On Xenon's Team page, the add-member field suggests public Bluesky accounts as you type.
JavaScript calls `https://public.api.bsky.app/xrpc/app.bsky.actor.searchActorsTypeahead`
directly from the browser, without cookies, authorization headers or a referrer; there is no
server-side search proxy. Searches wait for two characters and a 300 ms pause, and superseded
requests are canceled. Selecting an account fills in its handle. Suggestions are not identity
verification: enrollment still performs the existing server-side identity and permission checks.
Manual handle or DID entry works without JavaScript or when search is unavailable.
Hosts with a Content Security Policy must allow `https://public.api.bsky.app` in `connect-src`
to enable suggestions.
The member-search and change-member fields share handle suggestions from the full registered
team, including suspended and removed members, independently of the table's search and current
page. Suggestions show membership status and omit accounts whose handles cannot be verified.
They use the browser's built-in autocomplete and make no network requests. Selecting a
suggestion fills in the handle; Search still submits the filter. In Change member, selecting a
member also selects their current role. The server maps the handle to its registered member
internally; all changes still require an explicit submission and audit reason.

## Optional Ozone compatibility

**Limited compatibility is intentional and accepted.** This opt-in adapter supports
the labeler-focused subset listed below, not full Ozone parity. Use Xenon/native
views as the primary, complete moderation interface; stock Ozone must not be the
sole source of queue or event-history completeness.

Unresolved URI-only records cannot be represented in Ozone's event/status subject
unions and are omitted from those projections without blocking other entries.
They remain in native report/history views and may become representable after
successful resolution. Stock Ozone does not display the adapter's
`unresolvedRecordCount` extension or warn about these omissions, so a partial or
empty Ozone view does not prove that no matching work remains. This limitation
is accepted for the compatibility subset, not a promise of future full parity.

`app.MapOzoneCompatibility()` explicitly opts an application into the adapter while
building its pipeline. Omit the call and no Ozone compatibility routes are registered.
It uses the same administration services; register `AddLabelerAdministration` first.

```csharp
WebApplication app = builder.Build();
app.UseForwardedHeaders();
app.MapBlueskyLabeler();
app.MapLabelerAdministration();

// Optional Ozone compatibility; omit this call for a native-only host.
app.MapOzoneCompatibility(["http://127.0.0.1:3000", "https://ozone.example.com"]);
```

Configure trusted proxies before calling `UseForwardedHeaders`, as shown below.
The canonical host makes this call only when `LabelerOzone:Enabled` is `true`
(default `false`). This is a startup choice; changing it requires restarting the host.
Opting in **does not enable remote administration**:
authenticated Ozone methods also obey `RemoteMode` and, in `WebUiControlled` mode, the durable
owner switch. Protected local administration and public report/appeal intake are unchanged.

Pass an explicit allowlist of Ozone UI origins to `MapOzoneCompatibility` to enable browser
access. Origins include the scheme, host and non-default port, with no trailing slash, path,
query, fragment, credentials or wildcard. `http://localhost:3000` and
`http://127.0.0.1:3000` are different origins. The parameterless overload and an empty list
enable no cross-origin access, including discovery. The helper handles CORS automatically;
do not add a separate `AddCors` or `UseCors` step for these routes.

The canonical host reads the list from `LabelerOzone:AllowedOrigins`; for example, set
`LabelerOzone__AllowedOrigins__0=http://127.0.0.1:3000` for a local UI.
Changes require a host restart. CORS applies only to the mapped Ozone routes, permits their
GET or POST method and Authorization/Content-Type headers, and does not permit browser
credentials. Preflight requests need no service token; actual requests still pass every
existing authentication and authorization check. CORS is not an access-control boundary
for non-browser clients.

The adapter adds labeler-focused report inspection, subject/history views, label events and
report resolution to discovery and team inspection. It requires the shared moderation
services for those operations. Read-only report queues, case history and presence have been
exercised with unmodified Ozone; this is not comprehensive end-to-end verification.
The adapter is not a replacement for an Ozone PDS/AppView or its broader administration APIs.

| Endpoint | Current adapter support |
|---|---|
| `/.well-known/ozone-metadata.json` | Anonymous configured DID, HTTPS origin and active public `did:key`; no private material |
| `tools.ozone.server.getConfig` | Authenticated live viewer role; no fabricated PDS, AppView, chat, blob-diversion or verifier service |
| `tools.ozone.team.listMembers` | Bounded live team pages; DID text search, role/disabled filters and exclusive DID cursors |
| `tools.ozone.team.listOnlineMembers` | Active team members observed making authenticated Ozone requests on this host within five minutes, latest activity first; optional limit 1–100, default 50 |
| `tools.ozone.moderation.getEvent`, `queryEvents` | Durable Ozone events, original reports and native workflow comments with disjoint stable event IDs |
| `tools.ozone.moderation.getRecord`, `getRepo` | Independently resolved repository/record display and retained local labels/status; no PDS administrative authority |
| `tools.ozone.moderation.getRecords`, `getRepos` | Bounded batch hydration for event history; deleted records have explicit not-found views |
| `app.bsky.actor.getProfile` | Authenticated compatibility read from the public Bluesky AppView; incoming credentials are never forwarded |
| `app.bsky.feed.getPostThread` | Bounded public AppView thread preview for the requested post URI; unavailable/blocked views retained, no incoming credentials forwarded or original evidence changed |
| `tools.ozone.setting.listOptions` | Lists the caller's personal media preferences; unconfigured instance settings return an empty listing |
| `tools.ozone.setting.upsertOption` | Updates only the caller's personal `dev.idunno.labeler.mediaPreferences` option |
| `tools.ozone.moderation.queryStatuses` | Native CID-scoped case status, subject/collection/reviewer/appeal/mute filters and chronological report/review sorting |
| `tools.ozone.report.getReport`, `queryReports` | Original report evidence with current handling/assignment status and bounded live pages |
| `tools.ozone.report.listActivities` | Report closure activities committed by this adapter, including moderator and automation provenance |
| `tools.ozone.moderation.emitEvent` | Label additions/negations, acknowledgment, escalation and temporary subject/reporter mute/unmute; unsupported event types/options fail explicitly |
| `tools.ozone.report.closeReports`, `createActivity` | Subject report closure and explicit single-report close activities; other activities fail explicitly |
| `tools.ozone.team.addMember`, `deleteMember`, `updateMember` | Explicit authenticated `NotImplemented` error |
| Other Ozone methods | Not mapped; no claim of support |

An Ozone **Acknowledge** event or nonempty **Label** decision handles all open reports for the submitted subject
and record version unless an explicit `reportAction` selects a narrower set.
Optional comments are retained as the handling reason; an explicit report note takes
precedence for those reports. Acknowledgment leaves labels unchanged; label decisions
apply the requested additions or negations, including any expiry, atomically with report handling.
Empty label decisions do not implicitly handle reports. The case resolves only when no
open reports or pending appeals remain. Account-wide acknowledgment remains unsupported.

The personal media option shares Xenon's durable preferences. Its value contains
`graphicMediaPrefs`, a Boolean map with keys `graphic-pref-{filter}-{label}`:
filters are `blur`, `grayscale`, `translucent`; labels are `graphic-media`, `porn`,
`nudity`, `sexual`, `self-harm`. Upserts replace the whole map; omitted keys mean false.
Only `key`, `scope: "personal"` and `value` are accepted; arbitrary options, target DIDs,
descriptions and manager roles cannot be written. The unmodified Ozone graphic-media
preferences screen still uses browser local storage and does not call this API.

Online presence reflects authenticated Ozone and Xenon request activity, including presence polling, not an
open browser connection. It is held only in memory on the current host and resets on restart;
replicas do not share observations. Anonymous requests and CORS preflights do not record activity.
Every listing rechecks current active team membership and excludes removed or disabled members.
Xenon shows the count beside the account menu with an expandable member list using verified
handles, with canonical DIDs available as tooltips. A failed handle lookup is
shown explicitly as "Handle unavailable". The indicator has no border, matching adjacent controls.
It refreshes every 30 seconds while the page is visible. The indicator is hidden when fewer than two active
team members are configured. Revoked access clears the list; refresh failures show an explicit
unavailable status rather than a stale count. Xenon presence does not require the Ozone adapter
or remote administration to be enabled.

Discovery reads only the configured public origin and installed key, never an incoming Host
header. It is anonymously readable from explicitly allowed Ozone UI origins. Discovery
requires a signing key and returns `ServiceUnavailable` on a keyless retired host. Ozone
independently compares this public metadata to the DID document; successful discovery does
not establish successful identity publication.

The authentication boundary accepts method-bound assertions addressed to the exact
`DID#atproto_labeler` service or the same bare DID issued by PDS proxies. Generic and native
administrative verification remain exact-audience. Both adapters consume tokens through the
same durable replay store and recheck live team authority; browser cookies, request-body DIDs
and Basic credentials grant no permission.

Administrator, Moderator and Viewer map to Ozone's Admin, Moderator and Triage wire roles.
That mapping does not widen SDK permissions. Ozone derives some visible controls solely from
roles, so unsupported controls may remain visible and fail explicitly. Team projections omit
notification email, unverified profiles and unavailable timestamps rather than inventing them.
Search currently matches DIDs, not handles. Cursors are live continuations, not snapshots.
Event queries accept the stock UI's `includeAllUserRecords` flag, matching the subject's
repository when enabled, and `withStrike`, selecting only events with a recorded strike count.
The labeler does not manufacture strike events or import PDS enforcement history.
Profile enrichment uses only the fixed public Bluesky AppView endpoint, anonymously, after
the normal method-bound service authentication and live team checks. This is not a general
AppView proxy: other `app.bsky.*` methods remain unsupported.
Subject-status queries accept `subjectType` (`account` or `record`), repeated
`collections`, `includeAllUserRecords`, `lastReviewedBy`, `appealed`, `includeMuted`
and `onlyMuted`. Account-wide record matching requires an account DID and preserves
each original record URI and CID; it does not rewrite account or record evidence.
`lastReviewedBy` matches the latest qualifying review, not any historical actor.
Label-only events and assignment/report intake do not claim a review.
`appealed` means an unresolved native appeal, rather than any past appeal.

Omitted or `false` `includeMuted` excludes actively subject-muted cases. `onlyMuted=true`
selects cases with an active subject or reporter mute; it does not override `includeMuted`.
Use both `includeMuted=true&onlyMuted=true` to include subject-muted cases as well.
The stock Reports screen's `includeMuted=true` is supported without widening permissions.
Status results remain native-case projections: a mute does not create a case or manufacture
a subject-status row. Mutes without cases remain visible in Xenon's Mutes list and event history.
Report, event and status queries keep bounded pages in memory but may scan retained history;
the page size does not cap total query work.

Subject mute/unmute and reporter mute/unmute events share durable state and audit with
Xenon. Durations are 6 or 12 hours, or 1, 2, 3, 7, 30 or 90 days; omission on an Ozone
mute event means 24 hours. Permanent and other durations fail explicitly. Expiry uses the
database clock. Subject muting covers a DID or record URI across its CID-scoped cases,
without changing review state, assignment or labels. Reporter muting accepts only account
DIDs. Its incoming reports remain inspectable with `isMuted=true`, without creating,
reopening or otherwise changing a review case; unmuting never promotes those historical
reports. Report details omit a subject-status reference when no case exists.

Label events prepare every action before one shared provider transaction commits the labels,
selected report outcomes, Ozone event, case history and audit. `createdBy` must match the
authenticated actor. Without `reportAction`, label events leave reports open. Explicit report
IDs may select assigned reports; bulk/type selection skips assigned reports. Native assignment
is case-scoped, so it applies to every report in that case. Ozone assignment mutations are not
implemented. A sole eligible native moderator receives automatic case assignment; use explicit
report IDs to close those assigned reports. Report closure never implicitly decides a pending appeal.
Native workflow history remains visible even when its operation ID matches an Ozone operation;
it is projected as a comment, except native escalation, which uses `modEventEscalate`.

`tools.ozone.moderation.defs#modEventEscalate` accepts an optional `comment` and flags
existing unresolved cases for the exact account or record/CID subject. It does not
create cases or reopen resolved cases. Combining escalation with labels, report
selection, `reportAction` or mute actions is rejected. Assignment, labels, pending
appeals and report handling remain unchanged. Escalated cases stay available to
all moderators; subject status is `reviewEscalated` and unhandled reports have
status `escalated`. Escalation is not a completed review. A later Acknowledge or
Label decision can handle the reports normally.

On an escalated subject, a Label event without explicit `reportAction` changes
only labels and preserves escalation and report handling. Ozone's **De-escalate
the subject and close its review after this action** checkbox sends a separate
Acknowledge event when checked; that event handles the reports and closes review
unless a pending native appeal prevents closure. Leaving it unchecked keeps the
case escalated. Explicit `reportAction` retains its selected-report semantics.

`externalId` is unique for an event type and subject; a duplicate returns `DuplicateExternalId`.
Fresh requests without that identifier are distinct operations, even with identical content.
An uncertain storage outcome is not evidence of rollback. Multi-label events accept at most
16 distinct actions, explicit report selections at most 100 IDs, and a transaction at most
1000 cases/reports. Larger transactions fail before applying any action. Unknown query
filters and unsupported account-wide/blob/queue/takedown operations are rejected, not ignored.
Retained pending-verification reports remain in native Xenon/CLI recovery views until promoted;
they are not represented as Ozone-managed queues.

**CID-less/deleted record resolution:** authenticated intake observes the latest real CID
from the independently DID-resolved PDS and stores the result separately from immutable
URI-only report evidence. The first successful case observation is pinned for Ozone
references, preserving native case ID and label scope. Existing and failed observations
can recover on authenticated status/history inspection; unavailable/deleted records
remain in native reports rather than blocking all other queue entries.

Ozone's basic event/status unions cannot represent an unresolved URI-only record.
Those entries alone are omitted, with an explicit `unresolvedRecordCount` response field
for clients that understand this adapter extension. Stock Ozone does not display this
count or warn that results are incomplete; its queue and event views are not a complete
inventory when unresolved records exist. Use Xenon/native report/history views for
complete retained evidence. The adapter does not send the deprecated HTTP `Warning` header.
The count applies to matching entries (events for `queryEvents`, cases for `queryStatuses`),
not the page size; clients must not interpret a partial or empty projection as a complete
queue. No account reference or synthetic CID is substituted. Report detail retains the
original URI and a separate intake observation, and includes a subject status when resolved.
An Ozone CID-scoped action's implicit report selection matches submitted or independently
resolved report CIDs; it cannot silently close reports pinned to another version.
Explicit report-ID selection retains the existing cross-version review contract for
the same record URI: moderators can deliberately handle those reports without changing
their evidence or the label action's CID scope. URI-wide native actions and
explicit URI-wide report closure retain their existing scope. If no real CID can be
obtained, use Xenon/CLI for label actions; accounts do not require a record CID.

Resolution metadata is additive optional JSON within schema fifteen, not a new table or
schema version. Preserve these payloads in backups and avoid older writers that would
discard the metadata.

Schema fourteen added `label_ozone_events`; schema fifteen adds `label_mutes` to every
relational provider. Initialization is additive; do not deploy an older writer against the
upgraded schema. Include both tables and retained reports in whole-database backups: they
retain deadlines, operation identities and confidential moderation history, not private
signing keys. Public signed-label distribution is unchanged.

Schema sixteen expands the persisted case-status constraint to include escalation
without changing existing status values. SQLite rebuilds the case table while
preserving dependent history and validating foreign keys; PostgreSQL and MySQL
replace the status check. Stop older writers and take a whole-database backup
before initialization. Older schema-fifteen hosts cannot write to schema sixteen;
rollback requires a compatible host or restoring the pre-upgrade backup.

The reference contract was inspected against Ozone commit
`bd619ed5b930cb62be65d99cd1e080327e1e7cf6` and AT Protocol lexicon commit
`d3080834ac43dbb94e97e82fdd85b1a0f12558ad`. No Ozone code or dependencies are changed.

Team services, callers, permissions and administrative storage contracts are in
`idunno.Bluesky.Labeler.Administration`. Maintenance coordination is in `.Maintenance`,
signing providers are in `.Signing`, and common targets/results remain in
`idunno.Bluesky.Labeler`. HTTP hosting extensions remain in `idunno.Bluesky.AspNet.Labeler`.

## Roles and persistence

| Role | Permission |
|---|---|
| Viewer | Inspect team, administrative audit, operation receipts and publication progress |
| Moderator | Viewer permissions plus issue and negate labels |
| Administrator | Moderator permissions plus team/binding management and maintenance authorization |

When [Xenon](moderation.md) is explicitly enabled, Viewer also inspects confidential
reports, cases, workflow views, appeals and policy history; Moderator manages cases
and explicit atomic decisions/appeal outcomes; Administrator manages versioned
policy publication and recovery. Manual labeling still needs no report or case.
The shared local and HTTPS dispatcher uses these same services and live roles.

Authority is persisted by **DID**, never handle. `LabelTeamService` verifies a matching DID document;
enrollment by handle also verifies the handle in both directions. Enrollment is an administrator's
grant, not proof that a new member has signed in. Members authenticate independently.

Choose one existing SQLite, PostgreSQL or MySQL provider. Team state, bindings, token replay state,
revocations, administrative audit and receipts live in that provider's issuer-bound database.
Schema version nine upgrades supported earlier versions additively through `InitializeAsync`.
Initialization also installs the administrative token-expiry index on existing version-four
databases so cleanup can select expired replay entries without scanning all retained tokens.
Version five adds durable coordinated maintenance and signing-key rotation state. Version six
adds an indexed subject/value identity for bounded decision lookups during retirement; interrupted
backfills resume without changing signed history. Version seven persists immutable host signing
roles; existing hosts remain signing participants until stopped and replaced with newly registered
processes. Keyless replicas must still drain but do not activate signing keys.
Version eight adds optional database-backed signing selection, binding the key ID and public
key to the issuer/stream without persisting private keys; see [deployment](deployment.md).
Version nine adds optional moderation persistence, immutable appeal provenance,
report replay reservations and policy publication/recovery history without
changing existing signatures, stream progress or team/key-selection state.
Back up all tables together and stop older
application versions before upgrading.

Team changes, last-administrator checks and label commit fences use the same provider serialization
lock. Concurrent removal, suspension or demotion cannot eliminate the last active administrator.
Membership and binding generations prevent a removed/re-enrolled member or replaced binding from
reviving previously admitted invocation evidence.

## Register the shared boundary

After registering storage and an `ILabelIssuancePolicyProvider`, opt in explicitly:

```csharp
AtProtoServiceReference audience = new(
    new Did("did:web:labeler.example"), Audience.Administration);

builder.Services.AddLabelerAdministration(
    audience: audience,
    signingKey: labelSigningKey,
    httpClient: identityHttpClient);

// Optional: a protected local administration endpoint.
builder.Services.AddLabelerLocalAdministration(
    OperatingSystem.IsWindows() ? "bean-bureau-admin" : "/run/bean-bureau/admin.sock");

// Remote administration is disabled by default and follows the configured control mode.
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

## Remote administration control modes

The standalone host binds `LabelerAdministration:RemoteMode` (environment variable
`LabelerAdministration__RemoteMode`) to select one of three host policies. The custom SDK host
example above explicitly chooses its own `Labeler:Administration` section instead.

| Value | Remote CLI access |
|---|---|
| `Disabled` | Always unavailable; this is the default. |
| `Enabled` | Available after the existing HTTPS, method-bound service-token and live-team checks. |
| `WebUiControlled` | Available only while the current labeler owner has enabled the durable switch in Xenon. |

When `RemoteMode` is configured, it takes precedence over the legacy
`LabelerAdministration:RemoteEnabled` Boolean in the standalone host. If `RemoteMode` is absent, `RemoteEnabled`
continues to work for compatibility: `true` enables remote administration and `false` leaves it
disabled. This preserves explicitly enabled existing deployments without enabling deployments
whose old setting was absent or false. Monitored mode changes take effect on subsequent requests.
Invalid enum text fails configuration binding; an undefined numeric enum value is rejected by
the request gate. Neither falls back to enabling access through the legacy Boolean.

In `WebUiControlled` mode, the switch starts disabled and is changed from **Team and membership**
in Xenon. The initial owner is the DID that completed the one-time OS-authorized local bootstrap, not the
issuer DID by assumption and not every team member with the Administrator role. The store checks
current Administrator authority and the owner DID inside the same transaction that changes the
persisted setting and appends its audit receipt. Legacy installations recover the bootstrap DID
from their first durable bootstrap audit event; if that evidence is missing, no owner is inferred
and remote control fails closed. Other Administrators may inspect status but cannot change it.
The current owner cannot be removed, suspended or demoted, including by themselves. Other
Administrators cannot invalidate or replace the owner's local access bindings, including by
rebinding an owner's OS identity to another member.

Only the current owner can transfer ownership to another active Administrator. In Xenon, use
**Team and membership > Labeler ownership**, select the new owner's handle, give a reason and
confirm the transfer. The CLI exposes the same operation as
`change-team <administrator-DID> --action TransferOwnership` with the usual transport, operation
ID and reason options. Ownership protection and control of the remote CLI switch move together
in one audited transaction. The switch's enabled state, both members' roles and existing local
bindings are unchanged. The former owner remains an ordinary Administrator and can then be
removed, suspended or demoted. Only the new owner can transfer ownership back.
Upgrade every host sharing the database before relying on these protections; older hosts do not enforce them.

Protected local administration remains available in all three modes. Neither mode selection nor
the Xenon switch bypasses HTTPS, service-token method binding, authentication or live authorization.

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

Set `LabelerAdministration:RemoteMode` to `Disabled` in the standalone host to keep remote routes unavailable while
leaving a configured local pipe/socket available. The effective default is disabled.

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

`idunno.Bluesky.Labeler.Host` publishes `/native-client-metadata.json` with the administrative
scope superset and its actual desktop callback path, `http://127.0.0.1/`. Use that endpoint
directly with the CLI. See [deployment](deployment.md#native-oauth-and-tls-forwarding) for
explicit trusted-proxy configuration when HTTPS terminates before the container.

The document declares the permitted scope superset; request only needed method-bound permissions
at sign-in. Add `rpc:<administrative-method>?aud=<encoded-DID#service>` per method. The member's PDS
checks the requested service token's `lxm` permission, not a scope naming `getServiceAuth` itself.
The native metadata also advertises `identity:*`. Only PLC publication and recovery request that
permission (alongside `atproto`) because they mutate identity through the PDS; ordinary labeler
administration continues to request only its method-bound service permission.
Metadata can supply a name/logo, but an authorization server need not display untrusted branding.

After OAuth establishes the expected account DID and authoritative PDS/authorization-server
relationship, call `GetServiceAuth` using the new `AtProtoServiceReference` overload. Administrative
methods request the **exact** audience `did:web:labeler.example#labeler_admin` and exact `lxm`.
The labeler-agent issuance procedures use `did:web:labeler.example#labeler_issuance` on the separate
`dev.idunno.labeler.issuance` namespace. The host derives this audience from the configured administration
audience's DID; it is not accepted on `dev.idunno.labeler.admin` routes, and `labeler_admin` is not
accepted on issuance routes. `GetServiceAuth` requests
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
these methods remain in the `dev.idunno.labeler.admin` namespace for administrator clients. The
labeler-agent client uses the separate `dev.idunno.labeler.issuance.issueLabel`,
`dev.idunno.labeler.issuance.negateLabel`, `dev.idunno.labeler.issuance.getOperationReceipt`, and
`dev.idunno.labeler.issuance.getAvailableLabels` procedures, authenticated with the
`labeler_issuance` audience. Both namespaces enter the same team authorization, issuance policy,
replay protection and storage boundary.
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

The `retire` CLI commands use the shared authorization path and durable retirement service.
Follow the [retirement runbook](retirement.md) for action-specific preflight/challenge examples,
worker progress, pause/resume, explicit grace selection/extension, availability attestation,
optional label-history cleanup and separately attested external key removal.

`retire status` uses `getRetirementState` (GET); `retire preflight --action <exact-method>`
uses `prepareRetirement` (POST), with `target` naming the intended administration method.
Preflight and execution must carry identical operation ID, reason, grace days, inventory and
attestation. Begin, purge, completion and both key-removal actions require their own expiring
confirmation; bare `--yes` is insufficient. Every mutation remains POST.
`confirmRetirementHostStopped` is a separately challenged POST with an exact `hostInstanceId`
GUID and nonblank stopped-process attestation. It remains available in Retired/Purged after a
key-removal request fences rotation; it records independent operator evidence, not process detection.
Normal begin/resume uses the durable background worker, not a CLI polling loop. `advance`
is manual one-step compatibility only. Completion never automatically stops distribution, and
purge deletes label history/receipts rather than moderation, administrative audit, backups or
downstream copies.

Team mutations retain same-intent receipts by operation ID. On `OutcomeUnknown`, recover using that
ID and the current authorized account; do not invent a new operation. Retirement commands also
retain immutable-intent receipts; legacy maintenance audit observations alone are not an execution
ledger. Keep clocks synchronized,
protect database credentials and backups, and treat raw provider/setup/key helpers as trusted
infrastructure unavailable to remote callers.

Protocol references: [OAuth](https://atproto.com/specs/oauth),
[permissions](https://atproto.com/specs/permission), [XRPC](https://atproto.com/specs/xrpc), and
the [inspected reference getServiceAuth route](https://github.com/bluesky-social/atproto/blob/57c85d5ea03583dae3ae8d1f51106210dcad315b/packages/pds/src/api/com/atproto/server/getServiceAuth.ts).

## Administration CLI

The trimmed, self-contained single-file `idunno.Bluesky.Labeler.Cli` executable is published for
Windows x64, Windows ARM64, Linux x64 and Linux arm64. Each invocation selects exactly one
transport. Local mode uses the configured protected named pipe or Unix socket and must run under
the OS identity explicitly bound to a live team member. Remote mode opens the user's browser for
native OAuth, obtains a method-bound service token from the user's PDS, and calls the labeler's
HTTPS administration endpoint. OAuth credentials are temporary unless `--remember-me` is
explicitly supplied. The CLI never accepts a private label-signing key.

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

The update requires a self-contained installation from a supported Windows x64, Windows ARM64,
Linux x64 or Linux arm64 release ZIP, GitHub CLI authentication/verification support, and write
access to the executable's directory. It does not update a `dotnet tool`, package-manager
installation or renamed launcher. The command asks for confirmation; `--yes` is only for an
explicitly controlled script.

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
require the labeler's HTTPS origin and administrator handle. The CLI discovers the exact service
reference from the native OAuth metadata unless `--admin-service` is supplied as an override.
The native OAuth metadata URL defaults to `/native-client-metadata.json` on that origin.
Use `--oauth-client-id` to select another metadata document on the same origin; do not substitute
a development client ID.

```text
labeler-admin team --labeler-url https://labeler.example --admin-handle admin.example
labeler-admin operation --operation-id label-op-123 --labeler-url https://labeler.example --admin-handle admin.example --remember-me
```

PLC publication/recovery instead uses `--account-handle` because it signs in as the account whose
identity will change, not a moderator. Those commands require `--oauth-client-id`.

Before opening the browser, Xenon explains the account and purpose of the sign-in.
Administrator sessions request just one method-bound permission each. Policy publication also
requires a separate labeler-account session for the conditional repository write. Its
`--labeler-oauth-client-id` overrides the metadata URL only for that
account; otherwise it uses the administrator's metadata URL, or the default native metadata on
`--labeler-url`. A local-transport publication must supply this URL explicitly because there is
no remote origin from which to derive it.

The loopback callback page is Xenon-branded, including its embedded logo, and does not claim
that authentication or the requested mutation has completed merely because the browser returned.
The logo is inlined so the callback does not load third-party resources. Callback-server logging
is disabled so callback URLs and authorization codes are not written to the terminal.
Denied or failed sign-ins, browser-wait timeouts and cancellation stop the workflow; already
committed publication gates remain available for explicit recovery rather than being silently
abandoned or retried.

### Remembering sign-ins

Supply `--remember-me` on each invocation that may load or save a remembered session. Without it,
the CLI neither reads nor writes the cache. Credentials are stored beneath the current user's
`~/.xenon` directory, separately bound to the actor DID, PDS, OAuth client ID and exact permission
set. Administrator method sessions and labeler-account sessions are not interchangeable.
Refreshed credentials, including rotating refresh tokens and DPoP state, are saved before the
credential-update notification completes.

On Windows, saved credentials are protected with current-user DPAPI. On macOS/Linux they are
**unencrypted**: the CLI warns and requires affirmative interactive consent before using
persistent credentials, creates a user-only directory (`0700`) and restricts credential files to
user read/write (`0600`). `--yes` does not bypass this consent. Anyone able to read an unencrypted
cache can act within its retained grants; do not place it on shared or synchronized storage.

There is no promised retention period: credentials remain locally until removed, while the
PDS controls token expiry, refresh eligibility and revocation. Removing the `.xenon` cache forgets
local sessions but does not revoke grants at the PDS; use the account's connected-app controls
to revoke them. Quit CLI invocations before removing cached files. An unreadable, invalid or
unsafe cache is an explicit error, not a silent fallback to a fresh session.

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
Results use human-readable text by default, with success/failure confirmations on standard error.
Add `--verbose` to print raw JSON responses on standard output instead, including failed responses.
The option works before or after a subcommand. Use it when scripting or generating a JSON
`rotation did-web` snippet. It does not enable HTTP or OAuth credential logging.
A successful no-op is distinguished from a change. Exit codes are 0 for success, 2 for invalid input or declined
confirmation, 3 for transport/setup errors, 4 for authentication or authorization failure, 5 for
service failure, and 130 for cancellation.

The CLI exposes shared team, audit, publication/lifecycle state, operation receipts, issuance,
negation, enrollment, team changes, resumable retirement, and coordinated signing-key rotation.
Rotation uses durable maintenance shared by all hosts; all participating hosts must drain, and
signing participants must acknowledge the replacement key before distribution reopens.
Distribution-only replicas remain keyless through rotation and restart. `rotation recover-plc` checks the
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
