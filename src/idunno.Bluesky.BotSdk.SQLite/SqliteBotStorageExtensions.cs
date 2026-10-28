// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130 // Preserve the established namespace independently of folder layout.
namespace idunno.Bluesky.BotSdk.DataProviders;
#pragma warning restore IDE0130

/// <summary>Registers SQLite storage for the durable bot runtime.</summary>
public static class SqliteBotStorageExtensions
{
    /// <summary>Registers SQLite storage in a persistent local file shared by processes on one host.</summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="connectionString">The SQLite connection string pointing to a persistent local file.</param>
    /// <returns>The service collection for further configuration.</returns>
    /// <exception cref="ArgumentNullException">The service collection is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">The connection string is empty.</exception>
    /// <exception cref="InvalidOperationException">A database provider is already registered.</exception>
    public static IServiceCollection AddSqliteBotStorage(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (services.Any(service => service.ServiceType == typeof(IDatabaseProvider)))
        {
            throw new InvalidOperationException("A bot database provider is already registered. Register exactly one storage provider.");
        }

        services.AddSingleton<IDatabaseProvider>(provider => new SqliteDatabaseProvider(
            connectionString,
            provider.GetRequiredService<BotOptions>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<SqliteDatabaseProvider>>()));

        return services;
    }

    /// <summary>Registers SQLite storage on the bot builder.</summary>
    /// <param name="builder">The bot builder.</param>
    /// <param name="connectionString">The SQLite connection string pointing to a persistent local file.</param>
    /// <returns>The bot builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="connectionString"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="InvalidOperationException">A database provider is already registered.</exception>
    public static BlueskyBotBuilder WithSqliteStorage(this BlueskyBotBuilder builder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        builder.Services.AddSqliteBotStorage(connectionString);
        return builder;
    }
}
