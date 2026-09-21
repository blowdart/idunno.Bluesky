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

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.Bluesky.AspNet.Authentication.SQLite;

/// <summary>
/// Implements an <see cref="IIdentityStore"/> using SQLite.
/// </summary>
public class SqliteIdentityStore : IIdentityStore
{
    private const string IdentitiesTable = "idunno_bluesky_identities";
    private const string RefreshLocksTable = "idunno_bluesky_refresh_locks";

    private static readonly TimeSpan s_defaultEntryTimeToLive = TimeSpan.FromDays(7);
    private static readonly TimeSpan s_defaultRefreshLockLength = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan s_defaultExpiredEntrySweepInterval = TimeSpan.FromMinutes(5);

    private readonly string _connectionString;
    private readonly TimeSpan _entryTimeToLive;
    private readonly TimeSpan _refreshLockLength;
    private readonly ExpiredEntrySweepThrottle _sweepThrottle;
    private readonly BlueskyAuthenticationMetrics _metrics;
    private readonly ILogger _logger;

    /// <summary>
    /// Creates a new instance of <see cref="SqliteIdentityStore"/>.
    /// </summary>
    /// <param name="connectionString">The connection string for the SQLite database.</param>
    /// <param name="entryTimeToLive">The sliding time to live for stored identities.</param>
    /// <param name="refreshLockLength">The time to live for refresh locks.</param>
    /// <param name="meterFactory">An optional meter factory used to record identity store metrics.</param>
    /// <param name="loggerFactory">An optional logger factory used to report refresh lock contention.</param>
    /// <param name="expiredEntrySweepInterval">
    /// How often expired identities and abandoned refresh locks are deleted, or <see cref="TimeSpan.Zero"/> to never
    /// delete them. Defaults to five minutes.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="connectionString"/> is empty or invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="entryTimeToLive"/> or <paramref name="refreshLockLength"/> is not positive, or when
    /// <paramref name="expiredEntrySweepInterval"/> is negative.
    /// </exception>
    /// <remarks>
    /// <para>
    ///   An expired identity or refresh lock is already unreadable, as every read filters on expiry, so sweeping only
    ///   reclaims the storage it occupies and changes no behaviour. Disable it when an operator reclaims the rows themselves.
    /// </para>
    /// </remarks>
    public SqliteIdentityStore(
        string connectionString,
        TimeSpan? entryTimeToLive = null,
        TimeSpan? refreshLockLength = null,
        IMeterFactory? meterFactory = null,
        ILoggerFactory? loggerFactory = null,
        TimeSpan? expiredEntrySweepInterval = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        _connectionString = new SqliteConnectionStringBuilder(connectionString).ConnectionString;
        _entryTimeToLive = ValidateTimeToLive(entryTimeToLive ?? s_defaultEntryTimeToLive, nameof(entryTimeToLive));
        _refreshLockLength = ValidateTimeToLive(refreshLockLength ?? s_defaultRefreshLockLength, nameof(refreshLockLength));
        _sweepThrottle = new ExpiredEntrySweepThrottle(
            expiredEntrySweepInterval ?? s_defaultExpiredEntrySweepInterval,
            nameof(expiredEntrySweepInterval));
        _metrics = new BlueskyAuthenticationMetrics(meterFactory);
        _logger = loggerFactory?.CreateLogger<SqliteIdentityStore>() ?? NullLogger<SqliteIdentityStore>.Instance;
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
    /// <returns>The stored identity, or <see langword="null"/> when no unexpired readable identity exists.</returns>
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

        long startTimestamp = Stopwatch.GetTimestamp();

        using SqliteConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM "idunno_bluesky_identities"
            WHERE "Did" = @did;
            """;
        command.Parameters.Add("@did", SqliteType.Text).Value = did.ToString();
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

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

        string refreshLockToken = Guid.NewGuid().ToString("N");
        long now = DateTime.UtcNow.Ticks;

        using SqliteConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "idunno_bluesky_refresh_locks" ("Did", "LockToken", "ExpiresAtUtcTicks")
            VALUES (@did, @lockToken, @expiresAtUtcTicks)
            ON CONFLICT("Did") DO UPDATE SET
                "LockToken" = excluded."LockToken",
                "ExpiresAtUtcTicks" = excluded."ExpiresAtUtcTicks"
            WHERE "idunno_bluesky_refresh_locks"."ExpiresAtUtcTicks" <= @now;
            """;
        command.Parameters.Add("@did", SqliteType.Text).Value = did.ToString();
        command.Parameters.Add("@lockToken", SqliteType.Text).Value = refreshLockToken;
        command.Parameters.Add("@expiresAtUtcTicks", SqliteType.Integer).Value = DateTime.UtcNow.Add(_refreshLockLength).Ticks;
        command.Parameters.Add("@now", SqliteType.Integer).Value = now;

        int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        if (rowsAffected != 1)
        {
            _logger.StartRefreshDenied(did);
            return null;
        }

        _logger.StartRefreshEntered(did);

        return refreshLockToken;
    }

