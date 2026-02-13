// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.InteropServices;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;

using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.DPoP;
using Duende.IdentityModel.OidcClient.Results;

using idunno.AtProto.Server.Models;


namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// Helper methods for oauth authentication.
    /// </summary>
    public class OAuthClient
    {
        const string OAuthDiscoveryDocumentEndpoint = ".well-known/oauth-authorization-server";

        private readonly OAuthOptions? _options;

        private OidcClient? _oidcClient;

        private readonly Func<HttpClient, HttpClient> _clientConfigurationHandler = (httpClient) => { return httpClient; };
        private readonly Func<HttpClientHandler> _innerFactoryHandler = () => { throw new OAuthException("Handler factory not configured"); };
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
            Func<HttpClientHandler> innerHandlerFactory,
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
        /// Gets or sets the state the needs to be held between starting the authorize request and the parsing the response
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when setting to <see langword="null"/>.</exception>
        public OAuthLoginState? State
        {
            get
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

            internal set
            {
                ArgumentNullException.ThrowIfNull(value);

                _authorizeState = value;
                _expectedAuthority = new Uri(value.ExpectedAuthority);
                _expectedService = new Uri(value.ExpectedService);
                _proofKey = value.ProofKey;
                _correlationId = value.CorrelationId;
                _stateExtraProperties = value.ExtraProperties;
            }
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

            _proofKey = JsonWebKeys.CreateRsaJson();

            if (scopes is not null)
            {
                ArgumentOutOfRangeException.ThrowIfZero(scopes.Count());
            }

            _expectedAuthority = authority;
            _expectedService = service;

            scopes ??= _options?.Scopes;
            scopes ??= DefaultScopes;

            string scopeString = string.Join(" ", scopes.Where(s => !string.IsNullOrEmpty(s)));

            // Special case the client ID if it matches localhost to add the desired scope as query string parameters.
            // See Localhost Client Development at https://atproto.com/specs/oauth#clients.
            if (clientId == "http://localhost")
            {
                clientId = QueryHelpers.AddQueryString(clientId, "scope", scopeString);
            }

            OidcClientOptions oidcClientOptions = new()
            {
                ClientId = clientId,
                Authority = _expectedAuthority.ToString(),
                Scope = scopeString,
                RedirectUri = returnUri.ToString(),
                LoadProfile = false,
                DisablePushedAuthorization = false,
                LoggerFactory = _loggerFactory,
                HttpClientFactory = (oidcOptions) =>
                {
                    var httpClient = new HttpClient(new ProofTokenMessageHandler(
                        new DefaultDPoPProofTokenFactory(_proofKey),
                        _innerFactoryHandler()), true);
                    return _clientConfigurationHandler(httpClient);
                }
            };

            oidcClientOptions.Policy.Discovery.DiscoveryDocumentPath = OAuthDiscoveryDocumentEndpoint;
            oidcClientOptions.ConfigureDPoP(_proofKey);

            _oidcClient = new OidcClient(oidcClientOptions);

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

            _authorizeState = await _oidcClient.PrepareLoginAsync(extraParameters, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (_authorizeState is null)
            {
                throw new OAuthException("state preparation failed");
            }
            else if (_authorizeState.IsError)
            {
                throw new OAuthException(_authorizeState.Error);
            }
            else
            {
                if (stateExtraProperties is not null)
                {
                    _stateExtraProperties = stateExtraProperties;
                }

                Uri startUri = new(_authorizeState.StartUrl);

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

            scopes ??= _options?.Scopes;
            scopes ??= DefaultScopes;

            string scopeString = string.Join(" ", scopes.Where(s => !string.IsNullOrEmpty(s)));

            // Special case the client ID if it matches localhost to add the desired scope as query string parameters.
            // See Localhost Client Development at https://atproto.com/specs/oauth#clients.
            if (clientId == "http://localhost")
            {
                clientId = QueryHelpers.AddQueryString(clientId, "scope", scopeString);
            }

            if (_proofKey is null)
            {
                throw new OAuthException("ProofKey is null");
            }

            if (_authorizeState is null)
            {
                throw new OAuthException("Internal _authorizeState is null");
            }

            if (_expectedService is null)
            {
                throw new OAuthException("Internal _expectedService is null");
            }

            if (_oidcClient is null)
            {
                OidcClientOptions oidcClientOptions = BuildOidcClientOptions(clientId, null, scopes);
                _oidcClient = new OidcClient(oidcClientOptions);
            }

            LoginResult loginResult = await _oidcClient.ProcessResponseAsync(callbackData, _authorizeState, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (loginResult.IsError)
            {
                _proofKey = null;
                Logger.OAuthLoginFailed(_logger, _correlationId, loginResult.Error, loginResult.ErrorDescription);
                return null;
            }

            if (loginResult.TokenResponse.DPoPNonce is null)
            {
                throw new OAuthException("login result has no dPoP nonce");
            }

            JsonWebToken accessToken = new(loginResult.AccessToken);

            if (DateTimeOffset.UtcNow < new DateTimeOffset(accessToken.ValidFrom))
            {
                throw new OAuthException("Issued token is not yet valid.");
            }

            if (DateTimeOffset.UtcNow > new DateTimeOffset(accessToken.ValidTo))
            {
                throw new OAuthException("Issued token has already expired.");
            }

            if (accessToken.Audiences is null || !accessToken.Audiences.Any())
            {
                throw new OAuthException("Issued token does not contain aud.");
            }

            if (!accessToken.GetClaim("scope").ToString().Contains("atproto", StringComparison.Ordinal))
            {
                Logger.OAuthTokenDoesNotContainAtProtoScope(_logger, _correlationId);
                throw new OAuthException("Issued token does not contain atproto in scope.");
            }

            Uri issuer = new(accessToken.Issuer);
            if (!issuer.Equals(_expectedAuthority))
            {
                Logger.OAuthTokenHasMismatchedAuthority(_logger, issuer, _expectedAuthority!, _correlationId);
                throw new OAuthException("Unexpected access token issuer");
            }

            AtProtoHttpResult<ServerDescription> serverDescriptionResult;

            using (HttpClientHandler handler = _innerFactoryHandler())
            using (var httpClient = new HttpClient(handler))
            {
                _clientConfigurationHandler(httpClient);
                serverDescriptionResult = await AtProtoServer.DescribeServer(_expectedService, httpClient, _loggerFactory, cancellationToken).ConfigureAwait(false);
            }

            if (!serverDescriptionResult.Succeeded)
            {
                throw new OAuthException($"Could not get service description for {_expectedService}");
            }
            else if (!accessToken.Audiences.Contains(serverDescriptionResult.Result.Did.ToString()))
            {
                throw new OAuthException($"Access token audience did not contain {serverDescriptionResult.Result.Did}");
            }

            Logger.OAuthLoginCompleted(_logger, _correlationId);

            return new(
                _expectedService,
                loginResult.AccessToken,
                loginResult.RefreshToken,
                _proofKey,
                loginResult.TokenResponse.DPoPNonce);
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

            scopes ??= _options?.Scopes;
            scopes ??= DefaultScopes;

            string scopeString = string.Join(" ", scopes.Where(s => !string.IsNullOrEmpty(s)));

            clientId ??= _options?.ClientId;

            ArgumentNullException.ThrowIfNull(clientId);

            // Special case the client ID if it matches localhost to add the desired scope as query string parameters.
            // See Localhost Client Development at https://atproto.com/specs/oauth#clients.
            if (clientId == "http://localhost")
            {
                clientId = QueryHelpers.AddQueryString(clientId, "scope", scopeString);
            }

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
                    var httpClient = new HttpClient(new ProofTokenMessageHandler(new DefaultDPoPProofTokenFactory(refreshCredential.DPoPProofKey), _innerFactoryHandler()), true);
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

                    if (DateTimeOffset.UtcNow < new DateTimeOffset(accessToken.ValidFrom))
                    {
                        throw new OAuthException("Issued token is not yet valid.");
                    }

                    if (DateTimeOffset.UtcNow > new DateTimeOffset(accessToken.ValidTo))
                    {
                        throw new OAuthException("Issued token has already expired.");
                    }

                    if (accessToken.Audiences is null || !accessToken.Audiences.Any())
                    {
                        throw new OAuthException("Issued token does not contain aud.");
                    }

                    if (!accessToken.GetClaim("scope").ToString().Contains("atproto", StringComparison.Ordinal))
                    {
                        Logger.OAuthTokenDoesNotContainAtProtoScope(_logger, _correlationId);
                        throw new OAuthException("Issued token does not contain atproto in scope.");
                    }

                    Uri issuer = new(accessToken.Issuer);
                    if (!issuer.Equals(authority))
                    {
                        Logger.OAuthTokenHasMismatchedAuthority(_logger, issuer, _expectedAuthority!, _correlationId);
                        throw new OAuthException("Unexpected access token issuer");
                    }

                    AtProtoHttpResult<ServerDescription> serverDescriptionResult;
                    using (HttpClientHandler handler = _innerFactoryHandler())
                    using (var httpClient = new HttpClient(handler))
                    {
                        _clientConfigurationHandler(httpClient);
                        serverDescriptionResult = await AtProtoServer.DescribeServer(refreshCredential.Service, httpClient, _loggerFactory, cancellationToken).ConfigureAwait(false);
                    }

                    if (!serverDescriptionResult.Succeeded)
                    {
                        throw new OAuthException($"Could not get service description for {_expectedService}");
                    }
                    else if (!accessToken.Audiences.Contains(serverDescriptionResult.Result.Did.ToString()))
                    {
                        throw new OAuthException($"Access token audience did not contain {serverDescriptionResult.Result.Did}");
                    }
                    else if (serverDescriptionResult.HttpResponseHeaders is null)
                    {
                        throw new OAuthException("DescribeServer() returned no headers");
                    }

                    if (!serverDescriptionResult.Succeeded)
                    {
                        throw new OAuthException($"Could not get service description for {_expectedService}");
                    }
                    else if (!accessToken.Audiences.Contains(serverDescriptionResult.Result.Did.ToString()))
                    {
                        throw new OAuthException($"Access token audience did not contain {serverDescriptionResult.Result.Did}");
                    }

                    Logger.OAuthClientRefreshSucceeded(_logger, authority);

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
        public static void OpenBrowser(Uri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);

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
}
