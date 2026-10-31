// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.AspNet.Authentication;
using idunno.Bluesky.AspNet.Authentication.PostgreSQL;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace idunno.Bluesky.Labeler.Host;

internal static partial class Program
{
    /// <summary>Shares the labeler database for protected website identities, login state and Data Protection keys.</summary>
    internal static void AddPersistentAuthentication(IServiceCollection services, string connectionString, Uri origin)
    {
        services.AddDataProtection().SetApplicationName("idunno.Bluesky.Labeler.Host:" + origin.GetLeftPart(UriPartial.Authority));
        services.AddSingleton(new PostgreSqlXmlRepository(connectionString));
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
