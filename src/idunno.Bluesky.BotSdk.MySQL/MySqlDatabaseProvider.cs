// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto;
using idunno.AtProto.Jetstream.Archive;

using MySqlConnector;

namespace idunno.Bluesky.BotSdk.DataProviders;

/// <summary>Persists durable bot state in MySQL 8.4 or later using InnoDB transactions.</summary>
/// <param name="connectionString">The MySQL connection string; use verified TLS outside local development.</param>
/// <param name="options">The runtime configuration bound to this database.</param>
/// <param name="time">The clock used for initial witnessed-time eligibility.</param>
/// <param name="logger">The provider diagnostic logger.</param>
/// <remarks><para>All lease-protected operations lock the ownership row, serializing mutations with renewals and takeover.</para></remarks>
public sealed class MySqlDatabaseProvider(
    string connectionString,
    BotOptions options,
    TimeProvider time,
    ILogger<MySqlDatabaseProvider> logger) : IDatabaseProvider, IBotDatabaseDiagnostics
{
    private static readonly JsonTypeInfo<SnapshotRequest> s_snapshotRequestTypeInfo =
        BotJson.SnapshotRequest;
    private static readonly JsonTypeInfo<SnapshotCheckpoint> s_snapshotCheckpointTypeInfo =
        BotJson.SnapshotCheckpoint;

    private static readonly Action<ILogger, int, Exception?> s_pruned =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(14, "Pruned"),
            "Pruned {Count} expired rate reservations, skip audits, and safely covered terminal actions.");

    private readonly string _connectionString = Configure(connectionString);
    private readonly BotOptions _options = options;
    private readonly TimeProvider _time = time;
    private readonly ILogger _logger = logger;
    private const string NowSql = "TIMESTAMPDIFF(MICROSECOND, '1970-01-01', UTC_TIMESTAMP(6))";

    /// <summary>Applies bounded database waits and UTC date handling to the host's connection settings.</summary>
    /// <param name="connectionString">The host-supplied settings, including database identity and TLS configuration.</param>
    /// <returns>The connection string with a small pool and finite connection and command timeouts.</returns>
    /// <remarks><para>These limits bound dependency stalls without replacing the host's server or security choices.</para></remarks>
    private static string Configure(string connectionString)
    {
        MySqlConnectionStringBuilder builder = new(connectionString)
        {
            MaximumPoolSize = 5,
            ConnectionTimeout = 10,
            DefaultCommandTimeout = 15,
            DateTimeKind = MySqlDateTimeKind.Utc
        };

        return builder.ConnectionString;
    }

    /// <summary>Gets the unique identity of this provider's process owner.</summary>
    public Guid Owner { get; } = Guid.NewGuid();

    /// <summary>Determines whether an exception originated from the MySQL client.</summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns><see langword="true"/> for a MySQL exception; otherwise, <see langword="false"/>.</returns>
    public bool IsDatabaseException(Exception exception) => exception is MySqlException;

    /// <summary>Determines whether a MySQL failure permits retry under the recovery policy.</summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns><see langword="true"/> for a transient MySQL failure; otherwise, <see langword="false"/>.</returns>
    public bool IsTransientDatabaseException(Exception exception) => exception is MySqlException { IsTransient: true };

    /// <summary>Creates a connection using this provider's bounded settings.</summary>
    /// <returns>The unopened connection; the caller opens and disposes it.</returns>
    private MySqlConnection CreateConnection() => new(_connectionString);

    /// <summary>Starts a transaction whose reads see committed data while explicit row locks fence ownership.</summary>
    /// <param name="connection">The open connection for the operation.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The transaction that the caller commits or disposes to roll back.</returns>
    private static async Task<DbTransaction> BeginAsync(MySqlConnection connection, CancellationToken token)
    {
        return await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token).ConfigureAwait(false);
    }

    /// <summary>Builds a bounded command with separately bound values rather than SQL string interpolation.</summary>
    /// <param name="connection">The open connection that owns the command.</param>
    /// <param name="transaction">The active transaction, or <see langword="null"/> for schema initialization commands.</param>
    /// <param name="sql">The provider-owned SQL statement containing parameter placeholders.</param>
    /// <param name="parameters">The named values; missing values are stored as database nulls.</param>
    /// <returns>The command that the caller executes and disposes.</returns>
    private static DbCommand Command(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        DbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
#pragma warning disable CA2100 // The caller supplies only provider-owned SQL; data values are always bound as parameters.
        command.CommandText = sql;
#pragma warning restore CA2100
        command.CommandTimeout = 15;
        foreach ((string name, object? value) in parameters)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        return command;
    }

    /// <summary>Executes a parameterized mutation and disposes its command.</summary>
    /// <param name="connection">The open connection for the mutation.</param>
    /// <param name="transaction">The transaction containing the mutation, or <see langword="null"/> for schema changes.</param>
    /// <param name="sql">The provider-owned SQL statement.</param>
    /// <param name="token">The cancellation token.</param>
    /// <param name="parameters">The named values bound to SQL placeholders.</param>
    /// <returns>The affected-row count reported by the database.</returns>
    private static async Task<int> ExecuteAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        CancellationToken token,
        params (string Name, object? Value)[] parameters)
    {
        DbCommand command = Command(connection, transaction, sql, parameters);
        await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);

        return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    /// <summary>Reads one database value, translating a database null into an application null.</summary>
    /// <param name="connection">The open connection for the read.</param>
    /// <param name="transaction">The transaction keeping the read consistent with its related operation.</param>
    /// <param name="sql">The provider-owned query.</param>
    /// <param name="token">The cancellation token.</param>
    /// <param name="parameters">The named values bound to query placeholders.</param>
    /// <returns>The first value, or <see langword="null"/> for a null value or an empty result.</returns>
    private static async Task<object?> ScalarAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken token,
        params (string Name, object? Value)[] parameters)
    {
        DbCommand command = Command(connection, transaction, sql, parameters);
        await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
        object? result = await command.ExecuteScalarAsync(token).ConfigureAwait(false);

        return result is DBNull ? null : result;
    }

    /// <summary>Converts a required numeric database value without depending on the host's culture.</summary>
    /// <param name="value">The required database value.</param>
    /// <returns>The value as a signed 64-bit integer.</returns>
    /// <exception cref="InvalidOperationException">The required durable value is missing.</exception>
    private static long Integer(object? value) =>
        Convert.ToInt64(value ?? throw new InvalidOperationException("Missing durable state."), CultureInfo.InvariantCulture);
    /// <summary>Converts an instant to the UTC microsecond representation used by stored deadlines.</summary>
    /// <param name="value">The instant to store.</param>
    /// <returns>The microseconds since the Unix epoch, truncating sub-microsecond precision.</returns>
    private static long Micros(DateTimeOffset value) => (value.UtcTicks - DateTimeOffset.UnixEpoch.Ticks) / 10;

    /// <summary>Restores an instant from a stored UTC microsecond value.</summary>
    /// <param name="value">The microseconds since the Unix epoch.</param>
    /// <returns>The UTC instant represented by the saved value.</returns>
    private static DateTimeOffset Timestamp(long value) => DateTimeOffset.UnixEpoch.AddTicks(checked(value * 10));

    /// <summary>Produces a fixed-size index key for a source URI without truncating the saved URI.</summary>
    /// <param name="uri">The complete source URI.</param>
    /// <returns>The SHA-256 hash of its UTF-8 bytes.</returns>
    /// <remarks><para>The enqueue path compares the saved URI as well as this hash and rejects a collision.
    /// Row updates also match the complete URI, kind, and action key; a hash alone is not the action identity.</para></remarks>
    private static byte[] SourceHash(string uri) => SHA256.HashData(Encoding.UTF8.GetBytes(uri));

    /// <summary>Runs a storage operation atomically, checking the lease first when ownership is required.</summary>
    /// <typeparam name="T">The operation's result type.</typeparam>
    /// <param name="owned"><see langword="true"/> to require this process's ready lease; otherwise, <see langword="false"/> for ownership acquisition or release.</param>
    /// <param name="action">The operation receiving the connection, transaction, and current database time in microseconds.</param>
    /// <param name="token">The cancellation token for database work and commit.</param>
    /// <returns>The result after its transaction commits.</returns>
    /// <exception cref="OwnershipLostException">The required ownership lease is unavailable or has insufficient time remaining.</exception>
    /// <remarks><para>Owned operations lock the lease row before reading database time, so waiting for another
    /// writer cannot admit work using a stale clock. Exceptions roll back all uncommitted effects.</para></remarks>
    private async Task<T> TransactionAsync<T>(
        bool owned,
        Func<DbConnection, DbTransaction, long, Task<T>> action,
        CancellationToken token)
    {
        MySqlConnection connection = CreateConnection();
        await using ConfiguredAsyncDisposable connectionDisposal = connection.ConfigureAwait(false);
        await connection.OpenAsync(token).ConfigureAwait(false);
        DbTransaction transaction = await BeginAsync(connection, token).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable transactionDisposal = transaction.ConfigureAwait(false);
        if (owned)
        {
            // Lock first, then read the clock: waiting on a writer must not admit work using stale time.
            object? owner = await ScalarAsync(
                connection,
                transaction,
                "SELECT owner FROM bot_ownership WHERE id = 1 FOR UPDATE",
                token).ConfigureAwait(false);
            long now = Integer(await ScalarAsync(connection, transaction, $"SELECT {NowSql}", token).ConfigureAwait(false));
            object? valid = await ScalarAsync(
                connection,
                transaction,
                "SELECT 1 FROM bot_ownership WHERE id = 1 AND owner = @owner AND lease_until > @reserve AND ready_at <= @now",
                token,
                ("@owner", Owner.ToString("N")),
                ("@reserve", now + 35_000_000),
                ("@now", now)).ConfigureAwait(false);
            if (owner is null || valid is null)
            {
                throw new OwnershipLostException();
            }

            T result = await action(connection, transaction, now).ConfigureAwait(false);
            await transaction.CommitAsync(token).ConfigureAwait(false);

            return result;
        }

        long clock = Integer(await ScalarAsync(connection, transaction, $"SELECT {NowSql}", token).ConfigureAwait(false));
        T unownedResult = await action(connection, transaction, clock).ConfigureAwait(false);
        await transaction.CommitAsync(token).ConfigureAwait(false);

        return unownedResult;
    }

    /// <summary>Initializes MySQL storage without deleting existing state.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="TimeoutException">The schema initialization lock was not acquired before its deadline.</exception>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        MySqlConnection connection = CreateConnection();
        await using ConfiguredAsyncDisposable connectionDisposal = connection.ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        // MySQL commits DDL implicitly, so this connection-scoped lock serializes schema initialization.
        DbCommand schemaLock = Command(
            connection,
            null,
            "SELECT GET_LOCK(CONCAT('bot-schema-', LEFT(SHA2(DATABASE(), 256), 40)), 15)");
        await using ConfiguredAsyncDisposable schemaLockDisposal = schemaLock.ConfigureAwait(false);
        if (Integer(await schemaLock.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) != 1)
        {
            throw new TimeoutException("Could not acquire MySQL schema initialization lock.");
        }
        try
        {
            foreach (string statement in Schema())
            {
                await ExecuteAsync(connection, null, statement, cancellationToken).ConfigureAwait(false);
            }
            // Keep legacy queued replies as the default reply identity. Each DDL step is
            // repeatable because MySQL commits schema changes independently.
            foreach ((string name, string definition) in new (string, string)[]
            {
                ("action_kind", "VARCHAR(128) COLLATE utf8mb4_0900_bin NOT NULL DEFAULT 'reply' CHECK(CHAR_LENGTH(action_kind) BETWEEN 1 AND 128)"),
                ("action_key", "VARCHAR(128) COLLATE utf8mb4_0900_bin NOT NULL DEFAULT 'default' CHECK(CHAR_LENGTH(action_key) BETWEEN 1 AND 128)"),
                ("payload_version", "INTEGER NOT NULL DEFAULT 1 CHECK(payload_version > 0)")
            })
            {
                DbCommand column = Command(
                    connection,
                    null,
                    "SELECT count(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'reply_work' AND column_name = @name",
                    ("@name", name));
                await using ConfiguredAsyncDisposable columnDisposal = column.ConfigureAwait(false);
                if (Integer(await column.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) == 0)
                {
                    await ExecuteAsync(
                        connection,
                        null,
                        $"ALTER TABLE reply_work ADD COLUMN {name} {definition}",
                        cancellationToken).ConfigureAwait(false);
                }
            }

            DbCommand primary = Command(
                connection,
                null,
                "SELECT count(*) FROM information_schema.key_column_usage WHERE table_schema = DATABASE() AND table_name = 'reply_work' AND constraint_name = 'PRIMARY'");
            await using ConfiguredAsyncDisposable primaryDisposal = primary.ConfigureAwait(false);
            if (Integer(await primary.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) == 1)
            {
                await ExecuteAsync(
                    connection,
                    null,
                    "ALTER TABLE reply_work DROP PRIMARY KEY, ADD PRIMARY KEY(source_hash, action_kind, action_key)",
                    cancellationToken).ConfigureAwait(false);
            }

            List<string> recordKeyIndexes = [];
            DbCommand indexes = Command(
                connection,
                null,
                """
                SELECT index_name FROM information_schema.statistics
                WHERE table_schema = DATABASE() AND table_name = 'reply_work' AND non_unique = 0
                    AND index_name <> 'PRIMARY'
                GROUP BY index_name HAVING count(*) = 1 AND min(column_name) = 'record_key'
                """);
            await using ConfiguredAsyncDisposable indexesDisposal = indexes.ConfigureAwait(false);
#pragma warning disable S1199 // The reader must close before schema migration issues its next command.
            {
                DbDataReader reader = await indexes.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    recordKeyIndexes.Add(reader.GetString(0));
                }
            }
