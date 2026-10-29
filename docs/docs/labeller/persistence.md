# Saving your labeller's labels

If you haven't already, start with [What is a labeller?](overview.md). It follows the Bean Bureau from
its first bean-related decision through to retirement. Here we'll look at how to save its work.

Your labeller needs to remember the labels it issues. Otherwise, how will it answer a query tomorrow,
or help a subscriber catch up after a disconnection? Signing a label doesn't save it.
Keep everything in memory and a restart will lose both the labels and your place in publishing them.

Things can go wrong between steps too. You might save a label and crash before publishing it, or commit
a batch and lose the acknowledgement. The store remembers publication progress and operation receipts
so you can pick up where you left off without issuing another batch by mistake. It also gives labels
permanent sequence numbers and coordinates writers through transactions.

We need to save the **exact signed representation**, including timestamp and CID text.
Rebuilding a label from normalized database fields can change what was signed.
We'll also save retirement and purge progress: closing the Bureau shouldn't depend on one process
staying alive until the last bean is counted.

Start with `idunno.Bluesky.Labeler` and choose one provider: `idunno.Bluesky.Labeler.SQLite`,
`.PostgreSQL`, or `.MySQL`. The storage API stays the same whichever database you choose.
Your services, CLI and UI shouldn't need their own database-specific code.
The providers accept labels already signed with the [label APIs](../labels.md). For application
decisions, use the [shared issuance and withdrawal service](issuance.md), which adds authorization,
policy checks, target verification and atomic audit. Opt into [administrative authentication and
teams](administration.md) and [public hosting](hosting.md) separately. There is no moderation UI or
managed private-key store.

## Choose a provider and set it up

Let's start with a local SQLite database:

```csharp
using idunno.AtProto;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Storage;
using Microsoft.Extensions.DependencyInjection;

ServiceCollection services = new();
services.AddLogging();
services.AddMetrics();
services.AddSqliteLabelStorage("Data Source=labels.db");
// Alternatively:
// services.AddPostgreSqlLabelStorage(postgreSqlConnectionString);
// services.AddMySqlLabelStorage(mySqlConnectionString);

await using ServiceProvider provider = services.BuildServiceProvider();
ILabelStore store = provider.GetRequiredService<ILabelStore>();
await store.InitializeAsync(new Did("did:plc:your-labeler"));
```

Use a **dedicated database for one issuer DID**. `InitializeAsync` records the issuer and a permanent
stream identity, so you can't accidentally open the Bureau's database as a different labeller.
Registering a provider doesn't connect to or migrate the database.
The provider opens and disposes a connection for each operation; your host owns any `IMeterFactory`
you register through DI.

Storage contracts and snapshots are in `idunno.Bluesky.Labeler.Storage`; registration extensions
remain in `idunno.Bluesky.Labeler`. Lifecycle phases are in `.Maintenance` and signing-key
selection contracts are in `.Signing`. The provider package names have not changed.

Each provider includes `schema.sql` under `contentFiles/any/any`. You can apply it with the database's
SQL client or let `InitializeAsync` do it with setup privileges. If you use the script, you'll still need
`InitializeAsync(issuer)` to bind the database to your labeller.
Ordinary operations reject an unbound, incomplete or unsupported schema.
Use the supplied setup path rather than hand-building tables or editing rows to administer the service.

SQLite uses the SQLite library distributed with Microsoft.Data.Sqlite, WAL mode, FULL synchronization,
foreign keys and immediate writer transactions. Use persistent local storage, not memory, a URI filename,
UNC path, network filesystem or ephemeral container layer. The filesystem must honor flush requests.
SQLite operations include synchronous client work; lock waits are bounded, not truly asynchronous I/O.

