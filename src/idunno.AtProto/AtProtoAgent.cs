// Copyright(c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using System.Timers;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using idunno.AtProto.Repo;
using idunno.AtProto.Server;
using idunno.AtProto.Events;
using Blob = idunno.AtProto.Repo.Blob;

using idunno.DidPlcDirectory;

namespace idunno.AtProto
{
    /// <summary>
    /// Provides a class for sending requests to and receiving responses from an atproto service, identified by its service URI.
    /// </summary>
    public class AtProtoAgent : Agent
    {
        private readonly bool _enableTokenRefresh = true;
        private readonly TimeSpan _refreshAccessTokenInterval = new(1, 0, 0);

        private System.Timers.Timer? _sessionRefreshTimer;
        private readonly object _session_SyncLock = new();
        private volatile bool _disposed;

        private readonly DirectoryAgent _directoryAgent;

        private readonly ILogger<AtProtoAgent> _logger;

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgent"/>
        /// </summary>
        /// <param name="service">The URI of the AtProto service to connect to.</param>
        /// <param name="httpClient">An optional <see cref="HttpClient"/> to use when making requests.</param>
        /// <param name="options"><see cref="AtProtoAgentOptions"/> for the use in the creation of this instance of <see cref="AtProtoAgent"/>.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use when creating loggers.</param>
        public AtProtoAgent(Uri service, HttpClient? httpClient = null, AtProtoAgentOptions? options = null, ILoggerFactory? loggerFactory = default) : base(httpClient)
        {
            ArgumentNullException.ThrowIfNull(service);

            Service = service;

            if (options is not null)
            {
                _enableTokenRefresh = options.EnableBackgroundTokenRefresh;
            }

            loggerFactory ??= NullLoggerFactory.Instance;

            _logger = loggerFactory.CreateLogger<AtProtoAgent>();

            _directoryAgent = new DirectoryAgent(httpClient, loggerFactory: loggerFactory);
        }

        /// <summary>
        /// Gets the <see cref="Uri"/> for the AT Proto service the agent is configured to issue commands against.
        /// </summary>
        /// <remarks>
        /// <para>
        ///   This may change based on the results of a <see cref="Login(Credentials, Uri?, CancellationToken)"/> operation if the
        ///   Personal Data Server (PDS) discovered in the user's DID Document is different from the service URI provided at construction.
        ///</para>
        /// </remarks>
        public Uri Service { get; protected set; }

        /// <summary>
        /// Gets or sets an authenticated session to use when making requests that require authentication.
        /// </summary>
        /// <value>
        /// An authenticated session to use when making requests that require authentication.
        /// </value>
        public Session? CurrentSession { get; protected set; }

        /// <summary>
        /// Gets a value indicating whether the agent has an active session.
        /// </summary>
        public override bool IsAuthenticated
        {
            get
            {
                return CurrentSession is not null && !string.IsNullOrEmpty(CurrentSession.AccessJwt);
            }
        }

