// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;

using Microsoft.IdentityModel.JsonWebTokens;

using idunno.AtProto.Authentication;
using idunno.AtProto.Events;
using idunno.AtProto.Models;
using idunno.AtProto.Server;

namespace idunno.AtProto
{
    public partial class AtProtoAgent
    {

        /// <summary>
        /// Authenticates to and creates a session on the <paramref name="service"/> with the specified <paramref name="loginCredentials"/>.
        /// </summary>
        /// <param name="loginCredentials">The credentials to use when authenticating.</param>
        /// <param name="service">The service to authenticate to.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="loginCredentials"/> is null.</exception>
        /// <exception cref="SecurityTokenValidationException">Thrown when the access token issued by the <see cref="Service"/> is not valid.</exception>
        public async Task<AtProtoHttpResult<bool>> Login(LoginCredentials loginCredentials, Uri? service = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(loginCredentials);

            using (_logger.BeginScope($"Handle/Password login for {loginCredentials.Identifier}"))
            {
                lock (_session_SyncLock)
                {
                    StopTokenRefreshTimer();
                }

                if (service is null)
                {
                    Did? userDid = await ResolveHandle(loginCredentials.Identifier, cancellationToken: cancellationToken).ConfigureAwait(false);

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

                Logger.CreateSessionCalled(_logger, loginCredentials.Identifier, service);
                AtProtoHttpResult<CreateSessionResponse> createSessionResult =
                    await AtProtoServer.CreateSession(
                        loginCredentials,
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

                    lock (_session_SyncLock)
                    {
                        Service = service;
                        Session = new Session(service, createSessionResult.Result);

                        _sessionRefreshTimer ??= new();
                        StartTokenRefreshTimer();

                        AuthenticationType authenticationType = AuthenticationType.HandlePassword;
                        if (loginCredentials.AuthFactorToken is not null)
                        {
                            authenticationType = AuthenticationType.HandlePasswordToken;
                        }

                        var sessionCreatedEventArgs = new SessionCreatedEventArgs(
                            Session.Did,
                            service,
                            Session.Handle,
                            authenticationType,
                            Session.AccessCredentials);

                        OnSessionCreated(sessionCreatedEventArgs);
                    }

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

                    lock (_session_SyncLock)
                    {
                        StopTokenRefreshTimer();
                        Session = null;
                    }

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
        /// Authenticates to and creates a session on the <paramref name="service"/> with the specified <paramref name="identifier"/> and <paramref name="password"/>.
        /// </summary>
        /// <param name="identifier">The identifier used to authenticate.</param>
        /// <param name="password">The password used to authenticated.</param>
        /// <param name="emailAuthFactor">An optional email authentication code.</param>
        /// <param name="service">The service to authenticate to.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="identifier" /> or <paramref name="password"/> is null or empty.</exception>
        public async Task<AtProtoHttpResult<bool>> Login(string identifier, string password, string? emailAuthFactor = null, Uri? service = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(identifier);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(password);

            return await Login(new LoginCredentials(identifier, password, emailAuthFactor), service, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Logins in with the specified OAuth <see cref="AccessCredentials"/>.
        /// </summary>
        /// <param name="accessCredentials">The access to authenticate with.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="accessCredentials"/> is null.</exception>
        public async Task<AtProtoHttpResult<bool>> Login(
            AccessCredentials accessCredentials,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(accessCredentials);

            JsonWebToken accessJwt = new(accessCredentials.AccessJwt);
            Did did = accessJwt.Subject;

            AtProtoHttpResult<GetSessionResponse> getSessionResult = await GetSession(accessCredentials, cancellationToken).ConfigureAwait(false);

            if (getSessionResult.Succeeded)
            {
                var restoredSession = new Session(getSessionResult.Result, accessCredentials);

                lock (_session_SyncLock)
                {
                    Logger.SessionCreatedFromOAuthLogin(_logger, did, accessCredentials.Service);

                    Session = restoredSession;
                    _sessionRefreshTimer ??= new();
                    StartTokenRefreshTimer();

                    var sessionCreatedEventArgs = new SessionCreatedEventArgs(
                        restoredSession.Did,
                        restoredSession.Service,
                        restoredSession.Handle,
                        AuthenticationType.OAuth,
                        accessCredentials);

                    OnSessionCreated(sessionCreatedEventArgs);
                }

                return new AtProtoHttpResult<bool>(true, getSessionResult.StatusCode, getSessionResult.HttpResponseHeaders, getSessionResult.AtErrorDetail, getSessionResult.RateLimit);
            }
            else
            {
                lock (_session_SyncLock)
                {
                    StopTokenRefreshTimer(true);
                }

                Logger.SessionCreatedFromOAuthLoginFailed(_logger, did, accessCredentials.Service, getSessionResult.StatusCode, getSessionResult.AtErrorDetail?.Error, getSessionResult.AtErrorDetail?.Message);

                return new AtProtoHttpResult<bool>(false, getSessionResult.StatusCode, getSessionResult.HttpResponseHeaders, getSessionResult.AtErrorDetail, getSessionResult.RateLimit);
            }
        }

        /// <summary>
        /// Clears the internal session state used by the agent and tells the service for this agent's current session to cancel the session.
        /// </summary>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="InvalidSessionException">Thrown when the current session does not have enough information to call the DeleteSession API.</exception>
        /// <exception cref="LogoutException">Thrown when the DeleteSession API call fails.</exception>
        public async Task Logout(CancellationToken cancellationToken = default)
        {
            SessionConfigurationErrorType sessionErrorFlags = SessionConfigurationErrorType.None;

            if (Session is null)
            {
                sessionErrorFlags |= SessionConfigurationErrorType.NullSession;
            }
            else
            {
                if (Session.AccessCredentials.RefreshJwt is null)
                {
                    sessionErrorFlags |= SessionConfigurationErrorType.MissingRefreshToken;
                }

                if (Session.Service is null)
                {
                    sessionErrorFlags |= SessionConfigurationErrorType.MissingService;
                }
            }

            if (sessionErrorFlags != SessionConfigurationErrorType.None)
            {
                throw new InvalidSessionException($"The current session does not have enough information to logout: {sessionErrorFlags}")
                {
                    SessionErrors = sessionErrorFlags
                };
            }

            Logger.LogoutCalled(_logger, Session!.Did, Session.Service!);

            AtProtoHttpResult<EmptyResponse> deleteSessionResult =
                await AtProtoServer.DeleteSession(Session.AccessCredentials!, HttpClient, LoggerFactory, cancellationToken).ConfigureAwait(false);

            lock (_session_SyncLock)
            {
                StopTokenRefreshTimer();

                if (deleteSessionResult.Succeeded)
                {
                    if (Session is not null)
                    {
                        var loggedOutEventArgs = new SessionEndedEventArgs(Session.Did, Session.AccessCredentials.Service);

                        Session = null;

                        OnSessionEnded(loggedOutEventArgs);
                    }
                    else
                    {
                        Logger.LogoutFailed(_logger, Session!.Did, Session.AccessCredentials.Service, deleteSessionResult.StatusCode);
                        Session = null;
                        throw new LogoutException()
                        {
                            StatusCode = deleteSessionResult.StatusCode,
                            Error = deleteSessionResult.AtErrorDetail
                        };
                    }
                }
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