PostgreSQL requires version 17 or later with `fsync=on` and `full_page_writes=on`; sessions use `synchronous_commit=on`,
15-second lock/statement deadlines and row locks. MySQL requires version 8.4 or later, InnoDB,
`innodb_flush_log_at_trx_commit=1` and `sync_binlog=1`; sessions use strict data handling and bounded
lock waits. MariaDB is not a verified substitute. Unsupported settings fail instead of reducing guarantees.
Storage hardware, replication and failover must independently preserve acknowledged commits.
Use verified TLS, secret-managed credentials and restricted networking in production.

### Start a development database

For PostgreSQL or MySQL, the repository has helpers in `.artifacts`:

```powershell
$postgres = .\.artifacts\Start-PostgreSqlLabelerContainer.ps1
$mysql = .\.artifacts\Start-MySqlLabelerContainer.ps1
```

Each helper sets up the schema and returns a connection string. The container binds to loopback and
uses a **named Docker data volume**, so removing the container needn't lose your beans.
You can choose the container and volume names, port, database, user and image through its parameters.
For a new volume, omitting passwords lets the script generate them.

Keep those credentials somewhere safe, not in source control or logs: the connection string contains a secret.
When recreating a container with an existing volume, supply the same credentials explicitly.
Changing initialization environment variables won't change passwords in an existing database.
The helpers won't silently overwrite a conflicting container.
They're development tools, not hardened production deployments; anyone with Docker access can inspect
container secrets.

Stop/remove a specific container without removing its volume to preserve the database.
Recreate it with the same volume and credentials, then use the documented upgrade path.
Removing a volume destroys its database; do so only through a separate, deliberate operator action
after backups. Disposable tests use unique container/volume names and remove only their own resources.
Preserve failure logs before cleanup. Never use broad Docker prune commands.

The older authentication helpers are now explicitly named `Start-MySqlAuthenticationContainer.ps1`,
`Start-PostgreSqlAuthenticationContainer.ps1` and `Start-RedisAuthenticationContainer.ps1`.
They initialize authentication storage, not labeler storage.

The SQLite package also ships:

```powershell
.\New-LabelDatabase.ps1 -OutputDirectory .\data
```

This requires `sqlite3` on PATH, uses the adjacent `schema.sql`, refuses to overwrite an existing file,
and returns the created file path. Use `Data Source=<returned-path>` as the connection string.
A failed creation rolls back schema changes and removes only its newly created file.

## Save a signed label

Once you've signed a `Label`, wrap it in `SignedLabelSnapshot`. This takes a fixed snapshot of its signed UTF-8
JSON and canonical unsigned DAG-CBOR signing payload. The byte getters return copies, and `Label`
returns a fresh object, so changing a returned value won't change what's waiting to be saved.
If you pass UTF-8 JSON to the constructor, it keeps those exact bytes.

Timestamp offsets, fractional precision, original CID text, and the difference between an omitted `neg`
and `neg: false` all matter. Don't rebuild them from normalized database values or use `Label.Equals`
to decide whether two signed representations are identical. The database stores the JSON as binary;
it doesn't parse or normalize it.

Representation validation rejects unsigned/malformed/unsupported labels and wrong issuers.
It is **not cryptographic trust verification**: verify untrusted labels against trusted public keys
before submitting them. Reading checks stored canonical-payload agreement but is not a substitute
for verification when database tampering is in scope.

`AppendAsync(operationId, labels, expectedRevision, retirementThrough)` commits the fixed batch,
idempotency receipt, sequence allocation and optional retirement checkpoint together. Batches are
limited to 1,000 labels; an empty batch is permitted only to advance a retirement checkpoint.
Sequences are positive Int64 values allocated under the stream-state lock in commit order.
There is no timestamp ordering and no sequence allocation outside the transaction.
An exception before commit leaves no partial batch.

An identical key/input returns its original `LabelAppendReceipt`, even if the caller's expected
revision has since become stale. Changed signed input under that key conflicts.
The receipt records the original sequence range and revision; its zero range identifies an empty batch.
Keys are nonblank ordinal UTF-8 identities of at most 256 bytes and cannot contain U+0000 characters;
hash-index matches are checked against full keys.

