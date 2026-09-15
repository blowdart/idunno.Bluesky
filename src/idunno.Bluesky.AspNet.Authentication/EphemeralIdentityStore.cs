// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Security.Claims;
using System.Security.Cryptography;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication.Events;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// An in memory implementation of <see cref="IIdentityStore"/>.
/// </summary>
/// <remarks>
/// <para>
///   This store is intended for development time use only. It holds serialized identities, including their access and
///   refresh token claims, in the memory of a single process. Unless <see cref="Events"/> is configured to encrypt them
///   they are readable by anything with access to the process memory or to a dump of it, and even when they are encrypted
///   the key material used to protect them is held in the same process.
/// </para>
/// <para>
///   Its contents are also lost when the process restarts, signing every user out, and are not shared between instances of an
///   application, so it cannot be used in a farm or with more than one worker process.
/// </para>
/// <para>
///   Production applications should use <see cref="DistributedCacheIdentityStore"/>, or another
///   <see cref="IIdentityStore"/> implementation backed by durable storage, with
///   <see cref="DataProtectingIdentityStoreEvents"/> configured so the stored credentials are encrypted at rest.
/// </para>
/// </remarks>
public class EphemeralIdentityStore : IIdentityStore
{
    /// <summary>
    /// The number of identities the store holds before it starts evicting them.
    /// </summary>
    public const int DefaultSizeLimit = 1024;

    private static volatile bool s_warned;
    private static volatile bool s_capacityWarned;

    private static MemoryCache? s_cache;
    private static MemoryCache? s_refreshCache;
    private static int s_configuredSizeLimit;

    private readonly BlueskyAuthenticationMetrics _metrics;

#if NET9_0_OR_GREATER
    private static readonly Lock s_warnedLock = new ();
    private static readonly Lock s_refreshLock = new ();
    private static readonly Lock s_cacheLock = new ();
#else
    private static readonly object s_warnedLock = new();
    private static readonly object s_refreshLock = new();
    private static readonly object s_cacheLock = new();
#endif

    /// <summary>
    /// Creates a new instance of <see cref="EphemeralIdentityStore"/>.
    /// </summary>
    /// <param name="loggerFactory">The logger to create loggers from.</param>
    /// <param name="entryTimeToLive">The time to live for cache entries.</param>
    /// <param name="refreshLockExpiration">The time to lock a token refresh attempt for.</param>
    /// <param name="sizeLimit">The number of identities to hold before evicting them. Defaults to <see cref="DefaultSizeLimit"/>.</param>
    /// <param name="meterFactory">An optional meter factory to create meters from.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="sizeLimit"/> is not greater than zero.</exception>
    /// <remarks>
    /// <para>
    ///   The caches backing this store are static, so the first instance created fixes <paramref name="sizeLimit"/> for
    ///   the lifetime of the process. A later instance asking for a different limit is warned that its value was ignored.
    /// </para>
    /// </remarks>
    [SuppressMessage("Major Code Smell", "S3010:Static fields should not be updated in constructors", Justification = "Used to ensure the emphermal warning is only logged once")]
    public EphemeralIdentityStore(
        ILoggerFactory loggerFactory,
        TimeSpan? entryTimeToLive = null,
        TimeSpan? refreshLockExpiration = null,
        int? sizeLimit = null,
        IMeterFactory? meterFactory = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sizeLimit ?? DefaultSizeLimit, 0);

        Logger = loggerFactory.CreateLogger<EphemeralIdentityStore>();

        EnsureCaches(sizeLimit ?? DefaultSizeLimit, Logger);

        _metrics = new BlueskyAuthenticationMetrics(meterFactory);

        TokenCacheMemoryOptions = new MemoryCacheEntryOptions()
        {
            SlidingExpiration = entryTimeToLive ?? new(7, 0, 0, 0),
            Size = 1
        };

        // Eviction for capacity silently signs a user out, as their authentication cookie outlives the identity the
        // store was holding for them, so it needs to be reported rather than left to look like an expired login.
        TokenCacheMemoryOptions.RegisterPostEvictionCallback(OnIdentityEvicted, new EvictionCallbackState(Logger, _metrics));

        RefreshCacheMemoryOptions = new MemoryCacheEntryOptions()
        {
            AbsoluteExpirationRelativeToNow = refreshLockExpiration ?? TimeSpan.FromSeconds(90),
            Size = 1
        };

