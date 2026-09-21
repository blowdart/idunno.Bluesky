// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text.Json;

using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication.Events;

using MySqlConnector;

namespace idunno.Bluesky.AspNet.Authentication.MySQL;

/// <summary>
/// Implements an <see cref="ICorrelationStateCache"/> using MySQL.
/// </summary>
public class MySqlCorrelationStateCache : ICorrelationStateCache
{
    private static readonly TimeSpan s_defaultEntryTimeToLive = TimeSpan.FromMinutes(15);

    private readonly string _connectionString;
    private readonly TimeSpan _entryTimeToLive;

    /// <summary>
    /// Creates a new instance of <see cref="MySqlCorrelationStateCache"/>.
    /// </summary>
    /// <param name="connectionString">The connection string for the MySQL database.</param>
    /// <param name="entryTimeToLive">The time to live for stored correlation state.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="connectionString"/> is empty or invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="entryTimeToLive"/> is not positive.</exception>
    public MySqlCorrelationStateCache(string connectionString, TimeSpan? entryTimeToLive = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        _connectionString = new MySqlConnectionStringBuilder(connectionString).ConnectionString;
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

        using MySqlConnection connection = await OpenConnection().ConfigureAwait(false);
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO `idunno_bluesky_correlation_states` (`CorrelationId`, `State`, `ExpiresAtUtc`)
            VALUES (@correlationId, @state, @expiresAtUtc)
            ON DUPLICATE KEY UPDATE
                `State` = @state,
                `ExpiresAtUtc` = @expiresAtUtc;
            """;
        command.Parameters.Add("@correlationId", MySqlDbType.Binary).Value = correlationId.ToByteArray();
        command.Parameters.Add("@state", MySqlDbType.LongText).Value = context.State;
        command.Parameters.Add("@expiresAtUtc", MySqlDbType.DateTime).Value = DateTime.UtcNow.Add(_entryTimeToLive);

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Peeks at unexpired OAuth login state without removing it.
    /// </summary>
    /// <param name="correlationId">The correlation identifier to retrieve.</param>
    /// <returns>The stored state, or <see langword="null"/> when it is missing, expired, or unreadable.</returns>
    /// <remarks>
    /// <para>
    ///   This does not consume the state, so it must not be used to validate an OAuth callback. Use
    ///   <see cref="TakeOAuthLoginState(Guid)"/> for that.
    /// </para>
    /// </remarks>
    public async Task<OAuthLoginState?> PeekOAuthLoginState(Guid correlationId)
    {
        using MySqlConnection connection = await OpenConnection().ConfigureAwait(false);
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT `State`
            FROM `idunno_bluesky_correlation_states`
            WHERE `CorrelationId` = @correlationId
                AND `ExpiresAtUtc` > UTC_TIMESTAMP(6);
            """;
        command.Parameters.Add("@correlationId", MySqlDbType.Binary).Value = correlationId.ToByteArray();

        string? encodedState = (string?)await command.ExecuteScalarAsync().ConfigureAwait(false);
        return await Decode(encodedState).ConfigureAwait(false);
    }

    /// <summary>
    /// Atomically gets and removes unexpired OAuth login state.
    /// </summary>
    /// <param name="correlationId">The correlation identifier to consume.</param>
    /// <returns>The stored state, or <see langword="null"/> when it is missing, expired, or unreadable.</returns>
    public async Task<OAuthLoginState?> TakeOAuthLoginState(Guid correlationId)
    {
        using MySqlConnection connection = await OpenConnection().ConfigureAwait(false);
        using MySqlTransaction transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);

        string? encodedState;
        using (MySqlCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                SELECT `State`
                FROM `idunno_bluesky_correlation_states`
                WHERE `CorrelationId` = @correlationId
                    AND `ExpiresAtUtc` > UTC_TIMESTAMP(6)
                FOR UPDATE;
                """;
            command.Parameters.Add("@correlationId", MySqlDbType.Binary).Value = correlationId.ToByteArray();

            encodedState = (string?)await command.ExecuteScalarAsync().ConfigureAwait(false);
        }

        using (MySqlCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                DELETE FROM `idunno_bluesky_correlation_states`
                WHERE `CorrelationId` = @correlationId;
                """;
            command.Parameters.Add("@correlationId", MySqlDbType.Binary).Value = correlationId.ToByteArray();

            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await transaction.CommitAsync().ConfigureAwait(false);

        return await Decode(encodedState).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes OAuth login state from the cache.
    /// </summary>
    /// <param name="correlationId">The correlation identifier to remove.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RemoveCorrelationState(Guid correlationId)
    {
        using MySqlConnection connection = await OpenConnection().ConfigureAwait(false);
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM `idunno_bluesky_correlation_states`
            WHERE `CorrelationId` = @correlationId;
            """;
        command.Parameters.Add("@correlationId", MySqlDbType.Binary).Value = correlationId.ToByteArray();

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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

    private async Task<MySqlConnection> OpenConnection()
    {
        MySqlConnection connection = new(_connectionString);

        try
        {
            await connection.OpenAsync().ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
