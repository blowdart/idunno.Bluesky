// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Diagnostics.Metrics;

using idunno.Bluesky.Labeler.Storage;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.Labeler;

/// <summary>Registers durable SQLite label persistence.</summary>
public static class SqliteLabelStorageExtensions
{
    /// <summary>Registers exactly one label store backed by a persistent local SQLite file.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The connection string for a local file, not a URI or memory database.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The connection settings do not identify persistent local storage.</exception>
    /// <exception cref="InvalidOperationException">A label store is already registered.</exception>
    public static IServiceCollection AddSqliteLabelStorage(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        SqliteConnectionStringBuilder settings = new(connectionString);
        if (string.IsNullOrWhiteSpace(settings.DataSource) || settings.DataSource.Contains(":memory:", StringComparison.OrdinalIgnoreCase) ||
            settings.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
            settings.Mode == SqliteOpenMode.Memory || Path.GetFullPath(settings.DataSource).StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new ArgumentException("Label storage requires a persistent local SQLite filename.", nameof(connectionString));
        }

        settings.DefaultTimeout = 15;
        settings.Pooling = false;
        settings.ForeignKeys = true;
        if (services.Any(service => service.ServiceType == typeof(ILabelStore)))
        {
            throw new InvalidOperationException("Register exactly one label storage provider.");
        }

        services.AddLogging();
        services.AddSingleton<ILabelStore>(provider => new SqliteLabelStore(settings.ConnectionString,
            provider.GetRequiredService<ILogger<SqliteLabelStore>>(), new StoreMetrics("sqlite", provider.GetService<IMeterFactory>())));
        return services;
    }
}

internal sealed class SqliteLabelStore(string connectionString, ILogger<SqliteLabelStore> logger, StoreMetrics metrics)
    : RelationalLabelStore(logger, metrics)
{
    protected override string Provider => "sqlite";
    protected override string LockSuffix => string.Empty;
    protected override string ClockSql => "SELECT CAST(unixepoch('subsec') * 1000 AS INTEGER)";
    protected override DbConnection CreateConnection() => new SqliteConnection(connectionString);
    protected override bool IsTransient(DbException exception) => exception is SqliteException { SqliteErrorCode: 5 or 6 };

    protected override Task<DbTransaction> BeginAsync(DbConnection connection, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
#pragma warning disable CA1849 // An immediate SQLite writer transaction requires this synchronous overload.
        return Task.FromResult<DbTransaction>(((SqliteConnection)connection).BeginTransaction(deferred: false));
#pragma warning restore CA1849
    }

    protected override async Task PrepareAsync(DbConnection connection, CancellationToken token)
    {
        object? mode = await ScalarAsync(connection, null, "PRAGMA journal_mode=WAL", token).ConfigureAwait(false);
        if (!string.Equals(mode?.ToString(), "wal", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SQLite label storage requires WAL mode.");
        }

        await ExecuteAsync(connection, null, "PRAGMA synchronous=FULL; PRAGMA foreign_keys=ON", token).ConfigureAwait(false);
    }

    protected override async Task MigrateAsync(DbConnection connection, CancellationToken token)
    {
        DbTransaction transaction = await BeginAsync(connection, token).ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        await ApplySchemaAsync(connection, transaction, token).ConfigureAwait(false);
        await transaction.CommitAsync(token).ConfigureAwait(false);
    }
}
