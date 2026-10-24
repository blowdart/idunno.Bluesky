// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;

using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using MySqlConnector;

#pragma warning disable IDE0130 // Providers share the labeler namespace across separate packages.
namespace idunno.Bluesky.Labeler;
#pragma warning restore IDE0130

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
        if (services.Any(service => service.ServiceType == typeof(ILabelStore) || service.ServiceType == typeof(ILabelDistributionStore)))
        {
            throw new InvalidOperationException("Register exactly one label storage provider.");
        }

        services.AddLogging();
        services.AddSingleton<ILabelStore>(
            provider => new MySqlLabelStore(
                settings.ConnectionString,
                provider.GetRequiredService<ILogger<MySqlLabelStore>>(),
                new StoreMetrics("mysql", provider.GetService<IMeterFactory>())));
        services.AddSingleton<ILabelDistributionStore>(provider => provider.GetRequiredService<ILabelStore>());
        services.AddSingleton<ILabelAdministrationStore>(provider => (ILabelAdministrationStore)provider.GetRequiredService<ILabelStore>());
        return services;
    }
}
