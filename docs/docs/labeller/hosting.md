# Hosting label distribution

`idunno.Bluesky.AspNet.Labeler` hosts the public, read-only
`com.atproto.label.queryLabels` HTTP endpoint and `com.atproto.label.subscribeLabels`
WebSocket endpoint. It uses the [shared persistence](persistence.md) and
[issuance](issuance.md) services without Ozone. [Administration](administration.md) is a separate,
explicit opt-in; it does not change these public routes. There is no moderation UI, identity
provisioning, automatic retention, or signing-key manager.

## Application setup

Register exactly one storage provider and bind its database to your issuer DID.
Use setup credentials to call `InitializeAsync` before starting the host; production
applications can initialize separately and use restricted serving credentials.
The host validates the issuer and schema but never migrates a database while serving.

```csharp
using idunno.AtProto;
using idunno.Bluesky.AspNet.Labeler;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Storage;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
Did issuer = new("did:plc:your-labeler");
builder.Services.AddSqliteLabelStorage(
    builder.Configuration.GetConnectionString("Labels")
        ?? throw new InvalidOperationException("Configure label storage."));
builder.Services.AddBlueskyLabeler(issuer);

WebApplication app = builder.Build();
// Initialize separately in production when serving credentials cannot perform DDL.
await app.Services.GetRequiredService<ILabelStore>().InitializeAsync(issuer);
app.UseWebSockets();
app.MapBlueskyLabeler();
await app.RunAsync();
```

Mapping deliberately allows anonymous access. Protect issuance separately; do not expose
your authorization/policy providers or signing key through these endpoints. Terminate client TLS
at the application or a trusted proxy configured for WebSocket upgrades. Apply deployment-wide
rate limits and connection admission at that boundary; in-process limits are not cluster quotas.
Configure trusted forwarded headers, request size limits, origin policies and proxy idle timeouts
for your deployment. Administrative routes require HTTPS, either directly on the connection to the application or via
a proxy that terminates TLS, with ASP.NET Core's `UseForwardedHeaders` configured with the trusted
proxies. See [administration](administration.md) for details.

The hosted worker promotes pending durable events in bounded transactions. Multiple hosts
can share a database. Readers poll its durable publication boundary, so publication from
another process is visible and restarting a host resumes pending publication. No process-local
notification queue is needed for correctness. Stopping the application cancels readers,
subscribers and publication; publication failure stops admission and fails the hosted worker.

## Retained results and signatures