    /// <summary>
    /// Releases a refresh lock when the supplied ownership token matches it.
    /// </summary>
    /// <param name="did">The DID whose refresh lock should be released.</param>
    /// <param name="refreshLockToken">The ownership token returned by <see cref="StartRefresh(Did, CancellationToken)"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when the caller still owned the lock and it was released; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    ///   The token comparison is part of the <c>DELETE</c> rather than a separate read, so the check and the release cannot be
    ///   interleaved with another caller acquiring the lock. <c>LockToken</c> is declared <c>COLLATE BINARY</c>, so the comparison
    ///   is byte exact.
    /// </para>
    /// </remarks>
    public async Task<bool> EndRefresh(Did did, string? refreshLockToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(did);

        if (string.IsNullOrEmpty(refreshLockToken))
        {
            _logger.EndRefreshLockNotOwned(did);
            return false;
        }

        using SqliteConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false);

        using SqliteCommand deleteCommand = connection.CreateCommand();
        deleteCommand.CommandText = """
            DELETE FROM "idunno_bluesky_refresh_locks"
            WHERE "Did" = @did
                AND "LockToken" = @refreshLockToken;
            """;
        deleteCommand.Parameters.Add("@did", SqliteType.Text).Value = did.ToString();
        deleteCommand.Parameters.Add("@refreshLockToken", SqliteType.Text).Value = refreshLockToken;

        int rowsAffected = await deleteCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        if (rowsAffected == 0)
        {
            // The lock expired, and may since have been acquired by another caller, so it is not ours to release.
            _logger.EndRefreshLockNotOwned(did);
            return false;
        }

        _logger.EndRefreshFinished(did);

        return true;
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

        using SqliteConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS(
                SELECT 1
                FROM "idunno_bluesky_refresh_locks"
                WHERE "Did" = @did
                    AND "ExpiresAtUtcTicks" > @now
            );
            """;
        command.Parameters.Add("@did", SqliteType.Text).Value = did.ToString();
        command.Parameters.Add("@now", SqliteType.Integer).Value = DateTime.UtcNow.Ticks;

        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToBoolean(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static TimeSpan ValidateTimeToLive(TimeSpan value, string paramName)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "The time to live must be positive.");
        }

        return value;
    }

    private async Task<Did> StoreIdentity(ClaimsIdentity claimsIdentity, string paramName, CancellationToken cancellationToken)
    {
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

        using SqliteConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "idunno_bluesky_identities" ("Did", "Identity", "ExpiresAtUtcTicks")
            VALUES (@did, @identity, @expiresAtUtcTicks)
            ON CONFLICT("Did") DO UPDATE SET
                "Identity" = excluded."Identity",
                "ExpiresAtUtcTicks" = excluded."ExpiresAtUtcTicks";
            """;
        command.Parameters.Add("@did", SqliteType.Text).Value = did.ToString();
        command.Parameters.Add("@identity", SqliteType.Blob).Value = context.Identity.ToArray();
        command.Parameters.Add("@expiresAtUtcTicks", SqliteType.Integer).Value = DateTime.UtcNow.Add(_entryTimeToLive).Ticks;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await SweepExpiredEntries(cancellationToken).ConfigureAwait(false);

