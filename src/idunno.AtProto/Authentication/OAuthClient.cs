// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Claims;

using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.DPoP;
using Duende.IdentityModel.OidcClient.Results;

using idunno.AtProto.Server;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;

namespace idunno.AtProto.Authentication;

/// <summary>
/// Helper methods for oauth authentication.
/// </summary>
/// <remarks>
/// <para>
///   An instance carries the state of a single login: the DPoP proof key, the authorize state holding the PKCE code
///   verifier, and the authority and service the issued token is validated against. Starting a second login on the same
///   instance replaces all of it, so an instance must not be shared between logins or between users. Every login starts
///   with a new instance, and a login which is completed in a later request is resumed by restoring <see cref="State"/>
///   onto a new instance.
/// </para>
/// <para>
///   The state is published and read as a set, so a caller reading <see cref="State"/> whilst another thread is starting
///   a login sees either all of the previous login or all of the new one, never a proof key from one paired with the
///   authorize state of another.
/// </para>
/// </remarks>
public class OAuthClient
{
    const string OAuthDiscoveryDocumentEndpoint = ".well-known/oauth-authorization-server";

    /// <summary>
    /// The token type an authorization server issues for a DPoP bound access token.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   RFC 9449 requires the token type of a DPoP bound token to be <c>DPoP</c>. An authorization server which answers
    ///   with <c>Bearer</c> has issued a token which is not bound to the proof key, and which anything that steals it can
    ///   replay. Treating that as a DPoP credential would leave the SDK believing a token is sender constrained when it
    ///   is not.
    /// </para>
    /// </remarks>
    private const string DPoPTokenType = "DPoP";

    private readonly OAuthOptions? _options;

#if NET9_0_OR_GREATER
    private readonly Lock _stateLock = new();
#else
    private readonly object _stateLock = new();
#endif

    private OidcClient? _oidcClient;

    private readonly Func<HttpClient, HttpClient> _clientConfigurationHandler = (httpClient) => { return httpClient; };
    private readonly Func<HttpMessageHandler> _innerFactoryHandler = () => { throw new OAuthException("Handler factory not configured"); };
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<OAuthClient> _logger;

    // Internal state parameters
    private Guid _correlationId = Guid.NewGuid();
    private AuthorizeState? _authorizeState;
    private Uri? _expectedAuthority;
    private Uri? _expectedService;
    private string? _proofKey;
    private IDictionary<string, string>? _stateExtraProperties;

    private OAuthClient(ILoggerFactory? loggerFactory = null, OAuthOptions? options = null)
    {
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<OAuthClient>();

        options?.Validate();
        _options = options;
    }

    internal OAuthClient(
        Func<HttpClient, HttpClient> httpClientConfigurator,
        Func<HttpMessageHandler> innerHandlerFactory,
        ILoggerFactory? loggerFactory = null,
        OAuthOptions? options = null) : this(loggerFactory, options)
    {
        ArgumentNullException.ThrowIfNull(httpClientConfigurator);
        ArgumentNullException.ThrowIfNull(innerHandlerFactory);
        _clientConfigurationHandler = httpClientConfigurator;
        _innerFactoryHandler = innerHandlerFactory;
    }

    /// <summary>
    /// Gets the default scopes an Oauth login or refresh will request.
    /// </summary>
    public static IEnumerable<string> DefaultScopes => ["atproto"];

    /// <summary>
    /// Gets the maximum number of bytes to read from an XRPC response body.
    /// </summary>
    internal int MaximumResponseSize { get; init; } = AtProtoHttpClient.DefaultMaximumResponseSize;

