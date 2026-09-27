# Migrating from Jetstream v1 to v2

The event and payload class names now prefer `Jetstream*` rather than `AtJetstream*`: for example,
`JetstreamEvent`, `JetstreamCommitEvent`, `JetstreamCommit`, `JetstreamIdentityEvent`,
`JetstreamAccountEvent` and `JetstreamSyncEvent`. The old `AtJetstream*` types remain for existing
consumer handlers and property signatures; migrate type patterns to the new names when updating code.
The legacy `AtJetstreamCommitEvent.Commit` property remains typed as `AtJetstreamCommit`; assign it to a
`JetstreamCommit` variable to convert it implicitly, including its lazily computed archive CID.
`JetstreamCommit` also converts back to `AtJetstreamCommit` for existing event initializers.
The `JetstreamCommitEvent.IsSyncBackfill` property identifies record assertions emitted during an archive
sync. It does not indicate a new live create. See [network replay](jetstreamReplay.md) for archive-specific
checkpointing, byte-metered downloads and live cutover.


From version 8.0.0 `AtProtoJetstream` connects with version 2 of the Jetstream protocol by default. Most code keeps working unchanged,
but the following sections describe the changes you may want, or need, to make.

### Migrating from event handlers to async enumeration

The event-driven `ConnectAsync()` and `RecordReceived` APIs remain available. For a **live v2 tail**, you can
instead consume `JetstreamEvent` values from `StreamAsync()` in a single `await foreach` loop. Move the
work in your `RecordReceived` handler into that loop. You no longer call `ConnectAsync()` to start it or
`CloseAsync()` to stop it:

Before (event-driven):

```csharp
using var jetstream = new AtProtoJetstream(collections: ["app.bsky.feed.post"]);
jetstream.RecordReceived += (_, args) =>
{
    if (args.ParsedEvent is JetstreamCommitEvent commit)
    {
        Console.WriteLine($"{commit.Did}: {commit.Commit.Operation}");
    }
};

await jetstream.ConnectAsync(cancellationToken);
await WaitForShutdownAsync(); // Your application's shutdown signal; events run in the background.
await jetstream.CloseAsync();
```

After (async enumeration):

```csharp
await using var jetstream = new AtProtoJetstream(collections: ["app.bsky.feed.post"]);
await foreach (JetstreamEvent evt in jetstream.StreamAsync(cancellationToken: cancellationToken))
{
    if (evt is JetstreamCommitEvent commit)
    {
        Console.WriteLine($"{commit.Did}: {commit.Commit.Operation}");
    }
}
```

`WaitForShutdownAsync` represents your application's existing lifetime management, not a Jetstream API.

Iteration opens the connection; breaking the loop disposes its single enumerator and closes the
connection. Cancellation ends iteration with `OperationCanceledException`, which you can catch when
shutdown is expected. The enumerator processes events in sequence order and automatically reconnects
after transient disconnects, starting at the last yielded sequence and suppressing the inclusive
duplicate. Event callbacks may run concurrently and do not have the same ordering guarantee.
Retries are unlimited by default; pass `maximumReconnectAttempts: 5` to `StreamAsync()` to stop
after five consecutive retries with an `IOException`. Yielding an event resets the retry count.

Choose **one model per active connection**: do not retain subscriptions to `RecordReceived`,
`MessageReceived`, `ConnectionStateChanged`, `FaultRaised` or `InfoReceived` when starting `StreamAsync()`,
and do not call `ConnectAsync()` or start a second enumerator while streaming. Remove handlers and close
an existing event-driven connection before switching. You can use the event API again after disposing
the enumerator. For v1, keep the event-driven model; `StreamAsync()` requires v2.

