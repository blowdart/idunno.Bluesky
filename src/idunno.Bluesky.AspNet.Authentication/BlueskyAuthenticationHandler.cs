// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication.Events;
using System.Diagnostics.CodeAnalysis;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Implements the ASP.NET Core authentication handler using Bluesky logins
/// </summary>
public class BlueskyAuthenticationHandler : SignInAuthenticationHandler<BlueskyAuthenticationOptions>
{
    private const string HeaderValueNoCache = "no-cache";
    private const string HeaderValueNoCacheNoStore = "no-cache,no-store";
    private const string HeaderValueEpochDate = "Thu, 01 Jan 1970 00:00:00 GMT";

    private readonly IIdentityStore _identityStore;

    private Task<AuthenticateResult>? _readCookieTask;
    
#pragma warning disable S4487 // Unread "private" fields should be removed
    private DateTimeOffset? _refreshIssuedUtc;
    private DateTimeOffset? _refreshExpiresUtc;
    private AuthenticationTicket? _refreshTicket;
#pragma warning disable CS0414 // Assigned but its value is never used
    private bool _shouldRefresh;
    private bool _signInCalled;
    private bool _signOutCalled;
#pragma warning restore CS0414 // Assigned but its value is never used
#pragma warning restore S4487 // Unread "private" fields should be removed

    /// <summary>
    /// Initalizes a new instance of <see cref="BlueskyAuthenticationHandler"/>
    /// </summary>
    /// <param name="options">The monitor for the options instance.</param>
    /// <param name="agentOptions">The monitor for the agent options instance.</param>
    /// <param name="logger">The <see cref="ILoggerFactory"/> to create loggers from.</param>
    /// <param name="encoder">The <see cref="UrlEncoder"/>.</param>
    /// <param name="clock">The <see cref="ISystemClock"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> or <paramref name="options.CurrentValue.IdentityStore"/> is <see langword="null"/>.</exception>
    [Obsolete("ISystemClock is obsolete, use TimeProvider on AuthenticationSchemeOptions instead.")]
    [SuppressMessage("Info Code Smell", "S1133:Deprecated code should be removed", Justification = "Until ASP.NET Core removes this from SignInAuthenticationHandler it must stay.")]
    public BlueskyAuthenticationHandler(
        IOptionsMonitor<BlueskyAuthenticationOptions> options,
        IOptionsMonitor<BlueskyAgentOptions> agentOptions,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock) : base(options, logger, encoder, clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.CurrentValue.IdentityStore);

        _identityStore = options.CurrentValue.IdentityStore;