#pragma warning restore S1199
            foreach (string index in recordKeyIndexes)
            {
                await ExecuteAsync(
                    connection,
                    null,
                    $"ALTER TABLE reply_work DROP INDEX `{index.Replace("`", "``", StringComparison.Ordinal)}`",
                    cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            DbCommand release = Command(
                connection,
                null,
                "SELECT RELEASE_LOCK(CONCAT('bot-schema-', LEFT(SHA2(DATABASE(), 256), 40)))");
            await using ConfiguredAsyncDisposable releaseDisposal = release.ConfigureAwait(false);
            await release.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>Checks MySQL connectivity without changing durable state.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        MySqlConnection connection = CreateConnection();
        await using ConfiguredAsyncDisposable connectionDisposal = connection.ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        DbCommand command = Command(connection, null, "SELECT 1");
        await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
        _ = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Supplies repeatable creation statements for the provider's durable tables.</summary>
    /// <returns>The ordered SQL statements that initialize missing tables without deleting saved state.</returns>
    /// <remarks><para>Initialization holds a named schema lock because MySQL DDL commits independently.
    /// The separate migration steps retain legacy rows and remove obsolete record-key uniqueness.</para></remarks>
    private static string[] Schema()
    {
        return [
            "CREATE TABLE IF NOT EXISTS bot_ownership (id INTEGER PRIMARY KEY CHECK(id = 1), owner VARCHAR(32) NOT NULL, lease_until BIGINT NOT NULL, ready_at BIGINT NOT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin",
            "CREATE TABLE IF NOT EXISTS bot_state (id INTEGER PRIMARY KEY CHECK(id = 1), started_at BIGINT NOT NULL, service LONGTEXT NOT NULL, bot_did LONGTEXT NOT NULL, developer_mode INTEGER NOT NULL, request LONGTEXT, checkpoint LONGTEXT, processed_through BIGINT NOT NULL DEFAULT 0, credentials LONGBLOB, archive_cooldown BIGINT NOT NULL DEFAULT 0) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin",
            "CREATE TABLE IF NOT EXISTS reply_work (source_hash BINARY(32) NOT NULL, source_uri LONGTEXT NOT NULL, action_kind VARCHAR(128) COLLATE utf8mb4_0900_bin NOT NULL DEFAULT 'reply' CHECK(CHAR_LENGTH(action_kind) BETWEEN 1 AND 128), action_key VARCHAR(128) COLLATE utf8mb4_0900_bin NOT NULL DEFAULT 'default' CHECK(CHAR_LENGTH(action_key) BETWEEN 1 AND 128), payload_version INTEGER NOT NULL DEFAULT 1 CHECK(payload_version > 0), source_sequence BIGINT NOT NULL, source_did LONGTEXT NOT NULL, record_key VARCHAR(128) NOT NULL, record JSON NOT NULL, root_uri LONGTEXT, status VARCHAR(16) NOT NULL DEFAULT 'pending' CHECK(status IN ('pending','uncertain','sent','not-sent','simulated')), attempts INTEGER NOT NULL DEFAULT 0, next_retry BIGINT NOT NULL, outcome LONGTEXT, reply_uri LONGTEXT, completed_at BIGINT, target_deleted INTEGER NOT NULL DEFAULT 0, PRIMARY KEY(source_hash, action_kind, action_key)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin",
            "CREATE TABLE IF NOT EXISTS rate_accounting (id BIGINT PRIMARY KEY AUTO_INCREMENT, requested_at BIGINT NOT NULL, points INTEGER NOT NULL, session_create INTEGER NOT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin",
            "CREATE TABLE IF NOT EXISTS rate_state (id INTEGER PRIMARY KEY CHECK(id = 1), next_request BIGINT NOT NULL DEFAULT 0, cooldown_until BIGINT NOT NULL DEFAULT 0) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin",
            "INSERT INTO rate_state(id) VALUES(1) ON DUPLICATE KEY UPDATE id = id",
            "CREATE TABLE IF NOT EXISTS skipped_data (id BIGINT PRIMARY KEY AUTO_INCREMENT, recorded_at BIGINT NOT NULL, sequence BIGINT, scope LONGTEXT NOT NULL, reason LONGTEXT NOT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin"
        ];
    }

    /// <summary>Acquires or renews ownership using database time and a deployment drain period.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The time at which this owner may work, or <see langword="null"/> when another process owns the lease.</returns>
    public Task<DateTimeOffset?> AcquireOrRenewAsync(CancellationToken cancellationToken) =>
        TransactionAsync<DateTimeOffset?>(
            false,
            async (connection, transaction, _) =>
        {
            // Seed an expired row; conflicting inserts serialize simultaneous first owners.
            await ExecuteAsync(
                connection,
                transaction,
                "INSERT INTO bot_ownership(id, owner, lease_until, ready_at) VALUES(1, '', 0, 0) ON DUPLICATE KEY UPDATE id = id",
                cancellationToken).ConfigureAwait(false);
            DbCommand read = Command(
                connection,
                transaction,
                "SELECT owner, lease_until, ready_at FROM bot_ownership WHERE id = 1 FOR UPDATE");
            await using ConfiguredAsyncDisposable readDisposal = read.ConfigureAwait(false);
            string owner;
            long expiry;
            long ready;
#pragma warning disable S1199 // Ownership must be read and its reader closed before updating the lease.
            {
                DbDataReader reader = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) { throw new InvalidOperationException("Missing ownership row."); }
                    owner = reader.GetString(0);
                    expiry = reader.GetInt64(1);
                    ready = reader.GetInt64(2);
                }
            }
#pragma warning restore S1199
            long now = Integer(await ScalarAsync(connection, transaction, $"SELECT {NowSql}", cancellationToken).ConfigureAwait(false));
            if (owner != Owner.ToString("N") && expiry > now) { return null; }
            ready = owner == Owner.ToString("N") && expiry > now ? ready : now + 40_000_000;
            await ExecuteAsync(
                connection,
                transaction,
                "UPDATE bot_ownership SET owner = @owner, lease_until = @expiry, ready_at = @ready WHERE id = 1",
                cancellationToken,
                ("@owner", Owner.ToString("N")),
                ("@expiry", now + 90_000_000),
                ("@ready", ready)).ConfigureAwait(false);

            return Timestamp(ready);
        },
            cancellationToken);

    /// <summary>Releases this provider's lease without releasing another owner's lease.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task ReleaseAsync(CancellationToken cancellationToken) =>
        TransactionAsync(
            false,
            async (connection, transaction, _) =>
            await ExecuteAsync(
                connection,
                transaction,
                $"UPDATE bot_ownership SET lease_until = {NowSql} WHERE id = 1 AND owner = @owner",
                cancellationToken,
                ("@owner", Owner.ToString("N"))).ConfigureAwait(false),
            cancellationToken);

    /// <summary>Loads or initializes durable replay state and validates its account and service identity.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The complete saved replay state.</returns>
    /// <exception cref="InvalidOperationException">Stored replay state differs from the configured account or service.</exception>
    public Task<ReplayState> LoadStateAsync(CancellationToken cancellationToken) =>
        TransactionAsync(
            true,
            async (connection, transaction, _) =>
        {
            await ExecuteAsync(
                connection,
                transaction,
                "INSERT INTO bot_state(id, started_at, service, bot_did, developer_mode) VALUES(1, @start, @service, @did, @mode) ON DUPLICATE KEY UPDATE id = id",
                cancellationToken,
                ("@start", Micros(_time.GetUtcNow())),
                ("@service", _options.JetstreamService),
                ("@did", _options.Did),
                ("@mode", _options.DeveloperMode ? 1 : 0)).ConfigureAwait(false);
            DbCommand command = Command(
                connection,
                transaction,
                "SELECT started_at, service, bot_did, developer_mode, request, checkpoint, processed_through FROM bot_state WHERE id = 1");
            await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
            DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) { throw new InvalidOperationException("Missing singleton bot state."); }
            ReplayState state = new(
                Timestamp(reader.GetInt64(0)),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3) != 0,
                await reader.IsDBNullAsync(4, cancellationToken).ConfigureAwait(false) ? null : reader.GetString(4),
                await reader.IsDBNullAsync(5, cancellationToken).ConfigureAwait(false) ? null : reader.GetString(5),
                reader.GetInt64(6));
            if (state.Service != _options.JetstreamService || state.BotDid != _options.Did || state.DeveloperMode != _options.DeveloperMode)
            {
                throw new InvalidOperationException("Stored service, bot DID, or developer mode differs. Use a separate database; do not reinterpret existing progress or work.");
            }

            return state;
        },
            cancellationToken);

    /// <summary>Reads a point-in-time operational snapshot without changing durable state.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Durable progress and counts for pending, uncertain, and retained terminal actions.</returns>
    /// <exception cref="InvalidOperationException">The operational snapshot query returned no row.</exception>
    public async Task<BotOperationalSnapshot> GetOperationalSnapshotAsync(CancellationToken cancellationToken)
    {
        MySqlConnection connection = CreateConnection();
        await using ConfiguredAsyncDisposable connectionDisposal = connection.ConfigureAwait(false);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        DbCommand command = Command(
            connection,
            null,
            $"""
            SELECT
                COALESCE((SELECT processed_through FROM bot_state WHERE id = 1), 0),
                COUNT(CASE WHEN status = 'pending' THEN 1 END),
                COUNT(CASE WHEN status = 'uncertain' THEN 1 END),
                COUNT(CASE WHEN status IN ('pending', 'uncertain') AND next_retry <= {NowSql} THEN 1 END),
                COUNT(CASE WHEN status = 'sent' THEN 1 END),
                COUNT(CASE WHEN status = 'not-sent' THEN 1 END),
                COUNT(CASE WHEN status = 'simulated' THEN 1 END)
            FROM reply_work
            """);
        await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
        DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Operational snapshot query returned no row.");
        }

        return new(
            Integer(reader.GetValue(0)),
            Integer(reader.GetValue(1)),
            Integer(reader.GetValue(2)),
            Integer(reader.GetValue(3)),
            Integer(reader.GetValue(4)),
            Integer(reader.GetValue(5)),
            Integer(reader.GetValue(6)));
    }

    /// <summary>Saves the original replay request only when no request has already been saved.</summary>
    /// <param name="request">The complete original request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    public Task<int> SaveRequestAsync(SnapshotRequest request, CancellationToken cancellationToken) =>
        TransactionAsync(
            true,
            async (connection, transaction, _) =>
        {
            if (request.AfterSeq is not > 0) { throw new InvalidOperationException("Initial replay must have a positive, deliberately chosen sequence boundary."); }

            return await ExecuteAsync(
                connection,
                transaction,
                "UPDATE bot_state SET request = @request, processed_through = @after WHERE id = 1 AND request IS NULL",
                cancellationToken,
                ("@request", JsonSerializer.Serialize(request, s_snapshotRequestTypeInfo)),
                ("@after", request.AfterSeq.Value)).ConfigureAwait(false);
        },
            cancellationToken);

    /// <summary>Commits event effects and processed-through progress in one transaction.</summary>
    /// <param name="sequence">The event sequence, not a timestamp.</param>
    /// <param name="actions">The complete actions to enqueue, or an empty list for an event without actions.</param>
    /// <param name="malformedReason">The reason for auditing a malformed post, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="deletedUri">The deleted source URI to mark, or <see langword="null"/>.</param>
    /// <returns>The number of newly inserted actions, excluding existing composite identities.</returns>
    /// <exception cref="ArgumentException">An action kind or key is empty or longer than 128 characters, its payload version is not positive, or a new action has a different source sequence.</exception>
    public Task<int> HandleAsync(
        long sequence,
        IReadOnlyList<BotAction> actions,
        string? malformedReason,
        CancellationToken cancellationToken,
        AtUri? deletedUri = null) =>
        TransactionAsync(
            true,
            async (connection, transaction, now) =>
        {
            long processed = Integer(await ScalarAsync(
                connection,
                transaction,
                "SELECT processed_through FROM bot_state WHERE id = 1",
                cancellationToken).ConfigureAwait(false));
            if (sequence <= processed) { return 0; }
            if (deletedUri is not null)
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    "UPDATE reply_work SET target_deleted = 1 WHERE (source_uri = @uri OR root_uri = @uri) AND status IN ('pending','uncertain')",
                    cancellationToken,
                    ("@uri", deletedUri.ToString())).ConfigureAwait(false);
            }
            int inserted = 0;
            foreach (BotAction work in actions)
            {
                ValidateAction(work);
                byte[] hash = SourceHash(work.SourceUri.ToString());
                string? existing = (string?)await ScalarAsync(
                    connection,
                    transaction,
                    "SELECT source_uri FROM reply_work WHERE source_hash = @hash LIMIT 1",
                    cancellationToken,
                    ("@hash", hash)).ConfigureAwait(false);
                if (existing is not null && existing != work.SourceUri.ToString()) { throw new InvalidDataException("Source URI hash collision."); }
                object? identity = await ScalarAsync(
                    connection,
                    transaction,
                    "SELECT 1 FROM reply_work WHERE source_hash = @hash AND action_kind = @kind AND action_key = @action",
                    cancellationToken,
                    ("@hash", hash),
                    ("@kind", work.ActionKind),
                    ("@action", work.ActionKey)).ConfigureAwait(false);
                if (identity is null)
                {
                    if (work.SourceSequence != sequence)
                    {
#pragma warning disable S3928 // The action list is the public argument validated in this transaction callback.
                        throw new ArgumentException(
                            "A new action must have the event's source sequence.",
                            nameof(actions));
#pragma warning restore S3928
                    }

                    using JsonDocument record = JsonDocument.Parse(work.RecordJson);
                    string? root = record.RootElement.TryGetProperty("reply", out JsonElement reply) &&
                        reply.TryGetProperty("root", out JsonElement reference) &&
                        reference.TryGetProperty("uri", out JsonElement uri) ? uri.GetString() : null;
                    inserted += await ExecuteAsync(
                        connection,
                        transaction,
                        """
                        INSERT INTO reply_work(source_hash, source_uri, source_sequence, source_did, record_key,
                            record, root_uri, next_retry, action_kind, action_key, payload_version, status, attempts, target_deleted)
                        VALUES(@hash,@uri,@seq,@did,@key,@record,@root,@now,@kind,@action,@version,@status,@attempts,@deleted)
                        """,
                        cancellationToken,
                        ("@hash", hash),
                        ("@uri", work.SourceUri.ToString()),
                        ("@seq", sequence),
                        ("@did", work.SourceDid),
                        ("@key", work.RecordKey),
                        ("@record", work.RecordJson),
                        ("@root", root),
                        ("@now", now),
                        ("@kind", work.ActionKind),
                        ("@action", work.ActionKey),
                        ("@version", work.PayloadVersion),
                        ("@status", work.Status),
                        ("@attempts", work.Attempts),
                        ("@deleted", work.TargetDeleted ? 1 : 0)).ConfigureAwait(false);
                }
            }
            if (malformedReason is not null) { await SkipAsync(connection, transaction, now, sequence, "post", malformedReason, cancellationToken).ConfigureAwait(false); }
            await ExecuteAsync(
                connection,
                transaction,
                "UPDATE bot_state SET processed_through = @seq WHERE id = 1",
                cancellationToken,
                ("@seq", sequence)).ConfigureAwait(false);

            return inserted;
        },
            cancellationToken);

    /// <summary>Rejects action metadata that cannot be represented by the durable identity contract.</summary>
    /// <param name="work">The action whose kind, identity key, and payload version are checked.</param>
    /// <exception cref="ArgumentException">The kind or key is empty or exceeds 128 characters, or the payload version is not positive.</exception>
    /// <remarks><para>Existing composite identities are checked separately before validating a new action's source sequence.</para></remarks>
    private static void ValidateAction(BotAction work)
    {
        if (string.IsNullOrEmpty(work.ActionKind) || work.ActionKind.Length > 128 ||
            string.IsNullOrEmpty(work.ActionKey) || work.ActionKey.Length > 128 ||
            work.PayloadVersion <= 0)
        {
            throw new ArgumentException(
                "Action kinds and keys require 1–128 characters and payload versions must be positive.",
                nameof(work));
        }
    }

    /// <summary>Applies an audited forward-only history skip while retaining queued work and request filters.</summary>
    /// <param name="cutoff">The fixed witnessed-time cutoff requested by the user.</param>
    /// <param name="replacement">The request containing the proposed starting boundary.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resulting processed-through boundary.</returns>
    public Task<long> OverrideLookbackAsync(DateTimeOffset cutoff, SnapshotRequest replacement, CancellationToken cancellationToken) =>
        TransactionAsync(
            true,
            async (connection, transaction, now) =>
        {
            DateTimeOffset started;
            long processed;
            SnapshotRequest original;
            DbCommand command = Command(
                connection,
                transaction,
                "SELECT started_at, processed_through, request FROM bot_state WHERE id = 1");
            await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
#pragma warning disable S1199 // The reader must close before the lookback update is issued.
            {
                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ||
                    await reader.IsDBNullAsync(2, cancellationToken).ConfigureAwait(false)) { throw new InvalidOperationException("Lookback override requires initialized replay state. Start normally first."); }
                started = Timestamp(reader.GetInt64(0));
                processed = reader.GetInt64(1);
                original = JsonSerializer.Deserialize(reader.GetString(2), s_snapshotRequestTypeInfo) ?? throw new InvalidDataException("Stored replay request is null.");
            }
#pragma warning restore S1199
            if (cutoff <= started || replacement.AfterSeq is not > 0) { throw new InvalidOperationException("Forward-only lookback must move the eligibility time forward and have a positive archive boundary."); }
            long after = Math.Max(processed, replacement.AfterSeq.Value);
            if (original.BeforeSeq is long upper && after >= upper) { throw new InvalidOperationException("Lookback override would exceed the original upper sequence bound."); }
            await ExecuteAsync(
                connection,
                transaction,
                "UPDATE bot_state SET started_at = @cutoff, request = @request, checkpoint = NULL, processed_through = @after WHERE id = 1",
                cancellationToken,
                ("@cutoff", Micros(cutoff)),
                ("@request", JsonSerializer.Serialize(original with { AfterSeq = after }, s_snapshotRequestTypeInfo)),
                ("@after", after)).ConfigureAwait(false);
            await SkipAsync(
                connection,
                transaction,
                now,
                after,
                "lookback override",
                $"User-directed forward-only override to {cutoff:O}; queued work retained.",
                cancellationToken).ConfigureAwait(false);

            return after;
        },
            cancellationToken);

    /// <summary>Saves the complete replay checkpoint after preceding events have been durably handled.</summary>
    /// <param name="checkpoint">The complete SDK checkpoint.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="consumedThrough">The consumed boundary to coalesce with durable progress, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="checkpoint"/> is <see langword="null"/>.</exception>
    public void SaveCheckpoint(SnapshotCheckpoint checkpoint, CancellationToken cancellationToken, long? consumedThrough = null)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        Owned(
            connectionAction: (connection, transaction, _) =>
        {
            using DbCommand read = Command(connection, transaction, "SELECT processed_through FROM bot_state WHERE id = 1");
            long processed = Integer(read.ExecuteScalar());
            long after = Math.Max(processed, consumedThrough ?? processed);
            if (checkpoint.LiveAfterSeq is long live && live > after) { throw new InvalidOperationException("Missing state or live checkpoint ahead of durable handling."); }
            using DbCommand save = Command(
                connection,
                transaction,
                "UPDATE bot_state SET processed_through = @after, checkpoint = @checkpoint WHERE id = 1",
                ("@after", after),
                ("@checkpoint", JsonSerializer.Serialize(checkpoint, s_snapshotCheckpointTypeInfo)));
            if (save.ExecuteNonQuery() != 1) { throw new InvalidOperationException("Missing state or live checkpoint ahead of durable handling."); }
        },
            cancellationToken);
    }

    /// <summary>Audits an explicitly permitted archive skip as potential data loss.</summary>
    /// <param name="sequence">The skipped sequence, or <see langword="null"/> for a block.</param>
    /// <param name="scope">The scope of skipped data.</param>
    /// <param name="reason">The reason for skipping.</param>
    public void RecordSkip(long? sequence, string scope, string reason)
    {
        Owned(
            (connection, transaction, now) =>
        {
            using DbCommand skip = Command(
                connection,
                transaction,
                "INSERT INTO skipped_data(recorded_at, sequence, scope, reason) VALUES(@now,@seq,@scope,@reason)",
                ("@now", now),
                ("@seq", sequence),
                ("@scope", scope),
                ("@reason", reason));
            skip.ExecuteNonQuery();
        },
            CancellationToken.None);
    }

    /// <summary>Runs a lease-protected synchronous mutation for replay callbacks that must finish before returning.</summary>
    /// <param name="connectionAction">The mutation receiving the connection, transaction, and current database time in microseconds.</param>
    /// <param name="token">The cancellation token checked before the operation and before commit.</param>
    /// <exception cref="OwnershipLostException">This process lacks a ready lease with enough time remaining.</exception>
    /// <remarks><para>The locked lease check and mutation share a transaction. Commands have bounded
    /// waits, and an exception or cancellation leaves the changes uncommitted.</para></remarks>
    private void Owned(Action<DbConnection, DbTransaction, long> connectionAction, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using MySqlConnection connection = CreateConnection();
        connection.Open();
        using DbTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        using DbCommand lease = Command(
            connection,
            transaction,
            "SELECT owner FROM bot_ownership WHERE id = 1 FOR UPDATE");
        lease.ExecuteScalar();
        using DbCommand clock = Command(connection, transaction, $"SELECT {NowSql}");
        long now = Integer(clock.ExecuteScalar());
        using DbCommand guard = Command(
            connection,
            transaction,
            "SELECT 1 FROM bot_ownership WHERE id = 1 AND owner = @owner AND lease_until > @reserve AND ready_at <= @now",
            ("@owner", Owner.ToString("N")),
            ("@reserve", now + 35_000_000),
            ("@now", now));
        if (guard.ExecuteScalar() is null) { throw new OwnershipLostException(); }
        token.ThrowIfCancellationRequested();
        connectionAction(connection, transaction, now);
        token.ThrowIfCancellationRequested();
        transaction.Commit();
    }

    /// <summary>Writes the audit for an explicitly accepted replay-data skip inside its existing transaction.</summary>
    /// <param name="connection">The open connection for the audit.</param>
    /// <param name="transaction">The transaction committing the audit with related progress changes.</param>
    /// <param name="now">The database time in UTC microseconds when the skip was recorded.</param>
    /// <param name="sequence">The affected event sequence, or <see langword="null"/> for a block-level skip.</param>
    /// <param name="scope">The description of the data being skipped.</param>
    /// <param name="reason">The explanation for accepting the data loss.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The affected-row count for the audit insert.</returns>
    private static Task<int> SkipAsync(
        DbConnection connection,
        DbTransaction transaction,
        long now,
        long? sequence,
        string scope,
        string reason,
        CancellationToken token) =>
        ExecuteAsync(
            connection,
            transaction,
            "INSERT INTO skipped_data(recorded_at, sequence, scope, reason) VALUES(@now,@seq,@scope,@reason)",
            token,
            ("@now", now),
            ("@seq", sequence),
            ("@scope", scope),
            ("@reason", reason));

    /// <summary>Loads the next pending or uncertain action whose retry time has arrived.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The eligible work, or <see langword="null"/>.</returns>
    public Task<BotAction?> NextWorkAsync(CancellationToken cancellationToken) =>
        TransactionAsync<BotAction?>(
            true,
            async (connection, transaction, now) =>
        {
            DbCommand command = Command(
                connection,
                transaction,
                """
                SELECT source_uri, source_sequence, source_did, record_key, record, status, attempts,
                    target_deleted, action_kind, action_key, payload_version
                FROM reply_work WHERE status IN ('pending','uncertain') AND next_retry <= @now
                ORDER BY next_retry, source_sequence LIMIT 1
                """,
                ("@now", now));
            await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
            DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) { return null; }

            return new BotAction(
                reader.GetString(0),
                reader.GetInt64(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetInt32(6),
                reader.GetInt32(7) != 0,
                reader.GetString(8),
                reader.GetString(9),
                reader.GetInt32(10));
        },
            cancellationToken);

    /// <summary>Changes one pending or uncertain action without changing other actions on the same source.</summary>
    /// <param name="work">The action identifying the row by source URI, kind, and action key.</param>
    /// <param name="assignment">The provider-owned SQL assignments describing the state change.</param>
    /// <param name="token">The cancellation token.</param>
    /// <param name="condition">The optional provider-owned eligibility condition, such as requiring an undeleted target.</param>
    /// <param name="parameters">The named values used by the assignments or condition.</param>
    /// <returns>The affected-row count, normally zero or one.</returns>
    /// <remarks><para>The source hash accelerates lookup, but the complete identity is matched.
    /// Shared remote record keys never cause a state change to affect multiple durable actions.</para></remarks>
