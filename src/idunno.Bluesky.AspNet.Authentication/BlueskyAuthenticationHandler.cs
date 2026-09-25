// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Security.Claims;
using System.Text.Encodings.Web;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication.Events;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Implements the ASP.NET Core authentication handler using Bluesky logins
/// </summary>
public class BlueskyAuthenticationHandler : SignInAuthenticationHandler<BlueskyAuthenticationOptions>
{
    private const string HeaderValueNoCache = "no-cache";
    private const string HeaderValueNoCacheNoStore = "no-cache,no-store";
    private const string HeaderValueEpochDate = "Thu, 01 Jan 1970 00:00:00 GMT";

    private static readonly TimeSpan s_refreshClockSkew = TimeSpan.FromMinutes(5);

    private Task<AuthenticateResult>? _readCookieTask;

    private DateTimeOffset? _refreshIssuedUtc;
    private DateTimeOffset? _refreshExpiresUtc;
    private AuthenticationTicket? _refreshTicket;
    private bool _shouldRefresh;
    private bool _signInCalled;
    private bool _signOutCalled;

    private readonly BlueskyAuthenticationMetrics _metrics;

    /// <summary>
    /// Initalizes a new instance of <see cref="BlueskyAuthenticationHandler"/>
    /// </summary>
    /// <param name="options">The monitor for the options instance.</param>
    /// <param name="agentOptions">The monitor for the agent options instance.</param>
    /// <param name="logger">The <see cref="ILoggerFactory"/> to create loggers from.</param>
    /// <param name="meterFactory">The <see cref="IMeterFactory"/> to create meters from.</param>
    /// <param name="encoder">The <see cref="UrlEncoder"/>.</param>
    /// <param name="clock">The <see cref="ISystemClock"/>.</param>
    /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> the agents it creates should make their requests through.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is <see langword="null"/>.</exception>
    [Obsolete("ISystemClock is obsolete, use TimeProvider on AuthenticationSchemeOptions instead.")]
    [SuppressMessage("Info Code Smell", "S1133:Deprecated code should be removed", Justification = "Until ASP.NET Core removes this from SignInAuthenticationHandler it must stay.")]
    public BlueskyAuthenticationHandler(
        IOptionsMonitor<BlueskyAuthenticationOptions> options,
        IOptionsMonitor<BlueskyAgentOptions> agentOptions,
        ILoggerFactory logger,
        IMeterFactory meterFactory,
        UrlEncoder encoder,
        ISystemClock clock,
        IHttpClientFactory httpClientFactory) : base(options, logger, encoder, clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        BlueskyAgentOptionsMonitor = agentOptions;
        HttpClientFactory = httpClientFactory;
        _metrics = new BlueskyAuthenticationMetrics(meterFactory);
    }

