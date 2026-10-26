// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Moderation;
using idunno.Bluesky.Labeler.Signing;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Npgsql;

#pragma warning disable IDE0130 // Providers share the labeler namespace across separate packages.
namespace idunno.Bluesky.Labeler;
#pragma warning restore IDE0130

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
        if (services.Any(service => service.ServiceType == typeof(ILabelStore) || service.ServiceType == typeof(ILabelDistributionStore)))
        {
            throw new InvalidOperationException("Register exactly one label storage provider.");
        }

        services.AddLogging();
        services.AddSingleton<ILabelStore>(
            provider => new PostgreSqlLabelStore(
                settings.ConnectionString,
                provider.GetRequiredService<ILogger<PostgreSqlLabelStore>>(),
                new StoreMetrics("postgresql", provider.GetService<IMeterFactory>())));
        services.AddSingleton<ILabelDistributionStore>(provider => provider.GetRequiredService<ILabelStore>());
        services.AddSingleton<ILabelAdministrationStore>(provider => (ILabelAdministrationStore)provider.GetRequiredService<ILabelStore>());
        services.AddSingleton<ILabelMaintenanceStore>(provider => (ILabelMaintenanceStore)provider.GetRequiredService<ILabelStore>());
        services.AddSingleton<ILabelModerationStore>(provider => (ILabelModerationStore)provider.GetRequiredService<ILabelStore>());
        services.AddSingleton<ILabelPolicyStore>(provider => (ILabelPolicyStore)provider.GetRequiredService<ILabelStore>());
        services.AddSingleton<ILabelReportStore>(provider => (ILabelReportStore)provider.GetRequiredService<ILabelStore>());
        services.AddSingleton<ILabelAppealStore>(provider => (ILabelAppealStore)provider.GetRequiredService<ILabelStore>());
        services.AddSingleton<ILabelSigningKeySelectionStore>(provider => (ILabelSigningKeySelectionStore)provider.GetRequiredService<ILabelStore>());
        return services;
    }
}
