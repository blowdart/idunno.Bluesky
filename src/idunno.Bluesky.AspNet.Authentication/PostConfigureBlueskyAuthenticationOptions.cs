// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Initializes a new instance of <see cref="PostConfigureBlueskyAuthenticationOptions"/> used to set default options..
/// </summary>
/// <param name="dataProtection">The <see cref="IDataProtectionProvider"/>.</param>
/// <param name="loggerFactory">The <see cref="ILoggerFactory"/> used to create loggers.</param>
public class PostConfigureBlueskyAuthenticationOptions(
    IDataProtectionProvider dataProtection,
    ILoggerFactory loggerFactory) : IPostConfigureOptions<BlueskyAuthenticationOptions>
{
    /// <summary>
    /// Invoked to post configure a TOptions instance.
    /// </summary>
    /// <param name="name">The name of the options instance being configured.</param>
    /// <param name="options">The options instance to configure.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> or <paramref name="name"/> is <see langword="null"/></exception>
    public void PostConfigure(string? name, BlueskyAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.DataProtectionProvider ??= dataProtection;

        ArgumentNullException.ThrowIfNull(name);

        if (string.IsNullOrEmpty(options.Cookie.Name))
        {
            options.Cookie.Name = BlueskyAuthenticationDefaults.CookiePrefix + Uri.EscapeDataString(name);
        }

        if (options.TicketDataFormat == null)
        {
            IDataProtector dataProtector = options.DataProtectionProvider.CreateProtector("idunno.Bluesky.AspNet.Authentication", name, "v1");
            options.TicketDataFormat = new TicketDataFormat(dataProtector);
        }

        options.CookieManager ??= new ChunkingCookieManager();

        if (!options.LoginPath.HasValue)
        {
            options.LoginPath = CookieAuthenticationDefaults.LoginPath;
        }

        if (!options.LogoutPath.HasValue)
        {
            options.LogoutPath = CookieAuthenticationDefaults.LogoutPath;
        }

        if (!options.AccessDeniedPath.HasValue)
        {
            options.AccessDeniedPath = CookieAuthenticationDefaults.AccessDeniedPath;
        }

        options.IdentityStore ??= new EphemeralIdentityStore(loggerFactory, options.IdentityStoreEntryTimeToLive);
        options.CorrelationCache ??= new EphemeralCorrelationStateCache(loggerFactory);
    }
}
