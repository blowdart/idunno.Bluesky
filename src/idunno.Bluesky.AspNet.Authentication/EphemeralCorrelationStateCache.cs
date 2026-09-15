// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;

using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication.Events;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Authentication;

internal sealed class EphemeralCorrelationStateCache : ICorrelationStateCache, IDisposable
{
    internal const int DefaultSizeLimit = 1024;

    // The proportion of the cache to discard when it is full, matching the MemoryCache default.
    private const double CompactionPercentage = 0.05d;

    private static volatile bool s_warned;
    private static volatile bool s_capacityWarned;

    private readonly MemoryCache _cache;
    private readonly int _sizeLimit;

    private bool _disposed;

#if NET9_0_OR_GREATER
    private static readonly Lock s_warnedLock = new ();
    private readonly Lock _takeLock = new ();
#else
    private static readonly object s_warnedLock = new();
    private readonly object _takeLock = new();
#endif

    private static readonly TimeSpan s_defaultSlidingExpiration = new(0, 0, 15, 0);

    [SuppressMessage("Major Code Smell", "S3010:Static fields should not be updated in constructors", Justification = "Used to ensure the emphermal warning is only logged once")]
    public EphemeralCorrelationStateCache(
        ILoggerFactory loggerFactory,
        TimeSpan? entryTimeToLive = null,
        int? sizeLimit = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sizeLimit ?? DefaultSizeLimit, 0);

        Logger = loggerFactory.CreateLogger<EphemeralCorrelationStateCache>();
        EntryTTL = entryTimeToLive ?? s_defaultSlidingExpiration;

        _sizeLimit = sizeLimit ?? DefaultSizeLimit;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = _sizeLimit });

        if (!s_warned)
        {
            lock (s_warnedLock)
            {
                if (!s_warned)
                {
                    s_warned = true;
                    Logger.UsingInMemoryCorrelationCacheWarning();
                }
            }
        }
    }

    private MemoryCache Cache => _cache;

    private TimeSpan EntryTTL { get; } = new(0, 15, 0);

    private ILogger<EphemeralCorrelationStateCache> Logger { get; set; }

    /// <inheritdoc/>
    public CorrelationStateCacheEvents Events { get; set; } = new CorrelationStateCacheEvents();

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    public async Task AddOAuthLoginState(Guid correlationId, OAuthLoginState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ObjectDisposedException.ThrowIf(_disposed, this);

        DateTime absoluteExpiration = DateTime.UtcNow.Add(EntryTTL);

        MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(absoluteExpiration)
            .SetSize(1);

        // Eviction for capacity fails a login which is still in flight, so it needs to be reported rather than
        // looking like a user who took too long over the authorization server.
        cacheOptions.RegisterPostEvictionCallback(OnStateEvicted, new EvictionCallbackState(Logger, _sizeLimit));

        CorrelationStateSettingContext context = new(state.ToJson());
        await Events.PreStoring(context).ConfigureAwait(false);

        string key = $"{correlationId}";

        Cache.Set(key, context.State, cacheOptions);

        // A short lived entry can reach the end of its life between being written and being read back, which leaves it
        // missing because it expired rather than because the cache had no room for it.
        if (Cache.TryGetValue(key, out _) || DateTime.UtcNow >= absoluteExpiration)
        {
            return;
        }

        // A size limited MemoryCache rejects an incoming entry when it is full rather than making room for it, only
        // scheduling a compaction to run later, so without this the state is silently dropped and the login fails at
        // the callback. Compact takes the proportion of the cache to remove and truncates the count it works out from
        // it, so asking for one and a half entries' worth guarantees at least one is removed however small it is.
        int count = Cache.Count;
        Cache.Compact(count > 0 ? Math.Max(CompactionPercentage, 1.5d / count) : CompactionPercentage);

        Cache.Set(key, context.State, cacheOptions);

        if (!Cache.TryGetValue(key, out _) && DateTime.UtcNow < absoluteExpiration)
        {
            throw new InvalidOperationException(
                $"The correlation state cache is full at its size limit of {_sizeLimit} and could not make room for the login state.");
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    public async Task<OAuthLoginState?> GetOAuthLoginState(Guid correlationId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string? encodedState = Cache.Get($"{correlationId}") as string;

        return await Decode(encodedState).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    public async Task<OAuthLoginState?> TakeOAuthLoginState(Guid correlationId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        string? encodedState;

        // The read and the removal are held together so two concurrent callers cannot both be given the same state.
        lock (_takeLock)
        {
            encodedState = Cache.Get($"{correlationId}") as string;
            Cache.Remove($"{correlationId}");
        }

        return await Decode(encodedState).ConfigureAwait(false);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A post eviction callback must not throw, as MemoryCache gives the exception nowhere to go")]
    private static void OnStateEvicted(object key, object? value, EvictionReason reason, object? state)
    {
        if (reason != EvictionReason.Capacity || state is not EvictionCallbackState callbackState || s_capacityWarned)
        {
            return;
        }

        // Reaching the limit compacts the cache, evicting a proportion of it rather than a single entry, so report
        // this once for the process rather than once for every login the compaction removed.
        lock (s_warnedLock)
        {
            if (s_capacityWarned)
            {
                return;
            }

            s_capacityWarned = true;
        }

        try
        {
            callbackState.Logger.EphemeralCorrelationStateCacheCapacityReached(callbackState.SizeLimit);
        }
        catch (Exception)
        {
            // Deliberately ignored. This runs on a thread pool thread after the entry has already gone, so there is
            // nothing to recover and nowhere for an exception to propagate to; a failed diagnostic must not crash the process.
        }
    }

    private sealed record EvictionCallbackState(ILogger Logger, int SizeLimit);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cache.Dispose();
        _disposed = true;
    }

    private async Task<OAuthLoginState?> Decode(string? encodedState)
    {
        if (string.IsNullOrEmpty(encodedState))
        {
            return null;
        }

        try
        {
            CorrelationStateRetrievedContext context = new(encodedState);
            await Events.PostRetrieval(context).ConfigureAwait(false);

            return OAuthLoginState.FromJson(context.State);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (CryptographicException)
        {
            // The state cannot be unprotected, which a key ring change or a tampered entry would both cause.
            // There is no state to return and no way to tell the two apart here, so the login simply fails.
            return null;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    public Task RemoveCorrelationState(Guid correlationId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Cache.Remove($"{correlationId}");
        return Task.CompletedTask;
    }
}
