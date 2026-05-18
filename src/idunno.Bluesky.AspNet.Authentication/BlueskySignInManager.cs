// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using idunno.AtProto;
using idunno.AtProto.Authentication;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Provides APIs for user sign in and sign out.
/// </summary>
public class BlueskySignInManager
{
    private static readonly Uri s_localhost = new("http://127.0.0.1");

    private readonly IHttpContextAccessor _contextAccessor;
    private readonly IHostEnvironment _env;
    private readonly ICorrelationStateCache _correlationCache;

    /// <summary>
    /// Creates a new instance of <see cref="BlueskySignInManager"/>.
    /// </summary>
    /// <param name="contextAccessor">The accessor used to access the <see cref="HttpContext"/>.</param>
    /// <param name="agentOptionsAccessor">The accessor used to access the <see cref="BlueskyAgentOptions"/>.</param>
    /// <param name="authenticationOptionsAccessor">The accessor used to access the <see cref="BlueskyAuthenticationOptions"/>.</param>
    /// <param name="env">The <see cref="IHostEnvironment"/> for the application</param>
    /// <param name="logger">The <see cref="ILogger"/> used to log messages, warnings and errors</param>
    /// <exception cref="ArgumentNullException">Thrown when any of the parameters are <see langword="null" />.</exception>
    public BlueskySignInManager(
        IHttpContextAccessor contextAccessor,
        IOptions<BlueskyAgentOptions> agentOptionsAccessor,
        IOptions<BlueskyAuthenticationOptions> authenticationOptionsAccessor,
        IHostEnvironment env,
        ILogger<BlueskySignInManager> logger)
    {
        ArgumentNullException.ThrowIfNull(contextAccessor);
        ArgumentNullException.ThrowIfNull(agentOptionsAccessor);
        ArgumentNullException.ThrowIfNull(agentOptionsAccessor.Value);
        ArgumentNullException.ThrowIfNull(agentOptionsAccessor.Value.OAuthOptions);
        ArgumentNullException.ThrowIfNull(authenticationOptionsAccessor);
        ArgumentNullException.ThrowIfNull(authenticationOptionsAccessor.Value);
        ArgumentNullException.ThrowIfNull(authenticationOptionsAccessor.Value.CorrelationCache);

        _contextAccessor = contextAccessor;
        _correlationCache = authenticationOptionsAccessor.Value.CorrelationCache;
        _env = env;

        Logger = logger;
        BlueskyAgentOptions = agentOptionsAccessor.Value;
        OAuthOptions = agentOptionsAccessor.Value.OAuthOptions;
        BlueskyAuthenticationOptions = authenticationOptionsAccessor.Value;

        DataProtector = BlueskyAuthenticationOptions.DataProtectionProvider!
            .CreateProtector(Constants.CorrelationPurpose, "v1")
            .ToTimeLimitedDataProtector();
    }

    /// <summary>
    /// The authentication scheme to sign in with. Defaults to <see cref="BlueskyAuthenticationDefaults.AuthenticationScheme"/>.
    /// </summary>
    public string AuthenticationScheme { get; set; } = BlueskyAuthenticationDefaults.AuthenticationScheme;

    /// <summary>
    /// Gets the <see cref="ILogger"/> used to log messages from the manager.
    /// </summary>
    /// <value>
    /// The <see cref="ILogger"/> used to log messages from the manager.
    /// </value>
    public virtual ILogger Logger { get; init; }

    /// <summary>
    /// The <see cref="Bluesky.BlueskyAgentOptions"/> used.
    /// </summary>
    public BlueskyAgentOptions BlueskyAgentOptions { get; init; }

    /// <summary>
    /// The Bluesky authentication options used.
    /// </summary>
    public BlueskyAuthenticationOptions BlueskyAuthenticationOptions { get; init; }

    /// <summary>
    /// The <see cref="AtProto.Authentication.OAuthOptions"/> used.
    /// </summary>
    public OAuthOptions OAuthOptions { get; init; }

    internal ITimeLimitedDataProtector DataProtector { get; init; }

    /// <summary>
    /// The <see cref="Microsoft.AspNetCore.Http.HttpContext"/> used.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the <see cref="HttpContext"/> is not available.</exception>
    public HttpContext HttpContext
    {
        get
        {
            HttpContext? context = _contextAccessor?.HttpContext;
            return context ?? throw new InvalidOperationException("HttpContext must not be null.");
        }
    }

