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

using MySqlConnector;

namespace idunno.Bluesky.AspNet.Authentication.MySQL;

/// <summary>
/// Implements an <see cref="IIdentityStore"/> using MySQL.
/// </summary>
public class MySqlIdentityStore : IIdentityStore
{
    private static readonly TimeSpan s_defaultEntryTimeToLive = TimeSpan.FromDays(7);
    private static readonly TimeSpan s_defaultRefreshLockLength = TimeSpan.FromSeconds(90);

    private readonly string _connectionString;
    private readonly TimeSpan _entryTimeToLive;
    private readonly TimeSpan _refreshLockLength;
    private readonly BlueskyAuthenticationMetrics _metrics;

    /// <summary>
    /// Creates a new instance of <see cref="MySqlIdentityStore"/>.
    /// </summary>
    /// <param name="connectionString">The connection string for the MySQL database.</param>
    /// <param name="entryTimeToLive">The sliding time to live for stored identities.</param>
    /// <param name="refreshLockLength">The time to live for refresh locks.</param>
    /// <param name="meterFactory">An optional meter factory used to record identity store metrics.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="connectionString"/> is empty or invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="entryTimeToLive"/> or <paramref name="refreshLockLength"/> is not positive.
    /// </exception>
    public MySqlIdentityStore(
        string connectionString,
        TimeSpan? entryTimeToLive = null,
        TimeSpan? refreshLockLength = null,
        IMeterFactory? meterFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        _connectionString = new MySqlConnectionStringBuilder(connectionString).ConnectionString;
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

        using MySqlConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false);
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM `idunno_bluesky_identities`
            WHERE `Did` = @did;
            """;
        command.Parameters.Add("@did", MySqlDbType.VarChar).Value = did.ToString();

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

        using MySqlConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false);
        using (MySqlCommand command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO `idunno_bluesky_refresh_locks` (`Did`, `LockToken`, `ExpiresAtUtc`)
                VALUES (@did, @lockToken, @expiresAtUtc)
                ON DUPLICATE KEY UPDATE
                    `LockToken` = IF(`ExpiresAtUtc` <= UTC_TIMESTAMP(6), @lockToken, `LockToken`),
                    `ExpiresAtUtc` = IF(`ExpiresAtUtc` <= UTC_TIMESTAMP(6), @expiresAtUtc, `ExpiresAtUtc`);
                """;
            command.Parameters.Add("@did", MySqlDbType.VarChar).Value = did.ToString();
            command.Parameters.Add("@lockToken", MySqlDbType.VarChar).Value = refreshLockToken;
            command.Parameters.Add("@expiresAtUtc", MySqlDbType.DateTime).Value = DateTime.UtcNow.Add(_refreshLockLength);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        using (MySqlCommand command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT `LockToken`
                FROM `idunno_bluesky_refresh_locks`
                WHERE `Did` = @did
                    AND `ExpiresAtUtc` > UTC_TIMESTAMP(6);
                """;
            command.Parameters.Add("@did", MySqlDbType.VarChar).Value = did.ToString();

            string? storedToken = (string?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            if (storedToken is null ||
                !CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(storedToken),
                    System.Text.Encoding.UTF8.GetBytes(refreshLockToken)))
            {
                return null;
            }
        }

        return refreshLockToken;
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

        using MySqlConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false);
        using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        string? storedToken;
        using (MySqlCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                SELECT `LockToken`
                FROM `idunno_bluesky_refresh_locks`
                WHERE `Did` = @did
                FOR UPDATE;
                """;
            command.Parameters.Add("@did", MySqlDbType.VarChar).Value = did.ToString();

            storedToken = (string?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }

        if (storedToken is not null &&
            CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(storedToken),
                System.Text.Encoding.UTF8.GetBytes(refreshLockToken ?? string.Empty)))
        {
            using MySqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                DELETE FROM `idunno_bluesky_refresh_locks`
                WHERE `Did` = @did;
                """;
            command.Parameters.Add("@did", MySqlDbType.VarChar).Value = did.ToString();

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
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

        using MySqlConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false);
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS(
                SELECT 1
                FROM `idunno_bluesky_refresh_locks`
                WHERE `Did` = @did
                    AND `ExpiresAtUtc` > UTC_TIMESTAMP(6)
            );
            """;
        command.Parameters.Add("@did", MySqlDbType.VarChar).Value = did.ToString();

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

    private async Task<Did> StoreIdentity(
        ClaimsIdentity claimsIdentity,
        string paramName,
        CancellationToken cancellationToken)
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

        using MySqlConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false);
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO `idunno_bluesky_identities` (`Did`, `Identity`, `ExpiresAtUtc`)
            VALUES (@did, @identity, @expiresAtUtc)
            ON DUPLICATE KEY UPDATE
                `Identity` = @identity,
                `ExpiresAtUtc` = @expiresAtUtc;
            """;
        command.Parameters.Add("@did", MySqlDbType.VarChar).Value = did.ToString();
        command.Parameters.Add("@identity", MySqlDbType.LongBlob).Value = context.Identity.ToArray();
        command.Parameters.Add("@expiresAtUtc", MySqlDbType.DateTime).Value = DateTime.UtcNow.Add(_entryTimeToLive);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return did;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A corrupt stored identity must be treated as a cache miss.")]
    private async Task<ClaimsIdentity?> GetIdentityCore(Did did, CancellationToken cancellationToken)
    {
        byte[]? serializedIdentity;

        using (MySqlConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false))
        {
            using MySqlCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT `Identity`
                FROM `idunno_bluesky_identities`
                WHERE `Did` = @did
                    AND `ExpiresAtUtc` > UTC_TIMESTAMP(6);
                """;
            command.Parameters.Add("@did", MySqlDbType.VarChar).Value = did.ToString();

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

    private async Task<MySqlConnection> OpenConnection(CancellationToken cancellationToken)
    {
        MySqlConnection connection = new(_connectionString);

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

    private async Task RefreshIdentityExpiration(Did did, CancellationToken cancellationToken)
    {
        using MySqlConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false);
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE `idunno_bluesky_identities`
            SET `ExpiresAtUtc` = @expiresAtUtc
            WHERE `Did` = @did
                AND `ExpiresAtUtc` > UTC_TIMESTAMP(6);
            """;
        command.Parameters.Add("@expiresAtUtc", MySqlDbType.DateTime).Value = DateTime.UtcNow.Add(_entryTimeToLive);
        command.Parameters.Add("@did", MySqlDbType.VarChar).Value = did.ToString();

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RemoveWithoutMetrics(Did did, CancellationToken cancellationToken)
    {
        using MySqlConnection connection = await OpenConnection(cancellationToken).ConfigureAwait(false);
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM `idunno_bluesky_identities`
            WHERE `Did` = @did;
            """;
        command.Parameters.Add("@did", MySqlDbType.VarChar).Value = did.ToString();

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
