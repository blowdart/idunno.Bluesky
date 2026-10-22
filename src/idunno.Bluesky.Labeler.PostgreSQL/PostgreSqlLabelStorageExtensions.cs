// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data;
using System.Data.Common;
using System.Diagnostics.Metrics;
using System.Globalization;

using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Npgsql;

namespace idunno.Bluesky.Labeler;

/// <summary>Registers durable PostgreSQL label persistence.</summary>
public static class PostgreSqlLabelStorageExtensions
{
    /// <summary>Registers exactly one label store backed by a dedicated PostgreSQL 17 or later database.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The database connection string.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The connection string is empty or malformed.</exception>
    /// <exception cref="InvalidOperationException">A label store is already registered.</exception>
    public static IServiceCollection AddPostgreSqlLabelStorage(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        NpgsqlConnectionStringBuilder settings = new(connectionString) { Timeout = 15, CommandTimeout = 15 };
        if (services.Any(service => service.ServiceType == typeof(ILabelStore)))
        {
            throw new InvalidOperationException("Register exactly one label storage provider.");
        }

        services.AddLogging();
        services.AddSingleton<ILabelStore>(provider => new PostgreSqlLabelStore(settings.ConnectionString,
            provider.GetRequiredService<ILogger<PostgreSqlLabelStore>>(), new StoreMetrics("postgresql", provider.GetService<IMeterFactory>())));
        return services;
    }
}

internal sealed class PostgreSqlLabelStore(string connectionString, ILogger<PostgreSqlLabelStore> logger, StoreMetrics metrics)
    : RelationalLabelStore(logger, metrics)
{
    protected override string Provider => "postgresql";
    protected override string LockSuffix => " FOR UPDATE";
    protected override string ClockSql => "SELECT CAST(FLOOR(EXTRACT(EPOCH FROM clock_timestamp()) * 1000) AS BIGINT)";
    protected override DbConnection CreateConnection() => new NpgsqlConnection(connectionString);
    protected override bool IsTransient(DbException exception) =>
        exception is PostgresException { SqlState: "40001" or "40P01" or "55P03" };
    protected override async Task<DbTransaction> BeginAsync(DbConnection connection, CancellationToken token) =>
        await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token).ConfigureAwait(false);

    protected override async Task PrepareAsync(DbConnection connection, CancellationToken token)
    {
        long version = Convert.ToInt64(await ScalarAsync(connection, null, "SHOW server_version_num", token).ConfigureAwait(false), CultureInfo.InvariantCulture);
        object? durable = await ScalarAsync(connection, null, "SHOW fsync", token).ConfigureAwait(false);
        object? fullPages = await ScalarAsync(connection, null, "SHOW full_page_writes", token).ConfigureAwait(false);
        if (version < 170000 || !string.Equals(durable?.ToString(), "on", StringComparison.Ordinal) ||
            !string.Equals(fullPages?.ToString(), "on", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("PostgreSQL 17 or later with fsync and full_page_writes enabled is required.");
        }

        await ExecuteAsync(connection, null,
            "SET synchronous_commit=on; SET lock_timeout='15s'; SET statement_timeout='15s'", token).ConfigureAwait(false);
    }

    protected override async Task MigrateAsync(DbConnection connection, CancellationToken token)
    {
        DbTransaction transaction = await BeginAsync(connection, token).ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        await ExecuteAsync(connection, transaction, "SELECT pg_advisory_xact_lock(673009001)", token).ConfigureAwait(false);
        await ApplySchemaAsync(connection, transaction, token).ConfigureAwait(false);
        await transaction.CommitAsync(token).ConfigureAwait(false);
    }
}
