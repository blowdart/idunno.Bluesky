# Resolving and caching handles for DIDs

Events from the [firehose](firehose.md) and [Jetstream](jetstream.md), labels, and many other AT Protocol records identify
accounts by their [DID](commonTerms.md). To display a handle you need to resolve the DID, and resolving a DID for every event
is too slow, and too expensive for the directory and hosts you query, to keep up with a busy stream.

`DidHandleCache`, in `idunno.AtProto`, resolves the handle for a DID, verifies it, and caches the result.

```csharp
using var didHandleCache = new DidHandleCache();

Handle handle = await didHandleCache.ResolveHandleAsync(did, cancellationToken);

Console.WriteLine($"{did} is {handle}");
```

## Verification and `handle.invalid`

A DID document's `alsoKnownAs` entry is only a claim; anyone can put any handle in their DID document. Following the
[AT Protocol identity specification](https://atproto.com/specs/handle#handle-resolution) the cache resolves the handle a DID
document claims, and only returns it if that handle resolves back to the same DID.

If the DID cannot be resolved, the handle cannot be resolved, the handle resolves to a different DID, or the resolution fails
or times out, the cache returns `Handle.Invalid`, which displays as `handle.invalid`. It never throws for a failed resolution,
so you can check the result with `Handle.IsValid`:

```csharp
Handle handle = await didHandleCache.ResolveHandleAsync(did, cancellationToken);

if (handle.IsValid)
{
    Console.WriteLine($"{did} is @{handle}");
}
else
{
    Console.WriteLine($"{did} does not have a verified handle");
}
```

Failures are logged at the `Debug` level, see [Configuring logging](logging.md).

## Looking up handles

`DidHandleCache` implements `IDidHandleResolver`, which has two ways to get a handle:

* `ResolveHandleAsync()` returns the cached handle, or resolves, verifies and caches it if it is not cached.
* `TryGetCachedHandle()` returns `true` and the handle only if it is already cached, and never makes a network request.

On a busy stream, such as the full relay firehose, resolving every DID you see will fall behind. A common pattern is to call
`ResolveHandleAsync()` for less frequent events, such as account events, and `TryGetCachedHandle()` for frequent ones, such as
commits, showing just the DID until a handle has been cached:

```csharp
string Describe(Did did) =>
    didHandleCache.TryGetCachedHandle(did, out Handle? handle) ? $"{did} ({handle})" : did;
```

### Concurrent lookups and cancellation

Concurrent calls to `ResolveHandleAsync()` for the same DID share a single resolution. Cancelling the token passed to
`ResolveHandleAsync()` stops that caller waiting, but does not cancel the shared resolution, which continues for any other
callers and is cached when it completes. Each resolution is bounded by `DidHandleCacheOptions.ResolutionTimeout`, and
disposing the cache cancels any resolutions in progress.

## Keeping handles up to date

When an account changes its handle the relay sends an `#identity` event. Pass the cache to the firehose or Jetstream and it
calls `Invalidate()` for the DID in each identity event it receives, so the next lookup resolves the new handle:

```csharp
using var didHandleCache = new DidHandleCache();

await using var firehose = new AtProtoFirehose(
    options: new FirehoseOptions
    {
        DidHandleResolver = didHandleCache
    });
```

```csharp
using var didHandleCache = new DidHandleCache();

await using var jetstream = new AtProtoJetstream(
    options: new JetstreamOptions
    {
        DidHandleResolver = didHandleCache
    });
```

If you use `AtProtoJetstreamBuilder`, call `WithDidHandleResolver()`.

The handle in an identity event is not verified, so it is not cached. If a handle is being resolved when its DID is invalidated,
callers already waiting for it receive the result, but it is not cached, so a handle resolved before an identity event is never
cached after it.

If you read identity events from another source, call `Invalidate()` yourself.

Without invalidation, a changed handle is reported until its cache entry expires.

## Configuration

Pass a `DidHandleCacheOptions` to the constructor to configure the cache:

```csharp
using var didHandleCache = new DidHandleCache(new DidHandleCacheOptions
{
    Size = 10_000,
    Duration = TimeSpan.FromMinutes(30),
    LoggerFactory = loggerFactory
});
```

| Option | Default | Description |
| --- | --- | --- |
| `Size` | 100,000 | The maximum number of handles cached. It also bounds the number of resolutions which can be shared between concurrent callers. |
| `Duration` | 1 hour | How long a verified handle is cached. |
| `FailedResolutionDuration` | 1 minute | How long `handle.invalid` is cached after a failure, so a DID which cannot be resolved is not resolved again for every lookup. It is never cached for longer than `Duration`. |
| `ResolutionTimeout` | 30 seconds | How long a single resolution can take before it is abandoned and treated as a failure. |
| `PlcDirectory` | `https://plc.directory` | The PLC directory used to resolve `did:plc` DIDs. |
| `HttpClient` | `null` | The `HttpClient` used for resolution. If `null` a client is created for each resolution. The cache does not dispose a client you supply. |
| `LoggerFactory` | `null` | The `ILoggerFactory` used to create loggers. |
| `MeterFactory` | `null` | The `IMeterFactory` used to create the cache's meter. |
| `TimeProvider` | `TimeProvider.System` | The `TimeProvider` used to expire entries and time out resolutions. Supply a fake time provider in tests. |

When the cache is full a new handle is not cached until the cache has been compacted, which happens in the background.

## Untrusted sources of DIDs

The cache helps with well behaved sources of DIDs, but a malicious source can defeat it. A server which sends more distinct
DIDs than `Size` evicts handles before they are reused, and one which sends an identity event before each event for a DID
invalidates its handle every time. Either forces a resolution for most lookups. Each resolution makes requests to the PLC
directory, or for `did:web` to a host the DID names, as well as to the host the handle names.

If you read from a source you do not control, consider using `TryGetCachedHandle()` for most events, choosing a `Size` that
fits the number of accounts you expect to see, and watching the cache's [metrics](#metrics).

## Dependency injection and testing

Depend on `IDidHandleResolver` rather than `DidHandleCache` so you can substitute your own implementation, for example a test
double, or a resolver backed by a distributed cache.

```csharp
services.AddSingleton<IDidHandleResolver>(serviceProvider => new DidHandleCache(new DidHandleCacheOptions
{
    LoggerFactory = serviceProvider.GetService<ILoggerFactory>(),
    MeterFactory = serviceProvider.GetService<IMeterFactory>()
}));
```

A `DidHandleCache` is thread safe and is meant to be shared, so register it as a singleton. The container disposes it when
the container is disposed. After disposal `ResolveHandleAsync()` and `TryGetCachedHandle()` throw `ObjectDisposedException`,
and `Invalidate()` does nothing.

## Metrics

The cache reports hits, misses, shared lookups, invalid handles and invalidations through the `idunno.AtProto.DidHandleCache`
meter. Register it with `AddAtProtoDidHandleCacheMetrics()`. See [Built-in metrics](metrics.md#idunnoatprotodidhandlecache)
for the full list.

## Samples

* [Samples.Firehose](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.Firehose) and
  [Samples.Jetstream](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.Jetstream) use a `DidHandleCache`
  to show handles alongside DIDs.
