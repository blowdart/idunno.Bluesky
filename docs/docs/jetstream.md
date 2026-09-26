# Using the JetStream

The [Jetstream](https://github.com/bluesky-social/jetstream) is a streaming service that provides information on activity on the ATProto network.
You can consume the Jetstream using the `AtProtoJetstream` class.

## Protocol versions

`AtProtoJetstream` supports both versions of the Jetstream protocol, selected with `JetstreamOptions.ProtocolVersion`.

* `JetstreamProtocolVersion.V2`, the default, connects to `/xrpc/network.bsky.jetstream.subscribeEvents` on `wss://jetstream.us-west.bsky.network`.
  Every event carries a sequence number, events can be filtered by kind, and `sync` events are delivered.
* `JetstreamProtocolVersion.V1` connects to `/subscribe` on `wss://jetstream1.us-west.bsky.network`.

If you do not pass a `uri` to the constructor, the default server for the selected protocol version is used. If you pass your own `uri` it must serve the protocol version you select.

```c#
using (var jetStream = new AtProtoJetstream(
    options: new JetstreamOptions()
    {
        ProtocolVersion = JetstreamProtocolVersion.V1
    }))
{
}
```

> [!IMPORTANT]
> Before version 8.0.0 `AtProtoJetstream` only spoke version 1 of the protocol, and connected to `wss://jetstream1.us-west.bsky.network` by default.
> If you connect to a version 1 server of your own, set `ProtocolVersion` to `JetstreamProtocolVersion.V1`.
> See [Migrating from version 1 to version 2](#migrating) for the changes you may need to make.

## Jetstream events

`AtProtoJetstream` has these events you can subscribe to:

* `ConnectionStateChanged` - fired when the state of the underlying WebSocket changes, typically on open and close.
* `MessageReceived` - fired when a message has been received from the Jetstream, but has not yet been parsed.
* `RecordReceived` - fired when a message has been parsed into a JetStream event.
* `FaultRaised` - fired when something goes wrong. For an error sent by a version 2 server, such as `ConsumerTooSlow`, `FaultRaisedEventArgs.Error` holds the error name. The server closes the connection after sending an error.
* `InfoReceived` - fired when a version 2 server sends an informational notice, such as `OutdatedCursor` when the cursor you connected with was older than the server keeps.

There are four types of Jetstream event that are passed to `RecordReceived`:

* `AtJetstreamCommitEvent` - an event raised when a change happens to a record in a repo, creation, deletion or changes. For example a post is created, or a user profile is updated.
* `AtJetstreamAccountEvent` - an event that has happened on an actor's account, activation or deactivation, with an optional status indicating if deactivation was performed by moderation.
* `AtJetstreamIdentityEvent` - an event raised when an actor changes their handle
* `AtJetstreamSyncEvent` - an event raised when a repo's commit history can no longer be followed, and the repo should be fetched again. Only sent by version 2 servers.

With version 2 every event has a `Sequence` number and a `WitnessedAt` time, the time the Jetstream saw the event.
Events of a kind this library does not recognise are raised as an `AtJetstreamEvent` with a `Kind` of `JetStreamEventKind.Unknown`.

## Consuming the Jetstream

To connect to the Jetstream create a new instance of `AtProtoJetstream` and subscribe to the events you are interesting in reacting to.

Typical use would be subscribing to the `RecordReceived` event, examining the event arguments and reacting accordingly to the wanted jetstream events.

```c#
using (var jetStream = new AtProtoJetstream())
{
    jetStream.RecordReceived += (sender, e) =>
    {
        string timeStamp = e.ParsedEvent.DateTimeOffset.ToLocalTime().ToString("G", CultureInfo.DefaultThreadCurrentUICulture);

        switch (e.ParsedEvent)
        {
            case AtJetstreamAccountEvent accountEvent:
                if (accountEvent.Account.Active)
                {
                    Console.WriteLine($"ACCOUNT: {accountEvent.Did} activated at {timeStamp}");
                }
                else
                {
                    Console.WriteLine($"ACCOUNT: {accountEvent.Did} deactivated at {timeStamp}");
                }
                break;

            case AtJetstreamCommitEvent commitEvent:
                Console.WriteLine($"COMMIT: {commitEvent.Did} executed a {commitEvent.Commit.Operation} in {commitEvent.Commit.Collection} at {timeStamp}");
                break;

            case AtJetstreamIdentityEvent identityEvent:
                Console.WriteLine($"IDENTITY: {identityEvent.Did} changed handle to {identityEvent.Identity.Handle} at {timeStamp}");
                break;

            default:
                break;
        }
    };
}
```

If you want the raw messages from the jetstream subscribe to the `MessageReceived` event.

> [!WARNING]
> The Jetstream covers all ATProto events. The Commit events cover not only Bluesky record commits but any commits from a registered PDS, such as [WhiteWind](https://whtwnd.com)
> blog records or [Tangled](https://blog.tangled.sh/intro) collaboration messages.  This is why the `Record` property in `AtJetstreamCommit` is presented as a `JsonElement`.
> When deserializing this property to, for example, a `BlueskyRecord` you will encounter exceptions if you attempt it on a non-Bluesky defined record 

> [!IMPORTANT]
> In version 7.0.0 `AtJetstreamCommit.Record` changed from a `JsonDocument?` to a `JsonElement?`.
>
> Reading the record directly changes from
>
> ```c#
> JsonDocument? record = commitEvent.Commit.Record;
>
> if (record is not null)
> {
>     Console.WriteLine(record.RootElement.GetProperty("text").GetString());
> }
> ```
>
> to
>
> ```c#
> JsonElement? record = commitEvent.Commit.Record;
>
> if (record is not null)
> {
>     Console.WriteLine(record.Value.GetProperty("text").GetString());
> }
> ```
>
> Deserializing it changes from
>
> ```c#
> Post? post = JsonSerializer.Deserialize<Post>(
>     commitEvent.Commit.Record.RootElement,
>     BlueskyServer.BlueskyJsonSerializerOptions);
> ```
>
> to
>
> ```c#
> Post? post = JsonSerializer.Deserialize<Post>(
>     commitEvent.Commit.Record.Value,
>     BlueskyServer.BlueskyJsonSerializerOptions);
> ```
>
> In short, replace `.RootElement` with `.Value`, and delete any `using` or `Dispose()` you had wrapped around the record.

Once you have a configured instance of `AtProtoJetstream` call `ConnectAsync` and processing will begin in the background, raising events as appropriate.
When you are finished with the Jetstream call `CloseAsync`

```c#
await jetStream.ConnectAsync();

/// Processing happens in the background.

await jetStream.CloseAsync();
```

If you create your own `CancellationTokenSource` and token and pass it to `ConnectAsync()` you can stop the background processing by calling `Cancel()` on the `CancellationTokenSource`.

```c#
/// Setup a cancellation token
CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
CancellationToken cancellationToken = cancellationTokenSource.Token;

await jetStream.ConnectAsync(cancellationToken);

/// Processing happens in the background.

/// Time to close
cancellationTokenSource.Cancel();
```

The [Jetstream sample](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.Jetstream) shows subscribing to both raw messages and events,
writing the raw message and a breakdown of the event to the console.

## Filtering commit events

You can limit the commit events you receive by [DID](commonTerms.md#dids) or [Collection](commonTerms.md#records). You can configure the filters
when creating an `AtProtoJetstream`:

```c#
using (var jetStream = new AtProtoJetstream(
    collections: ["app.bsky.feed.post"],
    dids: ["did:plc:ec72yg6n2sydzjvtovvdlxrk"]))
{
}
```

With version 2 you can also limit the events you receive by kind, with the `KindFilter` property or `FilterTo(JetStreamEventKind[])` on
`AtProtoJetstreamBuilder`. A collection filter only applies to commit events, so it can only be combined with a kind filter which includes
`JetStreamEventKind.Commit`.

```c#
using (var jetStream = new AtProtoJetstream())
{
    jetStream.KindFilter = [JetStreamEventKind.Identity, JetStreamEventKind.Account];
}
```

Version 2 servers accept at most 100 collections and 10,000 DIDs, and setting a larger filter throws an `ArgumentException`.

You can also change the `CollectionFilter`, `DidFilter` and `KindFilter` properties on a running instance. Version 1 servers are sent the
new filters on the open connection. Version 2 servers only accept filters when connecting, so `AtProtoJetstream` reconnects in the background,
resuming from `LastSequence`. The events the old connection had already delivered are not raised again.

## Resuming from where you left off

`LastSequence` holds the largest sequence number a version 2 server has sent. Pass it as the `cursor` to `ConnectAsync` to resume after a
disconnection. The cursor is inclusive and delivery is at least once, so check `AtJetstreamEvent.Sequence` if you must not process an
event twice.

```c#
await jetStream.ConnectAsync(uri: null, cursor: jetStream.LastSequence, httpClient: null, cancellationToken: cancellationToken);
```

You can also resume from a point in time with `ConnectAsync(startFrom: DateTimeOffset)`.

## Connection errors

A version 2 server refuses a connection it cannot serve, for example when the cursor is older than it keeps. `ConnectAsync` throws a
`JetstreamConnectionException` whose `StatusCode` and `ErrorDetail` say why.

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

## <a name="retry">Retrying connection loss</a>

If the underlying WebSocket to the Jetstream is closed by the server (for example, due to the connection dropping), message parsing will stop and exit. If you want
to implement retrying the connection then you can wrap the `ConnectAsync()` / `CloseAsync()` in logic like the following, which resumes from `LastSequence`. With version 1, which does not send sequence numbers, resume with `startFrom: jetStream.MessageLastReceived` instead:

```c#
const int maximumRetries = 5;
const int retryWaitPeriod = 10000; // milliseconds
TimeSpan resetRetryCountAfter = new(0, 5, 0);

int currentRetryCount = 0;
DateTimeOffset? lastConnectionAttemptedAt = null;

do
{
    if (currentRetryCount > maximumRetries)
    {
        break;
    }

    if (DateTimeOffset.UtcNow > lastConnectionAttemptedAt + resetRetryCountAfter)
    {
        currentRetryCount = 0;
    }

    lastConnectionAttemptedAt = DateTimeOffset.UtcNow;
    await jetStream.ConnectAsync(uri: null, cursor: jetStream.LastSequence, httpClient: null, cancellationToken: cancellationToken);
    while (jetStream.IsConnected && !cancellationToken.IsCancellationRequested)
    {
        // Let it run and process
    }

    if (cancellationToken.IsCancellationRequested)
    {
        await jetStream.CloseAsync(statusDescription: "Cancellation requested at console.");
        break;
    }
    else
    {
        await jetStream.CloseAsync(statusDescription: "Force closed on error");

        // The jetstream is no longer connected, but a cancellation isn't the reason.

        currentRetryCount++;

        if (currentRetryCount > maximumRetries)
        {
            break;
        }

        // Try to reconnect
        await Task.Delay(retryWaitPeriod);
    }

} while (!cancellationToken.IsCancellationRequested);

await jetStream.CloseAsync();

```

## Configuring AtProtoJetstream

`AtProtoJetstream` has two configuration options, `options` and `webSocketOptions`.

The `options` parameter on the constructor allows you to configure

* `LoggerFactory` - The `ILoggerFactory` to use for logging
* `ProtocolVersion` - the version of the Jetstream protocol to connect with. This defaults to `JetstreamProtocolVersion.V2`.
* `UseCompression` - a flag indicating whether compression should be used. This defaults to `true`.
* `Dictionary` - the zst compression/decompression dictionary to use with version 1 if compression is enabled. This defaults to a generated dictionary specific to the jetstream.
  Version 2 servers publish the dictionary they compress with, and `AtProtoJetstream` downloads it when connecting.
* `TaskFactory` - the `TaskFactory` to use when creating new tasks. This allows you to configure `TaskScheduler` settings if needed.
* `BufferSize` - the size, in bytes, of each block read from the web socket. This is the size of the buffer a single read fills, not a limit on anything; a larger message is read in several blocks and reassembled. This defaults to 8096.
* `MaxMessageSize` - the maximum size, in bytes, of a message you are willing to accept. Messages larger than this are rejected rather than reassembled, and the value is also sent to the server, which will not send a message larger than it. This defaults to 1048576.
* `CloseTimeout` - how long to wait for a server to answer a close handshake before the connection is aborted instead. This defaults to 30 seconds.

The `webSocketOptions` parameter allows you to configure the underlying web socket client,

* `Proxy` - A proxy to use, if supplied.
* `KeepAliveInterval` - Sets the keep alive interval.

The following code snippet demonstrates setting a `LoggerFactory` and a `Proxy`

```c#
using (var jetStream = new AtProtoJetstream(
    options: new JetstreamOptions()
    {
        LoggerFactory = loggerFactory
    },
    webSocketOptions: new WebSocketOptions()
    {
        Proxy = new WebProxy(new Uri("http://localhost:8866"))
    }))
{
}
```

## <a name="migrating">Migrating from version 1 to version 2</a>

From version 8.0.0 `AtProtoJetstream` connects with version 2 of the Jetstream protocol by default. Most code keeps working unchanged,
but the following sections describe the changes you may want, or need, to make.

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