        /// <summary>
        /// Gets the current session's access token if the agent is authenticated, otherwise returns null.
        /// </summary>
        protected string? AccessToken
        {
            get
            {
                if (IsAuthenticated)
                {
                    return CurrentSession?.AccessJwt;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Gets the current session's refresh token if the agent is authenticated, otherwise returns null.
        /// </summary>
        protected string? RefreshToken
        {
            get
            {
                if (IsAuthenticated)
                {
                    return CurrentSession?.RefreshJwt;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="AtProtoAgent"/> and optionally disposes of the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to releases only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                if (_sessionRefreshTimer is not null)
                {
                    _sessionRefreshTimer.Stop();
                    _sessionRefreshTimer.Enabled = false;
                    _sessionRefreshTimer.Dispose();
                    _sessionRefreshTimer = null;
                }

                SessionRefreshed = null;

                _directoryAgent.Dispose();
            }

            base.Dispose(disposing);

            _disposed = true;
        }

        /// <summary>
        /// Raised when a session is created for this instance of <see cref="AtProtoAgent"/>.
        /// </summary>
        public event EventHandler<SessionCreatedEventArgs>? SessionCreated;

        /// <summary>
        /// Raised when the session for this instance of <see cref="AtProtoAgent"/> is refreshed.
        /// </summary>
        public event EventHandler<SessionRefreshedEventArgs>? SessionRefreshed;

        /// <summary>
        /// Raised when the session refresh for this instance of <see cref="AtProtoAgent"/> failed.
        /// </summary>
        public event EventHandler<SessionRefreshFailedEventArgs>? SessionRefreshFailed;

        /// <summary>
        /// Raised when the session for this instance of <see cref="AtProtoAgent"/> ended.
        /// </summary>
        public event EventHandler<SessionEndedEventArgs>? SessionEnded;

        /// <summary>
        /// Called to raise any <see cref="SessionCreated"/> events, if any.
        /// </summary>
        /// <param name="e">The <see cref="SessionCreatedEventArgs"/> for the event.</param>
        protected virtual void OnSessionCreated(SessionCreatedEventArgs e)
        {
            EventHandler<SessionCreatedEventArgs>? sessionCreated = SessionCreated;

            if (!_disposed)
            {
                sessionCreated?.Invoke(this, e);
            }
        }

        /// <summary>
        /// Called to raise any <see cref="SessionRefreshed"/> events, if any.
        /// </summary>
        /// <param name="e">The <see cref="SessionRefreshedEventArgs"/> for the event.</param>
        protected virtual void OnSessionRefreshed(SessionRefreshedEventArgs e)
        {
            EventHandler<SessionRefreshedEventArgs>? sessionRefreshed = SessionRefreshed;

            if (!_disposed)
            {
                sessionRefreshed?.Invoke(this, e);
            }
        }

        /// <summary>
        /// Called to raise any <see cref="SessionRefreshFailed"/> events, if any.
        /// </summary>
        /// <param name="e">The <see cref="SessionRefreshFailedEventArgs"/> for the event.</param>
        protected virtual void OnSessionRefreshFailed(SessionRefreshFailedEventArgs e)
        {
            EventHandler<SessionRefreshFailedEventArgs>? sessionRefreshFailed = SessionRefreshFailed;

            if (!_disposed)
            {
                sessionRefreshFailed?.Invoke(this, e);
            }
        }

        /// <summary>
        /// Called to raise any <see cref="SessionEnded"/> events, if any.
        /// </summary>
        /// <param name="e">The <see cref="SessionEndedEventArgs"/> for the event.</param>
        protected virtual void OnSessionEnded(SessionEndedEventArgs e)
        {
            EventHandler<SessionEndedEventArgs>? sessionEnded = SessionEnded;

            if (!_disposed)
            {
                sessionEnded?.Invoke(this, e);
            }
        }

        /// <summary>
        /// Resolves a handle (domain name) to a DID.
        /// </summary>
        /// <param name="handle">The handle to resolve.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<Did?> ResolveHandle(string handle, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(handle);

            Logger.ResolveHandleCalled(_logger, handle);

            Did? result = await AtProtoServer.ResolveHandle(handle, HttpClient, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result is null)
            {
                Logger.CouldNotResolveHandleToDid(_logger, handle);
            }
            else
            {
                Logger.ResolveHandleToDid(_logger, handle, result);
            }

            return result;
        }

        /// <summary>
        /// Resolves the <see cref="DidDocument"/> for the specified <paramref name="did"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> to resolve the <see cref="DidDocument"/> for.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<DidDocument?> ResolveDidDocument(Did did, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(did);

            Logger.ResolveDidDocumentCalled(_logger, did);

            DidDocument? didDocument = null;

            if (!cancellationToken.IsCancellationRequested)
            {
                AtProtoHttpResult<DidDocument> didDocumentResolutionResult = await
                    _directoryAgent.ResolveDidDocument(did, cancellationToken: cancellationToken).ConfigureAwait(false);

                if (didDocumentResolutionResult.Succeeded && didDocumentResolutionResult.Result is not null)
                {
                    didDocument = didDocumentResolutionResult.Result;
                }
                else
                {
                    Logger.ResolveDidDocumentFailed(_logger, did, didDocumentResolutionResult.StatusCode);
                }
            }

            return didDocument;
        }

        /// <summary>
        /// Resolves the Personal Data Server (PDS) <see cref="Uri"/>for the specified <paramref name="did"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> to resolve the PDS <see cref="Uri"/> for.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<Uri?> ResolvePds(Did did, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(did);

            Logger.ResolvePdsCalled(_logger, did);

            Uri? pds = null;

            if (!cancellationToken.IsCancellationRequested)
            {
                DidDocument? didDocument = await ResolveDidDocument(did, cancellationToken).ConfigureAwait(false);

                if (didDocument is not null && didDocument.Services is not null)
                {
                    pds = didDocument.Services!.Where(s => s.Id == @"#atproto_pds").FirstOrDefault()!.ServiceEndpoint;
                }
            }

            if (pds is null)
            {
                Logger.ResolvePdsFailed(_logger, did);
            }

            return pds;
        }

        /// <summary>
        /// Resolves the authorization server <see cref="Uri"/> for the specified <paramref name="pds"/>.
        /// </summary>
        /// <param name="pds">The <see cref="Uri"/> of the PDS to resolve the authorization server for.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<Uri?> ResolveAuthorizationServer(Uri pds, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(pds);

            Logger.ResolveAuthorizationServerCalled(_logger, pds);

            Uri? authorizationServer = null;

            if (!cancellationToken.IsCancellationRequested)
            {
                using (Stream responseStream = await HttpClient.GetStreamAsync(new Uri($"https://{pds.Host}/.well-known/oauth-protected-resource"), cancellationToken).ConfigureAwait(false))
                using (JsonDocument protectedResultMetadata = await JsonDocument.ParseAsync(responseStream, cancellationToken:cancellationToken).ConfigureAwait(false))
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

        /// <summary>
        /// Logins into the <paramref name="service"/> with the specified <paramref name="credentials"/>.
        /// </summary>
        /// <param name="credentials">The credentials to use when authenticating.</param>
        /// <param name="service">The service to authenticate to.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<AtProtoHttpResult<bool>> Login(Credentials credentials, Uri? service = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(credentials);

            if (service is null)
            {
                Did? userDid = await ResolveHandle(credentials.Identifier, cancellationToken).ConfigureAwait(false);

                if (userDid is null || cancellationToken.IsCancellationRequested)
                {
                    return new AtProtoHttpResult<bool>(
                        HttpStatusCode.NotFound,
                        false,
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
                        HttpStatusCode.NotFound,
                        false,
                        new AtErrorDetail() { Error = "PdsNotResolvable", Message = $"Could not resolve a PDS for {userDid}." });
                }

                service = pds;
            }

            StopTokenRefreshTimer();

            Logger.CreateSessionCalled(_logger, credentials.Identifier, service);
            AtProtoHttpResult<CreateSessionResponse> createSessionResult = await AtProtoServer.CreateSession(credentials, service, HttpClient, cancellationToken).ConfigureAwait(false);
            Logger.CreateSessionReturned(_logger, createSessionResult.StatusCode);

            if (createSessionResult.Result is not null && createSessionResult.Succeeded)
            {
                if (!await ValidateJwtToken(createSessionResult.Result.AccessJwt, createSessionResult.Result.Did, service).ConfigureAwait(false))
                {
                    Logger.CreateSessionJwtValidationFailed(_logger);
                    throw new SecurityTokenValidationException("The issued access token could not be validated.");
                }

                lock (_session_SyncLock)
                {
                    CurrentSession = new Session(service, createSessionResult.Result);
                    _sessionRefreshTimer ??= new();
                    StartTokenRefreshTimer();

                    var sessionCreatedEventArgs = new SessionCreatedEventArgs(
                        createSessionResult.Result.Did,
                        service,
                        createSessionResult.Result.Handle,
                        createSessionResult.Result.AccessJwt,
                        createSessionResult.Result.RefreshJwt);
                    OnSessionCreated(sessionCreatedEventArgs);
                }

                return new AtProtoHttpResult<bool>() { StatusCode = createSessionResult.StatusCode, Result = true };
            }
            else
            {
                Logger.CreateSessionFailed(_logger, createSessionResult.StatusCode);

                lock (_session_SyncLock)
                {
                    _sessionRefreshTimer?.Stop();
                    CurrentSession = null;
                }

                return new AtProtoHttpResult<bool>
                {
                    AtErrorDetail = createSessionResult.AtErrorDetail,
                    StatusCode = createSessionResult.StatusCode,
                    Result = false
                };
            }
        }

        /// <summary>
        /// Logins into the <paramref name="service"/> with the specified <paramref name="identifier"/> and <paramref name="password"/>.
        /// </summary>
        /// <param name="identifier">The identifier used to authenticate.</param>
        /// <param name="password">The password used to authenticated.</param>
        /// <param name="emailAuthFactor">An optional email authentication code.</param>
        /// <param name="service">The service to authenticate to.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the identifier or password is null or empty.</exception>
        public async Task<AtProtoHttpResult<bool>> Login(string identifier, string password, string? emailAuthFactor = null, Uri? service = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentNullException(nameof(identifier));
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException(nameof(password));
            }

            return await Login(new Credentials(identifier, password, emailAuthFactor), service, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Clears the internal session state used by the agent and tells the service for this agent's current session to cancel the session.
        /// </summary>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="InvalidSessionException">Thrown if the current session does not have enough information to call the DeleteSession API.</exception>
        /// <exception cref="LogoutException">Thrown if the DeleteSession API call fails.</exception>
        public async Task Logout(CancellationToken cancellationToken = default)
        {
            SessionConfigurationErrorType sessionErrorFlags = SessionConfigurationErrorType.None;

            if (CurrentSession is null)
            {
                sessionErrorFlags &= SessionConfigurationErrorType.NullSession;
            }
            else
            {
                if (CurrentSession.RefreshJwt is null)
                {
                    sessionErrorFlags &= SessionConfigurationErrorType.MissingRefreshToken;
                }

                if (CurrentSession.Service is null)
                {
                    sessionErrorFlags &= SessionConfigurationErrorType.MissingService;
                }
            }

            if (sessionErrorFlags != SessionConfigurationErrorType.None)
            {
                throw new InvalidSessionException($"The current session does not have enough information to logout: {sessionErrorFlags}")
                {
                    SessionErrors = sessionErrorFlags
                };
            }

            Logger.LogoutCalled(_logger, CurrentSession!.Did, CurrentSession.Service!);

            Uri currentSessionService = CurrentSession!.Service!;

            AtProtoHttpResult<EmptyResponse> deleteSessionResult =
                await AtProtoServer.DeleteSession(CurrentSession!.RefreshJwt!, currentSessionService, HttpClient, cancellationToken).ConfigureAwait(false);

            lock (_session_SyncLock)
            {
                StopTokenRefreshTimer();

                if (deleteSessionResult.Succeeded)
                {
                    if (CurrentSession is not null)
                    {
                        var loggedOutEventArgs = new SessionEndedEventArgs(CurrentSession.Did, currentSessionService);

                        CurrentSession = null;

                        OnSessionEnded(loggedOutEventArgs);
                    }
                    else
                    {
                        Logger.LogoutFailed(_logger, CurrentSession!.Did, CurrentSession.Service, deleteSessionResult.StatusCode);
                        CurrentSession = null;
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
        /// Refreshes the current authenticated session and updates the access and refresh tokens.
        /// </summary>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="InvalidSessionException">Thrown if the current session does not have enough information to call refresh itself.</exception>
        public async Task<bool> RefreshSession(CancellationToken cancellationToken = default)
        {
            SessionConfigurationErrorType sessionErrorFlags = SessionConfigurationErrorType.None;

            if (CurrentSession is null)
            {
                sessionErrorFlags |= SessionConfigurationErrorType.NullSession;
            }

            if (CurrentSession is not null && CurrentSession.RefreshJwt is null)
            {
                sessionErrorFlags |= SessionConfigurationErrorType.MissingRefreshToken;
            }

            if (CurrentSession is not null && CurrentSession.Service is null)
            {
                sessionErrorFlags |= SessionConfigurationErrorType.MissingService;
            }

            if (sessionErrorFlags != SessionConfigurationErrorType.None)
            {
                var sessionRefreshFailedEventArgs = new SessionRefreshFailedEventArgs(sessionErrorFlags, CurrentSession?.Did, CurrentSession?.Service);

                OnSessionRefreshFailed(sessionRefreshFailedEventArgs);

                throw new InvalidSessionException($"The current session does not have enough information to refresh: {sessionErrorFlags}")
                {
                    SessionErrors = sessionErrorFlags
                };
            }

            return await RefreshSession(CurrentSession!.RefreshJwt!, CurrentSession.Service!, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Refreshes the session specified by the <paramref name="refreshJwt"/> on the <paramref name="service"/>
        /// </summary>
        /// <param name="refreshJwt">The refresh token to use to refresh the session.</param>
        /// <param name="service">The service to refresh the session on.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the refresh token or the service URI are null.</exception>
        public async Task<bool> RefreshSession(string refreshJwt, Uri? service = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(refreshJwt);

            service ??= Service;

            if (!IsAuthenticated)
            {
                Logger.RefreshSessionFailedNoSession(_logger, service);
                throw new AuthenticatedSessionRequiredException();
            }

            Logger.RefreshSessionCalled(_logger, CurrentSession!.Did, service);

            if (_sessionRefreshTimer is not null)
            {
                lock (_session_SyncLock)
                {
                    StopTokenRefreshTimer();
                }
            }

            AtProtoHttpResult<RefreshSessionResult> refreshSessionResult =
                await AtProtoServer.RefreshSession(refreshJwt, service, HttpClient, cancellationToken).ConfigureAwait(false);

            if (!refreshSessionResult.Succeeded || refreshSessionResult.Result is null || refreshSessionResult.Result.AccessJwt is null || refreshSessionResult.Result.RefreshJwt is null)
            {
                Logger.RefreshSessionApiCallFailed(_logger, CurrentSession!.Did, service, refreshSessionResult.StatusCode);

                var sessionRefreshFailedEventArgs = new SessionRefreshFailedEventArgs(
                    SessionConfigurationErrorType.None,
                    null,
                    service,
                    refreshSessionResult.StatusCode,
                    refreshSessionResult.AtErrorDetail);

                OnSessionRefreshFailed(sessionRefreshFailedEventArgs);

                return false;
            }

            if (! await ValidateJwtToken(refreshSessionResult.Result.AccessJwt, refreshSessionResult.Result.Did, service).ConfigureAwait(false))
            {
                Logger.RefreshSessionTokenValidationFailed(_logger, CurrentSession!.Did, service);

                throw new SecurityTokenValidationException("The issued access token could not be validated.");
            }

            lock (_session_SyncLock)
            {
                if (CurrentSession is not null)
                {
                    Logger.RefreshSessionSucceeded(_logger, CurrentSession.Did, service);

                    CurrentSession!.AccessJwt = refreshSessionResult.Result.AccessJwt;
                    CurrentSession!.RefreshJwt = refreshSessionResult.Result.RefreshJwt;
                    _sessionRefreshTimer ??= new();
                    StartTokenRefreshTimer();

                    var sessionRefreshedEventArgs = new SessionRefreshedEventArgs(
                        CurrentSession.Did,
                        service,
                        refreshSessionResult.Result.AccessJwt,
                        refreshSessionResult.Result.RefreshJwt);

                    OnSessionRefreshed(sessionRefreshedEventArgs);

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets information about the session associated with the access token provided.
        /// </summary>
        /// <param name="accessJwt">The access token whose session information to retrieve.</param>
        /// <param name="service">The service <see cref="Uri"/> the session access token was generated from.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the <paramref name="accessJwt"/> is null or empty.</exception>
        /// <exception cref="InvalidSessionException">Thrown if the current session does not have enough information to call refresh itself.</exception>
        public async Task<AtProtoHttpResult<GetSessionResult>> GetSession(string accessJwt, Uri? service = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(accessJwt);

            service ??= CurrentSession!.Service!;

            return await AtProtoServer.GetSession(accessJwt, service, HttpClient, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Restores a session on the <paramref name="service"/> from either the <paramref name="accessJwt"/> or the <paramref name="refreshJwt"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> the tokens are associated with.</param>
        /// <param name="accessJwt">The access token to restore the session for.</param>
        /// <param name="refreshJwt">The refresh token to restore the session for.</param>
        /// <param name="service">The <see cref="Uri"/> of the PDS that issued the tokens.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="SessionRestorationFailedException">Thrown if the session recreated on the PDS does not match the expected <paramref name="did"/>.</exception>
        public async Task<bool> RestoreSession(Did did, string? accessJwt, string refreshJwt, Uri service, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(did);
            ArgumentException.ThrowIfNullOrWhiteSpace(refreshJwt);
            ArgumentNullException.ThrowIfNull(service);

            Session? restoredSession = null;
            bool wereTokensRefreshed = false;

            Logger.RestoreSessionCalled(_logger, did, service);

            // Try the access token first if there is one.
            if (!string.IsNullOrEmpty(accessJwt) && GetTimeToJwtTokenExpiry(accessJwt) > new TimeSpan(0, 5, 0))
            {
                AtProtoHttpResult<GetSessionResult> getSessionResult = await GetSession(accessJwt, service, cancellationToken).ConfigureAwait(false);
                if (getSessionResult.Succeeded && getSessionResult.Result is not null)
                {
                    restoredSession = new Session(service, getSessionResult.Result)
                    {
                        AccessJwt = accessJwt,
                        RefreshJwt = refreshJwt
                    };
                }
            }

            // If that failed, try refreshing the session to get a new token, then try again.
            if (restoredSession is null && !cancellationToken.IsCancellationRequested)
            {
                AtProtoHttpResult<RefreshSessionResult> refreshSessionResult =
                        await AtProtoServer.RefreshSession(refreshJwt, service, HttpClient, cancellationToken).ConfigureAwait(false);

                if (refreshSessionResult.Succeeded && refreshSessionResult.Result is not null)
                {
                    if (! await ValidateJwtToken(refreshSessionResult.Result.AccessJwt, refreshSessionResult.Result.Did, service).ConfigureAwait(false))
                    {
                        throw new SecurityTokenValidationException("The issued access token could not be validated.");
                    }

                    AtProtoHttpResult<GetSessionResult> getSessionResult = await GetSession(refreshSessionResult.Result.AccessJwt, service, cancellationToken).ConfigureAwait(false);
                    if (getSessionResult.Succeeded && getSessionResult.Result is not null)
                    {
                        wereTokensRefreshed = true;
                        restoredSession = new Session(service, getSessionResult.Result)
                        {
                            AccessJwt = refreshSessionResult.Result.AccessJwt,
                            RefreshJwt = refreshSessionResult.Result.RefreshJwt
                        };
                    }
                }
            }

            if (restoredSession is not null)
            {
                if (restoredSession.Did != did)
                {
                    Logger.RestoreSessionDidValidationFailed(_logger, did, restoredSession.Did);
                    throw new SessionRestorationFailedException("The restored session DID not match the expected DID.");
                }

                lock (_session_SyncLock)
                {
                    Logger.RestoreSessionSucceeded(_logger, did, service);

                    CurrentSession = restoredSession;
                    _sessionRefreshTimer ??= new();
                    StartTokenRefreshTimer();

                    if (wereTokensRefreshed && restoredSession.AccessJwt is not null && restoredSession.RefreshJwt is not null)
                    {
                        var sessionRefreshedEventArgs = new SessionRefreshedEventArgs(
                            restoredSession.Did,
                            service,
                            restoredSession.AccessJwt,
                            restoredSession.RefreshJwt);

                        OnSessionRefreshed(sessionRefreshedEventArgs);
                    }
                }

                return true;
            }
            else
            {
                lock (_session_SyncLock)
                {
                    StopTokenRefreshTimer(true);
                }
                return false;
            }
        }

        /// <summary>
        /// Describes the <paramref name="server"/>'s account creation requirements and capabilities.
        /// </summary>
        /// <param name="server">The service whose account description is to be retrieved.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<AtProtoHttpResult<ServerDescription>> DescribeServer(Uri? server, CancellationToken cancellationToken = default)
        {
            server ??= Service;

            return await AtProtoServer.DescribeServer(server, HttpClient, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a record in the specified collection belonging to the current user.
        /// </summary>
        /// <param name="record">The record to be created.</param>
        /// <param name="collection">The collection the record should be created in.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the current session is not authenticated.</exception>
        public async Task<AtProtoHttpResult<StrongReference>> CreateRecord(AtProtoRecordValue record, string collection, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNullOrEmpty(collection);

            if (!IsAuthenticated)
            {
                Logger.CreateRecordFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticatedSessionRequiredException();
            }

            AtProtoHttpResult<StrongReference> result = await CreateRecord(record, collection, CurrentSession!.Did, cancellationToken).ConfigureAwait(false);

            return result;
        }
        /// <summary>
        /// Creates a record in the specified collection belonging to the current user.
        /// </summary>
        /// <param name="record">The record to be created.</param>
        /// <param name="creator">The <see cref="Did"/> of the actor whose collection the record should be created in. Typically this is the Did of the current user.</param>
        /// <param name="collection">The collection the record should be created in.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the current session is not authenticated.</exception>
        public async Task<AtProtoHttpResult<StrongReference>> CreateRecord(AtProtoRecordValue record, string collection, Did creator, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNullOrEmpty(collection);
            ArgumentNullException.ThrowIfNull(creator);

            if (!IsAuthenticated)
            {
                Logger.CreateRecordFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticatedSessionRequiredException();
            }

            AtProtoHttpResult<StrongReference> result = await AtProtoServer.CreateRecord(record, collection, creator, Service, AccessToken!, HttpClient, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Succeeded && result.Result is not null)
            {
                Logger.CreateRecordSucceeded(_logger, result.Result.Uri, result.Result.Cid, collection, Service);
            }
            else if (result.Succeeded && result.Result is null)
            {
                Logger.CreateRecordSucceededButNullResult(_logger, Service);
            }
            else
            {
                Logger.CreateRecordFailed(_logger, result.StatusCode, collection, result.AtErrorDetail.Error, result.AtErrorDetail.Message, Service);
            }

            return result;
        }

        /// <summary>Deletes a record, identified by the repo, collection and rkey from the specified service.</summary>
        /// <param name="collection">The collection the record should be deleted from.</param>
        /// <param name="rkey">The record key, identifying the record to be deleted.</param>
        /// <param name="swapRecord">Specified if the operation should compare and swap with the previous record by cid.</param>
        /// <param name="swapCommit">Specified if the operation should compare and swap with the previous commit by cid.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the current session is not an authenticated session.</exception>
        public async Task<AtProtoHttpResult<bool>> DeleteRecord(
            string collection,
            string rkey,
            AtCid? swapRecord = null,
            AtCid? swapCommit = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(collection);
            ArgumentNullException.ThrowIfNullOrEmpty(rkey);

            if (!IsAuthenticated)
            {
                Logger.DeleteRecordFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticatedSessionRequiredException();
            }

            return await DeleteRecord(CurrentSession!.Did, collection, rkey, swapRecord, swapCommit, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes a record, identified by the repo, collection and rkey from the specified service.</summary>
        /// <param name="strongReference">The <see cref="StrongReference"/> to the record to be deleted.</param>
        /// <param name="swapRecord">Specified if the operation should compare and swap with the previous record by cid.</param>
        /// <param name="swapCommit">Specified if the operation should compare and swap with the previous commit by cid.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="strongReference"/> is null, </exception>
        public async Task<AtProtoHttpResult<bool>> DeleteRecord(
            StrongReference strongReference,
            AtCid? swapRecord = null,
            AtCid? swapCommit = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);

            if (strongReference.Uri is null)
            {
                throw new ArgumentException("strongReference must have a Uri", nameof(strongReference));
            }

            if (strongReference.Uri.Repo is null)
            {
                throw new ArgumentException("strongReference.Uri must have a repo", nameof(strongReference));
            }

            if (string.IsNullOrEmpty(strongReference.Uri.Collection))
            {
                throw new ArgumentException("strongReference.Uri must have a collection", nameof(strongReference));
            }

            if (string.IsNullOrEmpty(strongReference.Uri.Rkey))
            {
                throw new ArgumentException("strongReference.Uri must have an rkey", nameof(strongReference));
            }

            if (!IsAuthenticated)
            {
                Logger.DeleteRecordFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticatedSessionRequiredException();
            }

            return await DeleteRecord(
                strongReference.Uri.Repo,
                strongReference.Uri.Collection,
                strongReference.Uri.Rkey,
                swapRecord,
                swapCommit,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes a record, identified by the repo, collection and rkey from the specified service.</summary>
        /// <param name="repo">The handle or Did of the repo to delete from. Typically this is the Did of the account that created the record.</param>
        /// <param name="collection">The collection the record should be deleted from.</param>
        /// <param name="rkey">The record key, identifying the record to be deleted.</param>
        /// <param name="swapRecord">Specified if the operation should compare and swap with the previous record by cid.</param>
        /// <param name="swapCommit">Specified if the operation should compare and swap with the previous commit by cid.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the current session is not an authenticated session.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="repo"/> is null, or <paramref name="collection"/> or <paramref name="rkey"/> are null or empty.</exception>
        public async Task<AtProtoHttpResult<bool>> DeleteRecord(
            AtIdentifier repo,
            string collection,
            string rkey,
            AtCid? swapRecord = null,
            AtCid? swapCommit = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(repo);
            ArgumentNullException.ThrowIfNullOrEmpty(collection);
            ArgumentNullException.ThrowIfNullOrEmpty(rkey);

            if (!IsAuthenticated)
            {
                Logger.DeleteRecordFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticatedSessionRequiredException();
            }

            AtProtoHttpResult<EmptyResponse> response =
                await AtProtoServer.DeleteRecord(
                    repo,
                    collection,
                    rkey,
                    swapRecord,
                    swapCommit,
                    Service,
                    AccessToken!,
                    HttpClient,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                Logger.DeleteRecordSucceeded(_logger, repo, collection, rkey, Service);
            }
            else
            {
                Logger.DeleteRecordFailed(_logger, response.StatusCode, response.AtErrorDetail.Error, response.AtErrorDetail.Message, repo, collection, rkey, Service);
            }

            AtProtoHttpResult<bool> booleanResponse = new()
            {
                Result = response.Succeeded,
                AtErrorDetail = response.AtErrorDetail,
                StatusCode = response.StatusCode
            };

            return booleanResponse;
        }

        /// <summary>
        /// Gets information about a repository, including the list of collections.
        /// </summary>
        /// <param name="repo">The <see cref="AtIdentifier"/> of the repo to retrieve information for.</param>
        /// <param name="service">The service to retrieve the record from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="repo"/> is null.</exception>
        public async Task<AtProtoHttpResult<RepoDescription>> DescribeRepo(AtIdentifier repo, Uri? service = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(repo);

            service ??= Service;

            return await AtProtoServer.DescribeRepo(repo, service, AccessToken, HttpClient, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the record specified by the identifying parameters.
        /// </summary>
        /// <typeparam name="T">The type of record to get.</typeparam>
        /// <param name="repo">The <see cref="AtIdentifier"/> of the repo to retrieve the record from.</param>
        /// <param name="collection">The NSID of the collection the record should be deleted from.</param>
        /// <param name="rkey">The record key, identifying the record to be deleted.</param>
        /// <param name="cid">The CID of the version of the record. If not specified, then return the most recent version.</param>
        /// <param name="service">The service to retrieve the record from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="repo"/> or <paramref name="collection"/> is null or empty.</exception>
        public async Task<AtProtoHttpResult<T>> GetRecord<T>(
            AtIdentifier repo,
            string collection,
            string rkey,
            AtCid? cid = null,
            Uri? service = null,
            CancellationToken cancellationToken = default) where T: AtProtoRecord
        {
            ArgumentNullException.ThrowIfNull(repo);
            ArgumentNullException.ThrowIfNullOrEmpty(collection);

            service ??= Service;

            Logger.GetRecordCalled(_logger, repo, collection, rkey, service);

            AtProtoHttpResult<T> result =
                await AtProtoServer.GetRecord<T>(repo, collection, rkey, cid, service, AccessToken, HttpClient, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                Logger.GetRecordFailed(_logger, result.StatusCode, repo, collection, rkey, result.AtErrorDetail.Error, result.AtErrorDetail.Message, service);
            }
            else if (result.Result is null)
            {
                Logger.GetRecordSucceededButReturnedNullResult(_logger, repo, collection, rkey, service);
            }

            return result;
        }

        /// <summary>
        /// Gets a page of records in the specified <paramref name="collection"/> for the current user.
        /// </summary>
        /// <param name="collection">The NSID of the collection the records should be retrieved from.</param>
        /// <param name="limit">The number of records to return in each page.</param>
        /// <param name="cursor">The cursor position to start retrieving records from.</param>
        /// <param name="reverse">A flag indicating if records should be listed in reverse order.</param>
        /// <param name="service">The service to retrieve the record from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="collection"/> is null or empty.</exception>
        public async Task<AtProtoHttpResult<AtProtoRecordList<T>>> ListRecords<T>(
            string collection,
            int? limit = 50,
            string? cursor = null,
            bool reverse = false,
            Uri? service = null,
            CancellationToken cancellationToken = default) where T: AtProtoRecord
        {
            ArgumentNullException.ThrowIfNullOrEmpty(collection);

            return await ListRecords<T>(CurrentSession!.Did, collection, limit, cursor, reverse, service, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a page of records in the specified <paramref name="collection"/>.
        /// </summary>
        /// <param name="repo">The <see cref="AtIdentifier"/> of the repo to retrieve the records from.</param>
        /// <param name="collection">The NSID of the collection the records should be retrieved from.</param>
        /// <param name="limit">The number of records to return in each page.</param>
        /// <param name="cursor">The cursor position to start retrieving records from.</param>
        /// <param name="reverse">A flag indicating if records should be listed in reverse order.</param>
        /// <param name="service">The service to retrieve the record from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the current session is not an authenticated session.</exception>
        public async Task<AtProtoHttpResult<AtProtoRecordList<T>>> ListRecords<T>(
            AtIdentifier repo,
            string collection,
            int? limit,
            string? cursor,
            bool reverse,
            Uri? service = null,
            CancellationToken cancellationToken = default) where T : AtProtoRecord
        {
            ArgumentNullException.ThrowIfNull(repo);
            ArgumentNullException.ThrowIfNullOrEmpty(collection);

            service ??= Service;

            Logger.ListRecordsCalled(_logger, repo, collection, service);

            AtProtoHttpResult<AtProtoRecordList<T>> result = await AtProtoServer.ListRecords<T>(
                repo,
                collection,
                limit,
                cursor,
                reverse,
                service,
                AccessToken,
                HttpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                Logger.ListRecordsFailed(_logger, result.StatusCode, repo, collection, result.AtErrorDetail.Error, result.AtErrorDetail.Message, service);
            }
            else if (result.Result is null)
            {
                Logger.ListRecordsSucceededButReturnedNullResult(_logger, repo, collection, service);
            }

            return result;
        }

        /// <summary>
        /// Uploads a blob, to be referenced from a repository record.
        /// </summary>
        /// <param name="blob">The blob to upload.</param>
        /// <param name="mimeType">The mime type of the blob to upload.</param>
        /// <param name="service">The service to upload the blob to.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>An <see cref="AtProtoHttpResult{T}"/> wrapping a <see cref="Blob"/> containing a reference to the newly created blob.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="blob"/> has a zero length or if <paramref name="mimeType"/> is null or empty.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the current session is not an authenticated session.</exception>
        public async Task<AtProtoHttpResult<Blob?>> UploadBlob(
            byte[] blob,
            string mimeType,
            Uri? service = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(blob);
            ArgumentException.ThrowIfNullOrEmpty(mimeType);

            service ??= Service;

            if (!IsAuthenticated)
            {
                Logger.UploadBlobFailedAsSessionIsAnonymous(_logger, service);

                throw new AuthenticatedSessionRequiredException();
            }

            if (blob.Length == 0)
            {
                Logger.UploadBlobFailedAsSessionIsEmpty(_logger, service);
                throw new ArgumentException("blob length cannot be 0.", nameof(blob));
            }

            return await AtProtoServer.UploadBlob(blob, mimeType, service, AccessToken!, HttpClient, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the access and refresh tokens for the current session.
        /// </summary>
        /// <param name="accessJwt">The new access token to use.</param>
        /// <param name="refreshJwt">The new refresh token to use.</param>
        /// <exception cref="ArgumentNullException">Thrown if either the provided <paramref name="accessJwt"/> or <paramref name="refreshJwt"/> is null or empty.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the agent is not authenticated.</exception>
        public async Task SetTokens(string accessJwt, string refreshJwt)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(accessJwt);
            ArgumentNullException.ThrowIfNullOrEmpty(refreshJwt);

            if (CurrentSession is null)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            if (await ValidateJwtToken(accessJwt, CurrentSession.Did, CurrentSession.Service!).ConfigureAwait(false) &&
                await ValidateJwtToken(refreshJwt, CurrentSession.Did, CurrentSession.Service!).ConfigureAwait(false)) 
            {
                Logger.UpdateTokensCalled(_logger, CurrentSession.Did, CurrentSession.Service!);

                CurrentSession.AccessJwt = accessJwt;
                CurrentSession.RefreshJwt = refreshJwt;
            }
            else
            {
                Logger.UpdateTokensGivenInvalidTokens(_logger, CurrentSession.Did, CurrentSession.Service!);
                throw new SecurityTokenValidationException("The issued access token could not be validated.");
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private void StartTokenRefreshTimer()
        {
            if (_enableTokenRefresh)
            {
                if (CurrentSession is not null && !string.IsNullOrEmpty(AccessToken))
                {
                    TimeSpan accessTokenExpiresIn = GetTimeToJwtTokenExpiry(AccessToken);

                    if (accessTokenExpiresIn.TotalSeconds < 60)
                    {
                        // As we're about to expire, go refresh the token
                        RefreshSession().FireAndForget();
                        return;
                    }

                    TimeSpan refreshIn = _refreshAccessTokenInterval;
                    if (accessTokenExpiresIn < _refreshAccessTokenInterval)
                    {
                        refreshIn = accessTokenExpiresIn - new TimeSpan(0, 1, 0);
                    }

                    if (_sessionRefreshTimer is not null)
                    {
                        _sessionRefreshTimer.Interval = refreshIn.TotalMilliseconds >= int.MaxValue ? int.MaxValue : refreshIn.TotalMilliseconds;
                        _sessionRefreshTimer.Elapsed += RefreshTimerElapsed;
                        _sessionRefreshTimer.Enabled = true;
                        _sessionRefreshTimer.Start();

                        Logger.TokenRefreshTimerStarted(_logger);
                    }
                }
            }
        }

        private void StopTokenRefreshTimer(bool dispose = false)
        {
            if (_sessionRefreshTimer is not null)
            {
                _sessionRefreshTimer.Stop();
                Logger.TokenRefreshTimerStopped(_logger);

                if (dispose)
                {
                    _sessionRefreshTimer.Dispose();
                    _sessionRefreshTimer = null;
                }
            }
        }

        private static TimeSpan GetTimeToJwtTokenExpiry(string jwt)
        {
            if (string.IsNullOrEmpty(jwt))
            {
                throw new ArgumentNullException(nameof(jwt));
            }

            JsonWebToken jsonWebToken = new(jwt);

            return jsonWebToken.ValidTo.ToUniversalTime() - DateTime.UtcNow;
        }

        private static async Task<bool> ValidateJwtToken(string jwt, Did did, Uri service)
        {
            bool isValid = false;

            // Disable issuer and signature validation because the Bluesky PDS implementation does not expose a
            // .well-known/openid-configuration endpoint to retrieve the issuer and signing key from.
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

            JsonWebToken token = new(jwt);
            JsonWebTokenHandler tokenHandler = new();
            TokenValidationResult validationResult = await tokenHandler.ValidateTokenAsync(jwt, validationParameters).ConfigureAwait(false);

            if (validationResult.IsValid)
            {
                // Validate the subject matches the expected DID.
                isValid = string.Equals((string?)validationResult.Claims.FirstOrDefault(c => c.Key == "sub").Value, did.ToString(), StringComparison.OrdinalIgnoreCase);
            }

            return isValid;
        }

        private void RefreshTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            Logger.BackgroundTokenRefreshFired(_logger);
            RefreshSession().FireAndForget();
        }
    }
}
