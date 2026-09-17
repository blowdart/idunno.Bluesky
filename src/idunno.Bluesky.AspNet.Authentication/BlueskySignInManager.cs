// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;

using idunno.AtProto;
using idunno.AtProto.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Provides APIs for user sign in and sign out.
/// </summary>
public class BlueskySignInManager
{
    private static readonly Uri s_localhost = new("http://127.0.0.1");

    private readonly IHttpContextAccessor _contextAccessor;
    private readonly IHostEnvironment _env;
    private readonly IOptionsMonitor<BlueskyAuthenticationOptions> _authenticationOptionsMonitor;
    private readonly IHttpClientFactory _httpClientFactory;

    private string? _dataProtectorScheme;

    [SuppressMessage("Style", "IDE0032:Use auto property", Justification = "Too much validation going on.")]
    private IDataProtector? _dataProtector;

    private readonly BlueskyAuthenticationMetrics _metrics;

    /// <summary>
    /// Creates a new instance of <see cref="BlueskySignInManager"/>.
    /// </summary>
    /// <param name="contextAccessor">The accessor used to access the <see cref="HttpContext"/>.</param>
    /// <param name="agentOptionsAccessor">The accessor used to access the <see cref="BlueskyAgentOptions"/>.</param>
    /// <param name="authenticationOptionsAccessor">The accessor used to access the <see cref="BlueskyAuthenticationOptions"/>.</param>
    /// <param name="env">The <see cref="IHostEnvironment"/> for the application</param>
    /// <param name="logger">The <see cref="ILogger"/> used to log messages, warnings and errors</param>
    /// <param name="meterFactory">The <see cref="IMeterFactory"/> used to create metrics instruments</param>
    /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> the agents it creates should make their requests through.</param>
    /// <exception cref="ArgumentNullException">Thrown when any of the parameters are <see langword="null" />.</exception>
    public BlueskySignInManager(
        IHttpContextAccessor contextAccessor,
        IOptions<BlueskyAgentOptions> agentOptionsAccessor,
        IOptionsMonitor<BlueskyAuthenticationOptions> authenticationOptionsAccessor,
        IHostEnvironment env,
        ILogger<BlueskySignInManager> logger,
        IMeterFactory meterFactory,
        IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(contextAccessor);
        ArgumentNullException.ThrowIfNull(agentOptionsAccessor);
        ArgumentNullException.ThrowIfNull(agentOptionsAccessor.Value);
        ArgumentNullException.ThrowIfNull(agentOptionsAccessor.Value.OAuthOptions);
        ArgumentNullException.ThrowIfNull(authenticationOptionsAccessor);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        _contextAccessor = contextAccessor;
        _authenticationOptionsMonitor = authenticationOptionsAccessor;
        _env = env;
        _httpClientFactory = httpClientFactory;

        Logger = logger;
        BlueskyAgentOptions = agentOptionsAccessor.Value;
        OAuthOptions = agentOptionsAccessor.Value.OAuthOptions;

        _metrics = new BlueskyAuthenticationMetrics(meterFactory);
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
    /// <value>
    /// The Bluesky authentication options configured for <see cref="AuthenticationScheme"/>.
    /// </value>
    /// <remarks>
    /// <para>
    ///   <see cref="BlueskyAuthenticationOptions"/> instances are configured against the name of the authentication
    ///   scheme they belong to, so the unnamed instance is not the one the handler for <see cref="AuthenticationScheme"/>
    ///   is using. Resolving on each access, rather than in the constructor, also keeps the manager correct when
    ///   <see cref="AuthenticationScheme"/> is changed after construction.
    /// </para>
    /// </remarks>
    public BlueskyAuthenticationOptions BlueskyAuthenticationOptions => _authenticationOptionsMonitor.Get(AuthenticationScheme);

    /// <summary>
    /// The <see cref="AtProto.Authentication.OAuthOptions"/> used.
    /// </summary>
    public OAuthOptions OAuthOptions { get; init; }

    internal ICorrelationStateCache CorrelationCache =>
        BlueskyAuthenticationOptions.CorrelationCache ??
        throw new InvalidOperationException($"No CorrelationCache is configured for the '{AuthenticationScheme}' authentication scheme.");

    /// <summary>
    /// Gets the name the correlation cookie is written with and read from.
    /// </summary>
    /// <value>
    /// <see cref="CookieBuilder.Name"/> from <see cref="BlueskyAuthenticationOptions.CorrelationCookie"/>, or
    /// <see cref="Constants.CorrelationCookieName"/> if the builder does not carry a name.
    /// </value>
    internal string CorrelationCookieName
    {
        get
        {
            string? configuredName = BlueskyAuthenticationOptions.CorrelationCookie.Name;

            return string.IsNullOrEmpty(configuredName) ? Constants.CorrelationCookieName : configuredName;
        }
    }

    internal IDataProtector DataProtector
    {
        get
        {
            string scheme = AuthenticationScheme;

            if (_dataProtector is null || !string.Equals(_dataProtectorScheme, scheme, StringComparison.Ordinal))
            {
                IDataProtectionProvider dataProtectionProvider =
                    BlueskyAuthenticationOptions.DataProtectionProvider ??
                    throw new InvalidOperationException($"No DataProtectionProvider is configured for the '{scheme}' authentication scheme.");

                // The expiry the correlation cookie is valid until is carried inside the protected payload rather than
                // by ITimeLimitedDataProtector, whose Unprotect throws a CryptographicException indistinguishable from
                // a key ring failure once the payload has expired. Carrying it here keeps an everyday expiry, a user
                // who left the login page open, separate from a genuine data protection problem. The payload is still
                // authenticated by the protector, so the expiry cannot be altered by whoever holds the cookie.
                _dataProtector = dataProtectionProvider.CreateProtector(Constants.CorrelationPurpose, "v2");
                _dataProtectorScheme = scheme;
            }

            return _dataProtector;
        }
    }

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
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A URI for OAuth sign-in.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the state could not be created.</exception>
    public async Task<Uri> CreateRedirectUri(
        Handle handle,
        Uri? returnUri = null,
        Guid? correlationId = null,
        IEnumerable<KeyValuePair<string, string>>? uriExtraParameters = null,
        Dictionary<string, string>? stateExtraProperties = null,
        CancellationToken cancellationToken = default)
    {
        returnUri ??= CreateReturnUri();

        using var agent = new BlueskyAgent(httpClientFactory: _httpClientFactory, options: BlueskyAgentOptions);
        OAuthClient oAuthClient = agent.CreateOAuthClient();

        Uri redirectUri = await agent.BuildOAuth2LoginUri(
            oAuthClient,
            handle: handle,
            returnUri: returnUri,
            uriExtraParameters: uriExtraParameters,
            stateExtraProperties: stateExtraProperties,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (oAuthClient.State is null)
        {
            Logger.CouldNotPrepareOAuthState();
            throw new InvalidOperationException("OAuthState could not be prepared");
        }

        await SaveStateAndCreateCorrelationCookie(oAuthClient.State, correlationId, returnUri.Scheme == "https").ConfigureAwait(false);

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
    ///   That default is applied by <see cref="PostConfigureBlueskyAgentOptions"/> when the options are built.
    /// </para>
    /// </remarks>
    public Uri CreateReturnUri()
    {
        // The configured options are read, never written. They are a singleton shared by every request.
        Uri? configuredReturnUri = OAuthOptions.ReturnUri ?? throw new InvalidOperationException("OAuthOptions does not specify a ReturnUri.");
        bool clientIdIsLocalhost = OAuthOptions.ClientId.StartsWith("http://localhost", StringComparison.InvariantCulture);
        bool returnUriIsLocalhostIP = configuredReturnUri.Host == s_localhost.Host;
        UriBuilder returnUriBuilder = new(configuredReturnUri);

        // Insert the port for local dev if the Client ID is localhost, and the return URL has a host of 127.0.0.1
        // and the request was made on a none default port.
        if (_env.IsDevelopment() &&
            clientIdIsLocalhost &&
            returnUriIsLocalhostIP &&
            configuredReturnUri.IsDefaultPort &&
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
    /// <exception cref="InvalidOperationException">If the <see cref="HttpContext"/> has no request.</exception>
    /// <remarks>
    /// <para>
    ///   A request which carries no correlation cookie, or one which is expired, unreadable or malformed, is a callback
    ///   this application cannot tie to a login it started. That is not an error in the application, so it yields
    ///   <see langword="null" /> and a <see cref="BlueskyAuthenticationMetrics.CorrelationStateRejections"/> count
    ///   carrying the reason, rather than an exception which would surface to the user as a server error.
    /// </para>
    /// <para>
    ///   Passing a <paramref name="correlationId"/> skips the correlation cookie, and with it the check which ties a callback
    ///   to a login this application started in this browser. Only pass an identifier the application is itself tracking, never
    ///   one taken from the request, otherwise the login flow loses its cross site request forgery protection.
    /// </para>
    /// </remarks>
    public async Task<OAuthLoginState?> LoadState(Guid? correlationId = null)
    {
        if (correlationId == null)
        {
            if (HttpContext.Request is null)
            {
                throw new InvalidOperationException("Context.Request is null");
            }

            string? cookieValue = HttpContext.Request.Cookies?[CorrelationCookieName];

            string? rejectionReason = null;

            if (string.IsNullOrEmpty(cookieValue))
            {
                Logger.MissingCorrelationCookie();
                rejectionReason = BlueskyAuthenticationMetrics.CorrelationStateRejectionMissingCookie;
            }
            else
            {
                try
                {
                    string unprotectedCookieValue = DataProtector.Unprotect(cookieValue);

                    if (!TryParseCorrelationCookiePayload(unprotectedCookieValue, out Guid parsedGuid, out DateTimeOffset expiration))
                    {
                        Logger.MalformedCorrelationCookie();
                        rejectionReason = BlueskyAuthenticationMetrics.CorrelationStateRejectionMalformedCookie;
                    }
                    else if (expiration < DateTimeOffset.UtcNow)
                    {
                        Logger.ExpiredCorrelationCookie();
                        rejectionReason = BlueskyAuthenticationMetrics.CorrelationStateRejectionExpiredCookie;
                    }
                    else
                    {
                        correlationId = parsedGuid;
                    }
                }
                catch (CryptographicException ex)
                {
                    Logger.ExceptionUnprotectingCorrelationCookie(ex);

                    // The expiry is carried inside the payload, so reaching here means the payload could not be read
                    // at all, which is a data protection problem rather than an everyday expiry.
                    _metrics.DataProtectionFailures.Add(
                        1,
                        new KeyValuePair<string, object?>(
                            BlueskyAuthenticationMetrics.DataProtectionSourceTagName,
                            BlueskyAuthenticationMetrics.DataProtectionSourceCorrelationCookie));
                    rejectionReason = BlueskyAuthenticationMetrics.CorrelationStateRejectionUnprotectFailed;
                }
            }

            // Delete with the options the cookie was written with, otherwise a correlation cookie written with a
            // path or domain from the CookieBuilder would not be matched and so would not be removed.
            HttpContext.Response.Cookies.Delete(
                CorrelationCookieName,
                BlueskyAuthenticationOptions.CorrelationCookie.Build(HttpContext, DateTimeOffset.UtcNow));

            // An expired or unreadable cookie has now been deleted, so it cannot be presented again.
            if (rejectionReason is not null)
            {
                _metrics.CorrelationStateRejections.Add(
                    1,
                    new KeyValuePair<string, object?>(
                        BlueskyAuthenticationMetrics.CorrelationStateRejectionReasonTagName,
                        rejectionReason));

                return null;
            }
        }

        // Login state is single use, so take it rather than reading it and removing it separately.
        OAuthLoginState? loginState = await CorrelationCache.TakeOAuthLoginState(correlationId!.Value).ConfigureAwait(false);

        if (loginState is null)
        {
            // The cookie was readable, so the state it named either aged out of the correlation cache or has already
            // been taken by an earlier callback carrying the same cookie.
            _metrics.CorrelationStateRejections.Add(
                1,
                new KeyValuePair<string, object?>(
                    BlueskyAuthenticationMetrics.CorrelationStateRejectionReasonTagName,
                    BlueskyAuthenticationMetrics.CorrelationStateRejectionStateNotFound));
        }

        return loginState;
    }

    /// <summary>
    /// Saves the OAuth login state in the correlation cache and drops a correlation cookie that can be used to restore the state.
    /// </summary>
    /// <param name="state">The state to save.</param>
    /// <param name="correlationId">A correlation id. If <see langword="null"/> a new identifier will be generated.</param>
    /// <param name="markCookieAsSecure">If <see langword="true"/> the correlation cookie will be marked as secure.</param>
    /// <returns>The correlation id that the state was saved against.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is <see langword="null" />.</exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security Hotspot", "S2092:Set the 'Secure' flag on this cookie", Justification = "The secure flag comes from the configured CorrelationCookie builder, raised by markCookieAsSecure, which cannot be required unconditionally because OAuth against a http://localhost client identifier is not served over https.")]
    public async Task<Guid> SaveStateAndCreateCorrelationCookie(
        OAuthLoginState state,
        Guid? correlationId = null,
        bool markCookieAsSecure = true)
    {
        ArgumentNullException.ThrowIfNull(state);

        var correlationValidityPeriod = new TimeSpan(0, 15, 0);

        correlationId = await SaveState(state, correlationId).ConfigureAwait(false);

        DateTimeOffset correlationExpiry = DateTimeOffset.UtcNow.Add(correlationValidityPeriod);

        string cookieValue = DataProtector.Protect(FormatCorrelationCookiePayload(correlationId.Value, correlationExpiry));

        CookieOptions cookieOptions = BlueskyAuthenticationOptions.CorrelationCookie.Build(HttpContext, DateTimeOffset.UtcNow);

        // CookieBuilder only sets an expiry when the application configured one, and the correlation cookie has no
        // value once the state it points at has aged out of the correlation cache.
        cookieOptions.Expires ??= correlationExpiry;

        // markCookieAsSecure may only raise the security of the cookie, never lower what the CookieBuilder asked for.
        cookieOptions.Secure = cookieOptions.Secure || markCookieAsSecure;

        HttpContext.Response.Cookies.Append(CorrelationCookieName, cookieValue, cookieOptions);

        return correlationId.Value;
    }

    private static string FormatCorrelationCookiePayload(Guid correlationId, DateTimeOffset expiration) =>
        string.Create(CultureInfo.InvariantCulture, $"{correlationId:D}|{expiration.ToUnixTimeSeconds()}");

    private static bool TryParseCorrelationCookiePayload(string payload, out Guid correlationId, out DateTimeOffset expiration)
    {
        correlationId = Guid.Empty;
        expiration = default;

        int separator = payload.IndexOf('|', StringComparison.Ordinal);

        if (separator < 0 ||
            !Guid.TryParseExact(payload.AsSpan(0, separator), "D", out correlationId) ||
            !long.TryParse(payload.AsSpan(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long expiresAtUnixSeconds))
        {
            correlationId = Guid.Empty;
            return false;
        }

        try
        {
            expiration = DateTimeOffset.FromUnixTimeSeconds(expiresAtUnixSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            correlationId = Guid.Empty;
            return false;
        }

        return true;
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

        await CorrelationCache.AddOAuthLoginState(correlationId.Value, state).ConfigureAwait(false);

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
            _metrics.SigninsFailed.Add(
                1,
                new KeyValuePair<string, object?>(BlueskyAuthenticationMetrics.SignInFailureReasonTagName, "NoQueryString"));
            return new SignInResult(Succeeded: false, MissingQueryString: true);
        }

        OAuthLoginState? correlationState = await LoadState().ConfigureAwait(false);
        if (correlationState == null)
        {
            Logger.SignInFailedNoCorrelation();
            _metrics.SigninsFailed.Add(
                1,
                new KeyValuePair<string, object?>(BlueskyAuthenticationMetrics.SignInFailureReasonTagName, "NoCorrelationState"));
            return new SignInResult(Succeeded: false, MissingCorrelationState: true);
        }
        
        using var agent = new BlueskyAgent(httpClientFactory: _httpClientFactory, options: BlueskyAgentOptions);
        OAuthClient oAuthClient = agent.CreateOAuthClient();
        DPoPAccessCredentials? accessCredentials = await oAuthClient.ProcessOAuth2Response(
            correlationState,
            HttpContext.Request.QueryString.Value[1..],
            HttpContext.RequestAborted).ConfigureAwait(false);
        if (accessCredentials is null)
        {
            Logger.SignInFailedOAuth2ProcessingFailed();
            _metrics.SigninsFailed.Add(
                1,
                new KeyValuePair<string, object?>(BlueskyAuthenticationMetrics.SignInFailureReasonTagName, "OAuth2StateFailure"));
            return new SignInResult(Succeeded: false, ErrorProcessingOAuth2Response: true);
        }

        ClaimsIdentity identity = IIdentityStore.BuildClaimsIdentity(accessCredentials, AuthenticationScheme);

        await HttpContext.SignInAsync(
            AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties()
            {
                AllowRefresh = true,
                IsPersistent = true,
                IssuedUtc = DateTimeOffset.UtcNow
            }).ConfigureAwait(false);

        // HttpContext.SignInAsync above dispatches to BlueskyAuthenticationHandler.HandleSignInAsync, which counts the
        // successful sign-in. Counting it here as well would double count every OAuth login.

        return new SignInResult(Succeeded: true, OAuthLoginState: correlationState);
    }

    /// <summary>
    /// Signs out the current session.
    /// </summary>
    /// <param name="scheme">The authentication scheme to sign out of. If <see langword="null" />, <see cref="AuthenticationScheme"/> will be used.</param>
    public async Task SignOut(string? scheme = null)
    {
        scheme ??= AuthenticationScheme;

        await HttpContext.SignOutAsync(scheme).ConfigureAwait(false);
    }
}