    /// <summary>
    /// Gets or sets the state the needs to be held between starting the authorize request and the parsing the response
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when setting to <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the <see cref="OAuthLoginState.ExpectedAuthority"/> or <see cref="OAuthLoginState.ExpectedService"/> of the
    /// value being set is not an absolute http or https uri.
    /// </exception>
    /// <remarks>
    /// <para>
    ///   <see cref="OAuthLoginState.ExpectedAuthority"/> is what an issued access token's <c>iss</c> claim is checked against,
    ///   so restored state decides which authorization server is trusted to have issued the token. It is validated when set
    ///   rather than when it is used, so state which has been tampered with in transit is rejected before it can widen that check.
    /// </para>
    /// </remarks>
    public OAuthLoginState? State
    {
        get
        {
            lock (_stateLock)
            {
                if (_authorizeState == null ||
                    _expectedAuthority == null ||
                    _expectedService == null ||
                    _proofKey == null)
                {
                    return null;
                }
                else
                {
                    return new OAuthLoginState(
                        _authorizeState,
                        _expectedAuthority.ToString(),
                        _expectedService.ToString(),
                        _proofKey,
                        _correlationId,
                        _stateExtraProperties);
                }
            }
        }

        internal set
        {
            ArgumentNullException.ThrowIfNull(value);

            Uri expectedAuthority = ParseLoginStateUri(value.ExpectedAuthority, nameof(OAuthLoginState.ExpectedAuthority));
            Uri expectedService = ParseLoginStateUri(value.ExpectedService, nameof(OAuthLoginState.ExpectedService));

            lock (_stateLock)
            {
                _authorizeState = value;
                _expectedAuthority = expectedAuthority;
                _expectedService = expectedService;
                _proofKey = value.ProofKey;
                _correlationId = value.CorrelationId;
                _stateExtraProperties = value.ExtraProperties;
            }
        }
    }

