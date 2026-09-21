// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;

using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication.Events;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.Bluesky.AspNet.Authentication.SQLite;

/// <summary>
/// Implements an <see cref="ICorrelationStateCache"/> using SQLite.
/// </summary>
public class SqliteCorrelationStateCache : ICorrelationStateCache
{
    private const string CorrelationStatesTable = "idunno_bluesky_correlation_states";

    private static readonly TimeSpan s_defaultEntryTimeToLive = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan s_defaultExpiredEntrySweepInterval = TimeSpan.FromMinutes(5);

    private readonly string _connectionString;
    private readonly TimeSpan _entryTimeToLive;
    private readonly ExpiredEntrySweepThrottle _sweepThrottle;
    private readonly ILogger _logger;

    /// <summary>
    /// Creates a new instance of <see cref="SqliteCorrelationStateCache"/>.
    /// </summary>
    /// <param name="connectionString">The connection string for the SQLite database.</param>
    /// <param name="entryTimeToLive">The time to live for stored correlation state.</param>
    /// <param name="expiredEntrySweepInterval">
    /// How often expired correlation state is deleted, or <see cref="TimeSpan.Zero"/> to never delete it. Defaults to five minutes.
    /// </param>
    /// <param name="loggerFactory">An optional logger factory used to report sweep activity and failures.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="connectionString"/> is empty or invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="entryTimeToLive"/> is not positive, or <paramref name="expiredEntrySweepInterval"/> is negative.
    /// </exception>
    /// <remarks>
    /// <para>
    ///   Expired correlation state is unreadable as soon as it expires, as every read filters on expiry. Sweeping only
    ///   reclaims the storage it occupies, so disabling it changes no behaviour beyond letting the table grow. Disable it
    ///   when an operator reclaims the rows themselves.
    /// </para>
    /// </remarks>
    public SqliteCorrelationStateCache(
        string connectionString,
        TimeSpan? entryTimeToLive = null,
        TimeSpan? expiredEntrySweepInterval = null,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        _connectionString = new SqliteConnectionStringBuilder(connectionString).ConnectionString;
        _entryTimeToLive = entryTimeToLive ?? s_defaultEntryTimeToLive;

        if (_entryTimeToLive <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(entryTimeToLive), _entryTimeToLive, "The time to live must be positive.");
        }