#pragma warning disable CA1822, S2325 // The retry update uses this instance's configured transaction and ownership context.
    private Task<int> UpdateWorkAsync(
        BotAction work,
        string assignment,
        CancellationToken token,
        string condition = "",
        params (string Name, object? Value)[] parameters) =>
        TransactionAsync(
            true,
            (connection, transaction, now) => ExecuteAsync(
    connection,
    transaction,
    $"UPDATE reply_work SET {assignment} WHERE source_hash = @hash AND source_uri = @uri AND action_kind = @kind AND action_key = @action AND status IN ('pending','uncertain'){condition}",
    token,
    [
                    .. parameters,
                    ("@hash", SourceHash(work.SourceUri.ToString())),
                    ("@uri", work.SourceUri.ToString()),
                    ("@kind", work.ActionKind),
                    ("@action", work.ActionKey),
                    ("@now", now)
                ]),
            token);
#pragma warning restore CA1822, S2325

    /// <summary>Marks one composite action identity uncertain and increments its attempt count before a remote write.</summary>
    /// <param name="work">The work being attempted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    public Task<int> MarkAttemptAsync(
        BotAction work,
        CancellationToken cancellationToken) =>
        UpdateWorkAsync(
            work,
            "status = 'uncertain', attempts = attempts + 1",
            cancellationToken,
            " AND target_deleted = 0");

    /// <summary>Saves a terminal action outcome for one composite identity.</summary>
    /// <param name="work">The completed work.</param>
    /// <param name="status">The terminal delivery status.</param>
    /// <param name="remoteReference">The remote action URI, or <see langword="null"/> when not sent.</param>
    /// <param name="reason">The outcome reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    public Task<int> FinishAsync(
        BotAction work,
        string status,
        string? remoteReference,
        string reason,
        CancellationToken cancellationToken) =>
        UpdateWorkAsync(
            work,
            "status = @status, completed_at = @now, reply_uri = @reply, outcome = @reason",
            cancellationToken,
            "",
            ("@status", status),
            ("@reply", remoteReference),
            ("@reason", reason));

    /// <summary>Schedules another reconciliation or send attempt without changing its fixed record.</summary>
    /// <param name="work">The work to retry.</param>
    /// <param name="retryAt">The earliest next attempt time.</param>
    /// <param name="reason">The retry reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    public Task<int> RetryAsync(
        BotAction work,
        DateTimeOffset retryAt,
        string reason,
        CancellationToken cancellationToken) =>
        UpdateWorkAsync(
            work,
            "next_retry = @retry, outcome = @reason",
            cancellationToken,
            "",
            ("@retry", Micros(retryAt)),
            ("@reason", reason));

    /// <summary>Loads the encrypted refresh-credential envelope.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The encrypted envelope, or <see langword="null"/>.</returns>
    public Task<byte[]?> LoadCredentialsAsync(CancellationToken cancellationToken) =>
        TransactionAsync(
            true,
            async (connection, transaction, _) =>
            await ScalarAsync(connection, transaction, "SELECT credentials FROM bot_state WHERE id = 1", cancellationToken).ConfigureAwait(false) as byte[],
            cancellationToken);

    /// <summary>Saves or clears the encrypted refresh-credential envelope.</summary>
    /// <param name="encrypted">The encrypted envelope, or <see langword="null"/> to clear it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    public Task<int> SaveCredentialsAsync(byte[]? encrypted, CancellationToken cancellationToken) =>
        TransactionAsync(
            true,
            (connection, transaction, _) => ExecuteAsync(
    connection,
    transaction,
    "UPDATE bot_state SET credentials = @value WHERE id = 1",
    cancellationToken,
    ("@value", encrypted)),
            cancellationToken);

    /// <summary>Reserves durable outbound budget before a request or reports when budget becomes available.</summary>
    /// <param name="points">The repository write-point cost.</param>
    /// <param name="session"><see langword="true"/> for session creation; otherwise, <see langword="false"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The required wait deadline, or <see langword="null"/> when the request was reserved.</returns>
    public Task<DateTimeOffset?> ReserveRequestAsync(int points, bool session, CancellationToken cancellationToken) =>
        TransactionAsync<DateTimeOffset?>(
            true,
            async (connection, transaction, now) =>
        {
            long wait;
            DbCommand state = Command(connection, transaction, "SELECT next_request, cooldown_until FROM rate_state WHERE id = 1");
            await using ConfiguredAsyncDisposable stateDisposal = state.ConfigureAwait(false);
#pragma warning disable S1199 // Reader scopes must close before subsequent commands in the same transaction.
            {
                DbDataReader reader = await state.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) { throw new InvalidOperationException("Missing rate state."); }
                wait = Math.Max(reader.GetInt64(0), reader.GetInt64(1));
            }
            long[] windows = [300_000_000, 3_600_000_000, 86_400_000_000, 300_000_000, 86_400_000_000];
            long[] sums = new long[5];
            long[] first = [.. Enumerable.Repeat(long.MaxValue, 5)];
            DbCommand accounting = Command(
                connection,
                transaction,
                "SELECT requested_at, points, session_create FROM rate_accounting WHERE requested_at > @day",
                ("@day", now - windows[2]));
            await using ConfiguredAsyncDisposable accountingDisposal = accounting.ConfigureAwait(false);
            {
                DbDataReader reader = await accounting.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    long at = reader.GetInt64(0);
                    int cost = reader.GetInt32(1);
                    bool login = reader.GetInt32(2) != 0;
                    long[] additions = [1, cost, cost, login ? 1 : 0, login ? 1 : 0];
                    for (int i = 0; i < windows.Length; i++)
                    {
                        if (at > now - windows[i])
                        {
                            sums[i] += additions[i];
                            if (additions[i] > 0) { first[i] = Math.Min(first[i], at); }
                        }
                    }
                }
            }