    /// <summary>
    /// Creates a redirect URI for OAuth sign-in, saving state internally and creating a correlation cookie.
    /// </summary>
    /// <param name="handle">The handle hint for the remote OAuth login page.</param>
    /// <param name="returnUri">The return URI to the remote server should return to.</param>
    /// <param name="correlationId">The <see cref="Guid"/> to use as a correlation identifier. If <see langword="null" /> a new GUID will be generated.</param>
    /// <param name="uriExtraParameters">Any extra parameters to attach to the URI.</param>
    /// <param name="stateExtraProperties">Any extra properties to save in the correlation state store.</param>
    /// <returns>A URI for OAuth sign-in.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the state could not be created.</exception>
    public async Task<Uri> CreateRedirectUri(
        Handle handle,
        Uri? returnUri = null,
        Guid? correlationId = null,
        IEnumerable<KeyValuePair<string, string>>? uriExtraParameters = null,
        Dictionary<string, string>? stateExtraProperties = null)
    {
        returnUri ??= CreateReturnUri();

        using var agent = new BlueskyAgent(options: BlueskyAgentOptions);
        OAuthClient oAuthClient = agent.CreateOAuthClient();

        Uri redirectUri = await agent.BuildOAuth2LoginUri(
            oAuthClient,
            handle: handle,
            returnUri: returnUri,
            uriExtraParameters: uriExtraParameters,
            stateExtraProperties: stateExtraProperties).ConfigureAwait(false);

        if (oAuthClient.State is null)
        {
            Logger.CouldNotPrepareOAuthState();
            throw new InvalidOperationException("OAuthState could not be prepared");
        }

        await SaveStateAndCreateCorrelationCookie(oAuthClient.State, correlationId).ConfigureAwait(false);

        return redirectUri;
    }

    /// <summary>
    /// Creates an OAuth return URI which takes in account the limitations placed on http://localhost Client Identifiers.
    /// </summary>
    /// <returns>A suitable return URI for a Bluesky OAuth call.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the application <see cref="Bluesky.BlueskyAgentOptions"/> OAuth configuration does not specify a return URI.</exception>
    /// <remarks>
    /// <para>
    ///   Bluesky OAuth special cases a client identifier of http://localhost, requiring the return URI to be http://127.0.0.1 with no path.
    /// </para>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded", Justification = "Special hardcoded case for localhost.")]
    public Uri CreateReturnUri()
    {
        bool clientIdIsLocalhost = OAuthOptions.ClientId.StartsWith("http://localhost", StringComparison.InvariantCulture);

        if (clientIdIsLocalhost && OAuthOptions.ReturnUri is null)
        {
            OAuthOptions.ReturnUri = new Uri("http://127.0.0.1/Account/Callback");
        }

        if (OAuthOptions.ReturnUri is null)
        {
            throw new InvalidOperationException("OAuthOptions does not specify a ReturnUri.");
        }

        bool returnUriIsLocalhostIP = OAuthOptions.ReturnUri.Host == s_localhost.Host;
        UriBuilder returnUriBuilder = new(OAuthOptions.ReturnUri);

        // Insert the port for local dev if the Client ID is localhost, and the return URL has a host of 127.0.0.1
        // and the request was made on a none default port.
        if (_env.IsDevelopment() &&
            clientIdIsLocalhost &&
            returnUriIsLocalhostIP &&
            OAuthOptions.ReturnUri.IsDefaultPort &&
            HttpContext.Request is not null &&
            HttpContext.Request.Host.Port is not null)
        {
            returnUriBuilder.Port = HttpContext.Request.Host.Port.Value;
            Logger.ReturnUriPortOverridden(returnUriBuilder.Port);
        }

        return returnUriBuilder.Uri;
    }

    /// <summary>
    /// Returns <see langword="true" /> if the principal has an identity with the application cookie identity
    /// </summary>
    /// <param name="principal">The <see cref="ClaimsPrincipal"/> instance.</param>
    /// <returns><see langword="true" /> if the user is logged in with identity, otherwise <see langword="false"/>.</returns>
    public bool IsSignedIn(ClaimsPrincipal principal)
    {
        if (principal is null)
        {
            return false;
        }

        return principal.Identities != null && principal.Identities.Any(i => i.AuthenticationType == AuthenticationScheme);
    }

