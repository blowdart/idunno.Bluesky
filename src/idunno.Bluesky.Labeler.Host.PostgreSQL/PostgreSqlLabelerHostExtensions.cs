// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.Bluesky.AspNet.Authentication;
using idunno.Bluesky.AspNet.Authentication.PostgreSQL;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace idunno.Bluesky.Labeler.Host.PostgreSQL;

/// <summary>Registers the canonical host's label and website persistence in one external PostgreSQL database.</summary>
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "Custom canonical hosts reuse this provider integration through a project reference.")]
public static class PostgreSqlLabelerHostExtensions
{
    /// <summary>Registers PostgreSQL labels and optional durable website authentication without a provider fallback.</summary>
    /// <param name="builder">The canonical application builder.</param>
    /// <returns>The application builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The database configuration is missing or selects a different provider.</exception>
    public static WebApplicationBuilder AddPostgreSqlCanonicalLabelerStorage(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.Configuration["Labeler:DatabaseProvider"] is string provider && !StringComparer.OrdinalIgnoreCase.Equals(provider, "postgresql"))
        {
            throw new InvalidOperationException("The PostgreSQL canonical host cannot select another database provider.");
        }
        string connectionString = builder.Configuration["Labeler:ConnectionString"]
            ?? throw new InvalidOperationException("Configure Labeler:ConnectionString for the external PostgreSQL database.");
        builder.Services.AddPostgreSqlLabelStorage(connectionString);
        if (builder.Configuration.GetValue<bool>("LabelerUI:Enabled"))
        {
            Uri origin = new(builder.Configuration["Labeler:PublicEndpoint"]
                ?? throw new InvalidOperationException("Configure Labeler:PublicEndpoint."));
            AddPersistentAuthentication(builder.Services, connectionString, origin);
        }
        return builder;
    }

    /// <summary>Shares the labeler database for protected website identities, login state and Data Protection keys.</summary>
    internal static void AddPersistentAuthentication(IServiceCollection services, string connectionString, Uri origin)
    {
        services.AddDataProtection().SetApplicationName("idunno.Bluesky.Labeler.Host:" + origin.GetLeftPart(UriPartial.Authority));
        services.AddSingleton(new PostgreSqlXmlRepository(connectionString));
        services.AddSingleton<ILabelerHostAuthenticationPersistence>(provider => provider.GetRequiredService<PostgreSqlXmlRepository>());
        services.AddOptions<KeyManagementOptions>()
            .Configure<PostgreSqlXmlRepository>((options, repository) => options.XmlRepository = repository);
        services.AddOptions<BlueskyAuthenticationOptions>(BlueskyAuthenticationDefaults.AuthenticationScheme)
            .Configure<ILoggerFactory>((options, loggerFactory) =>
            {
                options.IdentityStore = new PostgreSqlIdentityStore(
                    connectionString: connectionString,
                    entryTimeToLive: options.IdentityStoreEntryTimeToLive ?? options.ExpireTimeSpan,
                    refreshLockLength: options.RefreshLockLength,
                    loggerFactory: loggerFactory);
                options.CorrelationStateCache = new PostgreSqlCorrelationStateCache(
                    connectionString: connectionString,
                    loggerFactory: loggerFactory);
            });
    }
}