#pragma warning restore S1199
            int[] thresholds = [_options.RequestsPerFiveMinutes, _options.HourlyWritePoints, _options.DailyWritePoints,
                _options.SessionsPerFiveMinutes, _options.SessionsPerDay];
            int[] requested = [1, points, points, session ? 1 : 0, session ? 1 : 0];
            for (int i = 0; i < windows.Length; i++)
            {
                if (requested[i] > 0 && sums[i] + requested[i] > thresholds[i])
                {
                    if (first[i] == long.MaxValue) { throw new InvalidOperationException("A request exceeds its entire configured rate budget."); }
                    wait = Math.Max(wait, first[i] + windows[i] + 1_000_000);
                }
            }
            if (wait > now) { return Timestamp(wait); }
            await ExecuteAsync(
                connection,
                transaction,
                "INSERT INTO rate_accounting(requested_at, points, session_create) VALUES(@now,@points,@session)",
                cancellationToken,
                ("@now", now),
                ("@points", points),
                ("@session", session ? 1 : 0)).ConfigureAwait(false);
            await ExecuteAsync(
                connection,
                transaction,
                "UPDATE rate_state SET next_request = @next WHERE id = 1",
                cancellationToken,
                ("@next", now + _options.MinimumRequestIntervalSeconds * 1_000_000L)).ConfigureAwait(false);

            return null;
        },
            cancellationToken);

    /// <summary>Extends the durable outbound cooldown without shortening an existing cooldown.</summary>
    /// <param name="until">The cooldown deadline.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    public Task<int> CooldownAsync(DateTimeOffset until, CancellationToken cancellationToken) =>
        TransactionAsync(
            true,
            (connection, transaction, _) => ExecuteAsync(
    connection,
    transaction,
    "UPDATE rate_state SET cooldown_until = CASE WHEN cooldown_until < @until THEN @until ELSE cooldown_until END WHERE id = 1",
    cancellationToken,
    ("@until", Micros(until))),
            cancellationToken);

    /// <summary>Loads the durable archive cooldown deadline.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The saved cooldown deadline.</returns>
    public Task<DateTimeOffset> ArchiveCooldownAsync(CancellationToken cancellationToken) =>
        TransactionAsync(
            true,
            async (connection, transaction, _) =>
            Timestamp(Integer(await ScalarAsync(connection, transaction, "SELECT archive_cooldown FROM bot_state WHERE id = 1", cancellationToken).ConfigureAwait(false))),
            cancellationToken);

    /// <summary>Extends the durable archive cooldown without shortening an existing cooldown.</summary>
    /// <param name="until">The cooldown deadline.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    public Task<int> SaveArchiveCooldownAsync(DateTimeOffset until, CancellationToken cancellationToken) =>
        TransactionAsync(
            true,
            (connection, transaction, _) => ExecuteAsync(
    connection,
    transaction,
    "UPDATE bot_state SET archive_cooldown = CASE WHEN archive_cooldown < @until THEN @until ELSE archive_cooldown END WHERE id = 1",
    cancellationToken,
    ("@until", Micros(until))),
            cancellationToken);

    /// <summary>Prunes terminal work only when retention and processed-through coverage permit it.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    public Task<int> PruneAsync(CancellationToken cancellationToken) =>
        TransactionAsync(
            true,
            async (connection, transaction, now) =>
        {
            long before = now - _options.CompletedRetentionDays * 86_400_000_000L;
            int count = await ExecuteAsync(
                connection,
                transaction,
                "DELETE FROM rate_accounting WHERE requested_at < @day",
                cancellationToken,
                ("@day", now - 86_400_000_000)).ConfigureAwait(false);
            count += await ExecuteAsync(
                connection,
                transaction,
                "DELETE FROM skipped_data WHERE recorded_at < @before",
                cancellationToken,
                ("@before", before)).ConfigureAwait(false);
            count += await ExecuteAsync(
                connection,
                transaction,
                "DELETE FROM reply_work WHERE status IN ('sent','not-sent','simulated') AND completed_at < @before AND source_sequence <= (SELECT processed_through FROM bot_state WHERE id = 1)",
                cancellationToken,
                ("@before", before)).ConfigureAwait(false);
            s_pruned(_logger, count, null);

            return count;
        },
            cancellationToken);
}