**Keep the signed command if you might need to retry.** ECDSA can produce another signature for the same
payload. If you lose an acknowledgement, call `GetReceiptAsync` rather than signing everything again.
A saved receipt tells you the original operation committed. If there's no receipt, you can retry the
same fixed command; a freshly signed one is different input. Receipts survive restarts and stay until
you explicitly purge them.

The provider retries only classified busy/deadlock/serialization failures before starting commit,
at most twice with bounded backoff and cancellation. A commit-time error is ambiguous and is never
blindly replayed. Database/storage errors propagate; they are not empty pages or success results.
Optional revisions provide optimistic concurrency for caller decisions; stale decisions must be re-evaluated.

## Make saved labels available to readers

Saving a label and publishing it are separate steps. `IssuedThrough` tells you how far you've saved;
`PublishedThrough` tells you how far readers can see. `PublishAsync(limit)` makes the next group available
in one transaction, without skipping an earlier label. If that transaction fails, the old boundary stays
put and you can retry after a restart. There's no network work between claiming and completing publication,
so there's no work lease to expire.

Publication means durable local availability, **not** acknowledgement by all remote subscribers.
Every label has one permanent subscription-event sequence. Read batching does not regroup or renumber
events, and negations remain immutable history. There is no automatic expiry deletion or retention job.

`QueryAsync(uriPatterns, source, cursor, limit)` uses a captured published tip and keyset continuation.
Exact patterns and a single trailing `*` prefix compare ordinally; an empty list matches all subjects.
At most 100 patterns and 1,000 returned labels are accepted. The provider currently scans bounded
sequence chunks and filters signed labels in-process to ensure identical ordinal behavior across engines.
Sparse-filter queries can therefore scan substantial history and hold the stream lock; monitor latency.
This favors correctness over a provider-specific collation/index shortcut.

Pass the returned cursor back with the same filters to get the next page.
It records the format, stream, filters, captured end boundary and last sequence.
You'll get an error for malformed or incompatible cursors, different filters or streams, future boundaries,
or history that's been purged. A cursor helps you navigate; it doesn't grant permission to read.
Labels published later won't sneak into a query you're already paging through.

For subscriptions, read `ReplayAsync(streamId, after, through, limit)`, resume after the last returned
sequence, and drain the fixed tip. Capture a newer published tip and repeat to hand off to live reads
without gaps. A notification is only a wake-up hint; the durable log is authoritative.
All operations currently serialize on the stream row (the SQLite writer lock), including reads, so
multiple providers have identical snapshot and mutation behavior. This bounds design complexity but
limits parallel read throughput; do not hold a transaction across a network send.

## Retire your labeller without losing your place

When it's time to close the Bureau, there's more to do than switch off its host.
You'll need to work through existing labels, publish any required withdrawals, and give consumers
an opportunity to retrieve them. The store remembers your progress; it doesn't make those decisions
or run the shutdown for you.

For an actual deployment, use the authorized [retirement workflow](retirement.md), not raw store
helpers. It couples immutable command receipts, action-specific challenges, live authority and audit,
and runs bounded withdrawal/purge work in a durable server worker. It requires explicit 1-365 day grace
selection and completion availability attestation. The following raw primitives describe storage
mechanics only; they do not supply that operator authorization or confirmation.

Start with `BeginRetirementAsync(expectedRevision)`. This stops ordinary issuance and fixes the last
historical sequence your retirement service needs to inspect. That service works through the history
and signs the negations required by your policy. Publish all existing history before reading it through
query or replay.

Save each retirement batch with `AppendAsync` and a `retirementThrough` checkpoint. This is how far
you've inspected the original history, not the sequence of a new negation. It must move forward without
passing the fixed upper bound. The negations, receipt and checkpoint commit together.
Retirement batches accept only negations; the store doesn't decide which earlier labels they supersede
or whether the operator is authorized.

