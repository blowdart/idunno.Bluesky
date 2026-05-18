// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

using idunno.Bluesky.AspNet.Authentication.Events;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Encapsulates options for the <see cref="BlueskyAuthenticationHandler"/>.
/// </summary>
public class BlueskyAuthenticationOptions : AuthenticationSchemeOptions
{
    private const string CorrelationPrefix = ".BlueskyAuthentication.Correlation.";

    /// <summary>
    /// Initializes a new <see cref="BlueskyAuthenticationOptions"/>.
    /// </summary>
    public BlueskyAuthenticationOptions()
    {
        ExpireTimeSpan = TimeSpan.FromDays(14);
        ReturnUrlParameter = BlueskyAuthenticationDefaults.ReturnUrlParameter;
        SlidingExpiration = true;
        Events = new BlueskyAuthenticationEvents();
    }

    /// <summary>
    /// The AccessDeniedPath property is used by the handler for the redirection target when handling ForbidAsync.
    /// </summary>
    public PathString AccessDeniedPath { get; set; }

    /// <summary>
    /// The identity store used to persist and retrieve user identities. If not provided a default in-memory store will be used.
    /// </summary>
    public IIdentityStore? IdentityStore { get; set; } = default!;

    /// <summary>
    /// The correlation cache to use to store OAuth2 correlation state. If not provided a default in-memory store will be used.
    /// </summary>
    public ICorrelationStateCache? CorrelationCache { get; set; } = default!;

    /// <summary>
    /// Gets or sets a value for how long entries are kept in the identity store.
    /// </summary>
    public TimeSpan? IdentityStoreEntryTimeToLive { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Determines the settings used to create the authentication cookie.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null" />.</exception>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description>The default value for cookie <see cref="CookieBuilder.Name"/> is <c>.AspNetCore.Cookies</c>.
    /// This value should be changed if you change the name of the <c>AuthenticationScheme</c>, especially if your
    /// system uses the cookie authentication handler multiple times.</description></item>
    /// <item><description><see cref="CookieBuilder.SameSite"/> determines if the browser should allow the cookie to be attached to same-site or cross-site requests.
    /// The default is <c>Lax</c>, which means the cookie is only allowed to be attached to cross-site requests using safe HTTP methods and same-site requests.</description></item>
    /// <item><description><see cref="CookieBuilder.HttpOnly"/> determines if the browser should allow the cookie to be accessed by client-side JavaScript.
    /// The default is <see langword="true" />, which means the cookie will only be passed to HTTP requests and is not made available to JavaScript on the page.</description></item>
    /// <item><description><see cref="CookieBuilder.Expiration"/> is currently ignored. Use <see cref="ExpireTimeSpan"/> to control lifetime of cookie authentication.</description></item>
    /// <item><description><see cref="CookieBuilder.SecurePolicy"/> defaults to <see cref="CookieSecurePolicy.SameAsRequest"/>.</description></item>
    /// </list>
    /// </remarks>
    public CookieBuilder Cookie
    {
        get;
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    } = new RequestPathBaseCookieBuilder
    {
        // the default name is configured in PostConfigureCookieAuthenticationOptions

        // To support OAuth authentication, a lax mode is required, see https://github.com/aspnet/Security/issues/1231.
        SameSite = SameSiteMode.Lax,
        HttpOnly = true,
        SecurePolicy = CookieSecurePolicy.SameAsRequest,
        IsEssential = true,
    };

    /// <summary>
    /// The component used to get cookies from the request or set them on the response.
    ///
    /// ChunkingCookieManager will be used by default.
    /// </summary>
    public ICookieManager CookieManager { get; set; } = default!;

    /// <summary>
    /// Determines the settings used to create the correlation cookie before the
    /// cookie gets added to the response.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// If an explicit <see cref="CookieBuilder.Name"/> is not provided, the system will automatically generate a
    /// unique name that begins with <c>.BlueskyAuthentication.Correlation.</c>.
    /// </para>
    /// <list type="bullet">
    /// <item><description><see cref="CookieBuilder.SameSite"/> defaults to <see cref="SameSiteMode.Lax"/>.</description></item>
    /// <item><description><see cref="CookieBuilder.HttpOnly"/> defaults to <see langword="true"/>.</description></item>
    /// <item><description><see cref="CookieBuilder.IsEssential"/> defaults to <see langword="true"/>.</description></item>
    /// <item><description><see cref="CookieBuilder.SecurePolicy"/> defaults to <see cref="CookieSecurePolicy.None"/>.</description></item>
    /// </list>
    /// </remarks>
    public CookieBuilder CorrelationCookie
    {
        get;
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    } = new RequestPathBaseCookieBuilder()
    {
        Name = CorrelationPrefix,
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        SecurePolicy = CookieSecurePolicy.None,
        IsEssential = true,
    };

    /// <summary>
    /// If set this will be used by the BlueskyAuthenticationHandler for data protection.
    /// </summary>
    public IDataProtectionProvider? DataProtectionProvider { get; set; }

    /// <summary>
    /// The Provider may be assigned to an instance of an object created by the application at startup time. The handler
    /// calls methods on the provider which give the application control at certain points where processing is occurring.
    /// If it is not provided a default instance is supplied which does nothing when the methods are called.
    /// </summary>
    public new BlueskyAuthenticationEvents Events
    {
        get => (BlueskyAuthenticationEvents)base.Events!;
        set => base.Events = value;
    }

    /// <summary>
    /// <para>
    /// Controls how much time the authentication ticket stored in the cookie will remain valid from the point it is created.
    /// The expiration information is stored in the protected cookie ticket. Because of that an expired cookie will be ignored
    /// even if it is passed to the server after the browser should have purged it.
    /// </para>
    /// <para>
    /// This is separate from the value of <see cref="CookieOptions.Expires"/>, which specifies
    /// how long the browser will keep the cookie.
    /// </para>
    /// </summary>
    public TimeSpan ExpireTimeSpan { get; set; }

    /// <summary>
    /// The LoginPath property is used by the handler for the redirection target when handling ChallengeAsync.
    /// The current url which is added to the LoginPath as a query string parameter named by the ReturnUrlParameter.
    /// Once a request to the LoginPath grants a new SignIn identity, the ReturnUrlParameter value is used to redirect
    /// the browser back to the original url.
    /// </summary>
    public PathString LoginPath { get; set; }

    /// <summary>
    /// If the LogoutPath is provided the handler then a request to that path will redirect based on the ReturnUrlParameter.
    /// </summary>
    public PathString LogoutPath { get; set; }

    /// <summary>
    /// The ReturnUrlParameter determines the name of the query string parameter which is appended by the handler
    /// during a Challenge. This is also the query string parameter looked for when a request arrives on the login
    /// path or logout path, in order to return to the original url after the action is performed.
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "The authentication handler uses this string as a query parameter value, not a URI.")]
    public string ReturnUrlParameter { get; set; }

    /// <summary>
    /// The SlidingExpiration is set to <see langword="true" /> to instruct the handler to re-issue a new cookie with a new
    /// expiration time any time it processes a request which is more than halfway through the expiration window.
    /// </summary>
    public bool SlidingExpiration { get; set; }

    /// <summary>
    /// The TicketDataFormat is used to protect and unprotect the identity and other properties which are stored in the
    /// cookie value. If not provided one will be created using <see cref="DataProtectionProvider"/>.
    /// </summary>
    public ISecureDataFormat<AuthenticationTicket> TicketDataFormat { get; set; } = default!;
}
