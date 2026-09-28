# Jetstream live tail

[Jetstream](https://bsky.network/docs/jetstream) streams AT Protocol activity as decoded events over a WebSocket.
With `AtProtoJetstream`, you can filter at the server and process events with `await foreach`; the live v2
stream reconnects after transient disconnections and resumes from the last yielded sequence. You do not need
an archive API key to tail live events.

This guide covers the **live tail only**. For a bounded archive snapshot or an archive-to-live stream that
can recover beyond the live server's lookback window, see [network replay and snapshots](jetstreamReplay.md).
If you are upgrading an existing event-driven client or a v1 connection, see the
[Jetstream migration guide](jetstreamMigration.md).

## Quickstart: the live tail

Create a v2 client (the default), select a collection, and iterate over its events:

```csharp
using idunno.AtProto.Jetstream;

await using var jetstream = new AtProtoJetstream(
    collections: ["app.bsky.feed.post"]);

await foreach (JetstreamEvent evt in jetstream.StreamAsync())
{
    if (evt is JetstreamCommitEvent { Commit.Operation: JetstreamCommitOperation.Create } post)
    {
        Console.WriteLine($"{post.Did} posted {post.Commit.Record}");
    }
}
```

`StreamAsync` opens the connection when iteration starts. Each `JetstreamEvent` has a `Did`, a `Kind`, a
nullable `Sequence` (populated by v2), and a `WitnessedAt` timestamp. A commit carries the parsed record as
`JsonElement?`, not a typed Bluesky record; deletes have no record. Check the kind and operation before
accessing a commit's payload, and validate external records before deserializing them as a particular type.

The default v2 host is `wss://jetstream.us-west.bsky.network`. You can pass a different v2 host as `uri` to
the constructor; `AtProtoJetstream` appends the subscription path. `StreamAsync` does not support
`JetstreamProtocolVersion.V1`. See [migration](jetstreamMigration.md#staying-on-version-1) to retain an
event-driven v1 connection.

## Filtering

Supply collections and [DIDs](commonTerms.md#dids) to the constructor. A collection may be an exact NSID
or a namespace wildcard such as `app.bsky.feed.*`:

```csharp
await using var jetstream = new AtProtoJetstream(
    collections: ["app.bsky.feed.post", "app.bsky.feed.like"],
    dids: ["did:plc:ec72yg6n2sydzjvtovvdlxrk"]);

await foreach (JetstreamEvent evt in jetstream.StreamAsync())
{
    Console.WriteLine($"{evt.Sequence}: {evt.Kind} from {evt.Did}");
}
```

The collection filter applies to **commits**, not identity, account or sync events. The DID filter applies
to all kinds. To ask the server for commits only, set `KindFilter` before starting the stream:

```csharp
await using var jetstream = new AtProtoJetstream(collections: ["app.bsky.feed.post"]);
jetstream.KindFilter = [JetStreamEventKind.Commit];

await foreach (JetstreamEvent evt in jetstream.StreamAsync())
{
    if (evt is JetstreamCommitEvent commit)
    {
        Console.WriteLine($"{commit.Commit.Operation}: {commit.Commit.RKey}");
    }
}
```

Only omit identity, account and sync events if you do not need to act on account changes or repo
resynchronization. A collection filter cannot be combined with a nonempty kind filter that excludes commits.
V2 servers accept at most 100 collections and 10,000 DIDs. Changing filters while streaming reconnects
with the new filters; choose stable filters when persisting a cursor for that stream.

## Reacting to events

Match the decoded event type to process record changes, handles, account status, and sync markers:

```csharp
await using var jetstream = new AtProtoJetstream(
    collections: ["app.bsky.feed.post"]);

await foreach (JetstreamEvent evt in jetstream.StreamAsync())
{
    switch (evt)
    {
        case JetstreamCommitEvent commit:
            if (commit.Commit.Operation == JetstreamCommitOperation.Delete)
            {
                Console.WriteLine($"Delete at://{commit.Did}/{commit.Commit.Collection}/{commit.Commit.RKey}");
            }
            else
            {
                Console.WriteLine($"Put {commit.Commit.Collection}/{commit.Commit.RKey}: {commit.Commit.Record}");
            }
            break;

        case JetstreamIdentityEvent identity:
            Console.WriteLine($"{identity.Did} changed handle to {identity.Identity.Handle}");
            break;

        case JetstreamAccountEvent account:
            Console.WriteLine($"{account.Did} active: {account.Account.Active}");
            break;

        case JetstreamSyncEvent sync:
            Console.WriteLine($"{sync.Did} needs resync from revision {sync.Sync.Rev}");
            break;
    }
}
```

A sync marker means a repository's commit history can no longer be followed; consumers maintaining a
mirror should resynchronize that repository. Unknown future kinds can arrive as a plain `JetstreamEvent`
with `Kind == JetStreamEventKind.Unknown`.

## Stopping and shutdown

Breaking out of `await foreach` disposes the enumerator and closes its connection. You can also pass a
cancellation token for shutdown initiated elsewhere:

```csharp
using var shutdown = new CancellationTokenSource();
await using var jetstream = new AtProtoJetstream();

try
{
    await foreach (JetstreamEvent evt in jetstream.StreamAsync(cancellationToken: shutdown.Token))
    {
        Console.WriteLine(evt.Sequence);
        // Call shutdown.Cancel() from your shutdown handler.
    }
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
    // Normal shutdown.
}
```

One `AtProtoJetstream` instance supports only **one active enumerator/connection**. While enumerating,
you cannot subscribe to `RecordReceived`, `MessageReceived`, `ConnectionStateChanged`, `FaultRaised` or
`InfoReceived`, or call `ConnectAsync`. Remove existing handlers and close an event-driven connection
before switching to enumeration. The [migration guide](jetstreamMigration.md#migrating-from-event-handlers-to-async-enumeration)
shows both forms.

## Resuming where you left off

V2 sequences are cursors scoped to the issuing server. `StreamAsync()` retries transient connection
failures and suppresses the repeated event from its inclusive cursor within a single enumeration.
By default retries are unlimited. Pass `maximumReconnectAttempts: 5` to stop after five consecutive
reconnections; yielding an event resets the count. An exhausted limit throws `IOException`.
To resume after a process restart, supply the last **successfully processed** sequence:

```csharp
long? savedSequence = LoadSavedSequence(); // Your own durable cursor store.
await using var jetstream = new AtProtoJetstream();

await foreach (JetstreamEvent evt in jetstream.StreamAsync(cursor: savedSequence))
{
    await ProcessAsync(evt);
    if (evt.Sequence is long sequence)
    {
        SaveSequence(sequence); // Persist only after processing succeeds.
    }
}
```

`LoadSavedSequence`, `ProcessAsync` and `SaveSequence` represent your application code. The server
may send the saved cursor again after a restart, so processing should be idempotent (for example,
key writes by `at://{did}/{collection}/{rkey}`). Do not persist `LastSequence` as a substitute
for your processed cursor: it tracks received events, which can be ahead of completed work.

If the server refuses an expired cursor, `StreamAsync` throws `JetstreamConnectionException`
(for example, `ErrorDetail?.Error == "CursorTooOld"`). It does **not** skip to the current tip;
recover missing events with [archive-to-live replay](jetstreamReplay.md#replay-into-the-live-tail)
or explicitly choose a new live starting point. An `OutdatedCursor` notice also terminates
enumeration instead of silently losing the gap.

## See also

* [Network replay and snapshots](jetstreamReplay.md) for archive snapshots and archive-to-live recovery.
* [Migrating Jetstream clients](jetstreamMigration.md) for v1 compatibility and moving from event handlers to async enumeration.
