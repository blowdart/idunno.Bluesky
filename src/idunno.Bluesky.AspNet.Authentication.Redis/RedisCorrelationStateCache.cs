// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text.Json;

using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication.Events;

using StackExchange.Redis;

namespace idunno.Bluesky.AspNet.Authentication.Redis;

/// <summary>
/// Implements an <see cref="ICorrelationStateCache"/> using Redis.
/// </summary>
public class RedisCorrelationStateCache : ICorrelationStateCache
{
    private const string DefaultInstanceName = "idunno:bluesky:authentication:";

    private static readonly TimeSpan s_defaultEntryTimeToLive = TimeSpan.FromMinutes(15);

    private readonly IDatabase _database;
    private readonly string _instanceName;
    private readonly TimeSpan _entryTimeToLive;

    /// <summary>
    /// Creates a new instance of <see cref="RedisCorrelationStateCache"/>.
    /// </summary>
    /// <param name="connectionMultiplexer">The shared Redis connection multiplexer.</param>
    /// <param name="database">The Redis database number, or <c>-1</c> to use the configured default.</param>
    /// <param name="instanceName">An optional prefix used to isolate this application's keys.</param>
    /// <param name="entryTimeToLive">The time to live for stored correlation state.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connectionMultiplexer"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="entryTimeToLive"/> is not positive.</exception>
    public RedisCorrelationStateCache(
        IConnectionMultiplexer connectionMultiplexer,
        int database = -1,
        string? instanceName = null,
        TimeSpan? entryTimeToLive = null)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);

        _database = connectionMultiplexer.GetDatabase(database);
        _instanceName = instanceName ?? DefaultInstanceName;
        _entryTimeToLive = entryTimeToLive ?? s_defaultEntryTimeToLive;

        if (_entryTimeToLive <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(entryTimeToLive), _entryTimeToLive, "The time to live must be positive.");
        }
    }

    /// <summary>
    /// Gets or sets the events raised as correlation state is stored and retrieved.
    /// </summary>
    public CorrelationStateCacheEvents Events { get; set; } = new CorrelationStateCacheEvents();

    /// <summary>
    /// Adds OAuth login state to the cache.
    /// </summary>
    /// <param name="correlationId">The correlation identifier used as the key.</param>
    /// <param name="state">The state to store.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is <see langword="null"/>.</exception>
    public async Task AddOAuthLoginState(Guid correlationId, OAuthLoginState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        CorrelationStateSettingContext context = new(state.ToJson());
        await Events.PreStoring(context).ConfigureAwait(false);

        await _database.StringSetAsync(
            CorrelationStateKey(correlationId),
            context.State,
            _entryTimeToLive).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets unexpired OAuth login state without removing it.
    /// </summary>
    /// <param name="correlationId">The correlation identifier to retrieve.</param>
    /// <returns>The stored state, or <see langword="null"/> when it is missing, expired, or unreadable.</returns>
    public async Task<OAuthLoginState?> GetOAuthLoginState(Guid correlationId)
    {
        RedisValue storedState =
            await _database.StringGetAsync(CorrelationStateKey(correlationId)).ConfigureAwait(false);

        return await Decode(storedState.IsNull ? null : storedState.ToString()).ConfigureAwait(false);
    }

    /// <summary>
    /// Atomically gets and removes unexpired OAuth login state.
    /// </summary>
    /// <param name="correlationId">The correlation identifier to consume.</param>
    /// <returns>The stored state, or <see langword="null"/> when it is missing, expired, or unreadable.</returns>
    public async Task<OAuthLoginState?> TakeOAuthLoginState(Guid correlationId)
    {
        RedisValue storedState =
            await _database.StringGetDeleteAsync(CorrelationStateKey(correlationId)).ConfigureAwait(false);

        return await Decode(storedState.IsNull ? null : storedState.ToString()).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes OAuth login state from the cache.
    /// </summary>
    /// <param name="correlationId">The correlation identifier to remove.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RemoveCorrelationState(Guid correlationId)
    {
        await _database.KeyDeleteAsync(CorrelationStateKey(correlationId)).ConfigureAwait(false);
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
            return null;
        }
    }

    private RedisKey CorrelationStateKey(Guid correlationId) =>
        $"{_instanceName}correlation:{correlationId:N}";
}
