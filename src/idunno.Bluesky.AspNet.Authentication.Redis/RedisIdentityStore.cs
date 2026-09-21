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

using StackExchange.Redis;

namespace idunno.Bluesky.AspNet.Authentication.Redis;

/// <summary>
/// Implements an <see cref="IIdentityStore"/> using Redis.
/// </summary>
public class RedisIdentityStore : IIdentityStore
{
    private const string DefaultInstanceName = "idunno:bluesky:authentication:";

    private static readonly TimeSpan s_defaultEntryTimeToLive = TimeSpan.FromDays(7);
    private static readonly TimeSpan s_defaultRefreshLockLength = TimeSpan.FromSeconds(90);

    private readonly IDatabase _database;
    private readonly string _instanceName;
    private readonly TimeSpan _entryTimeToLive;
    private readonly TimeSpan _refreshLockLength;
    private readonly BlueskyAuthenticationMetrics _metrics;

    /// <summary>
    /// Creates a new instance of <see cref="RedisIdentityStore"/>.
    /// </summary>
    /// <param name="connectionMultiplexer">The shared Redis connection multiplexer.</param>
    /// <param name="database">The Redis database number, or <c>-1</c> to use the configured default.</param>
    /// <param name="instanceName">An optional prefix used to isolate this application's keys.</param>
    /// <param name="entryTimeToLive">The sliding time to live for stored identities.</param>
    /// <param name="refreshLockLength">The time to live for refresh locks.</param>
    /// <param name="meterFactory">An optional meter factory used to record identity store metrics.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connectionMultiplexer"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="entryTimeToLive"/> or <paramref name="refreshLockLength"/> is not positive.
    /// </exception>
    public RedisIdentityStore(
        IConnectionMultiplexer connectionMultiplexer,
        int database = -1,
        string? instanceName = null,
        TimeSpan? entryTimeToLive = null,
        TimeSpan? refreshLockLength = null,
        IMeterFactory? meterFactory = null)
    {
        ArgumentNullException.ThrowIfNull(connectionMultiplexer);

        _database = connectionMultiplexer.GetDatabase(database);
        _instanceName = instanceName ?? DefaultInstanceName;
        _entryTimeToLive = ValidateTimeToLive(entryTimeToLive ?? s_defaultEntryTimeToLive, nameof(entryTimeToLive));
        _refreshLockLength = ValidateTimeToLive(refreshLockLength ?? s_defaultRefreshLockLength, nameof(refreshLockLength));
        _metrics = new BlueskyAuthenticationMetrics(meterFactory);
    }

    /// <summary>
    /// Gets or sets the events raised as identities are stored and retrieved.
    /// </summary>
    public IdentityStoreEvents Events { get; set; } = new IdentityStoreEvents();

    /// <summary>
    /// Adds an identity to the store.
    /// </summary>
    /// <param name="claimsIdentity">The identity to add.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="claimsIdentity"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="claimsIdentity"/> has no valid DID claim.</exception>
    public async Task Add(ClaimsIdentity claimsIdentity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claimsIdentity);

