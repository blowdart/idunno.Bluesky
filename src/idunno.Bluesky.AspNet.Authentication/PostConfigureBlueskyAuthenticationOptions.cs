// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using idunno.Bluesky.AspNet.Authentication.Events;

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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="BlueskyAuthenticationOptions.RefreshCheckWait"/> is not greater than zero.</exception>
    public void PostConfigure(string? name, BlueskyAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Task.Delay throws on a negative wait, and a zero wait would spin through every check without ever giving the
        // refresh holder time to finish, so reject both here rather than part way through authenticating a request.
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.RefreshCheckWait, TimeSpan.Zero);

        options.DataProtectionProvider ??= dataProtection;

        ArgumentNullException.ThrowIfNull(name);

        if (string.IsNullOrEmpty(options.Cookie.Name))
        {
            options.Cookie.Name = BlueskyAuthenticationDefaults.CookiePrefix + Uri.EscapeDataString(name);
        }

        // The correlation cookie is named per scheme for the same reason as the authentication cookie. Two Bluesky
        // schemes in one application which shared a correlation cookie name would overwrite each other's logins.
        if (string.IsNullOrEmpty(options.CorrelationCookie.Name))
        {
            options.CorrelationCookie.Name = Constants.CorrelationCookieName + "." + Uri.EscapeDataString(name);
        }

        if (options.TicketDataFormat == null)
        {
            IDataProtector dataProtector = options.DataProtectionProvider.CreateProtector("idunno.Bluesky.AspNet.Authentication", name, "v1");
            options.TicketDataFormat = new TicketDataFormat(dataProtector);
        }

        options.CookieManager ??= new ChunkingCookieManager();

        // The authentication cookie is honoured for ExpireTimeSpan, but the credentials it refers to live in the
        // identity store. If the store drops its entries first the user is silently signed out part way through the
        // cookie's lifetime, so follow the cookie lifetime unless the application asked for something specific.
        options.IdentityStoreEntryTimeToLive ??= options.ExpireTimeSpan;

        if (options.IdentityStoreEntryTimeToLive < options.ExpireTimeSpan)
        {
            loggerFactory.CreateLogger<PostConfigureBlueskyAuthenticationOptions>()
                .IdentityStoreTimeToLiveShorterThanCookieLifetime(
                    name,
                    options.IdentityStoreEntryTimeToLive.Value,
                    options.ExpireTimeSpan);
        }

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

        options.IdentityStore ??= new EphemeralIdentityStore(loggerFactory, options.IdentityStoreEntryTimeToLive, options.RefreshLockLength);

        // The identity store holds the access token, the refresh token and the DPoP proof key, and the correlation
        // cache holds the PKCE verifier and the DPoP private key of a login in flight. Both are encrypted at rest
        // unless the application asked for something else, so a store backed by shared infrastructure does not hold
        // them in the clear by default.
        if (!options.IdentityStoreEventsConfigured)
        {
            options.IdentityStoreEvents = new DataProtectingIdentityStoreEvents(options.DataProtectionProvider);
        }

        if (!options.CorrelationStateCacheEventsConfigured)
        {
            options.CorrelationStateCacheEvents = new DataProtectingCorrelationStateCacheEvents(options.DataProtectionProvider);
        }

        // The options are configured named by scheme, so a store cannot read the events it should raise from its own
        // constructor, which only ever sees the unnamed options instance. They are pushed onto the store here instead.
        options.IdentityStore.Events = options.IdentityStoreEvents;

        options.CorrelationStateCache ??= new EphemeralCorrelationStateCache(loggerFactory);
        options.CorrelationStateCache.Events = options.CorrelationStateCacheEvents;
    }
}