After the enumeration reaches its bound, the phase becomes `Distributing`. Publish all pending batches,
then call `BeginGracePeriodAsync` with an explicit positive duration. The deadline uses database time
sampled after locking, rounded up to milliseconds with one extra millisecond for clock quantization.
`CompleteRetirementAsync` refuses early completion.
Provider recreation preserves every phase and checkpoint.

Only after retirement completes can you call `PurgeAsync(expectedRevision, confirmed: true, limit)`.
Your service must authorize that separately and ask for confirmation. Each call removes a bounded
batch of labels and receipts, so you can resume after an interruption.
The replay floor moves forward and continuations needing deleted history stop working.
The store keeps its stream identity, sequence high-water marks and lifecycle metadata; it won't reuse
sequences or reopen for issuance. Purging doesn't destroy keys or erase anyone else's copies.

Team, audit and moderation persistence belong to their owning workstreams. They must extend typed provider
transactions when atomic authorization/audit coupling is required. Service decision audit is already
committed atomically with labels through `ILabelStore`; team revocation fencing is not.
Do not bypass services through a raw
connection or claim that an independent second write is atomic with issuance.

## Upgrade and back up your database

Use setup credentials for schema installation/upgrades, and runtime credentials restricted to SELECT,
INSERT, UPDATE and DELETE on the provider's tables, including its moderation
tables when Xenon is enabled. Runtime PostgreSQL/MySQL sessions also require permission
to read the server settings validated above. Run `InitializeAsync` separately with setup credentials.
Registration and ordinary operations do not run DDL.

Version two adds `label_decisions` for immutable service receipts and audit, including no-ops.
`InitializeAsync` upgrades supported earlier schemas to version eleven additively without changing signed history, legacy receipts,
stream identity or progress. `label_schema` records completion only after all creation steps.
PostgreSQL setup takes an advisory lock and commits DDL transactionally; SQLite takes an immediate
transaction. MySQL takes a connection-scoped schema lock and resumes repeatable creation steps after
interruption, because its DDL commits independently. Run upgrades with services stopped/exclusive access,
especially on MySQL; do not promise rollback of a partially applied MySQL migration.
Repeated initialization preserves data. Unknown/newer versions fail rather than being downgraded.

Version three adds `label_distribution` metadata for CID-scoped retained-slot delivery.
Its backfill resumes missing entries and never rewrites signed JSON or canonical payload bytes.
New entries and projection metadata commit atomically. The raw store query/replay contracts
remain historical; [public hosting](hosting.md) selects the latest published slot at a fixed tip.

Version nine adds [optional moderation](moderation.md): reports, cases, appeals,
workflow projections and immutable policy/recovery history share the label
transaction boundary. Subject-index backfill resumes independently of older
decision projections. No report/case is required for manual issuance, and a
database without an initialized Xenon gate keeps the existing distribution path.

Version ten adds local normalized moderation-search text, expiring verified
subject-handle observations and persisted deleted-post review scheduling.
Text backfill resumes in bounded batches without changing original evidence or
contacting external services. Report/appeal resolution reasons are additive
payload fields; older handled records without a retained reason remain readable.
Back up these tables with the rest of the moderation database. Older writers
reject the newer schema; stop them before upgrading.

Version eleven adds durable pending-report storage for reports received while
policy verification is unavailable. The queue is bounded and replay-protected;
reports become actionable only after background verification succeeds.

Back up the whole database together: schema metadata, issuer and stream identity, labels, receipts,
publication boundaries, lifecycle checkpoints and all team/moderation/policy/audit
history. For SQLite, use its online backup facility or a properly
coordinated filesystem snapshot including WAL. Just copying an open `.db` file isn't safe.
Use consistent PostgreSQL/MySQL backups too, and practise restoring them.
Take a backup before a migration. Back up your private signing key separately.

