// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.Configuration;

using Npgsql;

#pragma warning disable IDE0130 // Preserve the established namespace independently of folder layout.
namespace idunno.Bluesky.BotSdk.DataProviders;
#pragma warning restore IDE0130

/// <summary>Registers PostgreSQL storage for the durable bot runtime.</summary>
public static class PostgreSqlBotStorageExtensions
{
    private static readonly object s_dataSourceKey = new();

    /// <summary>Registers PostgreSQL storage and a host-owned Npgsql connection pool.</summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="connectionString">The PostgreSQL connection string with bounded timeouts and appropriate TLS.</param>
    /// <param name="caCertificate">The optional trusted PEM server CA certificate.</param>
    /// <returns>The service collection for further configuration.</returns>
    /// <exception cref="ArgumentNullException">The service collection is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">The connection string is empty.</exception>
    /// <exception cref="InvalidOperationException">A database provider is already registered.</exception>
    public static IServiceCollection AddPostgreSqlBotStorage(
        this IServiceCollection services,
        string connectionString,
        string? caCertificate = null)
    {
        CheckRegistration(services, connectionString);
        services.AddKeyedSingleton<NpgsqlDataSource>(s_dataSourceKey, (_, _) =>
        {
            NpgsqlDataSourceBuilder source = new(connectionString);
            if (!string.IsNullOrWhiteSpace(caCertificate))
            {
                source.UseRootCertificate(X509Certificate2.CreateFromPem(caCertificate));
            }

            return source.Build();
        });
        services.AddSingleton<IDatabaseProvider>(provider => new PostgreSqlDatabaseProvider(
            provider.GetRequiredKeyedService<NpgsqlDataSource>(s_dataSourceKey),
            provider.GetRequiredService<BotOptions>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<PostgreSqlDatabaseProvider>>()));

        return services;
    }

    /// <summary>Registers PostgreSQL storage on the bot builder.</summary>
    /// <param name="builder">The bot builder.</param>
    /// <param name="connectionString">The PostgreSQL connection string with bounded timeouts and appropriate TLS.</param>
    /// <param name="caCertificate">The optional trusted PEM server CA certificate.</param>
    /// <returns>The bot builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="connectionString"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="InvalidOperationException">A database provider is already registered.</exception>
    public static BlueskyBotBuilder WithPostgreSqlStorage(
        this BlueskyBotBuilder builder,
        string connectionString,
        string? caCertificate = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddPostgreSqlBotStorage(connectionString, caCertificate);
        return builder;
    }

    /// <summary>Builds a bounded PostgreSQL connection string from the configured <c>Bot</c> connection.</summary>
    /// <param name="configuration">The configuration containing <c>ConnectionStrings:Bot</c>.</param>
    /// <param name="development"><see langword="true" /> to allow local development TLS settings; otherwise, <see langword="false" />.</param>
    /// <param name="applicationName">The application identity sent to PostgreSQL.</param>
    /// <returns>A connection string with bounded pool and command settings.</returns>
    /// <exception cref="ArgumentNullException">The configuration is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">The connection string is missing or production TLS is unsafe.</exception>
    public static string CreateConnectionString(
        IConfiguration configuration,
        bool development,
        string applicationName = "idunno.Bluesky.BotSdk")
    {
        ArgumentNullException.ThrowIfNull(configuration);
        string configured = configuration.GetConnectionString("Bot")
            ?? throw new InvalidOperationException("ConnectionStrings:Bot is required.");

        NpgsqlConnectionStringBuilder builder = new(configured)
        {
            Pooling = true,
            MaxPoolSize = 5,
            Timeout = 10,
            CommandTimeout = 15,
            KeepAlive = 10,
            ApplicationName = applicationName
        };

        if (!development && builder.SslMode != SslMode.VerifyFull)
        {
            throw new InvalidOperationException("Production PostgreSQL connections must use SSL Mode=VerifyFull and a trusted server CA.");
        }

        return builder.ConnectionString;
    }

    private static void CheckRegistration(IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (services.Any(service => service.ServiceType == typeof(IDatabaseProvider)))
        {
            throw new InvalidOperationException("A bot database provider is already registered. Register exactly one storage provider.");
        }
    }
}
