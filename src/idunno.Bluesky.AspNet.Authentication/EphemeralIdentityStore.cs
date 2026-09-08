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
    [SuppressMessage("Major Code Smell", "S3010:Static fields should not be updated in constructors", Justification = "Used to ensure the emphermal warning is only logged once")]
    public EphemeralIdentityStore(
        ILoggerFactory loggerFactory,
        TimeSpan? entryTimeToLive = null)
    {
        Logger = loggerFactory.CreateLogger<EphemeralIdentityStore>();
        SlidingExpiration = entryTimeToLive ?? new(7, 0, 0, 0);

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

    private TimeSpan SlidingExpiration { get; set; }

    private ILogger<EphemeralIdentityStore> Logger { get; set; }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="claimsIdentity"/> is <see langword="null" />./</exception>
    public async Task Add(ClaimsIdentity claimsIdentity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claimsIdentity);

        Did did = Set(claimsIdentity);

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
    public Task Renew(ClaimsIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);

        Did did = Set(identity);

        Logger.CachedIdentityRenewed(did);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> StartRefresh(Did did, CancellationToken cancellationToken = default)
    {
        var options = new MemoryCacheEntryOptions()
        {
            SlidingExpiration = SlidingExpiration,
            Size = 1
        };

        lock (s_refreshLock)
        {
            if (RefreshCache.Get($"{did}") is not null)
            {
                Logger.StartRefreshDenied(did);
                return false;
            }

            Logger.StartRefreshEntered(did);
            RefreshCache.Set($"{did}", true, options);
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

    private Did Set(ClaimsIdentity claimsIdentity)
    {
        ArgumentNullException.ThrowIfNull(claimsIdentity);

        string? didAsString = (claimsIdentity.Claims?.FirstOrDefault(
            x => x.Type.Equals(AtProtoClaims.Did, StringComparison.Ordinal))?.Value) ??
            throw new ArgumentException("No DID claim found", nameof(claimsIdentity));

        if (!Did.TryParse(didAsString, out Did? did))
        {
            throw new ArgumentException("DID claim was not a valid DID", nameof(claimsIdentity));
        }

        var options = new MemoryCacheEntryOptions()
        {
            SlidingExpiration = SlidingExpiration,
            Size = 1
        };

        Cache.Set($"{did}", claimsIdentity, options);

        return did;
    }
}