    /// <summary>
    /// Creates a new instance of <see cref="BlueskyAuthenticationHandler"/>
    /// </summary>
    /// <param name="options">The monitor for the options instance.</param>
    /// <param name="agentOptions">The monitor for the agent options instance.</param>
    /// <param name="logger">The <see cref="ILoggerFactory"/> to create loggers from.</param>
    /// <param name="meterFactory">The <see cref="IMeterFactory"/> to create meters from.</param>
    /// <param name="encoder">The <see cref="UrlEncoder"/>.</param>
    /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> the agents it creates should make their requests through.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is <see langword="null"/>.</exception>
    public BlueskyAuthenticationHandler(
        IOptionsMonitor<BlueskyAuthenticationOptions> options,
        IOptionsMonitor<BlueskyAgentOptions> agentOptions,
        ILoggerFactory logger,
        IMeterFactory meterFactory,
        UrlEncoder encoder,
        IHttpClientFactory httpClientFactory)
        : base(options, logger, encoder)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        BlueskyAgentOptionsMonitor = agentOptions;
        HttpClientFactory = httpClientFactory;
        _metrics = new BlueskyAuthenticationMetrics(meterFactory);
    }

    /// <summary>
    /// Gets the <see cref="IIdentityStore"/> configured for the authentication scheme this handler is running as.
    /// </summary>    /// <remarks>
    /// <para>
    ///   <see cref="BlueskyAuthenticationOptions"/> are configured against the name of the authentication scheme they belong to, so this must come from
    ///   <see cref="AuthenticationHandler{TOptions}.Options"/>, which the base class resolves with the scheme name, rather than from the unnamed options
    ///   instance exposed by <see cref="IOptionsMonitor{TOptions}.CurrentValue"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if no <see cref="IIdentityStore"/> is configured for the scheme.</exception>
    private IIdentityStore IdentityStore =>
        Options.IdentityStore ??
            throw new InvalidOperationException($"No {nameof(IIdentityStore)} is configured for the {Scheme.Name} authentication scheme.");

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
    /// Gets the <see cref="IHttpClientFactory"/> the agents this handler creates make their requests through.
    /// </summary>
    protected IHttpClientFactory HttpClientFactory { get; }

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
        AuthenticateResult result = await HandleAuthenticateCoreAsync().ConfigureAwait(false);

        // A request with no cookie for this scheme is not an authentication attempt, and counting it would make the
        // counter track request volume instead of the health of established sessions.
        if (!result.None)
        {
            _metrics.AuthenticationOutcomes.Add(
                1,
                new KeyValuePair<string, object?>(
                    BlueskyAuthenticationMetrics.AuthenticationResultTagName,
                    AuthenticateResults.ReasonFor(result)));
        }

        return result;
    }

    private async Task<AuthenticateResult> HandleAuthenticateCoreAsync()
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

        // Signing in replaces whatever session the request arrived with. Its credentials are still live at the
        // authorization server, and nothing will ever present the cookie which named them again, so they are revoked
        // and dropped rather than left in the store until they age out.
        if (CurrentUserDid is not null &&
            userIdentity.FindFirst(AtProtoClaims.Did)?.Value is string signingInDid &&
            !CurrentUserDid.Value.Equals(signingInDid, StringComparison.Ordinal))
        {
            Did replacedDid = CurrentUserDid;

            await RevokeCredentials(replacedDid).ConfigureAwait(false);
            await IdentityStore.Remove(replacedDid).ConfigureAwait(false);
            CurrentUserDid = null;

            Logger.PreviousSessionReplacedOnSignIn(signingInDid, replacedDid);
        }

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

        // The ticket carries its own expiry, which is what ReadCookieTicket enforces on the way back in. Without this
        // the cookie is honoured for as long as the browser presents it, whatever ExpireTimeSpan says, because nothing
        // else writes ExpiresUtc. This is set before the SigningIn event so a handler can still override it.
        signInContext.Properties.ExpiresUtc ??= issuedUtc.Add(Options.ExpireTimeSpan);

        await Events.SigningIn(signInContext).ConfigureAwait(false);

        if (signInContext.Properties.IsPersistent)
        {
            DateTimeOffset expiresUtc = signInContext.Properties.ExpiresUtc ?? issuedUtc.Add(Options.ExpireTimeSpan);
            signInContext.CookieOptions.Expires = expiresUtc.ToUniversalTime();
        }

        // Store the full identity in the identity store.
        await IdentityStore.Add(userIdentity).ConfigureAwait(false);

        // Strip the principal down to just the DID, acting as a reference cookie.
        if (!TryCreateReferencePrincipal(userIdentity, out ClaimsPrincipal? ticketPrincipal))
        {
            Logger.PrincipalDidNotContainADidClaim();
            return;
        }

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

        _metrics.SigninsSucceeded.Add(1);

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

        // Sign out can be called on a request which was never authenticated, for example a dedicated sign out endpoint
        // which does not require authorization, in which case CurrentUserDid has not been populated. Fall back to the
        // DID in the request cookie so the stored credentials are always cleaned up rather than left until they expire.
        Did? signingOutDid = CurrentUserDid;

        if (signingOutDid is null && TryReadDidFromRequestCookie(out Did? didFromCookie))
        {
            Logger.SignOutDidRecoveredFromCookie();
            signingOutDid = didFromCookie;
        }

        if (signingOutDid is not null)
        {
            await RevokeCredentials(signingOutDid).ConfigureAwait(false);
            await IdentityStore.Remove(signingOutDid).ConfigureAwait(false);
            CurrentUserDid = null;
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

        _metrics.SignOuts.Add(1);

        Logger.AuthenticationSchemeSignedOut(Scheme.Name);
    }

    /// <summary>
    /// Called when the handler is initialized for a request, to register the hook which re-issues the cookie when a
    /// renewal has been requested.
    /// </summary>
    /// <returns>A task that represents the completion of the initialization.</returns>
    /// <remarks>
    /// <para>
    ///   <see cref="FinishResponseAsync"/> has to run once the response is being sent, because a renewal can be asked
    ///   for while authenticating a request, which happens long before anything else would write the cookie. Without
    ///   this registration <see cref="BlueskyAuthenticationOptions.SlidingExpiration"/> and
    ///   <see cref="Events.BlueskyValidatePrincipalContext.ShouldRenew"/> would have no effect.
    /// </para>
    /// </remarks>
    protected override Task InitializeHandlerAsync()
    {
        Context.Response.OnStarting(FinishResponseAsync);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Hook that is called when the response about to be sent.
    /// </summary>
    /// <returns>A task that represents the completion of the response finishing.</returns>
    protected virtual async Task FinishResponseAsync()
    {
        // Only renew if requested, and neither sign in or sign out was called
        if (!_shouldRefresh || _signInCalled || _signOutCalled)
        {
            return;
        }

        AuthenticationTicket? ticket = _refreshTicket;
        if (ticket != null)
        {
            AuthenticationProperties properties = ticket.Properties;

            if (_refreshIssuedUtc.HasValue)
            {
                properties.IssuedUtc = _refreshIssuedUtc;
            }

            if (_refreshExpiresUtc.HasValue)
            {
                properties.ExpiresUtc = _refreshExpiresUtc;
            }

            string cookieValue = Options.TicketDataFormat.Protect(ticket, GetTlsTokenBinding());

            CookieOptions cookieOptions = BuildCookieOptions();
            if (properties.IsPersistent && _refreshExpiresUtc.HasValue)
            {
                cookieOptions.Expires = _refreshExpiresUtc.Value.ToUniversalTime();
            }

            Options.CookieManager.AppendResponseCookie(
                Context,
                Options.Cookie.Name!,
                cookieValue,
                cookieOptions);

            await ApplyHeaders(shouldRedirect: false, shouldHonorReturnUrlParameter: false, properties: properties).ConfigureAwait(false);
        }
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

    /// <summary>
    /// Builds the principal which is written to the authentication cookie, holding nothing but the DID claim.
    /// </summary>
    /// <param name="identity">The <see cref="ClaimsIdentity"/> to take the DID claim from.</param>
    /// <param name="referencePrincipal">The principal built, if <paramref name="identity"/> carries a DID claim.</param>
    /// <returns><see langword="true"/> if a reference principal was built; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>
    ///   The cookie is a reference to the identity store, not a copy of what it holds. The stored identity carries the
    ///   access token, the refresh token and the DPoP proof key, none of which belong in a cookie, so everything except
    ///   the DID which names the stored entry is dropped.
    /// </para>
    /// </remarks>
    private static bool TryCreateReferencePrincipal(ClaimsIdentity identity, [NotNullWhen(true)] out ClaimsPrincipal? referencePrincipal)
    {
        referencePrincipal = null;

        if (identity.FindFirst(AtProtoClaims.Did) is not Claim didClaim)
        {
            return false;
        }

        referencePrincipal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [
                    new Claim(AtProtoClaims.Did, didClaim.Value, ClaimValueTypes.String, didClaim.Issuer)
                ],
                identity.AuthenticationType));

        return true;
    }

    /// <summary>
    /// Builds the ticket a renewal writes back to the cookie.
    /// </summary>
    /// <param name="ticket">The ticket being renewed, whose properties the new ticket carries.</param>
    /// <param name="replacedPrincipal">The principal supplied by a caller which replaced the one on <paramref name="ticket"/>, if any.</param>
    /// <returns>The ticket to write, or <see langword="null"/> if no DID claim could be found to build one from.</returns>
    /// <remarks>
    /// <para>
    ///   A ticket reaching here has already been hydrated from the identity store, so its principal carries the stored
    ///   credentials. Writing it back as it stands would put the access token, refresh token and DPoP proof key in the
    ///   cookie, so the principal is reduced to the same DID reference a sign in writes.
    /// </para>
    /// </remarks>
    private static AuthenticationTicket? CloneTicket(AuthenticationTicket ticket, ClaimsPrincipal? replacedPrincipal)
    {
        ClaimsPrincipal principal = replacedPrincipal ?? ticket.Principal;

        if (principal.Identity is not ClaimsIdentity identity ||
            !TryCreateReferencePrincipal(identity, out ClaimsPrincipal? referencePrincipal))
        {
            return null;
        }

        var newProperties = new AuthenticationProperties();
        foreach (KeyValuePair<string, string?> item in ticket.Properties.Items)
        {
            newProperties.Items[item.Key] = item.Value;
        }

        return new AuthenticationTicket(referencePrincipal, newProperties, ticket.AuthenticationScheme);
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

    /// <summary>
    /// Returns a flag indicating whether the credentials carried by <paramref name="identity"/> are present and have not expired.
    /// </summary>
    /// <param name="identity">The <see cref="ClaimsIdentity"/> whose credentials should be checked.</param>
    /// <param name="currentUtc">The current UTC time to check the credential expiry against.</param>
    private static bool HasUnexpiredCredentials(ClaimsIdentity identity, DateTimeOffset currentUtc)
    {
        if (!AtProtoCredential.TryCreate(identity, out DPoPAccessCredentials? credentials) || credentials is null)
        {
            return false;
        }

        return (credentials.ExpiresOn - s_refreshClockSkew) >= currentUtc;
    }

    /// <summary>
    /// Attempts to read the DID for the current user from the request cookie.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> read from the cookie, if the cookie is present and valid.</param>
    /// <returns><see langword="true"/> if a DID was read from the request cookie; otherwise <see langword="false"/>.</returns>
    private bool TryReadDidFromRequestCookie([NotNullWhen(true)] out Did? did)
    {
        did = null;

        string? cookie = Options.CookieManager.GetRequestCookie(Context, Options.Cookie.Name!);

        if (string.IsNullOrEmpty(cookie))
        {
            return false;
        }

        AuthenticationTicket? ticket = Options.TicketDataFormat.Unprotect(cookie, GetTlsTokenBinding());

        Claim? didClaim = ticket?.Principal.Claims.FirstOrDefault(
            c => c.Type.Equals(AtProtoClaims.Did, StringComparison.OrdinalIgnoreCase));

        return didClaim is not null && Did.TryParse(didClaim.Value, out did);
    }

    /// <summary>
    /// Revokes the credentials held for <paramref name="did"/> at the authorization server, if any are still stored.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> whose credentials should be revoked.</param>
    /// <remarks>
    /// <para>
    ///   Revocation is best effort. Signing out locally must still happen when the authorization server cannot be
    ///   reached, or a user would be unable to sign out of an application while its PDS was unavailable.
    /// </para>
    /// </remarks>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Revocation is best effort, failures are logged and the local sign out continues.")]
    private async Task RevokeCredentials(Did did)
    {
        ClaimsIdentity? storedIdentity = await IdentityStore.GetIdentity(did, CancellationToken.None).ConfigureAwait(false);

        if (storedIdentity is null)
        {
            return;
        }

        try
        {
            using BlueskyAgent agent = new(new ClaimsPrincipal(storedIdentity), HttpClientFactory, BlueskyAgentOptions);

            if (!agent.IsAuthenticated)
            {
                return;
            }

            // Do not use Context.RequestAborted, revocation needs to complete even if the client walks away.
            await agent.Logout(CancellationToken.None).ConfigureAwait(false);

            Logger.CredentialsRevokedOnSignOut(did);
        }
        catch (Exception ex)
        {
            Logger.CredentialRevocationFailed(did, ex);
            _metrics.CredentialRevocationFailures.Add(1);
        }
    }

    private void RecordIdentityStoreMissAfterRefresh() =>
        _metrics.IdentityStoreMisses.Add(
            1,
            new KeyValuePair<string, object?>(
                BlueskyAuthenticationMetrics.IdentityStoreMissPhaseTagName,
                BlueskyAuthenticationMetrics.IdentityStoreMissPhaseTokenRefresh));

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

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Token refresh exceptions are logged and handled gracefully.")]
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

        if (!Did.TryParse(didClaim.Value, out Did? did))
        {
            // The ticket is protected, so a DID which will not parse means the data protection key ring is shared with
            // something issuing a different shape of ticket, rather than a user editing their cookie.
            Logger.InvalidDidInCookie();
            return AuthenticateResults.s_invalidDidInCookie;
        }

        CurrentUserDid = did;
        ClaimsIdentity? storedIdentity = await IdentityStore.GetIdentity(did).ConfigureAwait(false);

        if (storedIdentity == null)
        {
            _metrics.IdentityStoreMisses.Add(
                1,
                new KeyValuePair<string, object?>(
                    BlueskyAuthenticationMetrics.IdentityStoreMissPhaseTagName,
                    BlueskyAuthenticationMetrics.IdentityStoreMissPhaseAuthentication));
            return AuthenticateResults.s_missingIdentityInStore;
        }

        DateTimeOffset currentUtc = TimeProvider.GetUtcNow();
        DateTimeOffset? expiresUtc = ticket.Properties.ExpiresUtc;

        if (expiresUtc != null && expiresUtc.Value < currentUtc)
        {
            // The credentials the cookie referred to are still live at the authorization server until they expire on
            // their own, so revoke them rather than only dropping the local record of them.
            await RevokeCredentials(CurrentUserDid).ConfigureAwait(false);
            Logger.ExpiredTicketCredentialsRevoked(CurrentUserDid);
            await IdentityStore.Remove(CurrentUserDid).ConfigureAwait(false);
            CurrentUserDid = null;
            return AuthenticateResults.s_expiredTicket;
        }

        // Rehydrate the full identity from the identity store
        var hydratedTicket = new AuthenticationTicket(new ClaimsPrincipal(storedIdentity), ticket.Properties, ticket.AuthenticationScheme);

        // Now check the actual token from the store, and spin up an agent to check if the token is still valid
        using (BlueskyAgent agent = new(hydratedTicket.Principal, HttpClientFactory, BlueskyAgentOptions))
        {
            if (!agent.HasCredentials)
            {
                // The stored identity carries no credentials which can be read, so there is nothing to refresh and
                // nothing to call the PDS with. Authenticating the request on the strength of the DID alone would
                // hand the application a signed in user it can do nothing for.
                Logger.StoredIdentityHasNoCredentials(CurrentUserDid);
                await IdentityStore.Remove(CurrentUserDid).ConfigureAwait(false);
                CurrentUserDid = null;
                return AuthenticateResults.s_noCredentialsInStoredIdentity;
            }

            if ((agent.Credentials.ExpiresOn - s_refreshClockSkew) < currentUtc)
            {
                // Fresh the token as it has expired, and update the identity store with the new credentials
                // Do not use the cancellation token from HttpContext.RequestAborted, this needs to process all the way through
                if (Context.RequestAborted.IsCancellationRequested)
                {
                    return AuthenticateResults.s_cancellationRequested;
                }

                // A refresh already being under way when the request arrived is ordinary queueing, whereas losing the
                // race to start one means requests arrived closely enough together to contend for the lock.
                string refreshWaitReason = BlueskyAuthenticationMetrics.TokenRefreshWaitReasonRefreshInProgress;

                if (!await IdentityStore.IsRefreshing(CurrentUserDid, cancellationToken: CancellationToken.None).ConfigureAwait(false))
                {
                    refreshWaitReason = BlueskyAuthenticationMetrics.TokenRefreshWaitReasonLockDenied;

                    string? refreshLockToken = await IdentityStore.StartRefresh(CurrentUserDid, cancellationToken: CancellationToken.None).ConfigureAwait(false);

                    if (refreshLockToken is not null)
                    {
                        Did refreshingFor = CurrentUserDid;

                        try
                        {
                            bool refreshCredentialsResult = await agent.RefreshCredentials(cancellationToken: CancellationToken.None).ConfigureAwait(false);
                            if (!refreshCredentialsResult || !agent.IsAuthenticated)
                            {
                                // A refresh can fail because another request refreshed concurrently and consumed the single use refresh token, so
                                // check whether the store now holds usable credentials before signing the user out.
                                ClaimsIdentity? concurrentlyRefreshedIdentity =
                                    await IdentityStore.GetIdentity(refreshingFor, cancellationToken: CancellationToken.None).ConfigureAwait(false);

                                if (concurrentlyRefreshedIdentity is not null &&
                                    HasUnexpiredCredentials(concurrentlyRefreshedIdentity, currentUtc))
                                {
                                    Logger.TokenRefreshFailedButStoreIsCurrent(refreshingFor);
                                    _metrics.AccessTokensRefreshed.Add(
                                        1,
                                        new KeyValuePair<string, object?>(
                                            BlueskyAuthenticationMetrics.TokenRefreshOutcomeTagName,
                                            BlueskyAuthenticationMetrics.TokenRefreshOutcomeConcurrent));
                                    hydratedTicket = new AuthenticationTicket(
                                        new ClaimsPrincipal(concurrentlyRefreshedIdentity), ticket.Properties, ticket.AuthenticationScheme);
                                    return AuthenticateResult.Success(hydratedTicket);
                                }

                                CurrentUserDid = null;
                                await IdentityStore.Remove(refreshingFor).ConfigureAwait(false);
                                _metrics.AccessTokensRefreshFailures.Add(1);

                                return AuthenticateResults.s_tokenRefreshFailed;
                            }
                            else
                            {
                                if (!await IdentityStore.UpdateIfNewer(agent.Credentials, cancellationToken: CancellationToken.None).ConfigureAwait(false))
                                {
                                    Logger.RefreshedCredentialsSupersededByStore(refreshingFor);
                                }

                                // Update the ticket with the new credentials
                                ClaimsIdentity? updatedIdentity = await IdentityStore.GetIdentity(refreshingFor).ConfigureAwait(false);
                                if (updatedIdentity == null)
                                {
                                    RecordIdentityStoreMissAfterRefresh();
                                    return AuthenticateResults.s_identityStoreRefreshMissing;
                                }

                                _metrics.AccessTokensRefreshed.Add(
                                    1,
                                    new KeyValuePair<string, object?>(
                                        BlueskyAuthenticationMetrics.TokenRefreshOutcomeTagName,
                                        BlueskyAuthenticationMetrics.TokenRefreshOutcomeSelf));

                                hydratedTicket = new AuthenticationTicket(new ClaimsPrincipal(updatedIdentity), ticket.Properties, ticket.AuthenticationScheme);
                                return AuthenticateResult.Success(hydratedTicket);
                            }
                        }
                        catch (Exception ex)
                        {
                            _metrics.AccessTokensRefreshFailures.Add(1);
                            Logger.TokenRefreshThrew(refreshingFor, ex);
                            return AuthenticateResults.s_tokenRefreshFailed;
                        }
                        finally
                        {
                            if (!await IdentityStore.EndRefresh(refreshingFor, refreshLockToken, cancellationToken: CancellationToken.None).ConfigureAwait(false))
                            {
                                // The lock expired while the refresh was running, so another request may have been refreshing the
                                // same DID at the same time. The store resolves the resulting write on expiry, so the credentials
                                // are not lost, but the contention is worth surfacing.
                                Logger.RefreshLockLostDuringRefresh(refreshingFor);
                            }
                        }
                    }

                    // If we get here, then another request has already started a refresh, so we will wait for it to complete below.
                }

                // A refresh is in progress, so wait for it to complete, then use the credentials it stored.
                //
                // The refresh may also have completed in the window between the check above and here, so check the
                // store before waiting, otherwise a request which arrives just as a refresh finishes would be
                // failed even though valid credentials are sitting in the store.

                long startTimestamp = Stopwatch.GetTimestamp();
                try
                {
                    _metrics.AccessTokenRefreshWaits.Add(
                        1,
                        new KeyValuePair<string, object?>(
                            BlueskyAuthenticationMetrics.TokenRefreshWaitReasonTagName,
                            refreshWaitReason));
                    for (int refreshCheckCount = 0; refreshCheckCount < Options.MaxRefreshChecks; refreshCheckCount++)
                    {
                        if (Context.RequestAborted.IsCancellationRequested)
                        {
                            return AuthenticateResults.s_cancellationRequested;
                        }

                        if (!await IdentityStore.IsRefreshing(CurrentUserDid, cancellationToken: CancellationToken.None).ConfigureAwait(false))
                        {
                            // The refresh has finished, so pick up the identity it stored.
                            ClaimsIdentity? refreshedIdentity = await IdentityStore.GetIdentity(CurrentUserDid, cancellationToken: CancellationToken.None).ConfigureAwait(false);

                            if (refreshedIdentity is null)
                            {
                                RecordIdentityStoreMissAfterRefresh();
                                return AuthenticateResults.s_identityStoreRefreshMissing;
                            }

                            hydratedTicket = new AuthenticationTicket(new ClaimsPrincipal(refreshedIdentity), ticket.Properties, ticket.AuthenticationScheme);
                            return AuthenticateResult.Success(hydratedTicket);
                        }

                        try
                        {
                            await Task.Delay(Options.RefreshCheckWait, Context.RequestAborted).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            return AuthenticateResults.s_cancellationRequested;
                        }
                    }
                }
                finally
                {
                    _metrics.AccessTokenRefreshWaitDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds);
                }

                if (Context.RequestAborted.IsCancellationRequested)
                {
                    return AuthenticateResults.s_cancellationRequested;
                }

                return AuthenticateResults.s_awaitTokenRefreshLoopExpired;
            }

            // Ticket from store has not expired, so use it.
            return AuthenticateResult.Success(hydratedTicket);
        }
    }

    private void RequestRefresh(AuthenticationTicket ticket, ClaimsPrincipal? replacedPrincipal = null)
    {
        DateTimeOffset? issuedUtc = ticket.Properties.IssuedUtc;
        DateTimeOffset? expiresUtc = ticket.Properties.ExpiresUtc;

        if (issuedUtc != null && expiresUtc != null)
        {
            if (CloneTicket(ticket, replacedPrincipal) is not AuthenticationTicket refreshTicket)
            {
                // Without a DID the renewed cookie would not name an entry in the identity store, so it could never be
                // authenticated. Leaving the existing cookie in place lets the session run out its original lifetime
                // rather than ending it here.
                Logger.RenewalSkippedNoDidClaim();
                return;
            }

            _shouldRefresh = true;
            DateTimeOffset currentUtc = TimeProvider.GetUtcNow();
            _refreshIssuedUtc = currentUtc;
            TimeSpan timeSpan = expiresUtc.Value.Subtract(issuedUtc.Value);
            _refreshExpiresUtc = currentUtc.Add(timeSpan);
            _refreshTicket = refreshTicket;
        }
    }
}