        long startTimestamp = Stopwatch.GetTimestamp();
        await StoreIdentity(claimsIdentity, nameof(claimsIdentity), cancellationToken).ConfigureAwait(false);
        _metrics.RecordIdentityStoreOperation(BlueskyAuthenticationMetrics.IdentityStoreOperationAdd, startTimestamp);
    }

    /// <summary>
    /// Gets the identity stored for <paramref name="did"/>.
    /// </summary>
    /// <param name="did">The DID whose identity should be retrieved.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The stored identity, or <see langword="null"/> when no readable identity exists.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is <see langword="null"/>.</exception>
    public async Task<ClaimsIdentity?> GetIdentity(Did did, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(did);

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

    /// <summary>
    /// Removes the identity stored for <paramref name="did"/>.
    /// </summary>
    /// <param name="did">The DID whose identity should be removed.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is <see langword="null"/>.</exception>
    public async Task Remove(Did did, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(did);
        cancellationToken.ThrowIfCancellationRequested();

        long startTimestamp = Stopwatch.GetTimestamp();
        await _database.KeyDeleteAsync(IdentityKey(did)).ConfigureAwait(false);
        _metrics.RecordIdentityStoreOperation(BlueskyAuthenticationMetrics.IdentityStoreOperationRemove, startTimestamp);
    }

    /// <summary>
    /// Replaces a stored identity.
    /// </summary>
    /// <param name="identity">The replacement identity.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="identity"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="identity"/> has no valid DID claim.</exception>
    public async Task Update(ClaimsIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);

        long startTimestamp = Stopwatch.GetTimestamp();
        await StoreIdentity(identity, nameof(identity), cancellationToken).ConfigureAwait(false);
        _metrics.RecordIdentityStoreOperation(BlueskyAuthenticationMetrics.IdentityStoreOperationUpdate, startTimestamp);
    }

    /// <summary>
    /// Attempts to atomically acquire the refresh lock for <paramref name="did"/>.
    /// </summary>
    /// <param name="did">The DID whose credentials will be refreshed.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An ownership token when the lock was acquired; otherwise, <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is <see langword="null"/>.</exception>
    public async Task<string?> StartRefresh(Did did, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(did);
        cancellationToken.ThrowIfCancellationRequested();

        string refreshLockToken = Guid.NewGuid().ToString("N");
        bool acquired = await _database.StringSetAsync(
            RefreshLockKey(did),
            refreshLockToken,
            _refreshLockLength,
            When.NotExists).ConfigureAwait(false);

        return acquired ? refreshLockToken : null;
    }

    /// <summary>
    /// Releases a refresh lock when the supplied ownership token matches it.
    /// </summary>
    /// <param name="did">The DID whose refresh lock should be released.</param>
    /// <param name="refreshLockToken">The ownership token returned by <see cref="StartRefresh(Did, CancellationToken)"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is <see langword="null"/>.</exception>
    public async Task EndRefresh(Did did, string? refreshLockToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(did);
        cancellationToken.ThrowIfCancellationRequested();

        RedisKey key = RefreshLockKey(did);
        RedisValue current = await _database.StringGetAsync(key).ConfigureAwait(false);
        if (current.IsNull)
        {
            return;
        }

        string storedToken = current.ToString();
        if (!CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(storedToken),
            System.Text.Encoding.UTF8.GetBytes(refreshLockToken ?? string.Empty)))
        {
            return;
        }

        await _database.ScriptEvaluateAsync(
            """
            if redis.call('GET', KEYS[1]) == ARGV[1] then
                return redis.call('DEL', KEYS[1])
            end
            return 0
            """,
            [key],
            [storedToken]).ConfigureAwait(false);
    }

    /// <summary>
    /// Determines whether <paramref name="did"/> has an unexpired refresh lock.
    /// </summary>
    /// <param name="did">The DID to check.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when a refresh is in progress; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is <see langword="null"/>.</exception>
    public async Task<bool> IsRefreshing(Did did, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(did);
        cancellationToken.ThrowIfCancellationRequested();

        return await _database.KeyExistsAsync(RefreshLockKey(did)).ConfigureAwait(false);
    }

    private static TimeSpan ValidateTimeToLive(TimeSpan value, string paramName)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "The time to live must be positive.");
        }

        return value;
    }

    private async Task<Did> StoreIdentity(
        ClaimsIdentity claimsIdentity,
        string paramName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string? didAsString = claimsIdentity.Claims.FirstOrDefault(
            claim => claim.Type.Equals(AtProtoClaims.Did, StringComparison.Ordinal))?.Value;

        if (didAsString is null)
        {
            throw new ArgumentException("No DID claim found.", paramName);
        }

        if (!Did.TryParse(didAsString, out Did? did))
        {
            throw new ArgumentException("DID claim was not a valid DID.", paramName);
        }

        byte[] serializedIdentity;
        using (MemoryStream memoryStream = new())
        {
            using BinaryWriter writer = new(memoryStream);
            claimsIdentity.WriteTo(writer);
            writer.Flush();
            serializedIdentity = memoryStream.ToArray();
        }

        IdentityStoreSettingContext context = new(serializedIdentity);
        await Events.PreStoring(context).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        await _database.StringSetAsync(
            IdentityKey(did),
            context.Identity.ToArray(),
            _entryTimeToLive).ConfigureAwait(false);

        return did;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A corrupt stored identity must be treated as a cache miss.")]
    private async Task<ClaimsIdentity?> GetIdentityCore(Did did, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RedisKey key = IdentityKey(did);
        RedisValue storedIdentity = await _database.StringGetAsync(key).ConfigureAwait(false);
        if (storedIdentity.IsNull)
        {
            return null;
        }

        ClaimsIdentity result;
        try
        {
            byte[]? serializedIdentity = storedIdentity;
            if (serializedIdentity is null)
            {
                return null;
            }

            IdentityStoreRetrievedContext context = new(serializedIdentity);
            await Events.PostRetrieval(context).ConfigureAwait(false);

            using MemoryStream memoryStream = new(context.Identity.ToArray(), writable: false);
            using BinaryReader reader = new(memoryStream);
            result = new ClaimsIdentity(reader);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (CryptographicException)
        {
            await _database.KeyDeleteAsync(key).ConfigureAwait(false);
            _metrics.DataProtectionFailures.Add(
                1,
                new KeyValuePair<string, object?>(
                    BlueskyAuthenticationMetrics.DataProtectionSourceTagName,
                    BlueskyAuthenticationMetrics.DataProtectionSourceIdentityStore));
            return null;
        }
        catch (Exception)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _database.KeyExpireAsync(key, _entryTimeToLive).ConfigureAwait(false);

        return result;
    }

    private RedisKey IdentityKey(Did did) => $"{_instanceName}identity:{did}";

    private RedisKey RefreshLockKey(Did did) => $"{_instanceName}refresh:{did}";
}