        _sweepThrottle = new ExpiredEntrySweepThrottle(
            expiredEntrySweepInterval ?? s_defaultExpiredEntrySweepInterval,
            nameof(expiredEntrySweepInterval));
        _logger = loggerFactory?.CreateLogger<SqliteCorrelationStateCache>() ?? NullLogger<SqliteCorrelationStateCache>.Instance;
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
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is <see langword="null"/>.</exception>
    public async Task AddOAuthLoginState(Guid correlationId, OAuthLoginState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        CorrelationStateSettingContext context = new(state.ToJson());
        await Events.PreStoring(context).ConfigureAwait(false);

        using SqliteConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "idunno_bluesky_correlation_states" ("CorrelationId", "State", "ExpiresAtUtcTicks")
            VALUES (@correlationId, @state, @expiresAtUtcTicks)
            ON CONFLICT("CorrelationId") DO UPDATE SET
                "State" = excluded."State",
                "ExpiresAtUtcTicks" = excluded."ExpiresAtUtcTicks";
            """;
        command.Parameters.Add("@correlationId", SqliteType.Blob).Value = correlationId.ToByteArray();
        command.Parameters.Add("@state", SqliteType.Text).Value = context.State;
        command.Parameters.Add("@expiresAtUtcTicks", SqliteType.Integer).Value = DateTime.UtcNow.Add(_entryTimeToLive).Ticks;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await SweepExpiredEntries(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Peeks at unexpired OAuth login state without removing it.
    /// </summary>
    /// <param name="correlationId">The correlation identifier to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The stored state, or <see langword="null"/> when it is missing, expired, or unreadable.</returns>
    /// <remarks>
    /// <para>
    ///   This does not consume the state, so it must not be used to validate an OAuth callback. Use
    ///   <see cref="TakeOAuthLoginState(Guid, CancellationToken)"/> for that.
    /// </para>
    /// </remarks>
    public async Task<OAuthLoginState?> PeekOAuthLoginState(Guid correlationId, CancellationToken cancellationToken = default)
    {
        using SqliteConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT "State"
            FROM "idunno_bluesky_correlation_states"
            WHERE "CorrelationId" = @correlationId
                AND "ExpiresAtUtcTicks" > @now;
            """;
        command.Parameters.Add("@correlationId", SqliteType.Blob).Value = correlationId.ToByteArray();
        command.Parameters.Add("@now", SqliteType.Integer).Value = DateTime.UtcNow.Ticks;

        string? encodedState = (string?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return await Decode(encodedState).ConfigureAwait(false);
    }

    /// <summary>
    /// Atomically gets and removes unexpired OAuth login state.
    /// </summary>
    /// <param name="correlationId">The correlation identifier to consume.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The stored state, or <see langword="null"/> when it is missing, expired, or unreadable.</returns>
    public async Task<OAuthLoginState?> TakeOAuthLoginState(Guid correlationId, CancellationToken cancellationToken = default)
    {
        using SqliteConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM "idunno_bluesky_correlation_states"
            WHERE "CorrelationId" = @correlationId
            RETURNING CASE
                WHEN "ExpiresAtUtcTicks" > @now THEN "State"
                ELSE NULL
            END;
            """;
        command.Parameters.Add("@correlationId", SqliteType.Blob).Value = correlationId.ToByteArray();
        command.Parameters.Add("@now", SqliteType.Integer).Value = DateTime.UtcNow.Ticks;

        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return await Decode(result as string).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes OAuth login state from the cache.
    /// </summary>
    /// <param name="correlationId">The correlation identifier to remove.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RemoveCorrelationState(Guid correlationId, CancellationToken cancellationToken = default)
    {
        using SqliteConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM "idunno_bluesky_correlation_states"
            WHERE "CorrelationId" = @correlationId;
            """;
        command.Parameters.Add("@correlationId", SqliteType.Blob).Value = correlationId.ToByteArray();
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Deletes expired correlation state, at most once per configured sweep interval.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    ///   Sweeping is housekeeping for the write which triggered it, and that write has already committed by the time this
    ///   runs, so a failure here is logged and swallowed rather than surfaced. Failing a sign in because a table could not
    ///   be tidied would trade a storage problem for an availability one.
    /// </para>
    /// <para>
    ///   A cancelled sweep is abandoned rather than retried. The sweep interval has already been claimed, so the rows it
    ///   would have removed are simply collected by the next sweep.
    /// </para>
    /// </remarks>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Sweeping is best effort housekeeping, failures are logged and the triggering operation continues.")]
    private async Task SweepExpiredEntries(CancellationToken cancellationToken)
    {
        if (!_sweepThrottle.TryClaimSweep())
        {
            return;
        }

        try
        {
            using SqliteConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                DELETE FROM "idunno_bluesky_correlation_states"
                WHERE "ExpiresAtUtcTicks" <= @now;
                """;
            command.Parameters.Add("@now", SqliteType.Integer).Value = DateTime.UtcNow.Ticks;

            int rowsDeleted = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _logger.ExpiredEntriesSwept(rowsDeleted, CorrelationStatesTable);
        }
        catch (OperationCanceledException)
        {
            // The caller walked away. The sweep is housekeeping, so there is nothing to report and nothing to undo.
        }
        catch (Exception ex)
        {
            _logger.ExpiredEntrySweepFailed(CorrelationStatesTable, ex);
        }
    }

    private async Task<SqliteConnection> OpenConnection(CancellationToken cancellationToken)
    {
        SqliteConnection connection = new(_connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
