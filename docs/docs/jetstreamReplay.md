# Network replay and snapshots

Jetstream v2 serves a sealed archive over authenticated HTTP and an unauthenticated live WebSocket. Use
`AtProtoJetstream.SnapshotAsync()` for a **bounded decoded snapshot**; use `ReplayAsync()` to continue onto the
live tail without a gap. Both yield `JetstreamEvent` objects, including `JetstreamCommitEvent`,
`JetstreamIdentityEvent`, `JetstreamAccountEvent` and `JetstreamSyncEvent`.

An archive API key is required for HTTP requests, but **not** for a live-only WebSocket subscription. Configure it
with `JetstreamOptions.ApiKey` or `AtProtoJetstreamBuilder.WithApiKey()`. Do not store the key in source control.
The archive uses the same host configured for the live client, defaulting to `jetstream.us-west.bsky.network`.
A v1 Jetstream host does not provide the archive endpoints.

```csharp
using idunno.AtProto.Jetstream;
using idunno.AtProto.Jetstream.Archive;

string key = Environment.GetEnvironmentVariable("_JetstreamApiKey")
    ?? throw new InvalidOperationException("Set _JetstreamApiKey before downloading the archive.");
using var jetstream = new AtProtoJetstream(options: new JetstreamOptions { ApiKey = key });
var request = new SnapshotRequest
{
    AfterSeq = 0,
    Collections = [new CollectionSelector("app.bsky.feed.post")],
    Kinds = [JetStreamEventKind.Commit, JetStreamEventKind.Identity,
             JetStreamEventKind.Account, JetStreamEventKind.Sync]
};

await foreach (JetstreamEvent evt in jetstream.SnapshotAsync(request, cancellationToken: cancellationToken))
{
    Console.WriteLine($"{evt.Sequence}: {evt.Kind}");
}
```

The first plan pins a sealed tip. Following pages keep that tip as `beforeSeq`, so the archive window does not drift
as the server seals new segments. The server may select whole `.jss` segments or individual compressed blocks.
Planning is approximate: even a collection-filtered plan may download blocks with no matching events. The client
filters **every decoded row** by DID, collection, kind and sequence; collection filters apply to records, **not**
to identity, account or sync markers. Keep these markers if you need to fold account deletions or repo syncs.

## Checkpointing and cancellation

Pass a `SnapshotCheckpoint` restored from storage to resume a bounded snapshot. Its `SealedTipSeq` retains the
original tip; never substitute a newly planned tip for it. The callback receives progress **after** the iterator has
advanced past all events in a block, with segment name, checksum, next block index and (for a whole segment)
the next frame byte offset. Persist each checkpoint only after applying the preceding events. The server can compact
a sealed segment and change its checksum; the client then starts that segment again rather than resuming stale bytes.
Delivery is **at least once** across crashes: make record processing idempotent, for example by AT URI.

```csharp
SnapshotCheckpoint? saved = LoadCheckpoint();
await foreach (JetstreamEvent evt in jetstream.SnapshotAsync(
    request,
    checkpoint: saved,
    onCheckpoint: SaveCheckpoint,
    cancellationToken: cancellationToken))
{
    Apply(evt);
}
```

`LoadCheckpoint`, `SaveCheckpoint` and `Apply` above represent your own durable store and idempotent handler.
For AOT applications, register `SnapshotCheckpoint` with a source-generated `JsonSerializerContext` when storing
it as JSON. Cancellation stops HTTP requests, quota waits, downloads, decoding and the live subscription.

## Replay into the live tail

Replace `SnapshotAsync` with `ReplayAsync` to consume the same bounded archive and then connect once at its
pinned sealed tip. The live cursor is inclusive; the driver discards the overlap. If the live server can no longer
serve that cursor, the driver replans from the last delivered sequence rather than silently losing the interval.
Replay continues until cancelled. Backfilled kind-7 records appear as `JetstreamCommitEvent` with
`IsSyncBackfill == true`; they are assertions that a record existed at a sync revision, not new live creates.
Do not assume a corresponding sync event appears in the same block or even the same segment. `Commit.Cid` is
computed from owned DAG-CBOR bytes on first access and cached; delete events have no CID.

`ReplayAsync` also emits checkpoints after live events have been consumed, recording `LiveAfterSeq` alongside
the original sealed tip. Pass that checkpoint back to `ReplayAsync` after restarting: it reconnects directly
at the inclusive live cursor, drops the repeated event, and falls back to the archive if the cursor has aged
out. A live checkpoint is not valid input to `SnapshotAsync`.

Archive bandwidth is metered in downloaded bytes. The client paces downloads using advertised
`headwind-quota-*` headers, waits for `Retry-After` on HTTP 429, and resumes interrupted downloads with a
validated byte range and ETag. No quota is consumed for bytes skipped by a successful range request.
The archive may redirect a block download to a short-lived signed CDN URL. The client follows the redirect
without forwarding the API key; the response ETag still has to match the planned segment checksum.

Run the [Jetstream replay sample](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.JetstreamReplay)
to resolve `bot.idunno.blue` and replay its posts, likes, follows, identity and account events from `afterSeq=0`
before tailing live:

```powershell
$env:_JetstreamApiKey = '<your key>'
dotnet run --project samples\Samples.JetstreamReplay -- --host wss://jetstream.us-west.bsky.network
```

The sample also accepts `--api-key` (overriding the environment variable) and saves an archive or live checkpoint
to `jetstream-replay-checkpoint.json`. Never check in that file or an API key.
