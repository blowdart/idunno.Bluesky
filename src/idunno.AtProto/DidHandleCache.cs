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
/// <para>Concurrent lookups for the same DID share a single resolution. At most <see cref="DidHandleCacheOptions.MaximumConcurrentResolutions"/>
/// resolutions run at once, and other lookups wait for one to finish. At most <see cref="DidHandleCacheOptions.Size"/> lookups can be pending;
/// once that many are pending, a lookup for a DID which is not already being resolved returns <see cref="Handle.Invalid"/>, which is not cached.
/// Cancelling a lookup stops the caller waiting, but does not cancel the shared resolution, which is bounded by
/// <see cref="DidHandleCacheOptions.ResolutionTimeout"/>, including any time spent waiting to start, and cancelled when the cache is disposed.
/// A lookup which times out waiting to start returns <see cref="Handle.Invalid"/>, which is not cached.</para>
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
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    private readonly Dictionary<Did, InFlightLookup> _inFlight = [];
    // Not disposed, as resolutions still running when the cache is disposed release their slot as they finish, and releasing a disposed
    // semaphore throws. A SemaphoreSlim only holds unmanaged resources if its AvailableWaitHandle is used, which it never is.
    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "See comment.")]
    private readonly SemaphoreSlim _resolutionSlots;
    private readonly Func<Did, CancellationToken, Task<Handle>> _resolver;
    private readonly DidHandleCacheOptions _options;
    private readonly MemoryCache _cache;
    private readonly DidHandleCacheMetrics _metrics;
    private readonly ILogger<DidHandleCache> _logger;
    private readonly CancellationTokenSource _disposalTokenSource = new();
    private readonly CancellationToken _disposalToken;

    // Counts every lookup which has not completed, including those removed from _inFlight by an invalidation, so a source which
    // invalidates DIDs whilst they are resolved cannot grow the number of pending lookups without limit.
    private int _pendingLookups;

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
        _resolutionSlots = new SemaphoreSlim(_options.MaximumConcurrentResolutions, _options.MaximumConcurrentResolutions);
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = _options.Size, Clock = new TimeProviderSystemClock(_options.TimeProvider) });
        _metrics = new DidHandleCacheMetrics(_options.MeterFactory);
        _logger = (_options.LoggerFactory ?? NullLoggerFactory.Instance).CreateLogger<DidHandleCache>();
        _disposalToken = _disposalTokenSource.Token;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The cache has been disposed.</exception>
    /// <remarks>
    /// <para>If <see cref="DidHandleCacheOptions.Size"/> lookups are already pending, and <paramref name="did"/> is not one of them,
    /// <see cref="Handle.Invalid"/> is returned without being cached.</para>
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
            else if (_pendingLookups >= _options.Size)
            {
                // Bounded so a flood of distinct DIDs cannot grow the number of pending lookups without limit. The rejection says
                // nothing about the DID, so it is not cached and a later lookup tries again.
                _metrics.RejectedLookups.Add(1);
                return ValueTask.FromResult(Handle.Invalid);
            }
            else
            {
                _metrics.Misses.Add(1);

                // Every lookup is registered, so an invalidation can always find, and mark, a lookup in progress for its DID.
                lookup = new InFlightLookup();
                _inFlight.Add(did, lookup);
                _pendingLookups++;
                owner = true;
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

            _cache.Remove(did.Value);

            // A lookup in progress may have read the DID document before the invalidation, so its result is returned to the callers
            // already waiting for it but is never cached. Only the lookup for this DID is affected.
            if (_inFlight.Remove(did, out InFlightLookup? lookup))
            {
                lookup.Invalidated = true;
            }
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

    private Task<Handle> DefaultResolverAsync(Did did, CancellationToken cancellationToken) =>
        IdentityResolution.ResolveVerifiedHandleAsync(
            did: did,
            plcDirectory: _options.PlcDirectory,
            loggerFactory: _options.LoggerFactory,
            httpClient: _options.HttpClient,
            // ResolutionTimeout is enforced by a cancellation token driven by the configured TimeProvider. HttpClient.Timeout always
            // measures wall-clock time, so it is disabled rather than set to the same value.
            timeout: Timeout.InfiniteTimeSpan,
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

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A lookup must always complete, whatever fails.")]
    private async Task RunLookupAsync(Did did, InFlightLookup lookup)
    {
        Handle? handle = null;
        bool cacheable = false;

        try
        {
            (handle, cacheable) = await ResolveAsync(did).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Only reached if logging or metrics throw. The failure says nothing about the DID, so it is not cached.
            handle = _disposalToken.IsCancellationRequested ? null : Handle.Invalid;
            cacheable = false;
        }
        finally
        {
            // Always completed, otherwise every later lookup for the DID would wait for a lookup which never finishes.
            CompleteLookup(did, lookup, handle, cacheable);
        }
    }

    // Returns the handle to return, or null if the cache was disposed, and whether the handle may be cached.
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any failure to resolve a handle is reported as Handle.Invalid.")]
    private async Task<(Handle? Handle, bool Cacheable)> ResolveAsync(Did did)
    {
        using CancellationTokenSource timeoutTokenSource = new(_options.ResolutionTimeout, _options.TimeProvider);
        using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_disposalToken, timeoutTokenSource.Token);

        // Waiting for a resolution slot counts against the resolution timeout, so a flood of lookups cannot queue work indefinitely.
        try
        {
            await _resolutionSlots.WaitAsync(linkedTokenSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_disposalToken.IsCancellationRequested)
        {
            return (null, false);
        }
        catch (OperationCanceledException)
        {
            // The rejection says nothing about the DID, so it is not cached and a later lookup tries again.
            _metrics.RejectedLookups.Add(1);
            return (Handle.Invalid, false);
        }

        Handle handle;

        try
        {
            // WaitAsync bounds the wait even if the resolver ignores cancellation.
            handle = await StartResolution(did, linkedTokenSource.Token).WaitAsync(linkedTokenSource.Token).ConfigureAwait(false) ?? Handle.Invalid;
        }
        catch (OperationCanceledException) when (_disposalToken.IsCancellationRequested)
        {
            return (null, false);
        }
        catch (OperationCanceledException) when (timeoutTokenSource.IsCancellationRequested)
        {
            DidHandleCacheLogger.ResolutionTimedOut(_logger, did);
            handle = Handle.Invalid;
        }
        catch (Exception exception)
        {
            DidHandleCacheLogger.ResolutionFailed(_logger, exception, did);
            handle = Handle.Invalid;
        }
        if (!handle.IsValid)
        {
            DidHandleCacheLogger.HandleNotVerified(_logger, did);
            _metrics.InvalidHandles.Add(1);
            handle = Handle.Invalid;
        }

        return (handle, true);
    }

    // Starts the resolver, releasing the resolution slot the caller holds when the resolver finishes. The slot is held until then, rather
    // than until the cache stops waiting for it, so MaximumConcurrentResolutions bounds the resolutions actually running even when a
    // resolver is slow to observe cancellation.
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A resolver which throws must still release its slot.")]
    private Task<Handle> StartResolution(Did did, CancellationToken cancellationToken)
    {
        Task<Handle> resolution;

        try
        {
            resolution = _resolver(did, cancellationToken);
        }
        catch (Exception exception)
        {
            resolution = Task.FromException<Handle>(exception);
        }

        _ = resolution.ContinueWith(
            static (completed, state) =>
            {
                // Observed so a resolver which fails after the cache stopped waiting for it does not raise UnobservedTaskException.
                _ = completed.Exception;
                ((SemaphoreSlim)state!).Release();
            },
            _resolutionSlots,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return resolution;
    }

    // Must not throw, as it is called from a finally block to complete the lookup.
    private void CompleteLookup(Did did, InFlightLookup lookup, Handle? handle, bool cacheable)
    {
        lock (_lock)
        {
            if (_inFlight.TryGetValue(did, out InFlightLookup? current) && ReferenceEquals(current, lookup))
            {
                _inFlight.Remove(did);
            }

            _pendingLookups--;

            if (handle is not null && cacheable && !_disposed && !lookup.Invalidated)
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

    private sealed class InFlightLookup
    {
        public TaskCompletionSource<Handle> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Read and written whilst holding _lock.
        public bool Invalidated { get; set; }
    }

    private sealed record CachedHandle(Handle Handle, DateTimeOffset ExpiresAt);
}