        BlueskyAgentOptionsMonitor = agentOptions;
    }

    /// <summary>
    /// Creates a new instance of <see cref="BlueskyAuthenticationHandler"/>
    /// </summary>
    /// <param name="options">The monitor for the options instance.</param>
    /// <param name="agentOptions">The monitor for the agent options instance.</param>
    /// <param name="logger">The <see cref="ILoggerFactory"/> to create loggers from.</param>
    /// <param name="encoder">The <see cref="UrlEncoder"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> or <paramref name="options.CurrentValue.IdentityStore"/> is <see langword="null"/>.</exception>
    public BlueskyAuthenticationHandler(
        IOptionsMonitor<BlueskyAuthenticationOptions> options,
        IOptionsMonitor<BlueskyAgentOptions> agentOptions,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.CurrentValue.IdentityStore);

        _identityStore = options.CurrentValue.IdentityStore;

        BlueskyAgentOptionsMonitor = agentOptions;
    }

    /// <summary>
    /// The handler calls methods on the events which give the application control at certain points where processing is occurring.
    /// If it is not provided a default instance is supplied which does nothing when the methods are called.
    /// </summary>
    protected new BlueskyAuthenticationEvents Events
    {
        get { return (BlueskyAuthenticationEvents)base.Events!; }
        set { base.Events = value; }
    }

    /// <summary>
    /// Gets an <see cref="IOptionsMonitor{TOptions}"/> for the <see cref="BlueskyAgentOptions"/>.
    /// </summary>
    protected IOptionsMonitor<BlueskyAgentOptions> BlueskyAgentOptionsMonitor { get; }

    /// <summary>
    /// Gets the current <see cref="BlueskyAgentOptions"/>.
    /// </summary>
    protected BlueskyAgentOptions BlueskyAgentOptions => BlueskyAgentOptionsMonitor.CurrentValue;

    /// <summary>
    /// Gets or sets the <see cref="Did"/> for the current user.
    /// This is typically set during the authentication process from a claim in the authentication cookie.
    /// </summary>
    protected Did? CurrentUserDid { get; set; }

    /// <summary>
    /// Creates a new instance of the events instance.
    /// </summary>
    /// <returns>A new instance of the events instance.</returns>
    protected override Task<object> CreateEventsAsync() => Task.FromResult<object>(new BlueskyAuthenticationEvents());

    /// <summary>
    /// Handles authentication for the request.
    /// </summary>
    /// <returns>The result of the authentication attempt.</returns>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        AuthenticateResult result = await EnsureCookieTicket().ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return result;
        }

        await CheckForRefreshAsync(result.Ticket).ConfigureAwait(false);

        Debug.Assert(result.Ticket != null);

        var context = new BlueskyValidatePrincipalContext(Context, Scheme, Options, result.Ticket);

        await Events.ValidatePrincipal(context).ConfigureAwait(false);

        if (context.Principal == null || context.Principal.Identity is null || !context.Principal.Claims.Any())
        {
            Logger.PrincipalValidationFailedNoPrincipleOrClaims();
            return AuthenticateResults.s_noPrincipal;
        }

        if (context.ShouldRenew)
        {
            RequestRefresh(result.Ticket, context.Principal);
        }

        return AuthenticateResult.Success(new AuthenticationTicket(context.Principal, context.Properties, Scheme.Name));
    }

    /// <summary>
    /// Handles the authentication challenge for the request.
    /// </summary>
    /// <param name="properties">The authentication properties.</param>
    /// <returns>A task that represents the completion of the challenge handling.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="properties"/> is <see langword="null"/>.</exception>
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        string? redirectUri = properties.RedirectUri;
        if (string.IsNullOrEmpty(redirectUri))
        {
            redirectUri = OriginalPathBase + OriginalPath + Request.QueryString;
        }

        string loginUri = Options.LoginPath + QueryString.Create(Options.ReturnUrlParameter, redirectUri);
        var redirectContext = new RedirectContext<BlueskyAuthenticationOptions>(Context, Scheme, Options, properties, BuildRedirectUri(loginUri));
        await Events.RedirectToLogin(redirectContext).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles the sign in for the request.
    /// </summary>
    /// <param name="user">The user to sign in.</param>
    /// <param name="properties">The authentication properties, if any.</param>
    /// <returns>A task that represents the completion of the sign in handling.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="user"/> is <see langword="null"/>.</exception>
    protected override async Task HandleSignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.Identity is not ClaimsIdentity userIdentity)
        {
            Logger.PrincipalDidNotContainAClaimsIdentity();
            return;
        }

        properties ??= new AuthenticationProperties();
        _signInCalled = true;

        await EnsureCookieTicket().ConfigureAwait(false);
        CookieOptions cookieOptions = BuildCookieOptions();

        BlueskySigningInContext signInContext = new(
            Context,
            Scheme,
            Options,
            user,
            properties,
            cookieOptions);

        DateTimeOffset issuedUtc;
        if (signInContext.Properties.IssuedUtc.HasValue)
        {
            issuedUtc = signInContext.Properties.IssuedUtc.Value;
        }
        else
        {
            issuedUtc = TimeProvider.GetUtcNow();
            signInContext.Properties.IssuedUtc = issuedUtc;
        }

        await Events.SigningIn(signInContext).ConfigureAwait(false);

        if (signInContext.Properties.IsPersistent)
        {
            DateTimeOffset expiresUtc = signInContext.Properties.ExpiresUtc ?? issuedUtc.Add(Options.ExpireTimeSpan);
            signInContext.CookieOptions.Expires = expiresUtc.ToUniversalTime();
        }

        // Store the full identity in the identity store.
        await _identityStore.Add(userIdentity).ConfigureAwait(false);

        // Strip the principal down to just the DID, acting as a reference cookie.
        var ticketPrincipal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [
                    new Claim(AtProtoClaims.Did, userIdentity.FindFirst(AtProtoClaims.Did)!.Value, ClaimValueTypes.String, userIdentity.FindFirst(AtProtoClaims.Did)!.Issuer)
                ],
                userIdentity.AuthenticationType));

        var ticket = new AuthenticationTicket(ticketPrincipal, signInContext.Properties, signInContext.Scheme.Name);

        var signedInContext = new BlueskySignedInContext(
            Context,
            Scheme,
            signInContext.Principal!,
            signInContext.Properties,
            Options);

        string cookieValue = Options.TicketDataFormat.Protect(ticket, GetTlsTokenBinding());

        Options.CookieManager.AppendResponseCookie(
            Context,
            Options.Cookie.Name!,
            cookieValue,
            signInContext.CookieOptions);

        await Events.SignedIn(signedInContext).ConfigureAwait(false);

        // Only honor the ReturnUrl query string parameter on the login path
        bool shouldHonorReturnUrlParameter = Options.LoginPath.HasValue && OriginalPath == Options.LoginPath;
        await ApplyHeaders(shouldRedirect: true, shouldHonorReturnUrlParameter, signedInContext.Properties).ConfigureAwait(false);
        Logger.AuthenticationSchemeSignedIn(Scheme.Name);
    }

    /// <summary>
    /// Handles the sign out of the current user.
    /// </summary>
    /// <param name="properties">The authentication properties, if any.</param>
    /// <returns>A task that represents the completion of the sign out handling.</returns>
    protected override async Task HandleSignOutAsync(AuthenticationProperties? properties)
    {
        properties ??= new AuthenticationProperties();

        _signOutCalled = true;

        CookieOptions cookieOptions = BuildCookieOptions();

        if (CurrentUserDid is not null)
        {
            await _identityStore.Remove(CurrentUserDid).ConfigureAwait(false);
        }

        var context = new BlueskySigningOutContext(
            Context,
            Scheme,
            Options,
            properties,
            cookieOptions);

        await Events.SigningOut(context).ConfigureAwait(false);

        Options.CookieManager.DeleteCookie(
            Context,
            Options.Cookie.Name!,
            context.CookieOptions);

        // Only honor the ReturnUrl query string parameter on the logout path
        bool shouldHonorReturnUrlParameter = Options.LogoutPath.HasValue && OriginalPath == Options.LogoutPath;
        await ApplyHeaders(shouldRedirect: true, shouldHonorReturnUrlParameter, context.Properties).ConfigureAwait(false);

        Logger.AuthenticationSchemeSignedOut(Scheme.Name);
    }

    private CookieOptions BuildCookieOptions()
    {
        CookieOptions cookieOptions = Options.Cookie.Build(Context);
        // ignore the 'Expires' value as this will be computed elsewhere
        cookieOptions.Expires = null;

        return cookieOptions;
    }

    private async Task ApplyHeaders(bool shouldRedirect, bool shouldHonorReturnUrlParameter, AuthenticationProperties properties)
    {
        Response.Headers.CacheControl = HeaderValueNoCacheNoStore;
        Response.Headers.Pragma = HeaderValueNoCache;
        Response.Headers.Expires = HeaderValueEpochDate;

        if (shouldRedirect && Response.StatusCode == 200)
        {
            // set redirect uri in order:
            // 1. properties.RedirectUri
            // 2. query parameter ReturnUrlParameter (if the request path matches the path set in the options)
            //
            // Absolute uri is not allowed if it is from query string as query string is not
            // a trusted source.
            string? redirectUri = properties.RedirectUri;
            if (shouldHonorReturnUrlParameter && string.IsNullOrEmpty(redirectUri))
            {
                redirectUri = Request.Query[Options.ReturnUrlParameter];
                if (string.IsNullOrEmpty(redirectUri) || !IsHostRelative(redirectUri))
                {
                    redirectUri = null;
                }
            }

            if (redirectUri != null)
            {
                await Events.RedirectToReturnUrl(
                    new RedirectContext<BlueskyAuthenticationOptions>(Context, Scheme, Options, properties, redirectUri)).ConfigureAwait(false);
            }
        }
    }

    private async Task CheckForRefreshAsync(AuthenticationTicket ticket)
    {
        DateTimeOffset currentUtc = TimeProvider.GetUtcNow();
        DateTimeOffset? issuedUtc = ticket.Properties.IssuedUtc;
        DateTimeOffset? expiresUtc = ticket.Properties.ExpiresUtc;
        bool allowRefresh = ticket.Properties.AllowRefresh ?? true;
        if (issuedUtc != null && expiresUtc != null && Options.SlidingExpiration && allowRefresh)
        {
            TimeSpan timeElapsed = currentUtc.Subtract(issuedUtc.Value);
            TimeSpan timeRemaining = expiresUtc.Value.Subtract(currentUtc);

            var eventContext = new BlueskySlidingExpirationContext(Context, Scheme, Options, ticket, timeElapsed, timeRemaining)
            {
                ShouldRenew = timeRemaining < timeElapsed,
            };
            await Events.CheckSlidingExpiration(eventContext).ConfigureAwait(false);

            if (eventContext.ShouldRenew)
            {
                RequestRefresh(ticket);
            }
        }
    }

    private static AuthenticationTicket CloneTicket(AuthenticationTicket ticket, ClaimsPrincipal? replacedPrincipal)
    {
        ClaimsPrincipal principal = replacedPrincipal ?? ticket.Principal;
        ClaimsPrincipal newPrincipal = new();

        foreach (ClaimsIdentity identity in principal.Identities)
        {
            newPrincipal.AddIdentity(identity.Clone());
        }

        var newProperties = new AuthenticationProperties();
        foreach (KeyValuePair<string, string?> item in ticket.Properties.Items)
        {
            newProperties.Items[item.Key] = item.Value;
        }

        return new AuthenticationTicket(newPrincipal, newProperties, ticket.AuthenticationScheme);
    }

    private Task<AuthenticateResult> EnsureCookieTicket()
    {
        // We only need to read the ticket once
        _readCookieTask ??= ReadCookieTicket();
        return _readCookieTask;
    }

    private string? GetTlsTokenBinding()
    {
        byte[]? binding = Context.Features.Get<ITlsTokenBindingFeature>()?.GetProvidedTokenBindingId();
        return binding == null ? null : Convert.ToBase64String(binding);
    }

    private static bool IsHostRelative(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }
        if (path.Length == 1)
        {
            return path[0] == '/';
        }
        return path[0] == '/' && path[1] != '/' && path[1] != '\\';
    }

    private async Task<AuthenticateResult> ReadCookieTicket()
    {
        string? cookie = Options.CookieManager.GetRequestCookie(Context, Options.Cookie.Name!);
        if (string.IsNullOrEmpty(cookie))
        {
            return AuthenticateResult.NoResult();
        }

        AuthenticationTicket? ticket = Options.TicketDataFormat.Unprotect(cookie, GetTlsTokenBinding());
        if (ticket == null)
        {
            return AuthenticateResults.s_failedUnprotectingTicket;
        }

        Claim? didClaim = ticket.Principal.Claims.FirstOrDefault(c => c.Type.Equals(AtProtoClaims.Did, StringComparison.OrdinalIgnoreCase));

        if (didClaim is null)
        {
            return AuthenticateResults.s_missingDidInCookie;
        }
       
        CurrentUserDid = new Did(didClaim.Value);
        ClaimsIdentity? storedIdentity = await _identityStore.GetIdentity(didClaim.Value).ConfigureAwait(false);

        if (storedIdentity == null)
        {
            return AuthenticateResults.s_missingIdentityInStore;
        }

        DateTimeOffset currentUtc = TimeProvider.GetUtcNow();
        DateTimeOffset? expiresUtc = ticket.Properties.ExpiresUtc;

        if (expiresUtc != null && expiresUtc.Value < currentUtc)
        {
            await _identityStore.Remove(CurrentUserDid).ConfigureAwait(false);
            CurrentUserDid = null;
            return AuthenticateResults.s_expiredTicket;
        }

        // Rehydrate the full identity from the identity store
        var hydratedTicket = new AuthenticationTicket(new ClaimsPrincipal(storedIdentity), ticket.Properties, ticket.AuthenticationScheme);

        // Finally we have a valid ticket
        return AuthenticateResult.Success(hydratedTicket);
    }

    private void RequestRefresh(AuthenticationTicket ticket, ClaimsPrincipal? replacedPrincipal = null)
    {
        DateTimeOffset? issuedUtc = ticket.Properties.IssuedUtc;
        DateTimeOffset? expiresUtc = ticket.Properties.ExpiresUtc;

        if (issuedUtc != null && expiresUtc != null)
        {
            _shouldRefresh = true;
            DateTimeOffset currentUtc = TimeProvider.GetUtcNow();
            _refreshIssuedUtc = currentUtc;
            TimeSpan timeSpan = expiresUtc.Value.Subtract(issuedUtc.Value);
            _refreshExpiresUtc = currentUtc.Add(timeSpan);
            _refreshTicket = CloneTicket(ticket, replacedPrincipal);
        }
    }
}
