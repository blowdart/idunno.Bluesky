// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using idunno.Bluesky.AspNet.Authentication.Events;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// An in memory implementation of <see cref="IIdentityStore"/>.
/// </summary>
/// <remarks>
/// <para>
///   This store is intended for development time use only. It holds live <see cref="ClaimsIdentity"/> instances, including
///   their access and refresh token claims, in the memory of a single process. Nothing is encrypted, and the identities it
///   holds are readable by anything with access to the process memory or to a dump of it.
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
                if (!s_warned)
                {
                    s_warned = true;
                    Logger.UsingInMemoryCacheWarning();
                }
            }
        }
    }

    private static MemoryCache Cache { get; set; }

    private static MemoryCache RefreshCache { get; set; }

    private MemoryCacheEntryOptions TokenCacheMemoryOptions { get; set; }

    private MemoryCacheEntryOptions RefreshCacheMemoryOptions { get; set; }

    private ILogger<EphemeralIdentityStore> Logger { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    ///   This store holds live <see cref="ClaimsIdentity"/> instances in process and never serializes them, so there is no
    ///   payload to hand to the events and this property is not used.
    /// </para>
    /// </remarks>
    public IdentityStoreEvents Events { get; set; } = new IdentityStoreEvents();

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="claimsIdentity"/> is <see langword="null" />./</exception>
    public async Task Add(ClaimsIdentity claimsIdentity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claimsIdentity);

        Did did = Set(claimsIdentity, TokenCacheMemoryOptions);

        Logger.IdentityAddedToCache(did);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    ///   The returned identity is a copy. Concurrent requests for the same <see cref="Did"/> would otherwise share a
    ///   single <see cref="ClaimsIdentity"/> instance, which is not safe to enumerate while another request mutates it,
    ///   and would behave differently from a store which deserializes a fresh instance for every call.
    /// </para>
    /// </remarks>
    public async Task<ClaimsIdentity?> GetIdentity(Did did, CancellationToken cancellationToken = default)
    {
        if (Cache.Get($"{did}") is not ClaimsIdentity result)
        {
            Logger.IdentityNotFoundInCache(did);
            return null;
        }

        return result.Clone();
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

        Logger.CachedIdentityUpdated(did);
        return Task.CompletedTask;
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

        // Store a copy so later mutation of the caller's identity cannot change what the store hands out.
        Cache.Set($"{did}", claimsIdentity.Clone(), options);

        return did;
    }
}
