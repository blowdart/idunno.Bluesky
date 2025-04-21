// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Timers;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using idunno.AtProto.Authentication;
using idunno.AtProto.Authentication.Models;
using idunno.AtProto.Events;


namespace idunno.AtProto
{
    public partial class AtProtoAgent
    {
        private readonly ReaderWriterLockSlim _credentialReaderWriterLockSlim = new();
        private AccessCredentials? _credentials;

        internal readonly bool _enableTokenRefresh = true;
        private readonly TimeSpan _refreshAccessTokenInterval = new(1, 0, 0);
        private System.Timers.Timer? _credentialRefreshTimer;

        /// <summary>
        /// Gets the current credentials for the agent, if any.
        /// </summary>
        public AccessCredentials? Credentials
        {
            get
            {
                _credentialReaderWriterLockSlim.EnterReadLock();
                try
                {
                    return _credentials;
                }
                finally
                {
                    _credentialReaderWriterLockSlim.ExitReadLock();
                }
            }

            set
            {
                _credentialReaderWriterLockSlim.EnterWriteLock();
                try
                {
                    _credentials = value;

                }
                finally
                {
                    _credentialReaderWriterLockSlim.ExitWriteLock();
                }
            }
        }

        /// <summary>
        /// Gets the current <see cref="Did"/> of the agent's authenticated session, if any, otherwise returns null.
        /// </summary>
        public Did? Did
        {
            get
            {
                Did? did = null;
                _credentialReaderWriterLockSlim.EnterReadLock();

                try
                {
                    if (_credentials is AccessCredentials accessCredentials)
                    {
                        did = accessCredentials.Did;
                    }
                }
                finally
                {
                    _credentialReaderWriterLockSlim.ExitReadLock();
                }

                return did;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the agent has current authentication tokens.
        /// </summary>
        [MemberNotNullWhen(true, nameof(Credentials))]
        [MemberNotNullWhen(true, nameof(Did))]
        public override bool IsAuthenticated
        {
            get
            {
                return _credentials is IAccessCredential accessCredential &&
                    accessCredential.Did is not null &&
                    accessCredential.ExpiresOn > DateTimeOffset.UtcNow;
            }
        }

        /// <summary>
        /// Called internally by an <see cref="AtProtoHttpClient{TResult}"/> if the credentials were updated.
        /// </summary>
        /// <param name="credentials">The new credentials</param>
        protected internal virtual void InternalOnCredentialsUpdatedCallBack(AtProtoCredential credentials)
        {
            ArgumentNullException.ThrowIfNull(credentials);

            if (credentials is AccessCredentials accessCredentials)
            {
                Credentials = accessCredentials;
                Logger.OnCredentialUpdatedCallbackCalled(_logger);

                OnCredentialsUpdated(new CredentialsUpdatedEventArgs(accessCredentials.Did, accessCredentials.Service, accessCredentials));
            }
            else if (credentials is DPoPAccessCredentials dPopAccessCredentials)
            {
                Credentials = dPopAccessCredentials;
                Logger.OnCredentialUpdatedCallbackCalled(_logger);

                OnCredentialsUpdated(new CredentialsUpdatedEventArgs(dPopAccessCredentials.Did, dPopAccessCredentials.Service, dPopAccessCredentials));
            }
            else
            {
                // This should never happen.
                Logger.OnCredentialUpdatedCallbackCalledWithUnexpectedCredentialType(_logger);
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="OAuthClient"/>.
        /// </summary>
        public OAuthClient CreateOAuthClient()
        {
            return new OAuthClient(ConfigureHttpClient, CreateProxyHttpClientHandler, LoggerFactory, Options?.OAuthOptions);
        }

        /// <summary>
        /// Creates a new instance of <see cref="OAuthClient"/>.
        /// </summary>
        /// <param name="state">The state to restore in the <see cref="OAuthClient"/>.</param>
        public OAuthClient CreateOAuthClient(OAuthLoginState state)
        {
            ArgumentNullException.ThrowIfNull(state);

            var oAuthClient = new OAuthClient(ConfigureHttpClient, CreateProxyHttpClientHandler, LoggerFactory, Options?.OAuthOptions)
            {
                State = state
            };

            return oAuthClient;
        }

        /// <summary>
        /// Builds an OAuth authorization URI for starting the OAuth flow.
        /// </summary>
        /// <param name="oAuthClient">An instance of <paramref name="oAuthClient"/> to build the URI in.</param>
        /// <param name="handle">The handle to authorize for.</param>
        /// <param name="scopes">A collection of scopes to request. Defaults to "atproto".</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="returnUri">The URI the oauth server should post back to when it has authorized the application.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="oAuthClient"/> or <paramref name="handle"/> is null, or
        /// <paramref name="scopes"/> or <paramref name="returnUri"/>is not specified and is configured on the agent <see cref="Options"/>.
        /// </exception>
        /// <exception cref="OAuthException">
        /// Thrown when the OAuth options on the agent have not been configured, or
        /// the specified <paramref name="handle"/> cannot be resolved, or
        /// the PDS for the specified <paramref name="handle"/> cannot be resolved, or
        /// the authorization server for <paramref name="handle"/> cannot be discovered.
        /// </exception>
        public async Task<Uri> BuildOAuth2LoginUri(
            OAuthClient oAuthClient,
            Handle handle,
            IEnumerable<string>? scopes = null,
            Uri? returnUri = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(oAuthClient);
            ArgumentNullException.ThrowIfNull(handle);

            if (Options is null || Options.OAuthOptions is null)
            {
                throw new OAuthException("OAuth options are not configured.");
            }

            returnUri ??= Options.OAuthOptions.ReturnUri;
            ArgumentNullException.ThrowIfNull(returnUri);

            Options.OAuthOptions.Validate();

            scopes ??= Options.OAuthOptions.Scopes;
            ArgumentNullException.ThrowIfNull(scopes);

            Did? did = await ResolveHandle(handle, cancellationToken).ConfigureAwait(false) ?? throw new OAuthException("Could not resolve DID");
            Uri? pds = await ResolvePds(did, cancellationToken).ConfigureAwait(false) ?? throw new OAuthException($"Could not resolve PDS for {did}.");
            Uri? authorizationServer = await ResolveAuthorizationServer(pds, cancellationToken).ConfigureAwait(false) ?? throw new OAuthException($"Could not discover authorization server for {handle}.");

            return await oAuthClient.BuildOAuth2LoginUri(
                service: pds,
                returnUri: returnUri,
                authority: authorizationServer,
                scopes: scopes,
                handle: handle,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Processes the login response received from the client URI generated from CreateOAuth2StartUri() and sets the agent credentials if successful.
        /// </summary>
        /// <param name="oAuthClient">An instance of <paramref name="oAuthClient"/> to build the URI in.</param>
        /// <param name="callbackData">The data returned to the callback URI</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="OAuthException">Thrown when the internal state of this instance is faulty.</exception>
        public async Task<bool> ProcessOAuth2LoginResponse(OAuthClient oAuthClient, string callbackData, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(oAuthClient);
            DPoPAccessCredentials? accessCredentials = await oAuthClient.ProcessOAuth2LoginResponse(callbackData, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (accessCredentials is not null)
            {
                return await Login(accessCredentials, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Get a <see cref="ServiceCredential"/> on behalf of the requesting DID for the requested <paramref name="audience"/>.
        /// </summary>
        /// <param name="service">The server to request the service authentication from.</param>
        /// <param name="audience">The DID of the service that the token will be used to authenticate with.</param>
        /// <param name="lxm">Lexicon (XRPC) method to bind the requested token to</param>
        /// <param name="expiry">An optional length of the time the token should be valid for.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        ///   Thrown when <paramref name="service"/>, <paramref name="audience"/> or <paramref name="lxm"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="expiry"/> is specified but is zero or negative.</exception>
        public async Task<AtProtoHttpResult<ServiceCredential>> GetServiceAuth(
            Uri service,
            Did audience,
            Nsid lxm,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(audience);
            ArgumentNullException.ThrowIfNull(lxm);

            if (expiry is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiry.Value.TotalSeconds, 0);
            }

            using (_logger.BeginScope($"GetServiceAuth()"))
            {
                if (expiry is not null)
                {
                    Logger.RequestingServiceAuthToken(_logger, Service, audience, expiry.Value.ToString("c"), lxm);
                }
                else
                {
                    Logger.RequestingServiceAuthTokenNoExpirySpecified(_logger, Service, audience, lxm);
                }

                if (!IsAuthenticated)
                {
                    Logger.GetServiceAuthFailedAsSessionIsAnonymous(_logger, Service);

                    throw new AuthenticationRequiredException();
                }

                AtProtoHttpResult<ServiceCredential> serviceCredentialResult = await AtProtoServer.GetServiceAuth(
                    audience: audience,
                    expiry: expiry,
                    lxm: lxm,
                    service: service,
                    accessCredentials: Credentials,
                    httpClient: HttpClient,
                    loggerFactory: LoggerFactory,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (serviceCredentialResult.Succeeded && serviceCredentialResult.Result.AccessJwt is not null)
                {
                    TimeSpan expiresIn = GetTimeToJwtTokenExpiry(serviceCredentialResult.Result.AccessJwt);
                    Logger.ServiceAuthTokenAcquired(_logger, service, audience, expiresIn.ToString("c"), lxm);
                }
                else
                {
                    Logger.ServiceAuthTokenAcquisitionFailed(
                        _logger,
                        service,
                        Did,
                        audience,
                        lxm,
                        serviceCredentialResult.StatusCode,
                        serviceCredentialResult.AtErrorDetail?.Error,
                        serviceCredentialResult.AtErrorDetail?.Message);
                }

                return serviceCredentialResult;
            }
        }

        /// <summary>
        /// Gets information about the session associated with the access token provided.
        /// </summary>
        /// <param name="accessCredentials">The access credentials to authenticate with.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="accessCredentials"/> is null.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="accessCredentials"/>'s AccessJwt is null or empty.</exception>
        public async Task<AtProtoHttpResult<Session>> GetSession(
            AccessCredentials accessCredentials,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentException.ThrowIfNullOrEmpty(accessCredentials.AccessJwt);

            return await AtProtoServer.GetSession(accessCredentials, HttpClient, InternalOnCredentialsUpdatedCallBack, LoggerFactory, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resolves the token endpoint <see cref="Uri"/> for the specified <paramref name="authorizationServer"/>.
        /// </summary>
        /// <param name="authorizationServer">The <see cref="Uri"/> of the the authorization server whose token endpoint uri should be retrieved.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="authorizationServer"/> is null.</exception>
        public async Task<Uri?> GetTokenEndpoint(Uri authorizationServer, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(authorizationServer);

            Uri? tokenEndpoint = null;

            if (!cancellationToken.IsCancellationRequested)
            {
                using (Stream responseStream = await HttpClient.GetStreamAsync(new Uri($"https://{authorizationServer.Host}/.well-known/oauth-authorization-server"), cancellationToken).ConfigureAwait(false))
                using (JsonDocument protectedResultMetadata = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false))
                {
                    if (!cancellationToken.IsCancellationRequested && protectedResultMetadata is not null)
                    {
                        string? tokenEndpointValue = protectedResultMetadata.RootElement.GetProperty("token_endpoint").GetString();

                        if (!cancellationToken.IsCancellationRequested && !string.IsNullOrWhiteSpace(tokenEndpointValue))
                        {
                            tokenEndpoint = new Uri(tokenEndpointValue);
                        }
                    }
                }
            }

            return tokenEndpoint;
        }

        /// <summary>
        /// Resolves the revocation endpoint <see cref="Uri"/> for the specified <paramref name="authorizationServer"/>.
        /// </summary>
        /// <param name="authorizationServer">The <see cref="Uri"/> of the the authorization server whose token endpoint uri should be retrieved.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="authorizationServer"/> is null.</exception>
        public async Task<Uri?> GetRevocationEndpoint(Uri authorizationServer, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(authorizationServer);

            Uri? tokenEndpoint = null;

            if (!cancellationToken.IsCancellationRequested)
            {
                using (Stream responseStream = await HttpClient.GetStreamAsync(new Uri($"https://{authorizationServer.Host}/.well-known/oauth-authorization-server"), cancellationToken).ConfigureAwait(false))
                using (JsonDocument protectedResultMetadata = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false))
                {
                    if (!cancellationToken.IsCancellationRequested && protectedResultMetadata is not null)
                    {
                        string? tokenEndpointValue = protectedResultMetadata.RootElement.GetProperty("revocation_endpoint").GetString();

                        if (!cancellationToken.IsCancellationRequested && !string.IsNullOrWhiteSpace(tokenEndpointValue))
                        {
                            tokenEndpoint = new Uri(tokenEndpointValue);
                        }
                    }
                }
            }

            return tokenEndpoint;
        }

        /// <summary>
        /// Authenticates to and creates a session on the <paramref name="service"/> with the specified <paramref name="identifier"/> and <paramref name="password"/>.
        /// </summary>
        /// <param name="identifier">The identifier used to authenticate.</param>
        /// <param name="password">The password used to authenticated.</param>
        /// <param name="authFactorToken">An optional multi factory authentication code.</param>
        /// <param name="service">The service to authenticate to.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="identifier" /> or <paramref name="password"/> is null or empty.</exception>
        public async Task<AtProtoHttpResult<bool>> Login(string identifier, string password, string? authFactorToken = null, Uri? service = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
            ArgumentException.ThrowIfNullOrWhiteSpace(password);

            using (_logger.BeginScope($"Handle/Password login for {identifier}"))
            {
                StopTokenRefreshTimer();

                if (service is null)
                {
                    Did? userDid = await ResolveHandle(identifier, cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (userDid is null || cancellationToken.IsCancellationRequested)
                    {
                        return new AtProtoHttpResult<bool>(
                            false,
                            HttpStatusCode.NotFound,
                            null,
                            new AtErrorDetail() { Error = "HandleNotResolvable", Message = "Handle could not be resolved to a DID." });
                    }

                    Uri? pds = null;

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        pds = await ResolvePds(userDid, cancellationToken).ConfigureAwait(false);
                    }

                    if (pds is null || cancellationToken.IsCancellationRequested)
                    {
                        return new AtProtoHttpResult<bool>(
                            false,
                            HttpStatusCode.NotFound,
                            null,
                            new AtErrorDetail() { Error = "PdsNotResolvable", Message = $"Could not resolve a PDS for {userDid}." });
                    }

                    service = pds;
                }

                Logger.CreateSessionCalled(_logger, identifier, service);

                AtProtoHttpResult<Session> createSessionResult =
                    await AtProtoServer.CreateSession(
                        identifier,
                        password,
                        authFactorToken,
                        service,
                        httpClient: HttpClient,
                        loggerFactory: LoggerFactory,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                Logger.CreateSessionReturned(_logger, createSessionResult.StatusCode);

                if (createSessionResult.Succeeded)
                {
                    if (!await ValidateJwtToken(createSessionResult.Result.AccessJwt, createSessionResult.Result.Did, service).ConfigureAwait(false))
                    {
                        Logger.CreateSessionJwtValidationFailed(_logger);
                        throw new SecurityTokenValidationException("The issued access token could not be validated.");
                    }

                    AuthenticationType authenticationType = AuthenticationType.UsernamePassword;
                    if (!string.IsNullOrWhiteSpace(authFactorToken))
                    {
                        authenticationType = AuthenticationType.UsernamePasswordAuthFactorToken;
                    }

                    AccessCredentials accessCredentials = new(
                            service,
                            authenticationType,
                            createSessionResult.Result.AccessJwt,
                            createSessionResult.Result.RefreshJwt);

                    await InternalLogin(accessCredentials).ConfigureAwait(false);

                    return new AtProtoHttpResult<bool>()
                    {
                        Result = true,
                        StatusCode = createSessionResult.StatusCode,
                        AtErrorDetail = createSessionResult.AtErrorDetail,
                        RateLimit = createSessionResult.RateLimit
                    };
                }
                else
                {
                    Logger.CreateSessionFailed(_logger, createSessionResult.StatusCode);

                    StopTokenRefreshTimer();
                    Credentials = null;

                    return new AtProtoHttpResult<bool>
                    {
                        Result = false,
                        StatusCode = createSessionResult.StatusCode,
                        AtErrorDetail = createSessionResult.AtErrorDetail,
                        RateLimit = createSessionResult.RateLimit
                    };
                }
            }
        }

        /// <summary>
        /// Sets the agent credentials to the specified <paramref name="accessCredentials"/>.
        /// </summary>
        /// <param name="accessCredentials"><see cref="AccessCredentials"/> to use when authenticating to the service.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="accessCredentials"/> or any of its properties are null.</exception>
        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Matching other login methods, so keeping cancellationToken for ease of use.")]
        public async Task<bool> Login(
            AccessCredentials accessCredentials,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(accessCredentials.AccessJwt);
            ArgumentNullException.ThrowIfNull(accessCredentials.Did);
            ArgumentNullException.ThrowIfNull(accessCredentials.RefreshToken);
            ArgumentNullException.ThrowIfNull(accessCredentials.Service);

            if (accessCredentials is DPoPAccessCredentials dPoPAccessCredentials)
            {
                return await Login(dPoPAccessCredentials, cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            Logger.AgentAuthenticatedWithOAuthCredentials(_logger, accessCredentials.Did, accessCredentials.Service);

            await InternalLogin(accessCredentials).ConfigureAwait(false);

            return true;
        }

        /// <summary>
        /// Sets the agent credentials to the specified <paramref name="dPoPAccessCredentials"/>.
        /// </summary>
        /// <param name="dPoPAccessCredentials"><see cref="DPoPAccessCredentials"/> to use when authenticating to the service.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="dPoPAccessCredentials"/> or any of its properties are null.</exception>
        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Matching other login methods.")]
        public async Task<bool> Login(
            DPoPAccessCredentials dPoPAccessCredentials,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dPoPAccessCredentials);
            ArgumentNullException.ThrowIfNull(dPoPAccessCredentials.AccessJwt);
            ArgumentNullException.ThrowIfNull(dPoPAccessCredentials.Did);
            ArgumentNullException.ThrowIfNull(dPoPAccessCredentials.RefreshToken);
            ArgumentNullException.ThrowIfNull(dPoPAccessCredentials.Service);
            ArgumentNullException.ThrowIfNull(dPoPAccessCredentials.DPoPProofKey);
            ArgumentNullException.ThrowIfNull(dPoPAccessCredentials.DPoPNonce);

            Logger.AgentAuthenticatedWithDPoPOAuthCredentials(_logger, dPoPAccessCredentials.Did, dPoPAccessCredentials.Service);

            await InternalLogin(dPoPAccessCredentials).ConfigureAwait(false);

            return true;
        }

        /// <summary>
        /// Clears the internal session state used by the agent and tells the service for this agent's current session to cancel the session.
        /// </summary>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="CredentialException">Thrown when the current agent authentication state does not have enough information to call the DeleteSession API.</exception>
        /// <exception cref="LogoutException">Thrown when the DeleteSession API call fails.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public async Task Logout(CancellationToken cancellationToken = default)
        {
            if (Credentials is null)
            {
                return;
            }

            if (Credentials.AuthenticationType != AuthenticationType.UsernamePassword &&
                Credentials.AuthenticationType != AuthenticationType.UsernamePasswordAuthFactorToken)
            {
                // Call revocation openid-connect endpoint
                if (Credentials is not DPoPAccessCredentials accessCredentials)
                {
                    throw new CredentialException("Credential type is OAuth but it cannot be converted to DPoPAccessCredentials.");
                }

                Uri? authorizationService = await ResolveAuthorizationServer(accessCredentials.Service, cancellationToken).ConfigureAwait(false) ??
                    throw new CredentialException($"Could not resolve authorization server for {accessCredentials.Service}");

                Uri? revocationEndpoint = await GetRevocationEndpoint(authorizationService, cancellationToken).ConfigureAwait(false) ??
                    throw new CredentialException($"Could not resolve revocation endpoint for {authorizationService}");

                if (Options is null || Options.OAuthOptions is null)
                {
                    throw new OAuthException("OAuth options are not configured.");
                }

                Options.OAuthOptions.Validate();
                string scopeString = string.Join(" ", Options.OAuthOptions.Scopes.Where(s => !string.IsNullOrEmpty(s)));

                string clientId = Options.OAuthOptions.ClientId;

                // Special case the client ID if it matches localhost to add the desired scope as query string parameters.
                // See Localhost Client Development at https://atproto.com/specs/oauth#clients.
                if (clientId == "http://localhost")
                {
                    clientId = QueryHelpers.AddQueryString(clientId, "scope", scopeString);
                }

                Logger.LogoutCalled(_logger, Credentials.Did, Credentials.Service);

                StopTokenRefreshTimer();

                AtProtoHttpClient<EmptyResponse> revokeRequest = new(LoggerFactory);

                using (var formData = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("token", accessCredentials.RefreshToken),
                    new KeyValuePair<string, string>("token_type_hint", "refresh_token"),
                    new KeyValuePair<string, string>("client_id", clientId),
                ]))
                {
                    AtProtoHttpResult<EmptyResponse> revokeResponse = await revokeRequest.Post(
                        service: authorizationService,
                        endpoint: revocationEndpoint.AbsolutePath,
                        record: formData,
                        jsonSerializerOptions: AtProtoServer.AtProtoJsonSerializerOptions,
                        credentials: Credentials,
                        httpClient: HttpClient,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (!revokeResponse.Succeeded)
                    {
                        Logger.RevokeFailed(_logger, Credentials.Did, Credentials.Service, revokeResponse.StatusCode, "refresh_token");
                        throw new LogoutException()
                        {
                            StatusCode = revokeResponse.StatusCode,
                            Error = revokeResponse.AtErrorDetail
                        };
                    }
                }

                using (var formData = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("token", accessCredentials.AccessJwt),
                    new KeyValuePair<string, string>("token_type_hint", "access_token"),
                    new KeyValuePair<string, string>("client_id", clientId),
                ]))
                {
                    AtProtoHttpResult<EmptyResponse> revokeResponse = await revokeRequest.Post(
                        service: authorizationService,
                        endpoint: revocationEndpoint.AbsolutePath,
                        record: formData,
                        jsonSerializerOptions: AtProtoServer.AtProtoJsonSerializerOptions,
                        credentials: Credentials,
                        httpClient: HttpClient,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (!revokeResponse.Succeeded)
                    {
                        Logger.RevokeFailed(_logger, Credentials.Did, Credentials.Service, revokeResponse.StatusCode, "access_token");
                        throw new LogoutException()
                        {
                            StatusCode = revokeResponse.StatusCode,
                            Error = revokeResponse.AtErrorDetail
                        };
                    }
                }

                var unauthenticatedEventArgs = new UnauthenticatedEventArgs(Credentials.Did, Credentials.Service);

                Credentials = null;
                OnUnauthenticated(unauthenticatedEventArgs);
            }
            else
            {
                if (Credentials.Did is null || Credentials.Service is null || Credentials.RefreshToken is null)
                {
                    throw new CredentialException("agent.Credentials is missing information needed to call DeleteSession");
                }

                Logger.LogoutCalled(_logger, Credentials.Did, Credentials.Service);

                StopTokenRefreshTimer();

                // Take the refresh token value from credentials and make it a specific refresh token.
                RefreshCredential refreshCredential = new(Credentials);

                AtProtoHttpResult<EmptyResponse> deleteSessionResult =
                    await AtProtoServer.DeleteSession(refreshCredential, HttpClient, LoggerFactory, cancellationToken).ConfigureAwait(false);

                if (deleteSessionResult.Succeeded)
                {
                    var unauthenticatedEventArgs = new UnauthenticatedEventArgs(Credentials.Did, Credentials.Service);

                    Credentials = null;
                    OnUnauthenticated(unauthenticatedEventArgs);
                }
                else
                {
                    Logger.LogoutFailed(_logger, Credentials.Did, Credentials.Service, deleteSessionResult.StatusCode);
                    Credentials = null;
                    throw new LogoutException()
                    {
                        StatusCode = deleteSessionResult.StatusCode,
                        Error = deleteSessionResult.AtErrorDetail
                    };
                }
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        /// <summary>
        /// Acquires a new access and refresh token for the agent, and updates the agent <see cref="Credentials"/>.
        /// </summary>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when agent is not authenticated.</exception>
        public async Task<bool> RefreshCredentials(CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                Logger.RefreshCredentialsFailedNoSession(_logger);
                throw new AuthenticationRequiredException();
            }

            if (Credentials.AuthenticationType == AuthenticationType.UsernamePassword ||
                Credentials.AuthenticationType == AuthenticationType.UsernamePasswordAuthFactorToken)
            {
                return await RefreshSessionIssuedCredentials(Credentials, cancellationToken).ConfigureAwait(false);
            }
            else if (Credentials.AuthenticationType == AuthenticationType.OAuth)
            {
                if (Credentials is not DPoPAccessCredentials accessCredentials)
                {
                    throw new CredentialException("Credential type is OAuth but it cannot be converted to DPoPAccessCredentials.");
                }

                DPoPRefreshCredential refreshCredential = new (accessCredentials.Service, accessCredentials.RefreshToken, accessCredentials.DPoPProofKey, accessCredentials.DPoPNonce);

                return await RefreshOAuthIssuedCredentials(refreshCredential, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new CredentialException(Credentials);
            }
        }

        /// <summary>
        /// Acquires a new access and refresh token for the specified <paramref name="credential"/>, and updates the agent <see cref="Credentials"/>.
        /// </summary>
        /// <param name="credential">The credential to refresh.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when agent is not authenticated.</exception>
        public async Task<bool> RefreshCredentials(AtProtoCredential credential, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(credential);

            // Create refresh credentials if passed access credentials.
            if (credential is DPoPAccessCredentials dPoPAccessCredentials)
            {
                credential = new DPoPRefreshCredential(dPoPAccessCredentials);
            }
            else if (credential is AccessCredentials accessCredentials)
            {
                credential = new RefreshCredential(accessCredentials);
            }

            return credential switch
            {
                DPoPRefreshCredential dPoPRefreshCredential => await RefreshOAuthIssuedCredentials(dPoPRefreshCredential, cancellationToken).ConfigureAwait(false),
                RefreshCredential refreshCredential => await RefreshSessionIssuedCredentials(refreshCredential, cancellationToken).ConfigureAwait(false),
                _ => throw new CredentialException(credential, "Cannot refresh credentials of this type."),
            };
        }

        /// <summary>
        /// Resolves the authorization server <see cref="Uri"/> for the specified <paramref name="handle"/>.
        /// </summary>
        /// <param name="handle">The handle of the account to resolve the authorization server for.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="handle"/> is null or white space.</exception>
        public async Task<Uri?> ResolveAuthorizationServer(string handle, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(handle);

            Did? did = await ResolveHandle(handle, cancellationToken).ConfigureAwait(false) ?? throw new ArgumentException($"{handle} cannot be resolved to a DID", nameof(handle));
            Uri? pds = await ResolvePds(did, cancellationToken).ConfigureAwait(false) ?? throw new ArgumentException($"PDS cannot be discovered for {handle}.", nameof(handle));
            Logger.ResolveAuthorizationServerCalled(_logger, pds);

            return await ResolveAuthorizationServer(pds, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resolves the authorization server <see cref="Uri"/> for the specified <paramref name="pds"/>.
        /// </summary>
        /// <param name="pds">The <see cref="Uri"/> of the PDS to resolve the authorization server for.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pds"/> is null.</exception>
        public async Task<Uri?> ResolveAuthorizationServer(Uri pds, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(pds);

            Logger.ResolveAuthorizationServerCalled(_logger, pds);

            Uri? authorizationServer = null;

            if (!cancellationToken.IsCancellationRequested)
            {
                using (Stream responseStream = await HttpClient.GetStreamAsync(new Uri($"https://{pds.Host}/.well-known/oauth-protected-resource"), cancellationToken).ConfigureAwait(false))
                using (JsonDocument protectedResultMetadata = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false))
                {
                    if (!cancellationToken.IsCancellationRequested && protectedResultMetadata is not null)
                    {
                        JsonElement.ArrayEnumerator authorizationServers = protectedResultMetadata.RootElement.GetProperty("authorization_servers").EnumerateArray();

                        if (!cancellationToken.IsCancellationRequested && authorizationServers.Any())
                        {
                            string serverUri = authorizationServers.First(s => !string.IsNullOrEmpty(s.GetString())).ToString();

                            if (!string.IsNullOrEmpty(serverUri))
                            {
                                authorizationServer = new Uri(serverUri);
                            }
                        }
                    }
                }
            }

            if (authorizationServer is not null)
            {
                Logger.ResolveAuthorizationServerDiscovered(_logger, pds, authorizationServer);
            }
            else
            {
                Logger.ResolveAuthorizationServerFailed(_logger, pds);
            }

            return authorizationServer;
        }

        private static TimeSpan GetTimeToJwtTokenExpiry(string jwt)
        {
            if (string.IsNullOrEmpty(jwt))
            {
                throw new ArgumentNullException(nameof(jwt));
            }

            JsonWebToken token = new(jwt);

            DateTimeOffset validUntil = DateTime.SpecifyKind(token.ValidTo, DateTimeKind.Utc);
            TimeSpan validityPeriod = validUntil - DateTimeOffset.UtcNow;

            return validityPeriod;
        }

        private async Task InternalLogin(AccessCredentials accessCredentials)
        {
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(accessCredentials.AccessJwt);
            ArgumentNullException.ThrowIfNull(accessCredentials.Did);
            ArgumentNullException.ThrowIfNull(accessCredentials.RefreshToken);
            ArgumentNullException.ThrowIfNull(accessCredentials.Service);

            Service = accessCredentials.Service;
            Credentials = accessCredentials;

            _credentialRefreshTimer ??= new();
            StartTokenRefreshTimer();

            var authenticatedEventArgs = new AuthenticatedEventArgs(accessCredentials);

            OnAuthenticated(authenticatedEventArgs);

            await Task.CompletedTask.ConfigureAwait(false);
        }

        internal async Task<bool> RefreshOAuthIssuedCredentials(DPoPRefreshCredential refreshCredential, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(refreshCredential);
            ArgumentNullException.ThrowIfNull(refreshCredential.Service);

            ArgumentException.ThrowIfNullOrWhiteSpace(refreshCredential.RefreshToken);

            if (Options is null || Options.OAuthOptions is null)
            {
                throw new OAuthException("OAuthOptions have not been configured.");
            }

            Options.OAuthOptions.Validate();

            if (refreshCredential.AuthenticationType != AuthenticationType.OAuth)
            {
                throw new CredentialException(refreshCredential);
            }

            string tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshCredential.RefreshToken)));

            using (_logger.BeginScope($"RefreshOAuthIssuedCredentials() with refresh token #{tokenHash}"))
            {
                Logger.RefreshOAuthIssuedCredentialsCalled(_logger, refreshCredential.Service, tokenHash);

                if (_credentialRefreshTimer is not null)
                {
                    StopTokenRefreshTimer();
                }

                // Get authorization server
                Uri? authorizationServer = await ResolveAuthorizationServer(refreshCredential.Service, cancellationToken).ConfigureAwait(false) ??
                    throw new ArgumentException($"Authorization server cannot be found for {refreshCredential.Service}", nameof(refreshCredential));

                OAuthClient oAuthClient = CreateOAuthClient();

                DPoPAccessCredentials? refreshedCredentials = await oAuthClient.RefreshCredentials(
                    refreshCredential: refreshCredential,
                    authority: authorizationServer,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (refreshedCredentials is null)
                {
                    return false;
                }

                if (!await ValidateJwtToken(refreshedCredentials.AccessJwt, refreshedCredentials.Did, refreshCredential.Service).ConfigureAwait(false))
                {
                    Logger.RefreshOAuthIssuedCredentialsTokenValidationFailed(_logger, refreshedCredentials.Did, refreshCredential.Service);

                    throw new SecurityTokenValidationException("The issued access token could not be validated.");
                }

                Logger.RefreshOAuthIssuedCredentialsSucceeded(_logger, refreshedCredentials.Did, refreshedCredentials.Service);

                Credentials = refreshedCredentials;

                _credentialRefreshTimer ??= new();
                StartTokenRefreshTimer();

                var credentialsUpdatedEventArgs = new CredentialsUpdatedEventArgs(
                    refreshedCredentials.Did,
                    refreshedCredentials.Service,
                    refreshedCredentials);

                OnCredentialsUpdated(credentialsUpdatedEventArgs);

                return true;
            }
        }

        internal async Task<bool> RefreshSessionIssuedCredentials(RefreshCredential refreshCredential, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(refreshCredential);
            ArgumentNullException.ThrowIfNull(refreshCredential.Service);

            ArgumentException.ThrowIfNullOrWhiteSpace(refreshCredential.RefreshToken);

            if (refreshCredential.AuthenticationType != AuthenticationType.UsernamePassword &&
                refreshCredential.AuthenticationType != AuthenticationType.UsernamePasswordAuthFactorToken)
            {
                throw new CredentialException(refreshCredential);
            }

            if (refreshCredential is IAccessCredential)
            {
                refreshCredential = new RefreshCredential(refreshCredential.Service, refreshCredential.AuthenticationType, refreshCredential.RefreshToken);
            }

            string tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshCredential.RefreshToken)));

            using (_logger.BeginScope($"RefreshSessionIssuedCredentials() with refresh token #{tokenHash}"))
            {
                Logger.RefreshSessionIssuedCredentialsCalled(_logger, refreshCredential.Service, tokenHash);

                if (_credentialRefreshTimer is not null)
                {
                    StopTokenRefreshTimer();
                }

                AtProtoHttpResult<Session> refreshSessionResult;
                try
                {
                    refreshSessionResult = await AtProtoServer.RefreshSession(
                        refreshCredential,
                        HttpClient,
                        credentialsUpdated: null,
                        loggerFactory: LoggerFactory,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Logger.TokenRefreshApiThrew(_logger, e);
                    throw;
                }

                if (!refreshSessionResult.Succeeded || refreshSessionResult.Result.AccessJwt is null || refreshSessionResult.Result.RefreshJwt is null)
                {
                    Logger.RefreshSessionApiCallFailed(_logger, refreshCredential.Service, tokenHash, refreshSessionResult.StatusCode);

                    var tokenRefreshFailedEventArgs = new TokenRefreshFailedEventArgs(
                        refreshCredential.RefreshToken,
                        refreshCredential.Service,
                        refreshSessionResult.StatusCode,
                        refreshSessionResult.AtErrorDetail);

                    OnTokenRefreshFailed(tokenRefreshFailedEventArgs);
                    return false;
                }

                if (!await ValidateJwtToken(refreshSessionResult.Result.AccessJwt, refreshSessionResult.Result.Did, refreshCredential.Service).ConfigureAwait(false))
                {
                    Logger.RefreshSessionIssuedCredentialsTokenValidationFailed(_logger, refreshSessionResult.Result.Did, refreshCredential.Service);

                    throw new SecurityTokenValidationException("The issued access token could not be validated.");
                }

                AccessCredentials refreshedCredentials = new(
                        refreshCredential.Service,
                        refreshCredential.AuthenticationType,
                        refreshSessionResult.Result.AccessJwt,
                        refreshSessionResult.Result.RefreshJwt);

                Logger.RefreshSessionIssuedCredentialsSucceeded(_logger, refreshedCredentials.Did, refreshedCredentials.Service);

                Credentials = refreshedCredentials;

                _credentialRefreshTimer ??= new();
                StartTokenRefreshTimer();

                var credentialsUpdatedEventArgs = new CredentialsUpdatedEventArgs(
                    refreshedCredentials.Did,
                    refreshedCredentials.Service,
                    refreshedCredentials);

                OnCredentialsUpdated(credentialsUpdatedEventArgs);

                return true;
            }
        }

        private void RefreshTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            Logger.BackgroundTokenRefreshFired(_logger);

            RefreshCredentials().FireAndForget();
        }

        private void StartTokenRefreshTimer()
        {
            if (_enableTokenRefresh)
            {
                if (Credentials is AccessCredentials accessCredentials && !string.IsNullOrEmpty(accessCredentials.AccessJwt))
                {
                    TimeSpan accessTokenExpiresIn = GetTimeToJwtTokenExpiry(accessCredentials.AccessJwt);

                    if (accessTokenExpiresIn.TotalSeconds < 60)
                    {
                        // As we're about to expire, go refresh the token
                        RefreshCredentials().FireAndForget();
                        return;
                    }

                    TimeSpan refreshIn = _refreshAccessTokenInterval;
                    if (accessTokenExpiresIn < _refreshAccessTokenInterval)
                    {
                        refreshIn = accessTokenExpiresIn - new TimeSpan(0, 1, 0);
                    }

                    if (_credentialRefreshTimer is not null)
                    {
                        _credentialRefreshTimer.Interval = refreshIn.TotalMilliseconds >= int.MaxValue ? int.MaxValue : refreshIn.TotalMilliseconds;
                        _credentialRefreshTimer.Elapsed += RefreshTimerElapsed;
                        _credentialRefreshTimer.Enabled = true;
                        _credentialRefreshTimer.Start();

                        Logger.TokenRefreshTimerStarted(_logger, _credentialRefreshTimer.Interval);
                    }
                }
            }
            else
            {
                Logger.TokenRefreshTimerStartCalledButRefreshDisabled(_logger);
            }

        }

        private void StopTokenRefreshTimer(bool dispose = false)
        {
            if (_credentialRefreshTimer is not null)
            {
                _credentialRefreshTimer.Stop();
                Logger.TokenRefreshTimerStopped(_logger);

                if (dispose)
                {
                    _credentialRefreshTimer.Dispose();
                    _credentialRefreshTimer = null;
                }
            }
        }

        [SuppressMessage("Security", "CA5404:Do not disable token validation checks", Justification = "PDSs do not issue JWTs with issuers whose signing key can be retrieved.")]
        private static async Task<bool> ValidateJwtToken(string jwt, Did did, Uri service)
        {
            bool isValid = false;

            // Disable issuer and signature validation because the Bluesky PDS implementation does not expose a
            // .well-known/openid-configuration endpoint to retrieve the issuer and signing key from.

            // Oauth token signature validation is done during the oauth flow.

            TokenValidationParameters validationParameters = new()
            {
                ValidateAudience = true,
                ValidAudience = $"did:web:{service.Host}",
                ValidateIssuer = false,
                ValidateIssuerSigningKey = false,
                ValidateLifetime = true,
                IssuerSigningKeyValidator = (securityKey, securityToken, validationParameters) => true,
                SignatureValidator = (token, validationParameters) => new JsonWebToken(token)
            };

            JsonWebTokenHandler tokenHandler = new();
            TokenValidationResult validationResult = await tokenHandler.ValidateTokenAsync(jwt, validationParameters).ConfigureAwait(false);

            if (validationResult.IsValid)
            {
                // Validate the subject matches the expected DID.
                isValid = string.Equals((string?)validationResult.Claims.FirstOrDefault(c => c.Key == "sub").Value, did.ToString(), StringComparison.OrdinalIgnoreCase);
            }

            return isValid;
        }
    }
}
