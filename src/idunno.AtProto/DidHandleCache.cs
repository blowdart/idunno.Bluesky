// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.AtProto;

/// <summary>
/// Resolves and caches the verified <see cref="Handle"/> for a <see cref="Did"/>.
/// </summary>
/// <remarks>
/// <para>A handle is resolved with <see cref="IdentityResolution.ResolveVerifiedHandleAsync(Did, Uri?, ILoggerFactory?, HttpClient?, TimeSpan?, int, int, CancellationToken)"/>,
/// so it is only returned once the handle the DID document declares has been resolved back to the same DID. Any failure to resolve or verify
/// a handle results in <see cref="Handle.Invalid"/>, which is cached for <see cref="DidHandleCacheOptions.FailedResolutionDuration"/>.</para>
/// <para>Concurrent lookups for the same DID share a single resolution. Cancelling a lookup stops the caller waiting, but does not cancel the
/// shared resolution, which is bounded by <see cref="DidHandleCacheOptions.ResolutionTimeout"/> and cancelled when the cache is disposed.</para>
/// <para>Set <see cref="Firehose.FirehoseOptions.DidHandleResolver"/> or <see cref="Jetstream.JetstreamOptions.DidHandleResolver"/> to
/// have a stream call <see cref="Invalidate(Did)"/> for each <c>#identity</c> event it receives. A handle being resolved when its DID is
/// invalidated is returned to the callers already waiting for it, but is not cached.</para>
/// <para>The cache only helps against well behaved sources of DIDs. A malicious source can defeat it, forcing a resolution for most lookups,
/// either by sending more distinct DIDs than <see cref="DidHandleCacheOptions.Size"/> so handles are evicted before they are reused, or by
/// sending an <c>#identity</c> event before each event for a DID so its handle is invalidated each time. Each resolution, for the default
/// resolver, makes a request to <c>plc.directory</c> or, for <c>did:web</c>, to a host the DID names, as well as to the host the handle names.</para>
/// </remarks>
public sealed class DidHandleCache : IDidHandleResolver, IDisposable
{
    // Invalidation generations, striped by DID so they take bounded memory. A resolution which began before its DID's stripe was
    // invalidated is not cached, so a handle resolved before an #identity event cannot be cached after it. Unrelated DIDs sharing a
    // stripe only cost an extra resolution.
    private const int GenerationStripes = 256;