If you previously persisted `LastSequence` in a handler, persist `evt.Sequence` instead **after**
successfully processing each event, and supply that value as `StreamAsync(cursor: savedSequence)`.
The server's cursor is inclusive; a restart may receive the saved event again, so make processing
idempotent. Unlike `ConnectAsync()`, `StreamAsync()` handles transient reconnections itself, but
an expired cursor raises `JetstreamConnectionException` rather than silently skipping events.
For gaps beyond live lookback, use [archive-to-live replay](jetstreamReplay.md#replay-into-the-live-tail).
See the [live tail quickstart](jetstream.md#quickstart-the-live-tail) for filtering, event types and shutdown.

### Staying on version 1

If you are not ready to move, or you connect to a Jetstream server of your own that only speaks version 1, pin the protocol version.

```c#
using (var jetStream = new AtProtoJetstream(
    options: new JetstreamOptions()
    {
        ProtocolVersion = JetstreamProtocolVersion.V1
    }))
{
}
```

or, with the builder,

```c#
AtProtoJetstream jetStream = AtProtoJetstreamBuilder.Create()
    .UseProtocolVersion(JetstreamProtocolVersion.V1)
    .Build();
```

### Custom server addresses

If you passed the version 1 default address, `wss://jetstream1.us-west.bsky.network`, to the constructor or to `ConnectTo()`, remove it
and let `AtProtoJetstream` pick the default server for the protocol version, or change it to a version 2 server.

Before:

```c#
using (var jetStream = new AtProtoJetstream(uri: new Uri("wss://jetstream1.us-west.bsky.network")))
{
}
```

After:

```c#
using (var jetStream = new AtProtoJetstream())
{
}
```

Only give the host, the path is added for you. Version 1 uses `/subscribe` and version 2 uses `/xrpc/network.bsky.jetstream.subscribeEvents`.

### Handling sync events

Version 2 servers send `sync` events, raised as an `AtJetstreamSyncEvent`, when a repo's commit history can no longer be followed.
If you mirror repo contents you should fetch the repo again when you see one. Add a case to your `RecordReceived` handler:

```c#
jetStream.RecordReceived += (sender, e) =>
{
    switch (e.ParsedEvent)
    {
        case AtJetstreamCommitEvent commitEvent:
            // As before.
            break;

        case AtJetstreamSyncEvent syncEvent:
            Console.WriteLine($"SYNC: {syncEvent.Did} must be resynchronized from revision {syncEvent.Sync.Rev}");
            break;

        default:
            break;
    }
};
```

Events of a kind that is not recognised are raised as an `AtJetstreamEvent` with a `Kind` of `JetStreamEventKind.Unknown`, so a
`default` case keeps your handler working if servers add new kinds.

### Resuming with sequence numbers

With version 1 you resumed from a point in time, usually `MessageLastReceived`. Version 2 gives every event a `Sequence` number,
and `LastSequence` holds the largest one received, so you can resume exactly where you left off.

Before:

```c#
await jetStream.ConnectAsync(startFrom: jetStream.MessageLastReceived, cancellationToken: cancellationToken);
```

After:

```c#
await jetStream.ConnectAsync(uri: null, cursor: jetStream.LastSequence, httpClient: null, cancellationToken: cancellationToken);
```

If you persist the cursor between runs, save `LastSequence` rather than a timestamp. The cursor is inclusive and delivery is at
least once, so the event at the cursor is sent again. If you must not process an event twice, check `AtJetstreamEvent.Sequence`
against the last one you handled.

```c#
long? lastHandled = LoadSavedCursor();

jetStream.RecordReceived += (sender, e) =>
{
    if (e.ParsedEvent.Sequence <= lastHandled)
    {
        return;
    }

    Process(e.ParsedEvent);

    lastHandled = e.ParsedEvent.Sequence;
    SaveCursor(lastHandled);
};
```

`ConnectAsync(startFrom: DateTimeOffset)` still works with version 2, and is useful the first time you connect.

### Filtering by event kind

Version 1 always sent every kind of event. With version 2 you can ask the server for only the kinds you want, which reduces the traffic
you receive.

Before, filtering in your handler:

```c#
jetStream.RecordReceived += (sender, e) =>
{
    if (e.ParsedEvent is not AtJetstreamIdentityEvent identityEvent)
    {
        return;
    }

    Console.WriteLine($"IDENTITY: {identityEvent.Did} changed handle to {identityEvent.Identity.Handle}");
};
```

After, filtering on the server:

```c#
AtProtoJetstream jetStream = AtProtoJetstreamBuilder.Create()
    .FilterTo([JetStreamEventKind.Identity])
    .Build();
```

A collection filter only applies to commit events, so if you set `CollectionFilter` your kind filter must include `JetStreamEventKind.Commit`.
Setting a kind filter with version 1 throws a `NotSupportedException`.

### Filter limits and changing filters

Version 2 servers accept at most 100 collections and 10,000 DIDs. Setting a larger filter throws an `ArgumentException`, so if you filter on
more DIDs than that you must filter the rest in your handler.

With version 1 changing `CollectionFilter` or `DidFilter` on a running instance sent the new filters over the open connection. Version 2 only
accepts filters when connecting, so `AtProtoJetstream` reconnects in the background, resuming from `LastSequence`. You do not need to change
your code, but you will see `ConnectionStateChanged` raised as the old connection closes and the new one opens.

### Collection filters are now selectors

`CollectionFilter`, the `collections` constructor parameter and `AtProtoJetstreamBuilder.FilterTo()` now take `CollectionSelector` rather than
`Nsid`, so a filter can name a wildcard namespace as well as an exact collection. String literals still convert implicitly, so

```c#
jetStream.CollectionFilter = ["app.bsky.feed.post"];
```

needs no change, but code which builds an array of `Nsid` first does:

```c#
// Before
Nsid[] collections = [new Nsid("app.bsky.feed.post")];

// After
CollectionSelector[] collections = [new CollectionSelector("app.bsky.feed.post")];
```

An `Nsid` converts implicitly to a `CollectionSelector`, so `.Select(nsid => (CollectionSelector)nsid)` converts an existing sequence.

### Handling errors and notices from the server

Version 2 servers tell you why they refuse or close a connection. When a server refuses a connection with an HTTP error `ConnectAsync`
now throws a `JetstreamConnectionException`, whose `StatusCode` and `ErrorDetail` say why, where before it threw a `WebSocketException`.
`JetstreamConnectionException` does not derive from `WebSocketException`, so update your `catch` blocks. This applies to both protocol versions,
although only version 2 servers return an `ErrorDetail`. Other connection failures still throw a `WebSocketException`.

Before:

```c#
try
{
    await jetStream.ConnectAsync(startFrom: savedTime, cancellationToken: cancellationToken);
}
catch (WebSocketException)
{
    await jetStream.ConnectAsync(cancellationToken);
}
```

After:

```c#
try
{
    await jetStream.ConnectAsync(uri: null, cursor: savedCursor, httpClient: null, cancellationToken: cancellationToken);
}
catch (JetstreamConnectionException ex) when (ex.ErrorDetail?.Error == "CursorTooOld")
{
    await jetStream.ConnectAsync(cancellationToken);
}
```

Errors sent on an open connection, such as `ConsumerTooSlow`, are raised through `FaultRaised` with the error name in `FaultRaisedEventArgs.Error`,
and informational notices, such as `OutdatedCursor`, through the new `InfoReceived` event.

```c#
jetStream.FaultRaised += (sender, e) =>
{
    if (e.Error is not null)
    {
        Console.WriteLine($"Server error {e.Error}: {e.Fault}");
    }
};

jetStream.InfoReceived += (sender, e) =>
{
    Console.WriteLine($"Server notice {e.Name}: {e.Message}");
};
```

### Compression

Version 2 servers publish the dictionary they compress with, and `AtProtoJetstream` downloads it when connecting, so `JetstreamOptions.Dictionary`
and `AtProtoJetstreamBuilder.WithCompressionDictionary()` are only used with version 1. If you set a dictionary you can remove it when you move
to version 2. `UseCompression` works the same way with both versions.
