# Network replay and snapshots

Jetstream v2 serves a sealed archive over authenticated HTTP and an unauthenticated live WebSocket. Use
`AtProtoJetstream.SnapshotAsync()` for a **bounded decoded snapshot**; use `ReplayAsync()` to continue onto the
live tail without a gap. Both yield `JetstreamEvent` objects, including `JetstreamCommitEvent`,
`JetstreamIdentityEvent`, `JetstreamAccountEvent` and `JetstreamSyncEvent`.

An archive API key ([create one here](https://bsky.network/account#api-keys-section-heading)) is required for HTTP requests, but **not** for a live-only WebSocket subscription. Configure it
with `JetstreamOptions.ApiKey` or `AtProtoJetstreamBuilder.WithApiKey()`. Do not store the key in source control.
Archive requests require HTTPS or WSS for non-loopback services so the key is not transmitted in plaintext.
The archive uses the same host configured for the live client, defaulting to `jetstream.us-west.bsky.network`.
A v1 Jetstream host does not provide the archive endpoints.

[!include[Untrusted data warning](includes/untrusted-data-warning.md)]

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
as the server seals new segments. If `AfterSeq` is at or ahead of the sealed tip, the snapshot is empty and its
checkpoint can be resumed without replanning; replay still connects to the live stream from that cursor.
The server may select whole `.jss` segments or individual compressed blocks.
Planning is approximate: even a collection-filtered plan may download blocks with no matching events. The client
filters **every decoded row** by DID, collection, kind and sequence; collection filters apply to records, **not**
to identity, account or sync markers. Keep these markers if you need to fold account deletions or repo syncs.

## Checkpointing and cancellation

Pass a `SnapshotCheckpoint` restored from storage to resume a bounded snapshot. Its `SealedTipSeq` retains the
original tip; never substitute a newly planned tip for it. The callback receives progress **after** the iterator has
advanced past all events in a block, with segment name, checksum, next block index and (for a whole segment)
the next frame byte offset. Persist each checkpoint only after applying the preceding events. The server can compact
a sealed segment and change its checksum; when a new plan detects the change, the client starts that segment again
rather than resuming stale bytes.
Delivery is **at least once** across crashes: make record processing idempotent, for example by AT URI.
Checkpoints are bound to the original service, sequence bounds and filters; changing any of them requires
a new snapshot. Older checkpoints without a request fingerprint cannot be resumed safely and must be discarded.

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

## Handling invalid archive records

Archive records are untrusted input. By default, a record that cannot be decoded stops `SnapshotAsync()` or
`ReplayAsync()` with its decoding exception. To choose a recovery policy, pass `onArchiveError`; the callback receives
the record sequence for a row failure, or `null` for a whole-block decode failure, and the original exception.
Return `JetstreamArchiveErrorAction.Stop` to preserve fail-fast behavior, `SkipRecord` to omit one bad row and
continue with the next row, or `SkipBlock` to omit the rest of the current block and continue at the next block.
This includes invalid DAG-CBOR/JSON payloads, invalid row metadata such as DIDs, NSIDs, record keys or timestamps,
and malformed compressed block contents. Invalid frame lengths, truncated block downloads and transport failures
remain fatal because the client cannot safely establish the next block boundary.
Returning `Stop` preserves the default fail-fast behavior. Returning `SkipRecord` explicitly omits that record and
continues with the rest of the block. Records excluded by the snapshot filters are not decoded and do not invoke the
callback.

```csharp
await foreach (JetstreamEvent evt in jetstream.SnapshotAsync(
    request,
    onArchiveError: (sequence, exception) =>
    {
        if (sequence is long recordSequence)
        {
            Console.Error.WriteLine($"Skipping invalid record {recordSequence}: {exception.Message}");
            return JetstreamArchiveErrorAction.SkipRecord;
        }

        Console.Error.WriteLine($"Skipping invalid archive block: {exception.Message}");
        return JetstreamArchiveErrorAction.SkipBlock;
    },
    cancellationToken: cancellationToken))
{
    Apply(evt);
}
```

Skipping is an explicit data-loss decision. `SkipRecord` permanently omits one record; `SkipBlock` can omit multiple
records, including valid ones, from that block. In either case the checkpoint advances only after the block has been
consumed according to the selected policy. Persist the checkpoint only after applying emitted events, just as for
normal snapshot processing. `ReplayAsync()` uses the same callback for archive records; live events are unaffected.
The replay sample supplies this policy through `ReplayAsync()`, which forwards it to `SnapshotAsync()`: it logs
and skips individual invalid records, or logs and skips an entire block when block decoding fails. This intentionally
permits data loss, including valid records in an undecodable block; a production app should choose its policy explicitly.

## Recovering from archive generation mismatches

A segment can change between planning and downloading, or while a download is being resumed. The client validates
the download ETag against the planned checksum and stops enumeration with `InvalidDataException` if they differ.
An HTTP 200 does not establish that the returned segment matches the plan. A resumed whole-segment download also
opens a separate request at offset zero to read its header; a mismatch in that request can report offset zero
even though the checkpoint contains a nonzero resume offset.

This failure is not automatically replanned and does not invoke `onArchiveError`, which handles record and block
decoding failures only. Recovery **does not require an application restart** or a new `AtProtoJetstream` instance.
Catch the generation mismatch outside the `await foreach`, wait, and start a new `SnapshotAsync()` or `ReplayAsync()`
enumeration with the original request and latest durably persisted checkpoint. Update the in-memory checkpoint
only after successfully persisting it. Keep the original request's sequence bounds and filters unchanged.
Pass the entire saved checkpoint unchanged, including its request fingerprint, sealed tip, plan and replay cursors,
segment name and checksum, next block index, byte offset and live cursor. Do not replace the saved checksum or reuse
the old byte offset with a new checksum. The fresh plan can detect a changed generation and
restart that segment, so previously handled events may repeat and processing must be idempotent.

Use bounded retries and report persistent mismatches: repeated failures after fresh planning may indicate stale
planner metadata or inconsistent server/CDN responses, not just a one-off compaction race. Do not retry all
`InvalidDataException` failures; the same type also represents corrupt data and other archive inconsistencies.
There is currently no dedicated generation-mismatch exception type. The replay sample narrowly matches the SDK's
current ETag-mismatch and failed-generation-resume exception messages; this is a sample workaround, not a stable
exception-classification API, and must be checked when upgrading the SDK. Other failures propagate.

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
out. Archive checkpoints created during a fallback retain the original request fingerprint and record the
fallback starting sequence in `ReplayAfterSeq`; resume them with the same original request. A live checkpoint
is not valid input to `SnapshotAsync`. Transient connection failures are retried with a short delay; invalid
credentials and other permanent server refusals are surfaced to the caller.

The live handoff buffers at most 1,024 events. If a slow consumer fills that buffer, replay drains the
events already queued, closes the connection and reconnects from the last delivered sequence. It does
not advance the checkpoint to the last received sequence or drop buffered events to make room.
Events not queued must be served again by the inclusive cursor, or recovered from the archive if the
cursor has expired. Persist the full checkpoint only after durably processing preceding events.

Enable Jetstream logging to distinguish reconnect causes. Established live-connection warnings (event 42)
report buffer overflow, remote close, transport or receive failure, server error or notice-based cursor expiration,
with the connection's starting cursor, last received sequence and buffered event count. Recovery entries (event 44)
report the last delivered sequence and exception; an expired cursor reports archive fallback. Upgrade-time
`CursorTooOld` rejection emits the recovery entry without a live-end warning, so starting cursor,
last received sequence and buffered count are not available in that path. Debug entries identify cancellation
and enumeration disposal during cleanup. A close/disposal exception alone does not identify the original
reconnect trigger.
When a queued server-error frame is followed by a close or transport failure, replay retains the server error
as the initiating cause after draining pending parsers instead of reporting only the secondary disconnect.

Archive bandwidth is metered in downloaded bytes. The client paces downloads using advertised
`headwind-quota-*` headers, waits for `Retry-After` on HTTP 429, and resumes interrupted downloads with a
validated byte range and ETag. No quota is consumed for bytes skipped by a successful range request.
The archive may redirect a block download to a short-lived signed CDN URL. The client follows the redirect
without forwarding the API key; the response ETag still has to match the planned segment checksum.
Cross-origin CDN redirects are followed only when the SDK creates its SSRF-protected HTTP client; clients
provided through a factory or to the low-level server methods follow same-origin redirects only. Supplied
clients must disable automatic redirects. This does not verify that the redirect hostname belongs to Bluesky:
an archive server can choose any public HTTPS destination permitted by the SDK's SSRF-protected transport.
Each archive-body read has a configurable inactivity timeout
(`JetstreamOptions.ArchiveReadTimeout`, 30 seconds by default). Stalled reads resume with a validated
range and ETag within the bounded retry limit; quota and `Retry-After` waits do not count toward it.

Run the [Jetstream replay sample](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.JetstreamReplay)
to resolve the selected handle and replay its posts, likes, follows, identity and account events from `afterSeq=0`
before tailing live:

```powershell
$env:_JetstreamApiKey = '<your key>'
$env:_BlueskyHandle = 'bot.idunno.blue'
dotnet run --project samples\Samples.JetstreamReplay -- --host wss://jetstream.us-west.bsky.network
```

The sample also accepts `--api-key` and `--handle` (each overriding its environment variable), and saves an archive or live checkpoint
to `jetstream-replay-checkpoint.json`. On an archive generation mismatch it logs the failure and retries up to
**five times after the initial attempt**, waiting **30 seconds before each retry**, using the latest successfully
saved checkpoint and the same client. The retry budget is for the entire run and does not reset after progress.
The wait is cancellable; after five retries the next mismatch is logged and rethrown. Other invalid-data failures
are not retried. Record and block decoding failures are instead logged and skipped through `onArchiveError`;
invalid frame lengths, truncated downloads and other failures outside that callback still stop the sample.
Replayed events may appear in the console more than once.
It prints the collection for every commit, locally timed post text
(or `No Text`) and like subjects (AT URI and CID). Other commits display their operation and local event time.
Post and like records use the Bluesky source-generated JSON options. Deletes display the record's AT URI and
local deletion time. The sample keeps the most recent 1000 distinct posts in memory, refreshing an entry on update.
When a post is deleted while its entry is still cached, it prints the original post text and can use the cached
record CID for a strong reference (archive delete events do not contain a CID). Unmatched deletes retain the
AT URI and timestamp only. The cache is not stored in the checkpoint, so deletes after a restart can be unmatched.
When a PDS record does not conform to the Bluesky lexicon and typed deserialization fails, the sample warns with
the AT URI and Jetstream sequence, reads available display fields directly from the record JSON, and continues.
If `createdAt` is unavailable, it labels the event's observed time rather than presenting it as the record date.
Never check in the checkpoint file or an API key.
