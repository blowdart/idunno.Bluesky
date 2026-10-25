// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;

using idunno.Bluesky.Labeler.Storage;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Providers share the labeler namespace across separate packages.
namespace idunno.Bluesky.Labeler;
#pragma warning restore IDE0130

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
        if (services.Any(service => service.ServiceType == typeof(ILabelStore) || service.ServiceType == typeof(ILabelDistributionStore)))
        {
            throw new InvalidOperationException("Register exactly one label storage provider.");
        }

        services.AddLogging();
        services.AddSingleton<ILabelStore>(
            provider => new SqliteLabelStore(
                settings.ConnectionString,
                provider.GetRequiredService<ILogger<SqliteLabelStore>>(),
                new StoreMetrics("sqlite", provider.GetService<IMeterFactory>())));
        services.AddSingleton<ILabelDistributionStore>(provider => provider.GetRequiredService<ILabelStore>());
        services.AddSingleton<ILabelAdministrationStore>(provider => (ILabelAdministrationStore)provider.GetRequiredService<ILabelStore>());
        services.AddSingleton<ILabelMaintenanceStore>(provider => (ILabelMaintenanceStore)provider.GetRequiredService<ILabelStore>());
        return services;
    }
}
