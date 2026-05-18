// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using idunno.AtProto;
using idunno.AtProto.Authentication;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Implements an <see cref="IIdentityStore"/> using a distributed cache.
/// </summary>
public class DistributedCacheIdentityStore : IIdentityStore
{
    const string ClaimsStorePrefix = "_didMap:";

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

        if (options is not null && options.Value.IdentityStoreEntryTimeToLive is not null)
        {
            EntryTTL = options.Value.IdentityStoreEntryTimeToLive.Value;
        }
    }

    /// <summary>
    /// Gets or sets the cache used to store identities.
    /// </summary>
    [NotNull]
    protected static IDistributedCache? Cache { get; set; }

    /// <summary>
    /// Gets or sets how long a cache entry should live for.
    /// </summary>
    protected TimeSpan EntryTTL { get; } = new(7, 0, 0, 0);

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

        DistributedCacheEntryOptions options = new DistributedCacheEntryOptions()
            .SetAbsoluteExpiration(DateTime.UtcNow.Add(EntryTTL));

        await Cache.SetAsync($"{ClaimsStorePrefix}{did}", claimsIdentityAsBytes, options, token: cancellationToken).ConfigureAwait(false);

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
    public async Task Renew(ClaimsIdentity identity, CancellationToken cancellationToken)
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
}