A backup restored to an earlier instant may have lost externally observed, acknowledged tail events.
Do not resume public distribution under an old stream/sequence range as though nothing happened:
reconcile the missing tail from trustworthy durable evidence or perform an explicit service-level stream
recovery/reset that invalidates old cursors. The persistence package cannot detect every external
observation or recover data absent from the backup; it does not automatically renumber/reopen a stream.

## Keep an eye on things

The providers supply logs and metrics to help you spot trouble before subscribers start asking
where their bean labels went.

Providers inject typed loggers and use source-generated `[LoggerMessage]` events:
1 schema readiness (Information), 2 retry (Warning), 3 failure category (Error),
4 conflicting state/command (Warning), 5 lifecycle transition (Information).
Messages do not include payloads, subject/issuer identifiers, keys, credentials or connection strings.
Expected cancellation propagates without an error event.

Subscribe to `LabelPersistenceMetrics.MeterName` (`idunno.Bluesky.Labeler.Persistence`), version `1.0.0`.
For an existing OpenTelemetry setup use `builder.AddMeter(LabelPersistenceMetrics.MeterName)`.
No telemetry exporter is required by the persistence packages.

| Instrument prefix: `idunno.bluesky.labeler.` | Kind / unit | Meaning |
|---|---|---|
| `operation.duration` | Histogram / seconds | End-to-end operation duration, including database waits and retries |
| `operations` | Counter / operations | Outcomes plus retry/idempotent diagnostic events |
| `operations.active` | UpDownCounter / operations | Process-local in-flight calls |
| `labels` | Counter / labels | Committed append/publish labels or returned query/replay labels, by operation |
| `bytes` | Counter / bytes | Committed append or returned read JSON bytes |
| `publication.pending` | Histogram / labels | Explicitly sampled cross-process durable backlog |
| `publication.oldest` | Histogram / seconds | Database-clock age of oldest unpublished label |

Tags are bounded `provider` (`sqlite`, `postgresql`, `mysql`), operation and outcome categories.
No identity, URI, value, sequence, cursor, raw exception text or operation ID is a tag.
`GetSnapshotAsync` explicitly samples backlog; collecting instruments never issues SQL or starts a poller.
With multiple processes, snapshot measurements describe shared state and must not be summed as independent
queues. Local throughput/activity counters describe only the instrumented process.
Append/publish label counters advance only after a successful commit; an ambiguous commit may not be
reflected in that process's counters. Durable state is the source of truth.

Look at durations alongside contention and retry outcomes when investigating lock pressure.
If the backlog keeps growing and its oldest item keeps getting older, check whether your publisher is
running and keeping up. Lifecycle logs and saved checkpoints help you see where recovery has reached;
migration durations use the `initialize` operation.
The [metrics reference](../metrics.md#idunnoblueskylabelerpersistence) covers collection setup,
tags and counting details.

## Run the provider tests

The common suite requires on-disk SQLite plus real PostgreSQL/MySQL in **disposable Docker containers**.
Set `LABELER_TEST_POSTGRESQL_CONNECTION` and `LABELER_TEST_MYSQL_CONNECTION` to administrative
connections on those test servers: fixtures create/drop uniquely named databases per case and framework.
Never point these variables at production/shared databases. Missing/unreachable engines fail; they do not skip.
Use root/admin access only in these isolated tests, not application deployment.

```powershell
dotnet test --project .\test\idunno.Bluesky.Labeler.Test\idunno.Bluesky.Labeler.Test.csproj -f net10.0
```

CI provides isolated service containers and requires the same three-engine suite. Test telemetry uses official
`Microsoft.Extensions.Diagnostics.Metrics.Testing` collectors. Database tests do not need an HTTP listener;
if future HTTP fixtures are needed, use ASP.NET TestServer rather than a hand-rolled listener.
