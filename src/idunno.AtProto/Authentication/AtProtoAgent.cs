// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using System.Net;
using System.Text.Json;

using idunno.AtProto.Authentication;
using idunno.AtProto.Events;

using idunno.AtProto.Server.Models;

namespace idunno.AtProto
{
    public partial class AtProtoAgent
    {
        private readonly ReaderWriterLockSlim _credentialReaderWriterLockSlim = new();
        private AccessCredentials? _credentials;

        private readonly bool _enableTokenRefresh = true;
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
                    _credentialReaderWriterLockSlim.ExitWriteLock();
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

                AtProtoHttpResult<CreateSessionResponse> createSessionResult =
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

                    AccessCredentials accessCredentials = new(
                            service,
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
        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Matching other login methods.")]
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
        /// Called internally by an <see cref="AtProtoHttpClient{TResult}"/> if the credentials were updated.
        /// </summary>
        /// <param name="credentials">The new credentials</param>
        protected internal virtual void InternalOnCredentialsUpdatedCallBack(AtProtoCredentials credentials)
        {
            ArgumentNullException.ThrowIfNull(credentials);

            Debug.Assert(credentials is not AccessCredentials);

            if (credentials is AccessCredentials accessCredentials)
            {
                Credentials = accessCredentials;
                Logger.OnCredentialUpdatedCallbackCalled(_logger);

                OnCredentialsUpdated(new CredentialsUpdatedEventArgs(accessCredentials.Did, accessCredentials.Service, accessCredentials));
            }
            else
            {
                // This should never happen.
                Logger.OnCredentialUpdatedCallbackCalledWithUnexpectedCredentialType(_logger);
            }
        }

        /// <summary>
        /// Sets the agent credentials to the specified <paramref name="accessCredentials"/>.
        /// </summary>
        /// <param name="accessCredentials"><see cref="AccessCredentials"/> to use when authenticating to the service.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="accessCredentials"/> is null.</exception>
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

        /// <summary>
        /// Clears the internal session state used by the agent and tells the service for this agent's current session to cancel the session.
        /// </summary>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AtProtoException">Thrown when the current agent authentication state does not have enough information to call the DeleteSession API.</exception>
        /// <exception cref="LogoutException">Thrown when the DeleteSession API call fails.</exception>
        public async Task Logout(CancellationToken cancellationToken = default)
        {
            if (Credentials is null)
            {
                return;
            }

            if (Credentials.Did is null || Credentials.Service is null || Credentials.RefreshToken is null)
            {
                throw new AtProtoException("agent.Credentials is missing information needed to call DeleteSession");
            }

            Logger.LogoutCalled(_logger, Credentials.Did, Credentials.Service);

            AtProtoHttpResult<EmptyResponse> deleteSessionResult =
                await AtProtoServer.DeleteSession(Credentials, HttpClient, LoggerFactory, cancellationToken).ConfigureAwait(false);

            _credentialReaderWriterLockSlim.EnterWriteLock();
            try
            {
                StopTokenRefreshTimer();

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
            finally
            {
                _credentialReaderWriterLockSlim.ExitWriteLock();
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new instance of <see cref="OAuthClient"/>.
        /// </summary>
        public OAuthClient CreateOAuthClient()
        {
            return new OAuthClient(ConfigureHttpClient, CreateProxyHttpClientHandler, LoggerFactory);
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
    }
}
