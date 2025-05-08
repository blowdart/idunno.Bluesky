# Using the JetStream

The [Jetstream](https://github.com/bluesky-social/jetstream) is a streaming service that provides information on activity on the ATProto network.
You can consume the Jetstream using the `AtProtoJetstream` class.

## Jetstream events

`AtProtoJetstream` has three events you can subscribe to:

* `ConnectionStateChanged` - fired when the state of the underlying WebSocket changes, typically on open and close.
* `MessageReceived` - fired when a message has been received from the Jetstream, but has not yet been parsed.
* `RecordReceived` - fired when a message has been parsed into a JetStream event.

There are three types of Jetstream event that are passed to `RecordReceived`:

* `AtJetstreamCommitEvent` - an event raised when a change happens to a record in a repo, creation, deletion or changes. For example a post is created, or a user profile is updated.
* `AtJetstreamAccountEvent` - an event that has happened on an actor's account, activation or deactivation, with an optional status indicating if deactivation was performed by moderation.
* `AtJetstreamIdentityEvent` - an event raised when an actor changes their handle

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
> blog records or [Tangled](https://blog.tangled.sh/intro) collaboration messages.  This is why the `Record` property in `AtJetstreamCommit` is presented as a `JsonDocument`.
> When deserializing this property to, for example, a `BlueskyRecord` you will encounter exceptions if you attempt it on a non-Bluesky defined record 

Once you have a configured instance of `AtProtoJetstream` call `ConnectAsync` and processing will begin in the background, raising events as appropriate.
When you are finished with the jetstream call `CloseAsync`

```c#
await jetStream.ConnectAsync();

await jetStream.CloseAsync();
```

The [Jetstream sample](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.Jetstream) shows subscribing to both raw messages and events,
writing the raw message and a break down of the event to the console.type.

## Filtering commit events

You can limit the commit events you receive by [DID](commonTerms.md#dids) or [Collection](commonTerms.md#records). You can configure the filters
when creating of `AtProtoJetstream`:

```c#
using (var jetStream = new AtProtoJetstream(
    collections: ["app.bsky.feed.post"],
    dids: ["did:plc:ec72yg6n2sydzjvtovvdlxrk"])
{
}
```

You can also change the `CollectionFilter` and `DidFilter` properties on a running instance.

## Configuring AtProtoJetstream

`AtProtoJetstream` has two configuration options, `options` and `webSocketOptions`.

The `options` parameter on the constructor allows you to configure

* `LoggerFactory` - The `ILoggerFactory` to use for logging
* `UseCompression` - a flag indication whether compression should be used. This defaults to `true`.
* `Dictionary` - the zst compression/decompression dictionary to use if compression is enabled. This defaults to a generated dictionary specific to the jetstream.
* `TaskFactory` - the `TaskFactory` to use when creating new tasks. This allows you to configure `TaskScheduler` settings if needed.
* `MaximumMessageSize` - the maximum size of messages you are willing to accept, in bytes. This defaults to 8096.

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