    private readonly long[] _generations = new long[GenerationStripes];

#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    private readonly Dictionary<Did, InFlightLookup> _inFlight = [];
    private readonly Func<Did, CancellationToken, Task<Handle>> _resolver;
    private readonly DidHandleCacheOptions _options;
    private readonly MemoryCache _cache;
    private readonly DidHandleCacheMetrics _metrics;
    private readonly ILogger<DidHandleCache> _logger;
    private readonly CancellationTokenSource _disposalTokenSource = new();
    private readonly CancellationToken _disposalToken;

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DidHandleCache"/> class with the default options.
    /// </summary>
    public DidHandleCache() : this(options: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DidHandleCache"/> class.
    /// </summary>
    /// <param name="options">The options configuring the cache, or <see langword="null"/> to use the default options.</param>
    public DidHandleCache(DidHandleCacheOptions? options) : this(resolver: null, options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DidHandleCache"/> class with a custom resolver.
    /// </summary>
    /// <param name="resolver">The function used to resolve a verified handle, or <see langword="null"/> to use <see cref="IdentityResolution.ResolveVerifiedHandleAsync(Did, Uri?, ILoggerFactory?, HttpClient?, TimeSpan?, int, int, CancellationToken)"/>.</param>
    /// <param name="options">The options configuring the cache, or <see langword="null"/> to use the default options.</param>
    internal DidHandleCache(Func<Did, CancellationToken, Task<Handle>>? resolver, DidHandleCacheOptions? options)
    {
        _options = options ?? new DidHandleCacheOptions();
        _resolver = resolver ?? DefaultResolverAsync;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = _options.Size, Clock = new TimeProviderSystemClock(_options.TimeProvider) });
        _metrics = new DidHandleCacheMetrics(_options.MeterFactory);
        _logger = (_options.LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<DidHandleCache>();
        _disposalToken = _disposalTokenSource.Token;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The cache has been disposed.</exception>
    /// <remarks>
    /// <para>If the cache is disposed whilst a resolution is in progress the returned task is cancelled.</para>
    /// </remarks>
    public ValueTask<Handle> ResolveHandleAsync(Did did, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(did);
        cancellationToken.ThrowIfCancellationRequested();

        InFlightLookup? lookup;
        bool owner = false;

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (TryGetLiveEntry(did, out Handle? cachedHandle))
            {
                _metrics.Hits.Add(1);
                return ValueTask.FromResult(cachedHandle);
            }

            if (_inFlight.TryGetValue(did, out lookup))
            {
                _metrics.CoalescedLookups.Add(1);
            }
            else
            {
                _metrics.Misses.Add(1);

                lookup = new InFlightLookup(_generations[Stripe(did)]);
                owner = true;

                // Bounded so a flood of distinct DIDs cannot grow the in-flight table without limit. A lookup which is not
                // registered still resolves and is cached, it just cannot be shared.
                if (_inFlight.Count < _options.Size)
                {
                    _inFlight.Add(did, lookup);
                }
            }
        }

        if (owner)
        {
            _ = RunLookupAsync(did, lookup);
        }

        return new ValueTask<Handle>(lookup.Completion.Task.WaitAsync(cancellationToken));
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The cache has been disposed.</exception>
    public bool TryGetCachedHandle(Did did, [NotNullWhen(true)] out Handle? handle)
    {
        ArgumentNullException.ThrowIfNull(did);

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            return TryGetLiveEntry(did, out handle);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>Callers already waiting for a resolution of <paramref name="did"/> receive its result, but the result is not cached and
    /// later lookups start a new resolution.</para>
    /// <para>Invalidation is not rate limited. A source which invalidates <paramref name="did"/> before each lookup forces a resolution for each lookup.</para>
    /// <para>Invalidating after the cache has been disposed does nothing.</para>
    /// </remarks>
    public void Invalidate(Did did)
    {
        ArgumentNullException.ThrowIfNull(did);

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _generations[Stripe(did)]++;
            _cache.Remove(did.Value);
            _inFlight.Remove(did);
        }

        _metrics.Invalidations.Add(1);
    }

    /// <summary>
    /// Releases the resources used by the cache and cancels any resolutions in progress.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _disposalTokenSource.Cancel();
        _disposalTokenSource.Dispose();
        _cache.Dispose();
    }

    private static int Stripe(Did did) => (int)((uint)did.GetHashCode() % GenerationStripes);

    private Task<Handle> DefaultResolverAsync(Did did, CancellationToken cancellationToken) =>
        IdentityResolution.ResolveVerifiedHandleAsync(
            did: did,
            plcDirectory: _options.PlcDirectory,
            loggerFactory: _options.LoggerFactory,
            httpClient: _options.HttpClient,
            timeout: _options.ResolutionTimeout,
            cancellationToken: cancellationToken);

    // Must be called whilst holding _lock, and only when the cache has not been disposed.
    // The cache is keyed by the DID's string value, because Did converts implicitly to a string and MemoryCache has string keyed
    // overloads, so keying by the Did itself can store an entry under one key and look it up under another.
    private bool TryGetLiveEntry(Did did, [NotNullWhen(true)] out Handle? handle)
    {
        if (_cache.TryGetValue(did.Value, out CachedHandle? entry) && entry is not null)
        {
            if (entry.ExpiresAt > _options.TimeProvider.GetUtcNow())
            {
                handle = entry.Handle;
                return true;
            }

            _cache.Remove(did.Value);
        }

        handle = null;
        return false;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any failure to resolve a handle is reported as Handle.Invalid.")]
    private async Task RunLookupAsync(Did did, InFlightLookup lookup)
    {
        Handle? handle;

        try
        {
            using CancellationTokenSource timeoutTokenSource = new(_options.ResolutionTimeout, _options.TimeProvider);
            using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_disposalToken, timeoutTokenSource.Token);

            try
            {
                handle = await _resolver(did, linkedTokenSource.Token).ConfigureAwait(false) ?? Handle.Invalid;
            }
            catch (OperationCanceledException) when (!_disposalToken.IsCancellationRequested && timeoutTokenSource.IsCancellationRequested)
            {
                DidHandleCacheLogger.ResolutionTimedOut(_logger, did);
                handle = Handle.Invalid;
            }
        }
        catch (OperationCanceledException) when (_disposalToken.IsCancellationRequested)
        {
            handle = null;
        }
        catch (Exception exception)
        {
            DidHandleCacheLogger.ResolutionFailed(_logger, exception, did);
            handle = Handle.Invalid;
        }

        if (handle is not null && !handle.IsValid)
        {
            DidHandleCacheLogger.HandleNotVerified(_logger, did);
            _metrics.InvalidHandles.Add(1);
            handle = Handle.Invalid;
        }

        lock (_lock)
        {
            if (_inFlight.TryGetValue(did, out InFlightLookup? current) && ReferenceEquals(current, lookup))
            {
                _inFlight.Remove(did);
            }

            // The handle is still returned to the callers waiting for it, but is not cached if the DID was invalidated whilst it was resolved.
            if (handle is not null && !_disposed && _generations[Stripe(did)] == lookup.Generation)
            {
                TimeSpan duration = handle.IsValid || _options.Duration < _options.FailedResolutionDuration
                    ? _options.Duration
                    : _options.FailedResolutionDuration;

                _cache.Set(did.Value, new CachedHandle(handle, _options.TimeProvider.GetUtcNow() + duration), new MemoryCacheEntryOptions
                {
                    Size = 1,
                    AbsoluteExpirationRelativeToNow = duration
                });
            }
        }

        if (handle is null)
        {
            lookup.Completion.TrySetCanceled(_disposalToken);
        }
        else
        {
            lookup.Completion.TrySetResult(handle);
        }
    }

    private sealed class InFlightLookup(long generation)
    {
        public long Generation { get; } = generation;

        public TaskCompletionSource<Handle> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed record CachedHandle(Handle Handle, DateTimeOffset ExpiresAt);
}
