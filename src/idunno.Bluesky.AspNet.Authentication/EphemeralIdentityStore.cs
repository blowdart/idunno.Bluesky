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
public class EphemeralIdentityStore : IIdentityStore, IDisposable
{
    /// <summary>
    /// The number of identities the store holds before it starts evicting them.
    /// </summary>
    public const int DefaultSizeLimit = 1024;

    // The proportion of the cache to discard when it is full, matching the MemoryCache default.
    private const double CompactionPercentage = 0.05d;

    private static volatile bool s_warned;
    private static volatile bool s_capacityWarned;

    private readonly MemoryCache _cache;
    private readonly MemoryCache _refreshCache;
    private readonly int _sizeLimit;
    private readonly BlueskyAuthenticationMetrics _metrics;

    private bool _disposed;

#if NET9_0_OR_GREATER
    private static readonly Lock s_warnedLock = new ();
    private readonly Lock _refreshLock = new ();
#else
    private static readonly object s_warnedLock = new();
    private readonly object _refreshLock = new();
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
    ///   Each instance holds its own identities, so an application configuring more than one authentication scheme gets a
    ///   store, and a <paramref name="sizeLimit"/>, for each of them rather than one shared between them.
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

        _sizeLimit = sizeLimit ?? DefaultSizeLimit;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = _sizeLimit });
        _refreshCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = _sizeLimit });

        _metrics = new BlueskyAuthenticationMetrics(meterFactory);

        TokenCacheMemoryOptions = new MemoryCacheEntryOptions()
        {
            SlidingExpiration = entryTimeToLive ?? new(7, 0, 0, 0),
            Size = 1
        };

        // Eviction for capacity silently signs a user out, as their authentication cookie outlives the identity the
        // store was holding for them, so it needs to be reported rather than left to look like an expired login.
        TokenCacheMemoryOptions.RegisterPostEvictionCallback(OnIdentityEvicted, new EvictionCallbackState(Logger, _metrics, _sizeLimit));

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

    private MemoryCache Cache => _cache;

    private MemoryCache RefreshCache => _refreshCache;

    private MemoryCacheEntryOptions TokenCacheMemoryOptions { get; set; }

    private MemoryCacheEntryOptions RefreshCacheMemoryOptions { get; set; }

    private ILogger<EphemeralIdentityStore> Logger { get; set; }

    /// <inheritdoc />
    public IdentityStoreEvents Events { get; set; } = new IdentityStoreEvents();

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="claimsIdentity"/> is <see langword="null" />./</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="claimsIdentity"/> does not have a DID claim, or the DID claim is invalid.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the store has been disposed.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public async Task Add(ClaimsIdentity claimsIdentity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claimsIdentity);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        long startTimestamp = Stopwatch.GetTimestamp();

        Did did = await Set(claimsIdentity).ConfigureAwait(false);

        _metrics.RecordIdentityStoreOperation(BlueskyAuthenticationMetrics.IdentityStoreOperationAdd, startTimestamp);

        Logger.IdentityAddedToCache(did);
    }

    /// <inheritdoc />
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the store has been disposed.</exception>
    public async Task<ClaimsIdentity?> GetIdentity(Did did, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

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
    /// <exception cref="ObjectDisposedException">Thrown when the store has been disposed.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public Task Remove(Did did, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        long startTimestamp = Stopwatch.GetTimestamp();

        Cache.Remove($"{did}");

        _metrics.RecordIdentityStoreOperation(BlueskyAuthenticationMetrics.IdentityStoreOperationRemove, startTimestamp);

        Logger.CachedIdentityRemoved(did);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="identity"/> is <see langword="null" />./</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="identity"/> does not have a DID claim, or the DID claim is invalid.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the store has been disposed.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public async Task Update(ClaimsIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

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
    /// <exception cref="ObjectDisposedException">Thrown when the store has been disposed.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public Task<string?> StartRefresh(Did did, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_refreshLock)
        {
            if (RefreshCache.Get($"{did}") is not null)
            {
                Logger.StartRefreshDenied(did);
                return Task.FromResult<string?>(null);
            }

            Logger.StartRefreshEntered(did);

            string refreshLockToken = Guid.NewGuid().ToString("N");
            RefreshCache.Set($"{did}", refreshLockToken, RefreshCacheMemoryOptions);
            return Task.FromResult<string?>(refreshLockToken);
        }
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">Thrown when the store has been disposed.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public Task EndRefresh(Did did, string? refreshLockToken, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_refreshLock)
        {
            if (RefreshCache.Get($"{did}") is not string currentToken)
            {
                // The lock already expired, so there is nothing of ours to release.
                Logger.EndRefreshLockNotOwned(did);
                return Task.CompletedTask;
            }

            if (!currentToken.Equals(refreshLockToken, StringComparison.Ordinal))
            {
                // The lock expired and someone else acquired it, so it is not ours to release.
                Logger.EndRefreshLockNotOwned(did);
                return Task.CompletedTask;
            }

            RefreshCache.Remove($"{did}");

            Logger.EndRefreshFinished(did);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">Thrown when the store has been disposed.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public Task<bool> IsRefreshing(Did did, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_refreshLock)
        {
            if (RefreshCache.Get($"{did}") is not null)
            {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
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
            callbackState.Logger.EphemeralIdentityStoreCapacityReached(callbackState.SizeLimit);
        }
        catch (Exception)
        {
            // Deliberately ignored. This runs on a thread pool thread after the entry has already gone, so there is
            // nothing to recover and nowhere for an exception to propagate to; a failed diagnostic must not crash the process.
        }
    }

    private sealed record EvictionCallbackState(ILogger Logger, BlueskyAuthenticationMetrics Metrics, int SizeLimit);

    /// <summary>
    /// Stores <paramref name="identity"/> against <paramref name="did"/>, making room for it if the store is full.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> to store <paramref name="identity"/> against.</param>
    /// <param name="identity">The serialized identity to store.</param>
    /// <exception cref="InvalidOperationException">Thrown when the identity could not be stored.</exception>
    /// <remarks>
    /// <para>
    ///   A size limited <see cref="MemoryCache"/> rejects an incoming entry when it is full rather than making room for
    ///   it, only scheduling a compaction to run later, and reports nothing to the caller. Left alone that loses the
    ///   write: a sign-in the user is never given, or refreshed credentials which are dropped after the refresh token
    ///   which produced them has already been spent. The entry is checked, the cache compacted, and the write retried
    ///   so the identity is either stored or the caller is told it was not.
    /// </para>
    /// </remarks>
    private void StoreIdentity(Did did, byte[] identity)
    {
        string key = $"{did}";

        DateTime expiresAfter = TokenCacheMemoryOptions.SlidingExpiration is TimeSpan slidingExpiration
            ? DateTime.UtcNow.Add(slidingExpiration)
            : DateTime.MaxValue;

        Cache.Set(key, identity, TokenCacheMemoryOptions);

        // A short lived entry can reach the end of its life between being written and being read back, which leaves it
        // missing because it expired rather than because the cache had no room for it.
        if (Cache.TryGetValue(key, out _) || DateTime.UtcNow >= expiresAfter)
        {
            return;
        }

        // Compact takes the proportion of the cache to remove, and truncates the count it works out from it, so a
        // proportion which rounds down to nothing would leave the cache just as full as it is now. Asking for one and
        // a half entries' worth guarantees at least one is removed however small the cache is.
        int count = Cache.Count;
        Cache.Compact(count > 0 ? Math.Max(CompactionPercentage, 1.5d / count) : CompactionPercentage);

        Cache.Set(key, identity, TokenCacheMemoryOptions);

        if (Cache.TryGetValue(key, out _) || DateTime.UtcNow >= expiresAfter)
        {
            return;
        }

        _metrics.IdentityStoreWriteFailures.Add(1);
        Logger.IdentityCouldNotBeStored(did, _sizeLimit);

        throw new InvalidOperationException(
            $"The identity store is full at its size limit of {_sizeLimit} and could not make room for the identity.");
    }

    /// <summary>
    /// Releases the unmanaged resources used by the store and, optionally, the managed resources.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources, otherwise <see langword="false"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _cache.Dispose();
            _refreshCache.Dispose();
        }

        _disposed = true;
    }

    /// <summary>
    /// Releases the resources used by the store, discarding every identity it holds.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private async Task<Did> Set(ClaimsIdentity claimsIdentity)
    {
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

        StoreIdentity(did, context.Identity.ToArray());

        return did;
    }
}
