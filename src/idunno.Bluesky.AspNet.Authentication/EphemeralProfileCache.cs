// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using idunno.AtProto;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Implements an in-memory profile cache.
/// </summary>
/// <remarks>
/// <para>
///   Each instance holds its own profiles, and holds them only for the lifetime of the process, so profiles are not
///   shared between instances of an application and are lost when it restarts.
/// </para>
/// </remarks>
public sealed class EphemeralProfileCache : IProfileCache, IDisposable
{
    /// <summary>
    /// The number of profiles the cache holds before it starts evicting them.
    /// </summary>
    public const int DefaultSizeLimit = 1024;

    // The proportion of the cache to discard when it is full, matching the MemoryCache default.
    private const double CompactionPercentage = 0.05d;

    private static volatile bool s_warned;
    private static volatile bool s_capacityWarned;

    private readonly MemoryCache _cache;
    private readonly int _sizeLimit;

    private volatile bool _disposed;

#if NET9_0_OR_GREATER
    private static readonly Lock s_warnedLock = new ();
#else
    private static readonly object s_warnedLock = new();
#endif

    /// <summary>
    /// Creates a new instance of <see cref="EphemeralProfileCache"/>.
    /// </summary>
    /// <param name="loggerFactory">The logger to create loggers from.</param>
    /// <param name="entryTimeToLive">The time to live for cache entries.</param>
    /// <param name="sizeLimit">The number of profiles to hold before evicting them. Defaults to <see cref="DefaultSizeLimit"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="sizeLimit"/> is not greater than zero.</exception>
    [SuppressMessage("Major Code Smell", "S3010:Static fields should not be updated in constructors", Justification = "Used to ensure the ephemeral warning is only logged once")]
    public EphemeralProfileCache(
        ILoggerFactory loggerFactory,
        TimeSpan? entryTimeToLive = null,
        int? sizeLimit = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sizeLimit ?? DefaultSizeLimit, 0);

        Logger = loggerFactory.CreateLogger<EphemeralProfileCache>();
        EntryTTL = entryTimeToLive ?? new(0, 0, 15, 0);

        _sizeLimit = sizeLimit ?? DefaultSizeLimit;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = _sizeLimit });

        if (!s_warned)
        {
            lock (s_warnedLock)
            {
                if (!s_warned)
                {
                    s_warned = true;
                    Logger.UsingInMemoryProfileCacheWarning(_sizeLimit);
                }
            }
        }
    }

    private MemoryCache Cache => _cache;

    private TimeSpan EntryTTL { get; set; }

    private ILogger<EphemeralProfileCache> Logger { get; set; }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is <see langword="null" />./</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    public Task Add(Did did, ProfileCacheEntry profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ObjectDisposedException.ThrowIf(_disposed, this);

        DateTime absoluteExpiration = DateTime.UtcNow.Add(EntryTTL);

        MemoryCacheEntryOptions options = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(absoluteExpiration)
            .SetSize(1);

        // Eviction for capacity costs a profile lookup rather than correctness, but it happens silently and drives the
        // hit rate towards nothing, so it needs to be visible.
        options.RegisterPostEvictionCallback(OnProfileEvicted, new EvictionCallbackState(Logger, _sizeLimit));

        string key = $"{did}";

        Cache.Set(key, profile, options);

        // A short lived entry can reach the end of its life between being written and being read back, which leaves it
        // missing because it expired rather than because the cache had no room for it.
        if (Cache.TryGetValue(key, out _) || DateTime.UtcNow >= absoluteExpiration)
        {
            return Task.CompletedTask;
        }

        // A size limited MemoryCache rejects an incoming entry when it is full rather than making room for it, only
        // scheduling a compaction to run later, so without this the newest profile is the one which is never cached.
        // Compact takes the proportion of the cache to remove and truncates the count it works out from it, so asking
        // for one and a half entries' worth guarantees at least one is removed however small it is.
        int count = Cache.Count;
        Cache.Compact(count > 0 ? Math.Max(CompactionPercentage, 1.5d / count) : CompactionPercentage);

        // Unlike the identity store, failing to store a profile is not fatal. The next request simply misses and
        // fetches it again, so a cache which cannot make room is left to say so through the eviction warning.
        Cache.Set(key, profile, options);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    public Task<ProfileCacheEntry?> GetCachedValue(Did did)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (Cache.Get($"{did}") is not ProfileCacheEntry result)
        {
            return Task.FromResult<ProfileCacheEntry?>(null);
        }

        return Task.FromResult<ProfileCacheEntry?>(result);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A post eviction callback must not throw, as MemoryCache gives the exception nowhere to go")]
    private static void OnProfileEvicted(object key, object? value, EvictionReason reason, object? state)
    {
        if (reason != EvictionReason.Capacity || state is not EvictionCallbackState callbackState || s_capacityWarned)
        {
            return;
        }

        // Reaching the limit compacts the cache, evicting a proportion of it rather than a single entry, so report
        // this once for the process rather than once for every profile the compaction removed.
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
            callbackState.Logger.EphemeralProfileCacheCapacityReached(callbackState.SizeLimit);
        }
        catch (Exception)
        {
            // Deliberately ignored. This runs on a thread pool thread after the entry has already gone, so there is
            // nothing to recover and nowhere for an exception to propagate to; a failed diagnostic must not crash the process.
        }
    }

    private sealed record EvictionCallbackState(ILogger Logger, int SizeLimit);

    /// <summary>
    /// Releases the resources used by the cache, discarding every profile it holds.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Set before the cache is disposed so a concurrent caller fails its disposal guard rather than reaching a
        // half disposed cache.
        _disposed = true;
        _cache.Dispose();
    }
}
