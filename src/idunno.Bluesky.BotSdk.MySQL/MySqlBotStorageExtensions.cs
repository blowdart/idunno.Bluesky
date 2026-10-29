// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130 // Preserve the established namespace independently of folder layout.
namespace idunno.Bluesky.BotSdk.DataProviders;
#pragma warning restore IDE0130

/// <summary>Registers MySQL storage for the durable bot runtime.</summary>
public static class MySqlBotStorageExtensions
{
    /// <summary>Registers MySQL 8.4 storage using bounded connections and InnoDB lease fencing.</summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="connectionString">The MySQL connection string; use verified TLS outside local development.</param>
    /// <returns>The service collection for further configuration.</returns>
    /// <exception cref="ArgumentNullException">The service collection is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">The connection string is empty.</exception>
    /// <exception cref="InvalidOperationException">A database provider is already registered.</exception>
    public static IServiceCollection AddMySqlBotStorage(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (services.Any(service => service.ServiceType == typeof(IDatabaseProvider)))
        {
            throw new InvalidOperationException("A bot database provider is already registered. Register exactly one storage provider.");
        }

        services.AddSingleton<IDatabaseProvider>(provider => new MySqlDatabaseProvider(
            connectionString,
            provider.GetRequiredService<BotOptions>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<MySqlDatabaseProvider>>()));

        return services;
    }

    /// <summary>Registers MySQL 8.4 storage on the bot builder.</summary>
    /// <param name="builder">The bot builder.</param>
    /// <param name="connectionString">The MySQL connection string; use verified TLS outside local development.</param>
    /// <returns>The bot builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="connectionString"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="InvalidOperationException">A database provider is already registered.</exception>
    public static BlueskyBotBuilder WithMySqlStorage(this BlueskyBotBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        builder.Services.AddMySqlBotStorage(connectionString);
        return builder;
    }
}
