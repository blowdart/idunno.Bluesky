// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.Extensions.Caching.Distributed;

using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication.Events;

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
    public CorrelationStateCacheEvents Events { get; set; } = new CorrelationStateCacheEvents();

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is <see langword="null"/>.</exception>
    public async Task AddOAuthLoginState(Guid correlationId, OAuthLoginState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        DistributedCacheEntryOptions options = new DistributedCacheEntryOptions()
            .SetAbsoluteExpiration(DateTime.UtcNow.Add(EntryTTL));

        CorrelationStateSettingContext context = new(state.ToJson());
        await Events.PreStoring(context).ConfigureAwait(false);

        await Cache.SetStringAsync($"{CorrelationPrefix}{correlationId}", context.State, options).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<OAuthLoginState?> PeekOAuthLoginState(Guid correlationId)
    {
        string? encodedState = await Cache.GetStringAsync($"{CorrelationPrefix}{correlationId}").ConfigureAwait(false);

        return await Decode(encodedState).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    ///   <see cref="IDistributedCache"/> has no atomic take, so the entry is removed immediately after it is read and
    ///   before it is unprotected or deserialized. This narrows the window in which two callers can both be given the
    ///   same state to a single cache round trip, but it cannot close it entirely.
    /// </para>
    /// <para>
    ///   A cache which supports an atomic take, such as Redis with <c>GETDEL</c>, should derive from this class and
    ///   override this method to use it.
    /// </para>
    /// </remarks>
    public virtual async Task<OAuthLoginState?> TakeOAuthLoginState(Guid correlationId)
    {
        string key = $"{CorrelationPrefix}{correlationId}";

        string? encodedState = await Cache.GetStringAsync(key).ConfigureAwait(false);

        await Cache.RemoveAsync(key).ConfigureAwait(false);

        return await Decode(encodedState).ConfigureAwait(false);
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
    public async Task RemoveCorrelationState(Guid correlationId)
    {
        await Cache.RemoveAsync($"{CorrelationPrefix}{correlationId}").ConfigureAwait(false);
    }
}
