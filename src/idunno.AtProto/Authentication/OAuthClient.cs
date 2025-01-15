// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.InteropServices;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using IdentityModel.OidcClient;
using IdentityModel.OidcClient.DPoP;
using Microsoft.IdentityModel.JsonWebTokens;
using idunno.AtProto.Server;

namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// Helper methods for oauth authentication.
    /// </summary>
    public class OAuthClient
    {
        const string OAuthDiscoveryDocumentEndpoint = ".well-known/oauth-authorization-server";

        private readonly string[] _defaultScopes = ["atproto"];

        private OidcClient? _oidcClient;
        private AuthorizeState? _authorizeState;

        private readonly Func<HttpClient, HttpClient> _clientConfigurationHandler = (httpClient) => { return httpClient; };
        private readonly Func<HttpClientHandler> _innerFactoryHandler = () => { throw new OAuthException("Handler factory not configured"); };
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<OAuthClient> _logger;

        private readonly Guid _logCorrelation = Guid.NewGuid();

        private Uri? _expectedAuthority;

        private Uri? _expectedService;

        private OAuthClient(ILoggerFactory? loggerFactory = null)
        {
            _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            _logger = _loggerFactory.CreateLogger<OAuthClient>();
        }

        /// <summary>
        /// Creates a new instance of <see cref="OAuthClient"/>.
        /// </summary>
        /// <param name="httpClientConfigurator">Func to apply configuration to an <see cref="HttpClient" />.</param>
        /// <param name="innerHandlerFactory">Func to generate the same <see cref="HttpClientHandler"/> used in factory configuration used elsewhere.</param>
        /// <param name="loggerFactory">An optional <see cref="ILoggerFactory"/> to use to create loggers.</param>
        internal OAuthClient(
            Func<HttpClient, HttpClient> httpClientConfigurator,
            Func<HttpClientHandler> innerHandlerFactory,
            ILoggerFactory? loggerFactory = null) : this(loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(httpClientConfigurator);
            ArgumentNullException.ThrowIfNull(innerHandlerFactory);
            _clientConfigurationHandler = httpClientConfigurator;
            _innerFactoryHandler = innerHandlerFactory;
        }

        /// <summary>
        /// Gets the DPoP key to use on subsequent requests.
        /// </summary>
        public string? ProofKey { get; internal set; }

        /// <summary>
        /// Gets the OAuth authorization URI for starting the OAuth flow.
        /// </summary>
        /// <param name="service">The service to acquire a token for.</param>
        /// <param name="clientId">The client ID</param>
        /// <param name="redirectUri">The redirect URI where the oauth server should send tokens back to.</param>
        /// <param name="authority">The authorization server to use.</param>
        /// <param name="scopes">A collection of scopes to request.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="clientId"/> is null or empty.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="redirectUri"/>, <paramref name="authority"/> or <paramref name="scopes"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="scopes"/> is empty.</exception>
        /// <exception cref="OAuthException">Thrown when the authorize state cannot be prepared or encounters an error during preparation.</exception>
        public async Task<Uri> CreateOAuth2StartUri(
            Uri service,
            string clientId,
            Uri redirectUri,
            Uri authority,
            IEnumerable<string>? scopes = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(clientId);
            ArgumentNullException.ThrowIfNull(authority);
            ArgumentNullException.ThrowIfNull(redirectUri);

            ProofKey = JsonWebKeys.CreateRsaJson();

            if (scopes is not null)
            {
                ArgumentOutOfRangeException.ThrowIfZero(scopes.Count());
            }

            _expectedAuthority = authority;
            _expectedService = service;
            scopes ??= _defaultScopes;

            OidcClientOptions oidcOptions = new()
            {
                ClientId = clientId,
                Authority = authority.ToString(),
                Scope = string.Join(" ", scopes.Where(s => !string.IsNullOrEmpty(s))),
                RedirectUri = redirectUri.ToString(),
                LoadProfile = false,
                LoggerFactory = _loggerFactory,
                HttpClientFactory = (oidcOptions) =>
                {
                    var httpClient = new HttpClient(new ProofTokenMessageHandler(ProofKey, _innerFactoryHandler()), true);
                    return _clientConfigurationHandler(httpClient);
                }
            };

            oidcOptions.Policy.Discovery.DiscoveryDocumentPath = OAuthDiscoveryDocumentEndpoint;
            oidcOptions.ConfigureDPoP(ProofKey);

            _oidcClient = new OidcClient(oidcOptions);
            _authorizeState = await _oidcClient.PrepareLoginAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

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
                Uri startUri = new (_authorizeState.StartUrl);

                Logger.OAuthLoginUriGenerated(_logger, authority, startUri, _logCorrelation);

                return startUri;
            }
        }

        /// <summary>
        /// Processes the login response received from the client URI generated from CreateOAuth2StartUri().
        /// </summary>
        /// <param name="data">The data returned to the callback URI</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="OAuthException">Thrown when the internal state of this instance is faulty.</exception>
        public async Task<AccessCredentials?> ProcessOAuth2Response(string data, CancellationToken cancellationToken = default)
        {
            if (ProofKey is null)
            {
                throw new OAuthException("ProofKey is null");
            }

            if (_oidcClient is null)
            {
                throw new OAuthException("Internal _oidcClient is null");
            }

            if (_authorizeState is null)
            {
                throw new OAuthException("Internal _authorizeState is null");
            }

            if (_expectedService is null)
            {
                throw new OAuthException("Internal _expectedService is null");
            }

            LoginResult loginResult = await _oidcClient.ProcessResponseAsync(data, _authorizeState, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (loginResult.IsError)
            {
                ProofKey = null;
                Logger.OAuthLoginFailed(_logger, _logCorrelation, loginResult.Error, loginResult.ErrorDescription);
                return null;
            }

            Logger.OAuthLoginCompleted(_logger, _logCorrelation);

            JsonWebToken accessToken = new(loginResult.AccessToken);

            if (accessToken.Audiences is null || !accessToken.Audiences.Any())
            {
                throw new OAuthException("Issued token does not contain aud.");
            }

            if (!accessToken.GetClaim("scope").ToString().Contains("atproto", StringComparison.Ordinal))
            {
                Logger.OAuthTokenDoesNotContainAtProtoScope(_logger, _logCorrelation);
                throw new OAuthException("Issued token does not contain atproto in scope.");
            }

            Uri issuer = new(accessToken.Issuer);
            if (!issuer.Equals(_expectedAuthority))
            {
                Logger.OAuthTokenHasMismatchedAuthority(_logger, issuer, _expectedAuthority!, _logCorrelation);
                throw new OAuthException("Unexpected access token issuer");
            }

            // TODO: More logging

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

            return new (
                _expectedService,
                loginResult.AccessToken,
                loginResult.RefreshToken,
                ProofKey,
                loginResult.TokenResponse.DPoPNonce);
        }

        /// <summary>
        /// Opens a browser to the specified <paramref name="uri"/>
        /// </summary>
        /// <param name="uri">The uri to open.</param>
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
    }
}
