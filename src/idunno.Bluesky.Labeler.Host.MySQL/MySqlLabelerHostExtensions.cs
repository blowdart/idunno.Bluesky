// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.Bluesky.AspNet.Authentication;
using idunno.Bluesky.AspNet.Authentication.MySQL;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace idunno.Bluesky.Labeler.Host.MySQL;

/// <summary>Registers the canonical host's label and website persistence in one external MySQL database.</summary>
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "Custom canonical hosts reuse this provider integration through a project reference.")]
public static class MySqlLabelerHostExtensions
{
    /// <summary>Registers MySQL labels and optional durable website authentication without a provider fallback.</summary>
    /// <param name="builder">The canonical application builder.</param>
    /// <returns>The application builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The database configuration is missing or selects a different provider.</exception>
    public static WebApplicationBuilder AddMySqlCanonicalLabelerStorage(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.Configuration["Labeler:DatabaseProvider"] is string provider && !StringComparer.OrdinalIgnoreCase.Equals(provider, "mysql"))
        {
            throw new InvalidOperationException("The MySQL canonical host cannot select another database provider.");
        }
        string connectionString = builder.Configuration["Labeler:ConnectionString"]
            ?? throw new InvalidOperationException("Configure Labeler:ConnectionString for the external MySQL database.");
        builder.Services.AddMySqlLabelStorage(connectionString);
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
        services.AddSingleton(new MySqlXmlRepository(connectionString));
        services.AddSingleton<ILabelerHostAuthenticationPersistence>(provider => provider.GetRequiredService<MySqlXmlRepository>());
        services.AddOptions<KeyManagementOptions>()
            .Configure<MySqlXmlRepository>((options, repository) => options.XmlRepository = repository);
        services.AddOptions<BlueskyAuthenticationOptions>(BlueskyAuthenticationDefaults.AuthenticationScheme)
            .Configure<ILoggerFactory>((options, loggerFactory) =>
            {
                options.IdentityStore = new MySqlIdentityStore(
                    connectionString: connectionString,
                    entryTimeToLive: options.IdentityStoreEntryTimeToLive ?? options.ExpireTimeSpan,
                    refreshLockLength: options.RefreshLockLength,
                    loggerFactory: loggerFactory);
                options.CorrelationStateCache = new MySqlCorrelationStateCache(
                    connectionString: connectionString,
                    loggerFactory: loggerFactory);
            });
    }
}
