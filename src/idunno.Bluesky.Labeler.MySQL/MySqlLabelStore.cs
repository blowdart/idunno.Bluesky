// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data;
using System.Data.Common;
using System.Globalization;

using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.Logging;

using MySqlConnector;

#pragma warning disable IDE0130 // Providers share the labeler namespace across separate packages.
namespace idunno.Bluesky.Labeler;
#pragma warning restore IDE0130

/// <summary>Applies MySQL durability checks and row locking to shared decision storage.</summary>
/// <remarks><para>MySQL DDL commits independently. Setup holds a schema lock and repeats additive
/// creation steps, recording the new schema version only when all steps have completed.</para></remarks>
internal sealed class MySqlLabelStore(string connectionString, ILogger<MySqlLabelStore> logger, StoreMetrics metrics)
    : RelationalLabelStore(logger, metrics)
{
    protected override string Provider => "mysql";
    protected override string LockSuffix => " FOR UPDATE";
    protected override string ClockSql => "SELECT CAST(UNIX_TIMESTAMP(UTC_TIMESTAMP(3)) * 1000 AS SIGNED)";
    /// <summary>Creates an operation-owned MySQL connection for the configured issuer database.</summary>
    protected override DbConnection CreateConnection() => new MySqlConnection(connectionString);
    /// <summary>Recognizes lock-wait timeouts and deadlocks that may be retried before commit submission.</summary>
    protected override bool IsTransient(DbException exception) => exception is MySqlException { Number: 1205 or 1213 };
    /// <summary>Begins a read-committed transaction whose stream-row lock orders decisions and publication.</summary>
    protected override async Task<DbTransaction> BeginAsync(DbConnection connection, CancellationToken token) =>
        await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token).ConfigureAwait(false);

    /// <summary>Requires supported MySQL/InnoDB durability and enables strict, bounded runtime sessions.</summary>
    protected override async Task PrepareAsync(DbConnection connection, CancellationToken token)
    {
        string version = (await ScalarAsync(
            connection: connection,
            transaction: null,
            sql: "SELECT VERSION()",
            token: token).ConfigureAwait(false))?.ToString() ?? string.Empty;
        if (version.Contains("MariaDB", StringComparison.OrdinalIgnoreCase) ||
            !Version.TryParse(version.Split('-')[0], out Version? parsed) || parsed < new Version(8, 4))
        {
            throw new InvalidOperationException("MySQL 8.4 or later is required; MariaDB is not a verified substitute.");
        }

        long durable = Convert.ToInt64(
            await ScalarAsync(
                connection: connection,
                transaction: null,
                sql: "SELECT @@GLOBAL.innodb_flush_log_at_trx_commit",
                token: token).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
        long sync = Convert.ToInt64(
            await ScalarAsync(
                connection: connection,
                transaction: null,
                sql: "SELECT @@GLOBAL.sync_binlog",
                token: token).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
        if (durable != 1 || sync != 1)
        {
            throw new InvalidOperationException("MySQL requires innodb_flush_log_at_trx_commit=1 and sync_binlog=1.");
        }

        long invalid = Convert.ToInt64(
            await ScalarAsync(
                connection: connection,
                transaction: null,
                sql: "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name IN ('label_schema','label_state','label_entries','label_operations','label_decisions','label_distribution','label_decision_index','label_team','label_admin_audit','label_admin_receipts','label_admin_tokens','label_maintenance','label_hosts','label_host_roles','label_maintenance_hosts') AND engine<>'InnoDB'",
                token: token).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
        if (invalid != 0)
        {
            throw new InvalidOperationException("All label tables must use InnoDB.");
        }

        await ExecuteAsync(
            connection: connection,
            transaction: null,
            sql: "SET SESSION innodb_lock_wait_timeout=15; SET SESSION time_zone='+00:00'; SET SESSION sql_mode='STRICT_ALL_TABLES,NO_ENGINE_SUBSTITUTION'; SET SESSION foreign_key_checks=1; SET SESSION unique_checks=1",
            token: token).ConfigureAwait(false);
    }

    /// <summary>Holds a connection-scoped setup lock while resuming additive DDL that MySQL commits independently.</summary>
    protected override async Task MigrateAsync(DbConnection connection, CancellationToken token)
    {
        object? acquired = await ScalarAsync(
            connection: connection,
            transaction: null,
            sql: "SELECT GET_LOCK(CONCAT('label-schema-', LEFT(SHA2(DATABASE(),256),40)),15)",
            token: token).ConfigureAwait(false);
        if (Convert.ToInt64(acquired, CultureInfo.InvariantCulture) != 1)
        {
            throw new TimeoutException("Could not acquire the label schema migration lock.");
        }

        try
        {
            await ApplySchemaAsync(connection, null, token).ConfigureAwait(false);
            object? expiryIndex = await ScalarAsync(
                connection: connection,
                transaction: null,
                sql: "SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='label_admin_tokens' AND index_name='label_admin_token_expiry'",
                token: token).ConfigureAwait(false);
            if (Convert.ToInt64(expiryIndex, CultureInfo.InvariantCulture) == 0)
            {
                await ExecuteAsync(
                    connection: connection,
                    transaction: null,
                    sql: "CREATE INDEX label_admin_token_expiry ON label_admin_tokens(expires_ms)",
                    token: token).ConfigureAwait(false);
            }
        }
        finally
        {
            await ExecuteAsync(
                connection: connection,
                transaction: null,
                sql: "SELECT RELEASE_LOCK(CONCAT('label-schema-', LEFT(SHA2(DATABASE(),256),40)))",
                token: CancellationToken.None).ConfigureAwait(false);
        }
    }
}
