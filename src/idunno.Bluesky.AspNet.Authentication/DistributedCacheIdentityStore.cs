// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Implements an <see cref="IIdentityStore"/> using a distributed cache.
/// </summary>
/// <remarks>
/// <para>This implementation uses a distributed cache to store identity information, allowing for scalable and shared access across multiple instances.</para>
/// <para>
/// Depending on the backing distributed cache, this may be a best effort. Caches that are eventually consistent will not guarantee locking.
/// </para>
/// </remarks>
public class DistributedCacheIdentityStore : IIdentityStore
{
    private static readonly TimeSpan s_defaultEntryTTL = TimeSpan.FromDays(7);
    private static readonly TimeSpan s_defaultRefreshLockTTL = TimeSpan.FromSeconds(90);

    const string ClaimsStorePrefix = "_didMap:";
    const string RefreshStorePrefix = "_tokenRefreshLock:";

    /// <summary>
    /// Creates a new instance of <see cref="DistributedCacheIdentityStore"/>.
    /// </summary>
    /// <param name="cache">The <see cref="IDistributedCache"/> to store the claims in.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to create loggers.</param>
    /// <param name="options">The <see cref="BlueskyAuthenticationOptions"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cache"/> is <see langword="null" />.</exception>
    public DistributedCacheIdentityStore(
        IDistributedCache cache, 
        ILoggerFactory loggerFactory,
        IOptions<BlueskyAuthenticationOptions>? options = null)
    {
        ArgumentNullException.ThrowIfNull(cache);

        Cache = cache;
        Logger = loggerFactory.CreateLogger<DistributedCacheIdentityStore>();

        if (options is not null)
        {
            TokenCacheMemoryOptions = new DistributedCacheEntryOptions()
            {
                SlidingExpiration = options.Value.IdentityStoreEntryTimeToLive ?? s_defaultEntryTTL
            };

            RefreshCacheMemoryOptions = new DistributedCacheEntryOptions()
            {
                AbsoluteExpirationRelativeToNow = options.Value.RefreshLockLength ?? s_defaultRefreshLockTTL
            };

        }
        else
        {
            TokenCacheMemoryOptions = new DistributedCacheEntryOptions()
            {
                SlidingExpiration = s_defaultEntryTTL
            };

            RefreshCacheMemoryOptions = new DistributedCacheEntryOptions()
            {
                AbsoluteExpirationRelativeToNow = s_defaultRefreshLockTTL
            };
        }
    }

    /// <summary>
    /// Gets or sets the cache used to store identities.
    /// </summary>
    [NotNull]
    protected static IDistributedCache? Cache { get; set; }

    /// <summary>
    /// Gets or sets the time to live for entries in the identity store.
    /// </summary>
    protected DistributedCacheEntryOptions TokenCacheMemoryOptions { get; init; }

    /// <summary>
    /// Gets or sets the time to live for entries in the refresh lock store.
    /// </summary>
    protected DistributedCacheEntryOptions RefreshCacheMemoryOptions { get; init; }

    private ILogger<DistributedCacheIdentityStore> Logger { get; }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="claimsIdentity"/> is <see langword="null" />./</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="claimsIdentity"/> does not have a DID claim, or the DID claim is invalid.</exception>
    public async Task Add(ClaimsIdentity claimsIdentity, CancellationToken cancellationToken = default)
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

        await Cache.SetAsync($"{ClaimsStorePrefix}{did}", claimsIdentityAsBytes, TokenCacheMemoryOptions, token: cancellationToken).ConfigureAwait(false);

        Logger.IdentityAddedToCache(did);
    }

    /// <inheritdoc/>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Error handling needs to catch all exceptions")]
    public async Task<ClaimsIdentity?> GetIdentity(Did did, CancellationToken cancellationToken = default)
    {
        ClaimsIdentity? result = null;

        byte[]? claimsIdentityAsBytes = await Cache.GetAsync($"{ClaimsStorePrefix}{did}", token: cancellationToken).ConfigureAwait(false);

        if (claimsIdentityAsBytes is null)
        {
            Logger.IdentityNotFoundInCache(did);
            return null;
        }

        try
        {
            using MemoryStream claimsMemoryStream = new();
            await claimsMemoryStream.WriteAsync(claimsIdentityAsBytes, cancellationToken).ConfigureAwait(false);
            claimsMemoryStream.Position = 0;
            using BinaryReader claimsReader = new(claimsMemoryStream);
            result = new ClaimsIdentity(claimsReader);
        }
        catch (Exception ex)
        {
            Logger.CachedIdentityIsCorrupt(did, ex);
            return null;
        }

        await Cache.RefreshAsync($"{ClaimsStorePrefix}{did}", token: cancellationToken).ConfigureAwait(false);

        return result;
    }

    /// <inheritdoc/>
    public async Task Remove(Did did, CancellationToken cancellationToken = default)
    {
        await Cache.RemoveAsync($"{ClaimsStorePrefix}{did}", token: cancellationToken).ConfigureAwait(false);
        Logger.CachedIdentityRemoved(did);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="identity"/> is <see langword="null" />./</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="identity"/> does not have a DID claim, or the DID claim is invalid.</exception>
    public async Task Update(ClaimsIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        string? didAsString = (identity.Claims?.FirstOrDefault(
            x => x.Type.Equals(AtProtoClaims.Did, StringComparison.Ordinal))?.Value) ??
            throw new ArgumentException("No DID claim found", nameof(identity));

        if (!Did.TryParse(didAsString, out Did? did))
        {
            throw new ArgumentException("DID claim was not a valid DID", nameof(identity));
        }

        await Add(identity, cancellationToken).ConfigureAwait(false);
        Logger.CachedIdentityRenewed(did);
    }

    /// <inheritdoc/>
    public async Task<bool> StartRefresh(Did did, CancellationToken cancellationToken = default)
    {
        byte[]? existing = await Cache.GetAsync($"{RefreshStorePrefix}{did}", token: cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            return false;
        }

        await Cache.SetAsync($"{RefreshStorePrefix}{did}", [1], RefreshCacheMemoryOptions, token: cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc/>
    public async Task EndRefresh(Did did, CancellationToken cancellationToken = default)
    {
        await Cache.RemoveAsync($"{RefreshStorePrefix}{did}", token: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> IsRefreshing(Did did, CancellationToken cancellationToken = default)
    {
        byte[]? existing = await Cache.GetAsync($"{RefreshStorePrefix}{did}", token: cancellationToken).ConfigureAwait(false);
        return existing is not null;
    }
}
