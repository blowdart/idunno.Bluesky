// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data;
using System.Data.Common;
using System.Diagnostics.Metrics;
using System.Globalization;

using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using MySqlConnector;

namespace idunno.Bluesky.Labeler;

/// <summary>Registers durable MySQL label persistence.</summary>
public static class MySqlLabelStorageExtensions
{
    /// <summary>Registers exactly one label store backed by a dedicated MySQL 8.4 or later database.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The database connection string.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The connection string is empty or malformed.</exception>
    /// <exception cref="InvalidOperationException">A label store is already registered.</exception>
    public static IServiceCollection AddMySqlLabelStorage(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        MySqlConnectionStringBuilder settings = new(connectionString) { ConnectionTimeout = 15, DefaultCommandTimeout = 15 };
        if (services.Any(service => service.ServiceType == typeof(ILabelStore)))
        {
            throw new InvalidOperationException("Register exactly one label storage provider.");
        }

        services.AddLogging();
        services.AddSingleton<ILabelStore>(provider => new MySqlLabelStore(settings.ConnectionString,
            provider.GetRequiredService<ILogger<MySqlLabelStore>>(), new StoreMetrics("mysql", provider.GetService<IMeterFactory>())));
        return services;
    }
}

internal sealed class MySqlLabelStore(string connectionString, ILogger<MySqlLabelStore> logger, StoreMetrics metrics)
    : RelationalLabelStore(logger, metrics)
{
    protected override string Provider => "mysql";
    protected override string LockSuffix => " FOR UPDATE";
    protected override string ClockSql => "SELECT CAST(UNIX_TIMESTAMP(UTC_TIMESTAMP(3)) * 1000 AS SIGNED)";
    protected override DbConnection CreateConnection() => new MySqlConnection(connectionString);
    protected override bool IsTransient(DbException exception) => exception is MySqlException { Number: 1205 or 1213 };
    protected override async Task<DbTransaction> BeginAsync(DbConnection connection, CancellationToken token) =>
        await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token).ConfigureAwait(false);

    protected override async Task PrepareAsync(DbConnection connection, CancellationToken token)
    {
        string version = (await ScalarAsync(connection, null, "SELECT VERSION()", token).ConfigureAwait(false))?.ToString() ?? string.Empty;
        if (version.Contains("MariaDB", StringComparison.OrdinalIgnoreCase) ||
            !Version.TryParse(version.Split('-')[0], out Version? parsed) || parsed < new Version(8, 4))
        {
            throw new InvalidOperationException("MySQL 8.4 or later is required; MariaDB is not a verified substitute.");
        }

        long durable = Convert.ToInt64(await ScalarAsync(connection, null,
            "SELECT @@GLOBAL.innodb_flush_log_at_trx_commit", token).ConfigureAwait(false), CultureInfo.InvariantCulture);
        long sync = Convert.ToInt64(await ScalarAsync(connection, null,
            "SELECT @@GLOBAL.sync_binlog", token).ConfigureAwait(false), CultureInfo.InvariantCulture);
        if (durable != 1 || sync != 1)
        {
            throw new InvalidOperationException("MySQL requires innodb_flush_log_at_trx_commit=1 and sync_binlog=1.");
        }

        long invalid = Convert.ToInt64(await ScalarAsync(connection, null,
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name IN ('label_schema','label_state','label_entries','label_operations') AND engine<>'InnoDB'",
            token).ConfigureAwait(false), CultureInfo.InvariantCulture);
        if (invalid != 0)
        {
            throw new InvalidOperationException("All label tables must use InnoDB.");
        }

        await ExecuteAsync(connection, null,
            "SET SESSION innodb_lock_wait_timeout=15; SET SESSION time_zone='+00:00'; SET SESSION sql_mode='STRICT_ALL_TABLES,NO_ENGINE_SUBSTITUTION'; SET SESSION foreign_key_checks=1; SET SESSION unique_checks=1",
            token).ConfigureAwait(false);
    }

    protected override async Task MigrateAsync(DbConnection connection, CancellationToken token)
    {
        object? acquired = await ScalarAsync(connection, null,
            "SELECT GET_LOCK(CONCAT('label-schema-', LEFT(SHA2(DATABASE(),256),40)),15)", token).ConfigureAwait(false);
        if (Convert.ToInt64(acquired, CultureInfo.InvariantCulture) != 1)
        {
            throw new TimeoutException("Could not acquire the label schema migration lock.");
        }

        try
        {
            // MySQL commits DDL independently. CREATE IF NOT EXISTS steps resume after interruption.
            await ApplySchemaAsync(connection, null, token).ConfigureAwait(false);
        }
        finally
        {
            await ExecuteAsync(connection, null,
                "SELECT RELEASE_LOCK(CONCAT('label-schema-', LEFT(SHA2(DATABASE(),256),40)))", CancellationToken.None).ConfigureAwait(false);
        }
    }
}
