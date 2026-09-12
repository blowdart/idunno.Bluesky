// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.Authentication;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// An in memory implementation of <see cref="IIdentityStore"/>.
/// </summary>
public class EphemeralIdentityStore : IIdentityStore
{
    private static volatile bool s_warned;

#if NET9_0_OR_GREATER
    private static readonly Lock s_warnedLock = new ();
    private static readonly Lock s_refreshLock = new ();
#else
    private static readonly object s_warnedLock = new();
    private static readonly object s_refreshLock = new();
#endif

    static EphemeralIdentityStore()
    {
        MemoryCacheOptions cacheOptions = new()
        {
            SizeLimit = 1024
        };

        Cache = new MemoryCache(cacheOptions);
        RefreshCache = new MemoryCache(cacheOptions);
    }

    /// <summary>
    /// Creates a new instance of <see cref="EphemeralIdentityStore"/>.
    /// </summary>
    /// <param name="loggerFactory">The logger to create loggers from.</param>
    /// <param name="entryTimeToLive">The time to live for cache entries.</param>
    /// <param name="refreshLockExpiration">The time to lock a token refresh attempt for.</param>
    [SuppressMessage("Major Code Smell", "S3010:Static fields should not be updated in constructors", Justification = "Used to ensure the emphermal warning is only logged once")]
    public EphemeralIdentityStore(
        ILoggerFactory loggerFactory,
        TimeSpan? entryTimeToLive = null,
        TimeSpan? refreshLockExpiration = null)
    {
        Logger = loggerFactory.CreateLogger<EphemeralIdentityStore>();

        TokenCacheMemoryOptions = new MemoryCacheEntryOptions()
        {
            SlidingExpiration = entryTimeToLive ?? new(7, 0, 0, 0),
            Size = 1
        };

        RefreshCacheMemoryOptions = new MemoryCacheEntryOptions()
        {
            AbsoluteExpirationRelativeToNow = refreshLockExpiration ?? TimeSpan.FromSeconds(90),
            Size = 1
        };

        if (!s_warned)
        {
            lock (s_warnedLock)
            {
                s_warned = true;
                Logger.UsingInMemoryCacheWarning();
            }
        }
    }

    private static MemoryCache Cache { get; set; }

    private static MemoryCache RefreshCache { get; set; }

    private MemoryCacheEntryOptions TokenCacheMemoryOptions { get; set; }

    private MemoryCacheEntryOptions RefreshCacheMemoryOptions { get; set; }

    private ILogger<EphemeralIdentityStore> Logger { get; set; }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="claimsIdentity"/> is <see langword="null" />./</exception>
    public async Task Add(ClaimsIdentity claimsIdentity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claimsIdentity);

        Did did = Set(claimsIdentity, TokenCacheMemoryOptions);

        Logger.IdentityAddedToCache(did);
    }

    /// <inheritdoc />
    public async Task<ClaimsIdentity?> GetIdentity(Did did, CancellationToken cancellationToken = default)
    {
        if (Cache.Get($"{did}") is not ClaimsIdentity result)
        {
            Logger.IdentityNotFoundInCache(did);
            return null;
        }

        return result;
    }

    /// <inheritdoc />
    public Task Remove(Did did, CancellationToken cancellationToken = default)
    {
        Cache.Remove($"{did}");
        Logger.CachedIdentityRemoved(did);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="identity"/> is <see langword="null" />./</exception>
    public Task Update(ClaimsIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);

        Did did = Set(identity, TokenCacheMemoryOptions);

        Logger.CachedIdentityRenewed(did);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> StartRefresh(Did did, CancellationToken cancellationToken = default)
    {
        lock (s_refreshLock)
        {
            if (RefreshCache.Get($"{did}") is not null)
            {
                Logger.StartRefreshDenied(did);
                return false;
            }

            Logger.StartRefreshEntered(did);
            RefreshCache.Set($"{did}", true, RefreshCacheMemoryOptions);
            return true;
        }
    }

    /// <inheritdoc />
    public async Task EndRefresh(Did did, CancellationToken cancellationToken = default)
    {
        lock (s_refreshLock)
        {
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

    private static Did Set(ClaimsIdentity claimsIdentity, MemoryCacheEntryOptions options)
    {
        ArgumentNullException.ThrowIfNull(claimsIdentity);

        string? didAsString = (claimsIdentity.Claims?.FirstOrDefault(
            x => x.Type.Equals(AtProtoClaims.Did, StringComparison.Ordinal))?.Value) ??
            throw new ArgumentException("No DID claim found", nameof(claimsIdentity));

        if (!Did.TryParse(didAsString, out Did? did))
        {
            throw new ArgumentException("DID claim was not a valid DID", nameof(claimsIdentity));
        }

        Cache.Set($"{did}", claimsIdentity, options);

        return did;
    }
}