        return did;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A corrupt stored identity must be treated as a cache miss.")]
    private async Task<ClaimsIdentity?> GetIdentityCore(Did did, CancellationToken cancellationToken)
    {
        byte[]? serializedIdentity;

        using (SqliteConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false))
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT "Identity"
                FROM "idunno_bluesky_identities"
                WHERE "Did" = @did
                    AND "ExpiresAtUtcTicks" > @now;
                """;
            command.Parameters.Add("@did", SqliteType.Text).Value = did.ToString();
            command.Parameters.Add("@now", SqliteType.Integer).Value = DateTime.UtcNow.Ticks;
            serializedIdentity = (byte[]?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }

        if (serializedIdentity is null)
        {
            return null;
        }

        ClaimsIdentity result;
        try
        {
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
            await RemoveWithoutMetrics(did, cancellationToken).ConfigureAwait(false);
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

        await RefreshIdentityExpiration(did, cancellationToken).ConfigureAwait(false);
        return result;
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

    /// <summary>
    /// Deletes expired identities and abandoned refresh locks, at most once per configured sweep interval.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    ///   Sweeping is housekeeping for the write which triggered it, and that write has already committed by the time this
    ///   runs, so a failure here is logged and swallowed rather than surfaced. Failing a sign in because a table could not
    ///   be tidied would trade a storage problem for an availability one.
    /// </para>
    /// <para>
    ///   A refresh lock is normally released by the caller which took it, so the locks collected here are the ones
    ///   abandoned by an application server which stopped mid refresh. They already grant nobody the lock, as taking one
    ///   overwrites an expired row.
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

        string table = IdentitiesTable;

        try
        {
            using SqliteConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false);

            long now = DateTime.UtcNow.Ticks;

            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = """
                    DELETE FROM "idunno_bluesky_identities"
                    WHERE "ExpiresAtUtcTicks" <= @now;
                    """;
                command.Parameters.Add("@now", SqliteType.Integer).Value = now;

                int rowsDeleted = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                _logger.ExpiredEntriesSwept(rowsDeleted, IdentitiesTable);
            }

            table = RefreshLocksTable;

            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = """
                    DELETE FROM "idunno_bluesky_refresh_locks"
                    WHERE "ExpiresAtUtcTicks" <= @now;
                    """;
                command.Parameters.Add("@now", SqliteType.Integer).Value = now;

                int rowsDeleted = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                _logger.ExpiredEntriesSwept(rowsDeleted, RefreshLocksTable);
            }
        }
        catch (OperationCanceledException)
        {
            // The caller walked away. The sweep is housekeeping, so there is nothing to report and nothing to undo.
        }
        catch (Exception ex)
        {
            _logger.ExpiredEntrySweepFailed(table, ex);
        }
    }

    private async Task RefreshIdentityExpiration(Did did, CancellationToken cancellationToken)
    {
        using SqliteConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE "idunno_bluesky_identities"
            SET "ExpiresAtUtcTicks" = @expiresAtUtcTicks
            WHERE "Did" = @did
                AND "ExpiresAtUtcTicks" > @now;
            """;
        command.Parameters.Add("@expiresAtUtcTicks", SqliteType.Integer).Value = DateTime.UtcNow.Add(_entryTimeToLive).Ticks;
        command.Parameters.Add("@did", SqliteType.Text).Value = did.ToString();
        command.Parameters.Add("@now", SqliteType.Integer).Value = DateTime.UtcNow.Ticks;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RemoveWithoutMetrics(Did did, CancellationToken cancellationToken)
    {
        using SqliteConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM "idunno_bluesky_identities"
            WHERE "Did" = @did;
            """;
        command.Parameters.Add("@did", SqliteType.Text).Value = did.ToString();
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