    private static Uri ParseLoginStateUri(string uri, string propertyName)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsedUri) ||
            (!parsedUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.Ordinal) &&
             !parsedUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"{propertyName} is not an absolute http or https uri.", nameof(uri));
        }

        return parsedUri;
    }

    /// <summary>
    /// Builds an OAuth authorization URI for starting the OAuth flow.
    /// </summary>
    /// <param name="service">The service to acquire a token for.</param>
    /// <param name="authority">The authorization server to use.</param>
    /// <param name="returnUri">The redirect URI where the oauth server should send tokens back to.</param>
    /// <param name="clientId">The client ID</param>
    /// <param name="scopes">A collection of scopes to request. Defaults to "atproto".</param>
    /// <param name="handle">The handle to acquire a token for.</param>
    /// <param name="uriExtraParameters">Any extra parameters to attach to the URI.</param>
    /// <param name="stateExtraProperties">Any extra properties to attach to the state.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="clientId"/> is <see langword="null"/> or white space and no default <see cref="OAuthOptions.ClientId" /> has been set on <see cref="OAuthOptions"/>.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="returnUri"/>, <paramref name="authority"/> or <paramref name="scopes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="scopes"/> is empty.</exception>
    /// <exception cref="OAuthException">Thrown when the authorize state cannot be prepared or encounters an error during preparation.</exception>
    public async Task<Uri> BuildOAuth2LoginUri(
        Uri service,
        Uri authority,
        Uri returnUri,
        string? clientId = null,
        IEnumerable<string>? scopes = null,
        Handle? handle = null,
        IEnumerable<KeyValuePair<string, string>>? uriExtraParameters = null,
        IDictionary<string, string>? stateExtraProperties = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(returnUri);

        clientId ??= _options?.ClientId;
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        string[]? requestedScopes = scopes is null ? null : [.. scopes];

        if (requestedScopes is not null)
        {
            ArgumentOutOfRangeException.ThrowIfZero(requestedScopes.Length);
        }

        requestedScopes ??= _options?.Scopes is null ? null : [.. _options.Scopes];
        requestedScopes ??= [.. DefaultScopes];

        string scopeString = string.Join(" ", requestedScopes.Where(s => !string.IsNullOrEmpty(s)));

        string proofKey = JsonWebKeys.CreateRsaJson();

        // Special case the client ID if it matches localhost to add the desired scope as query string parameters.
        // See Localhost Client Development at https://atproto.com/specs/oauth#clients.
        if (clientId == "http://localhost")
        {
            clientId = QueryHelpers.AddQueryString(clientId, "scope", scopeString);
        }

        OidcClientOptions oidcClientOptions = new()
        {
            ClientId = clientId,
            Authority = authority.ToString(),
            Scope = scopeString,
            RedirectUri = returnUri.ToString(),
            LoadProfile = false,
            DisablePushedAuthorization = false,
            LoggerFactory = _loggerFactory,
            HttpClientFactory = (oidcOptions) =>
            {
                var httpClient = new HttpClient(new ProofTokenMessageHandler(
                    new DefaultDPoPProofTokenFactory(proofKey),
                    _innerFactoryHandler()), true);
                return _clientConfigurationHandler(httpClient);
            }
        };

        oidcClientOptions.Policy.Discovery.DiscoveryDocumentPath = OAuthDiscoveryDocumentEndpoint;
        oidcClientOptions.ConfigureDPoP(proofKey);

        OidcClient oidcClient = new(oidcClientOptions);

        Parameters? extraParameters = null;

        if (handle is not null)
        {
            extraParameters = [KeyValuePair.Create("login_hint", handle.ToString())];
        }

        if (uriExtraParameters is not null && uriExtraParameters.Any())
        {
            if (extraParameters is null)
            {
                extraParameters = [.. uriExtraParameters];
            }
            else
            {
                extraParameters.AddRange(uriExtraParameters);
            }
        }

        AuthorizeState authorizeState = await oidcClient.PrepareLoginAsync(extraParameters, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (authorizeState is null)
        {
            throw new OAuthException("state preparation failed");
        }
        else if (authorizeState.IsError)
        {
            throw new OAuthException(authorizeState.Error);
        }
        else
        {
            // Nothing on the instance has been touched until this point, so a login which fails to prepare leaves any
            // previously prepared login intact rather than pairing its authorize state with a new proof key.
            _oidcClient = oidcClient;

            lock (_stateLock)
            {
                _proofKey = proofKey;
                _expectedAuthority = authority;
                _expectedService = service;
                _authorizeState = authorizeState;

                if (stateExtraProperties is not null)
                {
                    _stateExtraProperties = stateExtraProperties;
                }
            }

            Uri startUri = new(authorizeState.StartUrl);

            Logger.OAuthLoginUriGenerated(_logger, authority, startUri, _correlationId);

            return startUri;
        }
    }

    /// <summary>
    /// Processes the login response received from the client URI generated from CreateOAuth2StartUri().
    /// </summary>
    /// <param name="callbackData">The data returned to the callback URI</param>
    /// <param name="clientId">The client ID</param>
    /// <param name="scopes">A collection of scopes to request. Defaults to "atproto".</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="clientId"/> is <see langword="null"/> or white space and no default <see cref="OAuthOptions.ClientId" /> has been set on <see cref="OAuthOptions"/>.</exception>
    /// <exception cref="OAuthException">Thrown when the internal state of this instance is faulty.</exception>
    public async Task<DPoPAccessCredentials?> ProcessOAuth2LoginResponse(
        string callbackData,
        string? clientId = null,
        IEnumerable<string>? scopes = null,
        CancellationToken cancellationToken = default)
    {
        clientId ??= _options?.ClientId;
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        string[]? requestedScopes = scopes is null ? null : [.. scopes];
        requestedScopes ??= _options?.Scopes is null ? null : [.. _options.Scopes];
        requestedScopes ??= [.. DefaultScopes];

        string scopeString = string.Join(" ", requestedScopes.Where(s => !string.IsNullOrEmpty(s)));

        // Special case the client ID if it matches localhost to add the desired scope as query string parameters.
        // See Localhost Client Development at https://atproto.com/specs/oauth#clients.
        if (clientId == "http://localhost")
        {
            clientId = QueryHelpers.AddQueryString(clientId, "scope", scopeString);
        }

        string proofKey;
        AuthorizeState authorizeState;
        Uri expectedService;
        Uri expectedAuthority;

        // Snapshot the login state as a set, so the rest of the exchange works against one consistent login even if
        // another thread starts a new one part way through.
        lock (_stateLock)
        {
            if (_proofKey is null ||
                _authorizeState is null ||
                _expectedService is null ||
                _expectedAuthority is null)
            {
                throw new OAuthException("There is no login in progress on this instance. Start one with BuildOAuth2LoginUri(), or restore a saved one by setting State.");
            }

            proofKey = _proofKey;
            authorizeState = _authorizeState;
            expectedService = _expectedService;
            expectedAuthority = _expectedAuthority;
        }

        if (_oidcClient is null)
        {
            OidcClientOptions oidcClientOptions = BuildOidcClientOptions(clientId, null, requestedScopes);
            _oidcClient = new OidcClient(oidcClientOptions);
        }

        LoginResult loginResult = await _oidcClient.ProcessResponseAsync(callbackData, authorizeState, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (loginResult.IsError)
        {
            ClearLoginState();
            Logger.OAuthLoginFailed(_logger, _correlationId, loginResult.Error, loginResult.ErrorDescription);
            return null;
        }

        if (!DPoPTokenType.Equals(loginResult.TokenResponse.TokenType, StringComparison.OrdinalIgnoreCase))
        {
            throw new OAuthException($"Authorization server issued a '{loginResult.TokenResponse.TokenType}' token, not a DPoP bound token.");
        }

        if (loginResult.TokenResponse.DPoPNonce is null)
        {
            throw new OAuthException("login result has no dPoP nonce");
        }

        JsonWebToken accessToken = new(loginResult.AccessToken);

        ValidateAccessToken(accessToken, expectedAuthority, _correlationId);

        WarnOnScopesNotGranted(requestedScopes, loginResult.TokenResponse.Scope, _correlationId);

        AtProtoHttpResult<ServerDescription> serverDescriptionResult;

        using (var httpClient = new HttpClient(_innerFactoryHandler()))
        {
            _clientConfigurationHandler(httpClient);
            serverDescriptionResult = await AtProtoServer.DescribeServer(expectedService, httpClient, _loggerFactory, MaximumResponseSize, cancellationToken).ConfigureAwait(false);
        }

        if (!serverDescriptionResult.Succeeded)
        {
            throw new OAuthException($"Could not get service description for {expectedService}");
        }
        else if (!accessToken.Audiences.Contains(serverDescriptionResult.Result.Did.ToString()))
        {
            throw new OAuthException($"Access token audience did not contain {serverDescriptionResult.Result.Did}");
        }

        Logger.OAuthLoginCompleted(_logger, _correlationId);

        return new(
            expectedService,
            loginResult.AccessToken,
            loginResult.RefreshToken,
            proofKey,
            loginResult.TokenResponse.DPoPNonce);
    }

    /// <summary>
    /// Discards the state of the login in progress.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   The state is discarded as a set. Clearing only the proof key would leave the authorize state, and its single use
    ///   PKCE code verifier, available to a subsequent call which has no proof key to bind a token to.
    /// </para>
    /// </remarks>
    private void ClearLoginState()
    {
        lock (_stateLock)
        {
            _proofKey = null;
            _authorizeState = null;
            _expectedAuthority = null;
            _expectedService = null;
            _stateExtraProperties = null;
        }

        _oidcClient = null;
    }

    private void WarnOnScopesNotGranted(IEnumerable<string> requestedScopes, string? grantedScopes, Guid correlationId)
    {
        if (grantedScopes is null)
        {
            return;
        }

        string[] granted = grantedScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string requestedScope in requestedScopes.Where(s => !string.IsNullOrEmpty(s) && !granted.Contains(s, StringComparer.Ordinal)))
        {
            Logger.OAuthScopeNotGranted(_logger, correlationId, requestedScope, grantedScopes);
        }
    }

    /// <summary>
    /// Processes the login response received from the client URI generated from CreateOAuth2StartUri().
    /// </summary>
    /// <param name="state">The state the needs to be hold between starting the authorize request and the response.</param>
    /// <param name="callbackData">The data returned to the callback URI</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is <see langword="null"/>.</exception>
    /// <exception cref="OAuthException">Thrown when the internal state of this instance is faulty.</exception>
    public async Task<DPoPAccessCredentials?> ProcessOAuth2Response(OAuthLoginState state, string callbackData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        State = state;

        return await ProcessOAuth2LoginResponse(callbackData, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Prepares a logout URL for an interactive logout.
    /// </summary>
    /// <param name="credentials">The credentials to logout from.</param>
    /// <param name="authority">The expected authority URI.</param>
    /// <param name="clientId">The client ID for the application. If <see langword="null"/> will be taken from the configured OAuthOptions.</param>
    /// <param name="returnUri">The redirect URI where the oauth server should respond back to.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    internal async Task<Uri> BuildOAuth2LogoutUri(
        DPoPAccessCredentials credentials,
        Uri authority,
        string? clientId = null,
        Uri? returnUri = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        clientId ??= _options?.ClientId;
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        _expectedAuthority = authority;
        _proofKey = credentials.DPoPProofKey;

        if (_oidcClient is null)
        {
            OidcClientOptions oidcClientOptions = BuildOidcClientOptions(clientId, returnUri);
            _oidcClient = new OidcClient(oidcClientOptions);
        }

        string logoutUri = await _oidcClient.PrepareLogoutAsync(
            new LogoutRequest
            {
                IdTokenHint = credentials.AccessJwt,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new Uri(logoutUri);
    }

    /// <summary>
    /// Refreshes tokens from the OidcClient
    /// </summary>
    /// <param name="refreshCredential">The refresh credential to use.</param>
    /// <param name="authority">The authority to refresh the tokens against.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="scopes">Any scopes to be requested when refreshing the credentials.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="refreshCredential"/>, the refresh credential's service, or <paramref name="authority"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="refreshCredential"/>'s refresh token is <see langword="null"/> or white space.</exception>
    /// <exception cref="CredentialException">Thrown when <paramref name="refreshCredential"/> was not issued via OAuth.</exception>
    /// <exception cref="OAuthException">Thrown when an error was returned from the refresh operation, or validation of the issued tokens has failed.</exception>
    public async Task<DPoPAccessCredentials?> RefreshCredentials(
        DPoPRefreshCredential refreshCredential,
        Uri authority,
        string? clientId = null,
        IEnumerable<string>? scopes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refreshCredential);
        ArgumentNullException.ThrowIfNull(refreshCredential.Service);
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshCredential.RefreshToken);
        if (refreshCredential.AuthenticationType != AuthenticationType.OAuth)
        {
            throw new CredentialException(refreshCredential);
        }

        ArgumentNullException.ThrowIfNull(authority);

        string[]? requestedScopes = scopes is null ? null : [.. scopes];
        requestedScopes ??= _options?.Scopes is null ? null : [.. _options.Scopes];
        requestedScopes ??= [.. DefaultScopes];

        string scopeString = string.Join(" ", requestedScopes.Where(s => !string.IsNullOrEmpty(s)));

        clientId ??= _options?.ClientId;

        ArgumentNullException.ThrowIfNull(clientId);

        // Special case the client ID if it matches localhost to add the desired scope as query string parameters.
        // See Localhost Client Development at https://atproto.com/specs/oauth#clients.
        if (clientId == "http://localhost")
        {
            clientId = QueryHelpers.AddQueryString(clientId, "scope", scopeString);
        }

        // Importing the RSA proof key is not cheap, and Duende may call the client factory more than once for a single
        // refresh, so the factory is built once here rather than on every call into the lambda.
        DefaultDPoPProofTokenFactory proofTokenFactory = new(refreshCredential.DPoPProofKey);

        OidcClientOptions oidcOptions = new()
        {
            ClientId = clientId,
            Authority = authority.ToString(),
            Scope = scopeString,
            LoadProfile = false,
            DisablePushedAuthorization = false,
            LoggerFactory = _loggerFactory,
            HttpClientFactory = (oidcOptions) =>
            {
                var httpClient = new HttpClient(new ProofTokenMessageHandler(proofTokenFactory, _innerFactoryHandler()), true);
                return _clientConfigurationHandler(httpClient);
            }
        };

        oidcOptions.Policy.Discovery.DiscoveryDocumentPath = OAuthDiscoveryDocumentEndpoint;
        oidcOptions.ConfigureDPoP(refreshCredential.DPoPProofKey);

        OidcClient client = new(oidcOptions);

        Guid correlationId = Guid.NewGuid();

        using (_logger.BeginScope($"OAuthClient refresh correlation {correlationId}"))
        {
            Logger.OAuthClientRefreshCalled(_logger, refreshCredential.Service, authority);

            RefreshTokenResult refreshResult = await client.RefreshTokenAsync(
                refreshToken: refreshCredential.RefreshToken,
                backChannelParameters: null,
                scope: scopeString,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (refreshResult.IsError)
            {
                Logger.OAuthClientRefreshFailedByAuthority(_logger, refreshResult.Error, refreshResult.ErrorDescription);

                return null;
            }
            else
            {
                JsonWebToken accessToken = new(refreshResult.AccessToken);

                ValidateAccessToken(accessToken, authority, correlationId);

                // Duende's RefreshTokenResult does not surface the token response, so token_type cannot be checked here
                // the way it is on the login path. The granted scopes are taken from the issued token instead.
                if (accessToken.TryGetClaim("scope", out Claim? grantedScopeClaim))
                {
                    WarnOnScopesNotGranted(requestedScopes, grantedScopeClaim.Value, correlationId);
                }

                AtProtoHttpResult<ServerDescription> serverDescriptionResult;
                using (HttpMessageHandler handler = _innerFactoryHandler())
                using (var httpClient = new HttpClient(handler))
                {
                    _clientConfigurationHandler(httpClient);
                    serverDescriptionResult = await AtProtoServer.DescribeServer(refreshCredential.Service, httpClient, _loggerFactory, MaximumResponseSize, cancellationToken).ConfigureAwait(false);
                }

                if (!serverDescriptionResult.Succeeded)
                {
                    throw new OAuthException($"Could not get service description for {refreshCredential.Service}");
                }
                else if (!accessToken.Audiences.Contains(serverDescriptionResult.Result.Did.ToString()))
                {
                    throw new OAuthException($"Access token audience did not contain {serverDescriptionResult.Result.Did}");
                }

                Logger.OAuthClientRefreshSucceeded(_logger, authority);

                // Duende's RefreshTokenResult does not surface the token response, so the nonce the authorization server
                // returned with the refresh is not reachable here. Carrying the previous one forward costs at most one
                // use_dpop_nonce challenge, which the client handles. A credential holds a single nonce shared between the
                // authorization server and the PDS, so storing an authorization server nonce here would not be an
                // improvement; that needs per origin nonce storage rather than a different value in this line.
                return new(
                    refreshCredential.Service,
                    refreshResult.AccessToken,
                    refreshResult.RefreshToken,
                    refreshCredential.DPoPProofKey,
                    refreshCredential.DPoPNonce);
            }
        }
    }

    /// <summary>
    /// Opens a browser to the specified <paramref name="uri"/>
    /// </summary>
    /// <param name="uri">The uri to open.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="uri"/> is relative, or its scheme is not <c>http</c> or <c>https</c>.</exception>
    /// <remarks>
    /// <para>
    ///   On Windows <paramref name="uri"/> is handed to the shell, and on Linux and macOS to <c>xdg-open</c> and <c>open</c>,
    ///   all three of which launch whichever handler is registered for the scheme rather than a browser specifically. A login
    ///   flow builds its address from the authorization endpoint of a discovered authorization server, so an application which
    ///   turns discovery validation off could otherwise reach an arbitrary registered protocol handler from nothing more than
    ///   a hostile handle. Only <c>http</c> and <c>https</c> are opened.
    /// </para>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Vulnerability", "S4036:OS commands should not rely on PATH resolution", Justification = "Browser opening is platform-specific and relies on system commands, which may be installed anywhere.")]
    public static void OpenBrowser(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri)
        {
            throw new ArgumentException("Uri must be absolute.", nameof(uri));
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.Ordinal) &&
            !uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Uri scheme '{uri.Scheme}' is not opened, only http and https are.", nameof(uri));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.ToString(),
                UseShellExecute = true
            });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", uri.ToString());
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", uri.ToString());
        }
    }

    /// <summary>
    /// Converts a token lifetime <see cref="DateTime"/> to a <see cref="DateTimeOffset"/> in UTC.
    /// </summary>
    /// <param name="value">The <see cref="DateTime"/> to convert.</param>
    /// <returns>
    /// The <paramref name="value"/> as a UTC <see cref="DateTimeOffset"/>, or <see langword="null"/> if the
    /// claim the <paramref name="value"/> came from was not present on the token.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <see cref="JsonWebToken.ValidFrom"/> and <see cref="JsonWebToken.ValidTo"/> return
    /// <see cref="DateTime.MinValue"/> with an unspecified kind when the nbf or exp claim is absent.
    /// <see cref="DateTimeOffset"/> interprets an unspecified kind as a local time, and converting
    /// <see cref="DateTime.MinValue"/> from a local time east of UTC underflows, so an absent claim is
    /// detected before any conversion is attempted.
    /// </para>
    /// </remarks>
    internal static DateTimeOffset? ToUtcDateTimeOffset(DateTime value)
    {
        if (value == DateTime.MinValue)
        {
            return null;
        }

        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    /// <summary>
    /// Validates the claims in an access token issued by an authorization server.
    /// </summary>
    /// <param name="accessToken">The access token to validate.</param>
    /// <param name="expectedAuthority">The authority the token is expected to have been issued by.</param>
    /// <param name="correlationId">The correlation identifier used in logging to tie requests and responses together.</param>
    /// <exception cref="OAuthException">Thrown when validation of <paramref name="accessToken"/> fails.</exception>
    internal void ValidateAccessToken(JsonWebToken accessToken, Uri expectedAuthority, Guid correlationId)
    {
        TimeSpan clockSkew = _options?.ClockSkew ?? OAuthOptions.DefaultClockSkew;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        DateTimeOffset? validFrom = ToUtcDateTimeOffset(accessToken.ValidFrom);
        DateTimeOffset? validTo = ToUtcDateTimeOffset(accessToken.ValidTo);

        if (validFrom is not null && now + clockSkew < validFrom)
        {
            throw new OAuthException("Issued token is not yet valid.");
        }

        if (validTo is null)
        {
            throw new OAuthException("Issued token does not contain exp.");
        }

        if (now - clockSkew > validTo)
        {
            throw new OAuthException("Issued token has already expired.");
        }

        if (accessToken.Audiences is null || !accessToken.Audiences.Any())
        {
            throw new OAuthException("Issued token does not contain aud.");
        }

        // The subject becomes the Did of the resulting credentials. Rejecting it here keeps a token which cannot
        // identify its holder inside the validation contract, rather than letting the Did constructor throw an
        // ArgumentException from further down the call chain.
        if (!Did.TryParse(accessToken.Subject, out _))
        {
            throw new OAuthException("Issued token does not contain a valid sub.");
        }

        // The scope claim is a space delimited list, so it is compared entry by entry rather than as a
        // substring, which would also accept scopes that merely contain "atproto", such as "notatproto".
        if (!accessToken.TryGetClaim("scope", out Claim? scopeClaim) ||
            !scopeClaim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains("atproto", StringComparer.Ordinal))
        {
            Logger.OAuthTokenDoesNotContainAtProtoScope(_logger, correlationId);
            throw new OAuthException("Issued token does not contain atproto in scope.");
        }

        if (!Uri.TryCreate(accessToken.Issuer, UriKind.Absolute, out Uri? issuer))
        {
            throw new OAuthException("Issued token does not contain a valid iss.");
        }

        if (!issuer.Equals(expectedAuthority))
        {
            Logger.OAuthTokenHasMismatchedAuthority(_logger, issuer, expectedAuthority, correlationId);
            throw new OAuthException("Unexpected access token issuer");
        }
    }

    private OidcClientOptions BuildOidcClientOptions(
        string? clientId = null,
        Uri? returnUri = null,
        IEnumerable<string>? scopes = null)
    {
        if (_expectedAuthority is null)
        {
            throw new OAuthException("_expectedAuthority is null");
        }

        if (_proofKey is null)
        {
            throw new OAuthException("_proofKey is null");
        }

        OidcClientOptions oidcOptions = new()
        {
            Authority = _expectedAuthority.ToString(),
            LoadProfile = false,
            DisablePushedAuthorization = false,
            LoggerFactory = _loggerFactory,
            HttpClientFactory = (oidcOptions) =>
            {
                var httpClient = new HttpClient(new ProofTokenMessageHandler(new DefaultDPoPProofTokenFactory(_proofKey), _innerFactoryHandler()), true);
                return _clientConfigurationHandler(httpClient);
            }
        };

        if (clientId is not null)
        {
            oidcOptions.ClientId = clientId;
        }

        if (returnUri is not null)
        {
            oidcOptions.RedirectUri = returnUri.ToString();
        }

        if (scopes is not null)
        {
            string scopeString = string.Join(" ", scopes.Where(s => !string.IsNullOrEmpty(s)));
            oidcOptions.Scope = scopeString;
        }

        oidcOptions.Policy.Discovery.DiscoveryDocumentPath = OAuthDiscoveryDocumentEndpoint;
        oidcOptions.ConfigureDPoP(_proofKey);

        return oidcOptions;
    }
}