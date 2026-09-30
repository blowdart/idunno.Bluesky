# Firehose

The AT Protocol [event streams](https://atproto.com/specs/event-stream) deliver repository and label activity
over a WebSocket as DAG-CBOR frames. `AtProtoFirehose` reads two of them:

* [`com.atproto.sync.subscribeRepos`](https://github.com/bluesky-social/atproto/blob/main/lexicons/com/atproto/sync/subscribeRepos.json),
  the relay firehose of repository commits, syncs, identity and account changes, with `SubscribeReposAsync()`.
* [`com.atproto.label.subscribeLabels`](https://github.com/bluesky-social/atproto/blob/main/lexicons/com/atproto/label/subscribeLabels.json),
  a labeler's stream of labels, with `SubscribeLabelsAsync()`.

Unlike [Jetstream](jetstream.md), the firehose is not filtered or converted to JSON by the server. Each commit
carries a CAR file of the changed repository blocks, which `AtProtoFirehose` validates and decodes for you.
Expect considerably more traffic than a filtered Jetstream connection.

[!include[Untrusted data warning](includes/untrusted-data-warning.md)]

## Reading repository events

```csharp
using idunno.AtProto.Firehose;

await using var firehose = new AtProtoFirehose();

await foreach (FirehoseEvent evt in firehose.SubscribeReposAsync())
{
    switch (evt)
    {
        case FirehoseCommitEvent commit:
            foreach (FirehoseRepoOperation operation in commit.Operations)
            {
                Console.WriteLine($"{commit.Sequence}: {operation.Action} {commit.Repo}/{operation.Path}");

                if (operation.Action == FirehoseRepoAction.Create &&
                    operation.Collection == "app.bsky.feed.post")
                {
                    Console.WriteLine(operation.GetRecord());
                }
            }
            break;

        case FirehoseIdentityEvent identity:
            Console.WriteLine($"{identity.Did} identity changed");
            break;

        case FirehoseAccountEvent account:
            Console.WriteLine($"{account.Did} active: {account.Active} {account.Status}");
            break;

        case FirehoseInvalidEvent invalid:
            Console.WriteLine($"Ignored an invalid {invalid.Type} event: {invalid.Reason}");
            break;
    }
}
```

The default relay is `wss://bsky.network`. Set `FirehoseOptions.RelayUri` to read from another relay or a PDS.

The events are:

| Event | Meaning |
| ----- | ------- |
| `FirehoseCommitEvent` | A repository commit. `Operations` lists the created, updated and deleted records; `Blocks` holds the raw CAR. |
| `FirehoseSyncEvent` | The current state of a repository, sent when it must be resynchronised. |
| `FirehoseIdentityEvent` | An identity change. The optional `Handle` is unverified, with control and bidirectional formatting characters removed; resolve it before trusting it. |
| `FirehoseAccountEvent` | An account's hosting status changed. |
| `FirehoseLabelsEvent` | A batch of labels from `SubscribeLabelsAsync()`. |
| `FirehoseInfoEvent` | An informational message, such as `OutdatedCursor`. It has no sequence number. |
| `FirehoseUnknownEvent` | A message type the SDK does not recognise. The raw payload is available. |
| `FirehoseInvalidEvent` | A message that failed validation. It is not processed further; the stream continues. |

### Operations and records

Each `FirehoseRepoOperation` has an `Action`, a `Path`, its `Collection` and `RecordKey`, the new record `Cid`
and the previous record CID, `Prev`. Deletes have no `Cid` and no record. Creates have no `Prev`.

`GetRecord()` decodes the record's DAG-CBOR block to a `JsonElement` each time it is called, so only records you
are interested in are decoded. `RecordData` gives you the raw block. Records come from other users' repositories:
validate them before deserializing them as a particular type.

<a name="decodingRecords"></a>

### Decoding records to Bluesky types

The firehose carries every record written to every repository the relay serves, not just Bluesky records. Any
application can define its own lexicon and write records into a repository, so alongside `app.bsky.feed.post` and
`app.bsky.graph.follow` you will see collections such as `site.standard.publication` from
[standard.site](https://standard.site), `place.stream.livestream` from [Streamplace](https://stream.place),
`sh.tangled.repo` from [Tangled](https://tangled.org), and collections from lexicons which did not exist when your
application was written. Note that a collection is an [NSID](https://atproto.com/specs/nsid), which is written in
reverse domain name order, so records from `standard.site` appear as `site.standard.…` and records from
`stream.place` appear as `place.stream.…`. The AT Protocol firehose is a protocol level stream; Bluesky records
are only a subset of it.

Filter on the operation's `Collection` before you decode anything. Filtering first keeps you from decoding records
you have no type for, and, because `GetRecord()` decodes on every call, it avoids the cost of decoding the majority
of the stream you are going to discard:

```csharp
if (operation.Collection != CollectionNsid.Post)
{
    continue;
}
```

To turn a record into a Bluesky type, deserialize the `JsonElement` from `GetRecord()` with the type information
`BlueskyJsonSerializerOptions` publishes. Use the `JsonTypeInfo<T>` overload of `Deserialize`, not the
reflection based overloads, so that the code stays trimming and native AOT safe. Resolve the options and the type
information once and cache them: `BlueskyJsonSerializerOptions.Options` builds a new `JsonSerializerOptions` every
time it is read, and each new instance starts with an empty metadata cache.

This needs a reference to the `idunno.Bluesky` package. `idunno.AtProto`, which the firehose lives in, has no
knowledge of Bluesky's lexicons.

```csharp
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto.Firehose;
using idunno.Bluesky;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Record;

private static readonly JsonSerializerOptions s_blueskyOptions = BlueskyJsonSerializerOptions.Options;

private static readonly JsonTypeInfo<BlueskyRecord> s_blueskyRecordTypeInfo =
    (JsonTypeInfo<BlueskyRecord>)s_blueskyOptions.GetTypeInfo(typeof(BlueskyRecord));

// …

foreach (FirehoseRepoOperation operation in commit.Operations)
{
    if (operation.Collection != CollectionNsid.Post &&
        operation.Collection != CollectionNsid.Like)
    {
        continue;
    }

    try
    {
        if (operation.GetRecord() is JsonElement json &&
            json.Deserialize(s_blueskyRecordTypeInfo) is BlueskyRecord record)
        {
            switch (record)
            {
                case Post post when operation.Collection == CollectionNsid.Post:
                    Console.WriteLine($"{commit.Repo} posted {post.Text}");
                    break;

                case Like like when operation.Collection == CollectionNsid.Like:
                    Console.WriteLine($"{commit.Repo} liked {like.Subject.Uri}");
                    break;

                default:
                    Console.WriteLine($"{operation.Path} is in {operation.Collection} but its $type dispatched as {record.GetType().Name}");
                    break;
            }
        }
    }
    catch (InvalidDataException ex)
    {
        Console.WriteLine($"{operation.Path} is not valid DAG-CBOR: {ex.Message}");
    }
    catch (JsonException ex)
    {
        Console.WriteLine($"{operation.Path} is not the record it claims to be: {ex.Message}");
    }
    catch (ArgumentException ex)
    {
        Console.WriteLine($"{operation.Path} deserialized but failed the record's own validation: {ex.Message}");
    }
}
```

`BlueskyRecord` deserializes polymorphically on the record's `$type`, not on `operation.Collection`. Filtering on
`Collection` only limits which paths you attempt to decode; it does not confirm that a record's content matches the
collection it was written to. A repository is free to write a structurally valid `app.bsky.feed.like` payload under
an `app.bsky.feed.post` path, and `Deserialize` will happily produce a `Like` for it. The `when` clauses above check
that the two agree, so a mismatched record falls to the `default` arm instead of being reported as the wrong kind of
operation. A record whose `$type` is not one Bluesky declares deserializes to the nearest type the SDK does know,
which for an unrecognised collection is `BlueskyRecord` itself; that also reaches `default`. This is also why
filtering matters: without it, most of the stream falls through to `BlueskyRecord` and the properties you want are
not there.

All three `catch` blocks are needed. Record contents are written by the repository's owner and are not validated by
the relay, so a record can be malformed DAG-CBOR, or can carry a `$type` which does not match the shape of its data.
A record can also have a `$type` that matches its declared shape but still fail that record's own invariants, for
example an `app.bsky.feed.post` with no text and no embed: `Post`'s constructor rejects that combination directly
with `ArgumentNullException`, and an oversized `text` with `ArgumentOutOfRangeException`. `System.Text.Json` does not
wrap exceptions thrown by a record's own `[JsonConstructor]` as `JsonException`, so catch `ArgumentException`
separately to handle them. None of the three should end the subscription.

Deletes carry no record: `GetRecord()` returns `null` for them, which the `is JsonElement` pattern handles.

## Reading labels

```csharp
await using var firehose = new AtProtoFirehose();

await foreach (FirehoseEvent evt in firehose.SubscribeLabelsAsync())
{
    if (evt is FirehoseLabelsEvent labels)
    {
        foreach (Label label in labels.Labels)
        {
            Console.WriteLine($"{label.Source} labelled {label.Uri} {label.Value}");
        }
    }
}
```

The default labeler is `wss://mod.bsky.app`, the Bluesky moderation service. Set `FirehoseOptions.LabelerUri`
to read from another labeler.

## Samples

* [Samples.Firehose](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.Firehose) reads the relay and
  prints each kind of event.
* [Samples.ModerationLabels](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.ModerationLabels)
  prints the labels the Bluesky moderation service applies and negates.

## Cursors and resuming

Every sequenced event has a `Sequence`. Pass a sequence as `cursor` to resume after it:

```csharp
long? cursor = LoadCheckpoint();

await foreach (FirehoseEvent evt in firehose.SubscribeReposAsync(cursor))
{
    Process(evt);

    if (evt.Sequence is long sequence)
    {
        SaveCheckpoint(sequence);
    }
}
```

A `null` cursor starts at the live position, and `0` starts at the oldest event the server retains. If the cursor
is older than the server's retention window, the stream starts with a `FirehoseInfoEvent` named
`FirehoseInfoEvent.OutdatedCursor` and continues from the oldest retained event. A cursor ahead of the server
causes the server to return a `FutureCursor` error, thrown as a `FirehoseConnectionException`.

Sequence numbers are only meaningful for the host and endpoint that issued them. `LastRepoSequence` and
`LastLabelSequence` report the last sequence yielded for each endpoint.

## Ordering

Events are yielded one at a time, in the order the server sent them, and each sequence number must be greater
than the one before it. A duplicate or out-of-order sequence number ends the stream with an
`InvalidDataException` rather than being skipped.

There are two exceptions, both of which are dropped rather than yielded twice. When a connection resumes from a
cursor, a relay sends the cursor event again, so the first event on a resumed connection may repeat the last
sequence yielded. A relay also sends the last event it replays a second time, unchanged, when it switches from
replaying to live events, so an event that repeats the previous event's sequence number, with the same message
type and a byte-for-byte identical payload, is dropped once on each connection opened with a cursor, including a
cursor of 0. A second repeat, a repeat on a connection opened without a cursor, or a repeated sequence number with a
different type or payload, is still an error.

Order the stream as a whole by `Sequence`, not by `Time`, which is the server's timestamp and is not guaranteed
to increase.

The reader does **not** check the order of commits within a repository. It does not verify that a commit's `Rev`
is greater than the previous commit's for the same repository, that `Since` matches the previous commit's `Rev`,
or that `PrevData` matches the previous commit's `Data`. A buggy or malicious relay can send a repository's commits
out of order, replay an old one, or skip one, and the reader will yield them. If your application depends on
per-repository order, track the last `Rev` for each repository yourself, and treat a `FirehoseSyncEvent` as
resetting that repository's state.

> [!WARNING]
> Per-repository tracking is expensive at the volume the firehose emits. The relay carries events for every
> repository on the network, so the state grows with every repository you see. Bound it, for example with an
> expiring cache or by filtering to the repositories you care about, and expect a cold cache after a restart or
> eviction to accept a commit it cannot check.

## Reconnection and errors

The reader reconnects, resuming from the last sequence yielded, when the connection drops, when no frame arrives
within `FirehoseOptions.IdleTimeout` (5 minutes by default), when the server reports `ConsumerTooSlow`, and when
the server returns HTTP 408, 429, 500, 502, 503 or 504. It waits between attempts with a jittered exponential
backoff of 1 to 60 seconds, honouring `Retry-After` up to 10 minutes. The attempt count resets once an event with a
sequence number is delivered, so a server which sends only `#info` or unknown frames before dropping the connection
cannot keep the reader reconnecting forever. `maximumReconnectAttempts` defaults to 10; pass `null` to retry forever or `0` to never retry.
When the attempts are exhausted, an `IOException` is thrown.

A `FirehoseConnectionException` is thrown when the server refuses the connection with any other HTTP status, or
sends any other error, such as `FutureCursor`. `StatusCode`, `ErrorDetail` and `RetryAfter` describe the failure.
Server-supplied text, the error and message of a `FirehoseConnectionException`, and the name and message of a
`FirehoseInfoEvent`, is only lightly sanitized: control characters and bidirectional formatting characters, such
as right-to-left overrides, are removed and the text is truncated.

> [!WARNING]
> Sanitized server text is still untrusted input. It is not encoded for HTML, SQL, a shell or any other context, and
> can contain characters that look like others. Encode it for where you use it, and do not make security decisions
> based on it.

Only one subscription per endpoint may be enumerated at a time on an `AtProtoFirehose` instance.

## Protecting against malicious servers

A firehose server may be anyone's. `AtProtoFirehose` enforces these limits:

* Frames larger than `FirehoseOptions.MaxMessageSize` (5 MiB by default) are rejected while they are received.
* Commit CARs are limited to 2,000,000 bytes, sync CARs to 10,000 bytes and commits to 200 operations, as the
  lexicon specifies. CARs are limited to `MaximumCarBlocks` blocks of at most `MaximumCarBlockSize` bytes, label
  batches to `MaximumLabelsPerMessage` labels, and the deprecated commit `blobs` list to 200 entries, because the
  lexicon supplies no limit.
* CBOR must be valid DAG-CBOR, nested at most 128 levels deep. Nesting is checked as the frame is read, so a deeply nested frame is rejected before it can use more memory than its own size.
* Only the fields the reader understands are kept from each map; unknown fields are skipped without allocating their
  names, and no map or array is sized from the length the server declares. Labels whose signatures are checked keep
  every field, as the signature covers them all, so a label with more than 64 fields is yielded as FirehoseInvalidEvent.
* Every CAR block's CID is recomputed, the commit must be the CAR's first root, the commit's DID and revision must
  match the event, and each operation's record block must be present. The number of CAR roots a header declares is
  not trusted to size any allocation.
* Timestamps must be AT Protocol datetimes: RFC 3339 and ISO 8601 with an upper-case `T`, whole seconds, and a `Z` or
  `±hh:mm` timezone other than `-00:00`. An event with any other timestamp format is yielded as `FirehoseInvalidEvent`.
* Sequence numbers must be between 1 and 2^53 - 1 and strictly increasing, apart from the cursor event and a single identical repeat, of the same type and payload, a relay sends on each connection opened with a cursor.
* Redirects are not followed. A server that redirects the connection ends the stream with a
  `FirehoseConnectionException`, and the redirect is not retried.

Frames that cannot be parsed, text frames, and sequence violations end the stream with an `InvalidDataException`,
because the reader can no longer trust the connection. Messages that parse but fail content validation are yielded
as `FirehoseInvalidEvent` and the stream continues. Unknown message types and operations are skipped or yielded as
`FirehoseUnknownEvent`, as the specification requires. An unknown message type whose payload has a valid `seq` is
sequenced like any other event: it must be in order, sets `Sequence`, and advances the resume cursor. One without a valid
`seq` is yielded unsequenced.

### Signatures

Set `FirehoseOptions.VerifySignatures` to verify each commit's signature against the repository's `#atproto` key
and each label's signature against the labeler's `#atproto_label` key. Events that fail verification are yielded as
`FirehoseInvalidEvent`. Verification needs the signing key of every repository and labeler, taken from its DID
document, which is expensive at firehose volume, so it is off by default. Supply `FirehoseOptions.DidDocumentResolver`
to use your own resolver.

A labeler normally signs every label with its own key, so a labels message has one source. Each distinct source
must be resolved before the message is delivered, so a message whose labels claim more than
`MaximumLabelSourcesPerMessage` distinct sources, 10 by default, is yielded as `FirehoseInvalidEvent` without any of
them being resolved. This stops a labeler stalling the stream, or making the reader request DID documents from hosts
of its choosing, by listing many sources in one message.

#### Signing key cache

When verification is on, resolved signing keys are cached by default, so a DID document is resolved once per
repository or labeler rather than once per event.

* Keys are cached for `SigningKeyCacheDuration`, one hour by default. At most `SigningKeyCacheSize` keys are held,
  100,000 by default, so a relay cannot grow the cache without limit by sending events from millions of DIDs.
* A DID that cannot be resolved, or that has no usable key, is also cached, for one minute, so it is not resolved
  again for every event it sends. Its events are yielded as `FirehoseInvalidEvent` during that minute, even if the
  failure was transient.
* An `#identity` event removes the cached keys for its DID, so a key rotation that is announced takes effect on
  the next event.
* If a signature fails against a cached key, the key is resolved again in case it was rotated without an
  announcement, and the event is verified against the new key. This happens at most once every five minutes per
  DID, so a stream of bad signatures cannot become a stream of DID resolutions. If that resolution fails, the
  cached key is kept until it expires, and the attempt still counts towards the five minute limit.

Set `CacheSigningKeys` to `false` to turn the cache off, for example when your `DidDocumentResolver` does its own
caching. Without a cache every commit, sync event and labels message resolves a DID document.

> [!WARNING]
> Keys are resolved inline, one at a time, as each event is decoded, and each resolution of a DID that is not cached
> holds up the stream. This is not only a startup cost. The full relay carries events from far more repositories than
> the reader can resolve, so misses continue for as long as it runs. Against `bsky.network`, with the default resolver,
> a reader verifying signatures processed about 19 events a second while the relay sent about 370, and fell further
> behind every second until the relay would disconnect it with `ConsumerTooSlow`. Verification suits labelers, a
> single PDS, or other low-volume streams. It is not suitable for the full relay. Watch the
> `total.signing_key_cache_misses` metric to see how often keys are resolved.

> [!WARNING]
> A malicious server sending malicious data to the firehose can defeat the signing key cache. It can send events from
> more distinct DIDs than `SigningKeyCacheSize`, so keys are evicted before they are used again, or send an `#identity`
> event before each event for a DID, so that DID's cached keys are removed every time. Either way most events need a DID
> document resolution, which delays the stream and, with the default resolver, sends a request to `plc.directory` or,
> for `did:web` DIDs, to a host the server chose. This only applies when `VerifySignatures` is on, which is not the
> default. When verification is on, the cache is on by default. A high ratio of `total.signing_key_cache_misses` to
> `total.signing_key_cache_hits` can indicate a server doing this.

Signature verification does not verify the repository's Merkle Search Tree, so it does not prove that the
operations listed match the signed tree.

## Performance

These figures come from reading `wss://bsky.network` with the default options in September 2026, on .NET 11 on
Windows, in a Release build. The PC specifications were an AMD Ryzen AI MAX+ 395, 3000Mhz, 16 core, 128Gb RAM.
The test consumer counted events and read each operation's record with `GetRecord()`.
The results depend on the relay's volume and on your hardware and network, so treat them as a guide and measure your
own workload with the [metrics](metrics.md) the firehose publishes.

### Without signature verification

The reader kept up with the relay comfortably. Its speed was limited by the network, not the CPU.

| Measure | Result |
| --- | --- |
| Events | About 340 to 410 a second, which is everything the relay sent |
| CPU | About 3% to 6% of one core, about 130 µs of CPU time per event |
| Lag behind the event's `Time` | About 300 to 400 ms at the median, and not growing |
| Working set | Flat at 73 to 79 MB over a 10-minute run |
| Large object heap | Up to about 4 MB |
| Allocation per event | About 64 KB, or about 57 KB without reading records |
| GC | About 0.05% of elapsed time spent paused; 160 gen 0, 9 gen 1 and 1 gen 2 collections in 90 seconds |

The allocation for each event is about 13 times the size of the average commit's CAR file, which was about 5 KB,
with the largest about 43 KB. About a third of it comes from receiving the WebSocket message, which allocates a
new receive buffer and copies the reassembled message for every message. The rest comes from copying CAR blocks,
creating a `CborReader` for each object decoded, and the records decoded from each commit. Almost all of it is
short-lived, so it is collected in gen 0. At about 25 MB a second, collection took a negligible share of the time,
and memory did not grow.

When catching up from a cursor 5,000 events behind the live position, the reader processed about 890 events a
second, using about 20% of one core, for the first 10 seconds, until it caught up.

### With signature verification

With `VerifySignatures` on, the reader could not keep up with the relay.

| Measure | Result |
| --- | --- |
| Events | About 19 a second, while the relay sent about 370 |
| CPU | About 3% of one core |
| Lag behind the event's `Time` | Grew by about 1 second every second, to 165 seconds after 3 minutes |
| Working set | 77 to 89 MB |
| Allocation per event | About 80 KB |
| Signing key cache | 2,598 misses and 766 hits in 3 minutes, a hit rate of about 23% |

Each cache miss resolves a DID document inline, which took about 70 ms, and the relay carries events from more
repositories than the cache can warm up to at that rate. The reader spent its time waiting for resolutions, not
checking signatures, so CPU use stayed low. A reader that falls this far behind is eventually disconnected by the
relay with `ConsumerTooSlow`. Use verification for labelers, a single PDS, or other low-volume streams, not the
full relay.

## Configuration

Pass `FirehoseOptions` to the constructor to configure logging (`LoggerFactory`), metrics (`MeterFactory`), hosts,
limits and timeouts, and `WebSocketOptions` to configure the WebSocket's keep-alive. By default,
`AtProtoFirehose` uses an HTTP client that refuses connections to private and loopback addresses and does not
follow redirects.

To connect through a proxy, pass `HttpClientOptions` with a `ProxyUri`, which keeps those protections, or configure the
proxy on the handler of an `HttpClient` you supply. `WebSocketOptions.Proxy` cannot be used, because the firehose connects
through an HTTP client, and the constructor throws an `ArgumentException` if it is set.

### Supplying your own HTTP client

You can supply an `IHttpClientFactory`, or an `HttpClient`, to connect through instead. The firehose does not dispose
an `HttpClient` you supply.

> [!WARNING]
> A client you supply is used as it is, so it has none of the SSRF protections the firehose applies to its own
> client, unless it comes from an `IHttpClientFactory` configured with `AddAtProtoHttpClient()`. Without those
> protections the firehose can be made to connect to loopback, link-local or private network addresses, such as a
> cloud metadata service, through the relay or labeler URI, a host name that resolves to one of them, or a redirect.
>
> Disable automatic redirects on your client's handler (for example, `SocketsHttpHandler.AllowAutoRedirect = false`).
> An `HttpClient` created without a handler follows redirects. The firehose refuses a connection that was redirected
> and ends the stream with a `FirehoseConnectionException`, but a client that follows redirects has already sent the
> request to the redirected host before the firehose can see it.

```c#
using HttpClient httpClient = new(new SocketsHttpHandler { AllowAutoRedirect = false });
await using AtProtoFirehose firehose = new(httpClient);
```

Metrics are published under the `idunno.AtProto.Firehose` meter. See [metrics](metrics.md).
