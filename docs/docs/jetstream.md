# Jetstream Quickstart

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
> See [Migrating from version 1 to version 2](jetstreamMigration.md) for the changes you may need to make.

## Jetstream events

`AtProtoJetstream` has these events you can subscribe to:

* `ConnectionStateChanged` - fired when the state of the underlying WebSocket changes, typically on open and close.
* `MessageReceived` - fired when a message has been received from the Jetstream, but has not yet been parsed.
* `RecordReceived` - fired when a message has been parsed into a JetStream event.
* `FaultRaised` - fired when something goes wrong. For an error sent by a version 2 server, such as `ConsumerTooSlow`, `FaultRaisedEventArgs.Error` holds the error name. The server closes the connection after sending an error.
* `InfoReceived` - fired when a version 2 server sends an informational notice, such as `OutdatedCursor` when the cursor you connected with was older than the server keeps.

There are four types of Jetstream event that are passed to `RecordReceived`:

* `JetstreamCommitEvent` - an event raised when a change happens to a record in a repo, creation, deletion or changes. For example a post is created, or a user profile is updated.
* `JetstreamAccountEvent` - an event that has happened on an actor's account, activation or deactivation, with an optional status indicating if deactivation was performed by moderation.
* `JetstreamIdentityEvent` - an event raised when an actor changes their handle
* `JetstreamSyncEvent` - an event raised when a repo's commit history can no longer be followed, and the repo should be fetched again. Only sent by version 2 servers.

With version 2 every event has a `Sequence` number and a `WitnessedAt` time, the time the Jetstream saw the event.
Events of a kind this library does not recognise are raised as an `JetstreamEvent` with a `Kind` of `JetStreamEventKind.Unknown`.

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
            case JetstreamAccountEvent accountEvent:
                if (accountEvent.Account.Active)
                {
                    Console.WriteLine($"ACCOUNT: {accountEvent.Did} activated at {timeStamp}");
                }
                else
                {
                    Console.WriteLine($"ACCOUNT: {accountEvent.Did} deactivated at {timeStamp}");
                }
                break;

            case JetstreamCommitEvent commitEvent:
                Console.WriteLine($"COMMIT: {commitEvent.Did} executed a {commitEvent.Commit.Operation} in {commitEvent.Commit.Collection} at {timeStamp}");
                break;

            case JetstreamIdentityEvent identityEvent:
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
> blog records or [Tangled](https://blog.tangled.sh/intro) collaboration messages.  This is why the `Record` property in `JetstreamCommit` is presented as a `JsonElement`.
> When deserializing this property to, for example, a `BlueskyRecord` you will encounter exceptions if you attempt it on a non-Bluesky defined record 

> [!IMPORTANT]
> In version 7.0.0 `JetstreamCommit.Record` changed from a `JsonDocument?` to a `JsonElement?`.
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

### Matching every collection in a namespace

A collection filter entry is a `CollectionSelector`, which is either an exact collection or a namespace with a `.*` suffix, matching
every collection in that namespace. A `CollectionSelector` converts implicitly from a `string` or an `Nsid`, so you rarely need to name
the type.

```c#
using (var jetStream = new AtProtoJetstream(
    collections: ["app.bsky.feed.*", "app.bsky.graph.follow"]))
{
}
```

The namespace before `.*` is validated more loosely than a collection, so `app.bsky.*` is accepted although `app.bsky` is not itself
a valid collection name. A `*` anywhere other than as a whole final segment, such as `app.bsky.fo*` or `app.bsky.*.post`, is rejected
with an `NsidFormatException`.

You can also change the `CollectionFilter`, `DidFilter` and `KindFilter` properties on a running instance. Version 1 servers are sent the
new filters on the open connection. Version 2 servers only accept filters when connecting, so `AtProtoJetstream` reconnects in the background,
resuming from `LastSequence`. The events the old connection had already delivered are not raised again.

## Resuming from where you left off

`LastSequence` holds the largest sequence number a version 2 server has sent. Pass it as the `cursor` to `ConnectAsync` to resume after a
disconnection. The cursor is inclusive and delivery is at least once, so check `JetstreamEvent.Sequence` if you must not process an
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
