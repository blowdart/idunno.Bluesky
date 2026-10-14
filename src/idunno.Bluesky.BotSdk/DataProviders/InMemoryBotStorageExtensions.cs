// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.BotSdk.DataProviders;

/// <summary>Registers volatile in-memory storage for development and testing.</summary>
public static class InMemoryBotStorageExtensions
{
    /// <summary>Registers the process-local in-memory provider for the durable bot runtime.</summary>
    /// <param name="services">The host service collection.</param>
    /// <returns>The service collection for further configuration.</returns>
    /// <exception cref="ArgumentNullException">The service collection is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A database provider is already registered.</exception>
    /// <remarks>
    /// <para>This provider loses all state on process exit and cannot coordinate ownership across processes.
    /// It logs a warning when initialized and is not suitable for production.</para>
    /// </remarks>
    public static IServiceCollection AddInMemoryBotStorage(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(service => service.ServiceType == typeof(IDatabaseProvider)))
        {
            throw new InvalidOperationException("A bot database provider is already registered. Register exactly one storage provider.");
        }

        services.AddSingleton<IDatabaseProvider>(provider => new InMemoryDatabaseProvider(
            provider.GetRequiredService<BotOptions>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<InMemoryDatabaseProvider>>()));

        return services;
    }

    /// <summary>Registers the process-local in-memory provider on the bot builder.</summary>
    /// <param name="builder">The bot builder.</param>
    /// <returns>The bot builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A database provider is already registered.</exception>
    /// <remarks>
    /// <para>This provider loses all state on process exit and cannot coordinate ownership across processes.
    /// It logs a warning when initialized and is not suitable for production.</para>
    /// </remarks>
    public static BlueskyBotBuilder WithInMemoryStorage(this BlueskyBotBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddInMemoryBotStorage();
        return builder;
    }
}