Delivery follows the retained-content behavior of
[Ozone at revision `3b3bb05411992daa8a67837c9a9a15e3286fb2d8`](https://github.com/bluesky-social/atproto/tree/3b3bb05411992daa8a67837c9a9a15e3286fb2d8/packages/ozone):
the latest published sequence for each exact `(src, uri, cid, val)` slot wins.
Missing CID is a separate slot from any particular CID. Sequence/insertion order, not
`cts`, decides replacement. Negations and expired labels remain deliverable; consumers
apply their own current-state rules. CID-scoped labels remain specific to that version
of a record, while a label without CID is URI-wide.

Internally, old versions remain immutable. A distribution projection selects retained
slot winners at a captured publication tip without deleting audit/replay history.
The existing raw store query/replay APIs still expose that history, not this projection.
Projection metadata is written atomically with issuance and upgraded additively from
schema versions one and two. Back up the projection together with all other storage tables.

HTTP responses wrap the stored signed JSON. WebSocket frames preserve the signed field
representations, including timestamp text, optional `neg: false`, CID text and signature
bytes. Neither path regenerates signatures, filters expiry, invents negations, or publishes
uncommitted labels. Raw snapshots accepted with mixed-case JSON property names use the
protocol's canonical field names in CBOR without changing signed values or optional-field
presence. CID slot indexing also recognizes those property names without normalizing CID text.
Unlike Ozone's delivery-time key repair, this host deliberately does not re-sign labels
when delivering them. This keeps private signing keys out of the public-facing serving
tier and lets multiple hosts deliver stored signatures without coordinating signing-key
changes or signature caches. Delivery-time repair would make rotation easier for operators,
but would require signing-key access during requests and would only repair newly delivered
copies, not labels already cached by consumers.

Changing a signing key therefore does not automatically re-sign retained labels. Use
`LabelSigningKeyRotation` from `idunno.Bluesky.Labeler.Signing` as an explicit maintenance
operation, following the
[key rotation guidance](overview.md#2-set-things-up-and-announce-that-youre-open).
It preserves signed fields, creation timestamps and stream sequences without emitting
replacement events. Retain old public verification keys for historical signatures;
archiving them alone does not ensure consumers can discover or trust them through the
current DID document. The labeler maintenance API and administration CLI coordinate the
explicit rotation workflow; delivery remains independent of private keys and never repairs
signatures on demand.

Use lexicon-valid inputs at issuance. Some legacy representations accepted by the SDK,
such as uppercase base32 CID text, are rejected by stricter official-client validators.
Distribution cannot normalize already-signed fields without invalidating their signatures.

## Queries and stable pagination

Use repeated `uriPatterns` parameters (required), with exact ordinal matching or a single
trailing `*`. Patterns are ORed; `*` matches everything. Optional repeated `sources` DIDs
are ORed and then combined with the URI filter. Limits are 1 through 250, default 50.
Input bounds are 100 patterns, 100 sources, 2,048 characters per entry, a 512-character
cursor and a 32,768-character query string.

The opaque cursor binds the stream identity, normalized filter sets, captured published
tip and scanned position. Changing page size is allowed; changing filters or databases is
not. Later publications cannot replace rows in an existing pagination snapshot. Gaps and
sparse matches are valid: an empty page can carry a continuation when the bounded scan
has not finished. Follow the returned cursor until it is absent.

Malformed, incompatible, future or purged query cursors return HTTP 400 `InvalidRequest`.
Storage failures, exceeded response bounds and query deadlines return HTTP 503
`ServiceUnavailable`; internal database details are logged, not exposed. Non-GET requests
return 405 with `Allow: GET`. No new lexicon-specific query error is invented.

## Subscriptions and reconnect

Connect with a WebSocket upgrade to `/xrpc/com.atproto.label.subscribeLabels`.
Both HTTP/1.1 GET upgrades and HTTP/2 extended CONNECT handshakes are supported;
CONNECT is accepted only when ASP.NET Core recognizes a WebSocket handshake.
Binary messages contain the standard concatenated CBOR header and body. Each `#labels`
event carries one retained signed label and its permanent sequence. Sequence values are
positive safe integers, never renumbered; storage refuses allocation beyond `2^53 - 1`.

An absent cursor begins after the currently published tip (live-only). Cursor `0` starts
at retained history. A supplied cursor is **exclusive**, matching the pinned Ozone
implementation: save the last fully processed sequence and reconnect with that value.
This differs from inclusive wording in the generic event-stream specification.
Replacement and retention can cause gaps; do not assume contiguous sequence numbers.

Future cursors receive a terminal `FutureCursor` error frame. Invalid input receives
`InvalidRequest`. A nonzero cursor below the replay floor receives `#info` named
`OutdatedCursor`, then delivery starts at the oldest retained boundary. Cursor `0`
silently starts at that boundary. A purge racing an active replay repeats the floor
check before continuing. Consumers needing older state must resynchronize; the server
does not claim to recover purged events.

Each replay captures a published tip, finishes that range, then captures the next tip.
The first live range starts after the last completed range: labels published during
handoff or by another host cannot fall into a subscribe/listen race. Ordinary reconnects
see the current retained-slot winners, not all historical superseded versions, just as Ozone does.

## Bounds, backpressure and diagnostics

`LabelerHostingOptions` configures per-process admission (100 subscriptions), publication
and replay batches (100 events), polling (one second), socket send deadlines (30 seconds),
query deadlines (10 seconds), and maximum encoded responses/frames (1 MiB).
All settings are validated and finite. Each storage scan inspects at most 1,000 immutable
candidates, including superseded rows; indexed winner lookups still depend on history size
and provider deadlines.

Sockets send sequentially from bounded pages without holding database transactions while
waiting on the network. Slow consumers cannot create an unbounded application queue.
A send deadline attempts a terminal `ConsumerTooSlow` error then closes/aborts; transport
failure may prevent the peer from receiving that error. Clients should reconnect from
their last processed sequence, subject to retained history. Oversized stored labels or
storage failures terminate the subscription with `ServiceUnavailable` when transport permits.
Disconnect, cancellation and shutdown release admission; excess subscriptions receive
HTTP 429 with `Retry-After`, and a missing upgrade receives HTTP 426.

Logs distinguish storage/publication failures from ordinary disconnects without including
subjects, filters, signatures or credentials. Meter `idunno.Bluesky.AspNet.Labeler` exposes
`idunno.bluesky.labeler.host.frames` (completed frames),
`idunno.bluesky.labeler.host.subscriptions` (admitted connection deltas), and
`idunno.bluesky.labeler.host.pending` (sampled durable publication backlog).
Backlog is shared database state; do not sum samples across hosts.
`idunno.bluesky.labeler.host.query.duration` measures query work in seconds;
`idunno.bluesky.labeler.host.failures` and `idunno.bluesky.labeler.host.rejections`
count operational failures and rejected requests, tagged only with fixed server-selected
operation/reason categories.
Instrument creation is automatic; enable OpenTelemetry collection separately with
`AddBlueskyLabelerMetrics()` from the `OpenTelemetry.Metrics` namespace:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddBlueskyLabelerMetrics());
```

Configure your application's readers and exporter separately. The package does not
select a monitoring backend or install a separate metrics-scraping endpoint.

The mapping also exposes `/health/labeler/live` and `/health/labeler/ready` through ASP.NET
Core health checks. Liveness reports the running application without depending on storage.
Readiness checks the publication worker, issuer/stream compatibility and a bounded database
read; failures return 503 rather than a success-shaped default. Responses are aggregate
`Healthy`/`Unhealthy` status, never database or exception details. The readiness registration
is named `bluesky_labeler`, so applications can also include it in their own health-check
routes and monitoring. Backlog alone does not make readiness fail: it is normal during
startup recovery and is measured separately.

## Discovery and service descriptions

Discovery is split across protocol roles, not served by a fabricated local AppView:

* Your issuer's DID document advertises service `#atproto_labeler`, type `AtprotoLabeler`,
  with the public HTTPS endpoint, and verification method `#atproto_label`, type `Multikey`.
  Publish the correct externally managed identity and public signing key; the host does not
  modify DID documents.
* The labeler's PDS stores `app.bsky.labeler.service/self`. Use the SDK's declaration
  creation/update APIs and configure policies and report scopes there; this host is not a PDS.
* `app.bsky.labeler.getServices` is an AppView hydration endpoint combining indexed
  declarations, profiles and viewer state. Use `BlueskyAgent.GetLabelerServices()` against
  the AppView; the distribution host does not synthesize that response.

The DID document, declaration, database issuer and signing key must describe the same
service. Verify discovery and ingestion through your target AppView before deployment;
a locally correct endpoint alone cannot guarantee that an external AppView subscribes.