    /// <summary>
    /// Loads the OAuth login state from the correlation cache for the specified <paramref name="correlationId"/>.
    /// </summary>
    /// <param name="correlationId">The correlation ID to look up the state for. If <see langword="null" />, checks the HTTP Context for a correlation cookie.</param>
    /// <returns>The <see cref="OAuthLoginState"/> if found, otherwise <see langword="null" />.</returns>
    /// <exception cref="InvalidOperationException">If <paramref name="correlationId"/> is <see langword="null" /> and a correlation cookie cannot be found or cannot be parsed.</exception>
    public async Task<OAuthLoginState?> LoadState(Guid? correlationId = null)
    {
        if (correlationId == null)
        {
            if (HttpContext.Request is null)
            {
                throw new InvalidOperationException("Context.Request is null");
            }

            if (HttpContext.Request.Cookies is not null &&
                HttpContext.Request.Cookies.ContainsKey(Constants.CorrelationCookieName) &&
                HttpContext.Request.Cookies[Constants.CorrelationCookieName] is not null)
            {
                try
                {
                    string unprotectedCookieValue = DataProtector.Unprotect(HttpContext.Request.Cookies[Constants.CorrelationCookieName]!, out DateTimeOffset expiration);

                    if (expiration < DateTimeOffset.UtcNow)
                    {
                        Logger.ExpiredCorrelationCookie();
                        return null;
                    }

                    if (Guid.TryParse(unprotectedCookieValue, out Guid parsedGuid))
                    {
                        correlationId = parsedGuid;
                    }
                }
                catch (CryptographicException ex)
                {
                    Logger.ExceptionUnprotectingCorrelationCookie(ex);
                    return null;
                }
            }

            HttpContext.Response.Cookies.Delete(Constants.CorrelationCookieName);

            if (correlationId is null)
            {
                throw new InvalidOperationException("Missing or invalid correlation cookie.");
            }
        }

        return await _correlationCache.GetOAuthLoginState(correlationId.Value).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves the OAuth login state in the correlation cache and drops a correlation cookie that can be used to restore the state..
    /// </summary>
    /// <param name="state">The state to save.</param>
    /// <param name="correlationId">A correlation id. If <see langword="null"/> a new identifier will be generated.</param>
    /// <returns>The correlation id that the state was saved against.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is <see langword="null" />.</exception>
    public async Task<Guid> SaveStateAndCreateCorrelationCookie(
        OAuthLoginState state,
        Guid? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        var correlationValidityPeriod = new TimeSpan(0, 15, 0);

        correlationId = await SaveState(state, correlationId).ConfigureAwait(false);

        string cookieValue = DataProtector.Protect(correlationId.Value.ToString(), correlationValidityPeriod);

        HttpContext.Response.Cookies.Append(Constants.CorrelationCookieName, cookieValue,
            new CookieOptions
            {
                Expires = DateTime.Now + correlationValidityPeriod,
                SameSite = SameSiteMode.Lax
            });

        return correlationId.Value;
    }

    /// <summary>
    /// Saves the OAuth login state in the correlation cache.
    /// </summary>
    /// <param name="state">The state to save.</param>
    /// <param name="correlationId">A correlation id. If <see langword="null"/> a new identifier will be generated.</param>
    /// <returns>The correlation id that the state was saved against.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is <see langword="null" />.</exception>
    public async Task<Guid> SaveState(
        OAuthLoginState state,
        Guid? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        correlationId ??= Guid.NewGuid();

        await _correlationCache.AddOAuthLoginState(correlationId.Value, state).ConfigureAwait(false);

        return correlationId.Value;
    }

    /// <summary>
    /// Parses the incoming OAuth2 response.
    /// </summary>
    /// <returns>A <see cref="SignInResult"/> containing the result of the parsing.</returns>
    public async Task<SignInResult> SignIn()
    {
        if (!HttpContext.Request.QueryString.HasValue)
        {
            Logger.SignInFailedNoQueryString();
            return new SignInResult(Succeeded: false, MissingQueryString: true);
        }

        OAuthLoginState? correlationState = await LoadState().ConfigureAwait(false);
        if (correlationState == null)
        {
            Logger.SignInFailedNoCorrelation();
            return new SignInResult(Succeeded: false, MissingCorrelationState: true);
        }

        using var agent = new BlueskyAgent(options: BlueskyAgentOptions);
        OAuthClient oAuthClient = agent.CreateOAuthClient();
        DPoPAccessCredentials? accessCredentials = await oAuthClient.ProcessOAuth2Response(correlationState, HttpContext.Request.QueryString.Value[1..]).ConfigureAwait(false);
        if (accessCredentials is null)
        {
            Logger.SignInFailedOAuth2ProcessingFailed();
            return new SignInResult(Succeeded: false, ErrorProcessingOAuth2Response: true);
        }

        ClaimsIdentity identity = IIdentityStore.BuildClaimsIdentity(accessCredentials, AuthenticationScheme);

        await HttpContext.SignInAsync(
            new ClaimsPrincipal(identity),
            new AuthenticationProperties()
            {
                AllowRefresh = true,
                IsPersistent = true,
                IssuedUtc = DateTimeOffset.UtcNow
            }).ConfigureAwait(false);

        return new SignInResult(Succeeded: true, OAuthLoginState: correlationState);
    }

    /// <summary>
    /// Signs out the current session.
    /// </summary>
    /// <param name="scheme">The authentication scheme to sign out of. If <see langword="null" />, the default scheme will be used.</param>
    public async Task SignOut(string? scheme = null)
    {
        scheme ??= BlueskyAuthenticationDefaults.AuthenticationScheme;

        await HttpContext.SignOutAsync(scheme).ConfigureAwait(false);
    }
}
