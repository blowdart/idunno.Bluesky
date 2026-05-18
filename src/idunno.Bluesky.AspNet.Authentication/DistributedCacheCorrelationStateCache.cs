// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using Microsoft.Extensions.Caching.Distributed;

using idunno.AtProto.Authentication;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Implements an authenticate state cache using an <see cref="IDistributedCache"/>.
/// </summary>
public class DistributedCacheCorrelationStateCache : ICorrelationStateCache
{
    const string CorrelationPrefix = "_blueskyLoginCorrelation";

    /// <summary>
    /// Creates a new instance of <see cref="ICorrelationStateCache"/> using the specified <paramref name="cache"/>.
    /// </summary>
    /// <param name="cache">The <see cref="IDistributedCache"/> backing this instance.</param>
    /// <param name="entryTimeToLive">The TTL that entries in the cache should last for.</param>
    public DistributedCacheCorrelationStateCache(IDistributedCache cache, TimeSpan? entryTimeToLive = null)
    {
        Cache = cache;

        if (entryTimeToLive is not null)
        {
            EntryTTL = entryTimeToLive.Value;
        }
    }

    private IDistributedCache Cache { get; }

    private TimeSpan EntryTTL { get; } = new(0, 15, 0);

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is <see langword="null"/>.</exception>
    public async Task AddOAuthLoginState(Guid correlationId, OAuthLoginState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        DistributedCacheEntryOptions options = new DistributedCacheEntryOptions()
            .SetAbsoluteExpiration(DateTime.UtcNow.Add(EntryTTL));

        await Cache.SetStringAsync($"{CorrelationPrefix}{correlationId}", state.ToJson(), options).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<OAuthLoginState?> GetOAuthLoginState(Guid correlationId)
    {
        string? encodedState = await Cache.GetStringAsync($"{CorrelationPrefix}{correlationId}").ConfigureAwait(false);

        if (string.IsNullOrEmpty(encodedState))
        {
            return null;
        }

        try
        {
            return OAuthLoginState.FromJson(encodedState);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