        if (!s_warned)
        {
            lock (s_warnedLock)
            {
                if (!s_warned)
                {
                    s_warned = true;
                    Logger.UsingInMemoryCacheWarning();
                }
            }
        }
    }

    private static MemoryCache Cache => s_cache!;

    private static MemoryCache RefreshCache => s_refreshCache!;

    private MemoryCacheEntryOptions TokenCacheMemoryOptions { get; set; }

    private MemoryCacheEntryOptions RefreshCacheMemoryOptions { get; set; }

    private ILogger<EphemeralIdentityStore> Logger { get; set; }

    /// <inheritdoc />
    public IdentityStoreEvents Events { get; set; } = new IdentityStoreEvents();

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="claimsIdentity"/> is <see langword="null" />./</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="claimsIdentity"/> does not have a DID claim, or the DID claim is invalid.</exception>
    public async Task Add(ClaimsIdentity claimsIdentity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claimsIdentity);

        long startTimestamp = Stopwatch.GetTimestamp();

        Did did = await Set(claimsIdentity).ConfigureAwait(false);

        _metrics.RecordIdentityStoreOperation(BlueskyAuthenticationMetrics.IdentityStoreOperationAdd, startTimestamp);

        Logger.IdentityAddedToCache(did);
    }

    /// <inheritdoc />
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public async Task<ClaimsIdentity?> GetIdentity(Did did, CancellationToken cancellationToken = default)
    {
        long startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            return await GetIdentityCore(did, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _metrics.RecordIdentityStoreOperation(BlueskyAuthenticationMetrics.IdentityStoreOperationGet, startTimestamp);
        }
    }

    /// <inheritdoc />
    public Task Remove(Did did, CancellationToken cancellationToken = default)
    {
        long startTimestamp = Stopwatch.GetTimestamp();

        Cache.Remove($"{did}");

        _metrics.RecordIdentityStoreOperation(BlueskyAuthenticationMetrics.IdentityStoreOperationRemove, startTimestamp);

        Logger.CachedIdentityRemoved(did);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="identity"/> is <see langword="null" />./</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="identity"/> does not have a DID claim, or the DID claim is invalid.</exception>
    public async Task Update(ClaimsIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);

        long startTimestamp = Stopwatch.GetTimestamp();

        Did did = await Set(identity).ConfigureAwait(false);

        _metrics.RecordIdentityStoreOperation(BlueskyAuthenticationMetrics.IdentityStoreOperationUpdate, startTimestamp);

        Logger.CachedIdentityUpdated(did);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Error handling needs to catch all exceptions")]
    private async Task<ClaimsIdentity?> GetIdentityCore(Did did, CancellationToken cancellationToken)
    {
        if (Cache.Get($"{did}") is not byte[] claimsIdentityAsBytes)
        {
            Logger.IdentityNotFoundInCache(did);
            return null;
        }

        try
        {
            IdentityStoreRetrievedContext context = new(claimsIdentityAsBytes);
            await Events.PostRetrieval(context).ConfigureAwait(false);

            using MemoryStream contextMemoryStream = new();
            await contextMemoryStream.WriteAsync(context.Identity, cancellationToken).ConfigureAwait(false);
            contextMemoryStream.Position = 0;
            using BinaryReader contextReader = new(contextMemoryStream);

            return new ClaimsIdentity(contextReader);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (CryptographicException ex)
        {
            // The stored identity cannot be read, so remove it rather than leaving an entry every subsequent request will fail on.
            Cache.Remove($"{did}");
            Logger.CachedIdentityCouldNotBeUnprotected(did, ex);
            _metrics.DataProtectionFailures.Add(
                1,
                new KeyValuePair<string, object?>(
                    BlueskyAuthenticationMetrics.DataProtectionSourceTagName,
                    BlueskyAuthenticationMetrics.DataProtectionSourceIdentityStore));
            return null;
        }
        catch (Exception ex)
        {
            Logger.CachedIdentityIsCorrupt(did, ex);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<string?> StartRefresh(Did did, CancellationToken cancellationToken = default)
    {
        lock (s_refreshLock)
        {
            if (RefreshCache.Get($"{did}") is not null)
            {
                Logger.StartRefreshDenied(did);
                return null;
            }

            Logger.StartRefreshEntered(did);

            string refreshLockToken = Guid.NewGuid().ToString("N");
            RefreshCache.Set($"{did}", refreshLockToken, RefreshCacheMemoryOptions);
            return refreshLockToken;
        }
    }

    /// <inheritdoc />
    public async Task EndRefresh(Did did, string? refreshLockToken, CancellationToken cancellationToken = default)
    {
        lock (s_refreshLock)
        {
            if (RefreshCache.Get($"{did}") is string currentToken &&
                !currentToken.Equals(refreshLockToken, StringComparison.Ordinal))
            {
                // The lock expired and someone else acquired it, so it is not ours to release.
                Logger.EndRefreshLockNotOwned(did);
                return;
            }

            RefreshCache.Remove($"{did}");

            Logger.EndRefreshFinished(did);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsRefreshing(Did did, CancellationToken cancellationToken = default)
    {
        lock (s_refreshLock)
        {
            if (RefreshCache.Get($"{did}") is not null)
            {
                return true;
            }
        }
        return false;
    }

    private static void EnsureCaches(int sizeLimit, ILogger logger)
    {
        lock (s_cacheLock)
        {
            if (s_cache is null || s_refreshCache is null)
            {
                s_configuredSizeLimit = sizeLimit;

                // Each cache gets its own options, otherwise the limit reads as though it were shared between them.
                s_cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = sizeLimit });
                s_refreshCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = sizeLimit });
            }
            else if (s_configuredSizeLimit != sizeLimit)
            {
                logger.EphemeralStoreSizeLimitIgnored(nameof(EphemeralIdentityStore), sizeLimit, s_configuredSizeLimit);
            }
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A post eviction callback must not throw, as MemoryCache gives the exception nowhere to go")]
    private static void OnIdentityEvicted(object key, object? value, EvictionReason reason, object? state)
    {
        if (reason != EvictionReason.Capacity || state is not EvictionCallbackState callbackState)
        {
            return;
        }

        try
        {
            // Unlike the warning below, every evicted identity is counted, so the rate at which the store is signing
            // users out is visible rather than just the fact that it happened at least once.
            callbackState.Metrics.IdentityStoreEvictions.Add(1);
        }
        catch (Exception)
        {
            // Deliberately ignored. This runs on a thread pool thread after the entry has already gone, so there is
            // nothing to recover and nowhere for an exception to propagate to; a failed diagnostic must not crash the process.
        }

        if (s_capacityWarned)
        {
            return;
        }

        // Reaching the limit compacts the cache, evicting a proportion of it rather than a single entry, so report
        // this once for the process rather than once for every identity the compaction removed.
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
            callbackState.Logger.EphemeralIdentityStoreCapacityReached(s_configuredSizeLimit);
        }
        catch (Exception)
        {
            // Deliberately ignored. This runs on a thread pool thread after the entry has already gone, so there is
            // nothing to recover and nowhere for an exception to propagate to; a failed diagnostic must not crash the process.
        }
    }

    private sealed record EvictionCallbackState(ILogger Logger, BlueskyAuthenticationMetrics Metrics);

    private async Task<Did> Set(ClaimsIdentity claimsIdentity)    {
        ArgumentNullException.ThrowIfNull(claimsIdentity);

        string? didAsString = (claimsIdentity.Claims?.FirstOrDefault(
            x => x.Type.Equals(AtProtoClaims.Did, StringComparison.Ordinal))?.Value) ??
            throw new ArgumentException("No DID claim found", nameof(claimsIdentity));

        if (!Did.TryParse(didAsString, out Did? did))
        {
            throw new ArgumentException("DID claim was not a valid DID", nameof(claimsIdentity));
        }

        byte[] claimsIdentityAsBytes;
        using (MemoryStream claimsMemoryStream = new())
        {
            using BinaryWriter claimsWriter = new(claimsMemoryStream);
            claimsIdentity.WriteTo(claimsWriter);
            claimsWriter.Flush();
            claimsIdentityAsBytes = claimsMemoryStream.ToArray();
        }

        IdentityStoreSettingContext context = new(claimsIdentityAsBytes);
        await Events.PreStoring(context).ConfigureAwait(false);

        Cache.Set($"{did}", context.Identity.ToArray(), TokenCacheMemoryOptions);

        return did;
    }
}
