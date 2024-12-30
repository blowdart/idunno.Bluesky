// Copyright(c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
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
using idunno.AtProto.Models;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo.Models;

namespace idunno.AtProto
{
    /// <summary>
    /// Provides a class for sending requests to and receiving responses from an atproto service, identified by its service URI.
    /// </summary>
    public partial class AtProtoAgent : Agent
    {
        private readonly bool _enableTokenRefresh = true;
        private readonly TimeSpan _refreshAccessTokenInterval = new(1, 0, 0);

        private System.Timers.Timer? _sessionRefreshTimer;
        private readonly object _session_SyncLock = new();
        private volatile bool _disposed;

        private readonly DirectoryAgent _directoryAgent;

        private readonly ILogger<AtProtoAgent> _logger;

        private readonly Uri _initialServiceUri;

        private Session? _session;

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgent"/>
        /// </summary>
        /// <param name="service">The URI of the AtProto service to connect to.</param>
        /// <param name="httpClient">An optional <see cref="HttpClient"/> to use when making requests.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use when creating loggers.</param>
        /// <param name="options"><see cref="AtProtoAgentOptions"/> for the use in the creation of this instance of <see cref="AtProtoAgent"/>.</param>
        public AtProtoAgent(Uri service, HttpClient? httpClient = null, ILoggerFactory? loggerFactory = default, AtProtoAgentOptions? options = null) : base(httpClient)
        {
            ArgumentNullException.ThrowIfNull(service);

            _initialServiceUri = service;
            Service = service;

            if (options is not null)
            {
                _enableTokenRefresh = options.EnableBackgroundTokenRefresh;
            }

            LoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            _logger = LoggerFactory.CreateLogger<AtProtoAgent>();

            _directoryAgent = new DirectoryAgent(httpClient, loggerFactory: loggerFactory);
        }

        /// <summary>
        /// Gets the <see cref="Uri"/> for the AT Proto service the agent is issuing commands against.
        /// </summary>
        /// <remarks>
        /// <para>
        ///   This may change based on the results of a <see cref="Login(Credentials, Uri?, CancellationToken)"/> operation if the
        ///   Personal Data Server (PDS) discovered in the user's DID Document is different from the service URI provided at construction.
        ///</para>
        /// </remarks>
        public Uri Service { get; protected set; }

        /// <summary>
        /// Gets or sets the authenticated session to use when making requests that require authentication.
        /// </summary>
        [SuppressMessage("Naming", "CA1721:Property names should not match get methods", Justification = "It's not that confusing as GetSession() refers to the ATProto api, like the other Get* methods.")]
        public Session? Session
        {
            get
            {
                return _session;
            }

            protected set
            {
                _session = value;
                if (value is not null && value.Service is not null)
                {
                    Service = value.Service;
                }
                else
                {
                    Service = _initialServiceUri;
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
                if (IsAuthenticated)
                {
                    return Session.Did;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the agent has an active session.
        /// </summary>
        [MemberNotNullWhen(true, nameof(Session))]
        [MemberNotNullWhen(true, nameof(AccessToken))]
        [MemberNotNullWhen(true, nameof(Did))]
        public override bool IsAuthenticated
        {
            get
            {
                return Session is not null && Session.HasAccessToken && Session.AccessJwtExpiresOn > DateTime.Now;
            }
        }

        /// <summary>
        /// Gets the current session's access token if the agent is authenticated and the access token has not expired, otherwise returns null.
        /// </summary>
        protected string? AccessToken
        {
            get
            {
                if (IsAuthenticated)
                {
                    return Session.AccessJwt;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Gets the current session's refresh token if any, otherwise returns null.
        /// </summary>
        protected string? RefreshToken
        {
            get
            {
                if (Session is not null)
                {
                    return Session.RefreshJwt;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Gets a configured logger factory from which to create loggers.
        /// </summary>
        protected ILoggerFactory LoggerFactory { get; init; }

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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/> is null.</exception>
        public async Task<Did?> ResolveHandle(string handle, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(handle);

            Logger.ResolveHandleCalled(_logger, handle);

            Did? result = await AtProtoServer.ResolveHandle(
                handle,
                httpClient: HttpClient,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);

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
        /// Resolves a handle (domain name) to a DID.
        /// </summary>
        /// <param name="handle">The handle to resolve.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/> is null.</exception>
        public async Task<Did?> ResolveHandle(Handle handle, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(handle);
            return await ResolveHandle(handle.ToString(), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resolves the <see cref="DidDocument"/> for the specified <paramref name="did"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> to resolve the <see cref="DidDocument"/> for.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is null.</exception>
        public async Task<DidDocument?> ResolveDidDocument(Did did, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(did);

            Logger.ResolveDidDocumentCalled(_logger, did);

            DidDocument? didDocument = null;

            if (!cancellationToken.IsCancellationRequested)
            {
                AtProtoHttpResult<DidDocument> didDocumentResolutionResult = await
                    _directoryAgent.ResolveDidDocument(did, cancellationToken: cancellationToken).ConfigureAwait(false);

                if (didDocumentResolutionResult.Succeeded)
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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is null.</exception>
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
                    pds = didDocument.Services!.FirstOrDefault(s => s.Id == @"#atproto_pds")!.ServiceEndpoint;
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

        /// <summary>
        /// Logins into the <paramref name="service"/> with the specified <paramref name="credentials"/>.
        /// </summary>
        /// <param name="credentials">The credentials to use when authenticating.</param>
        /// <param name="service">The service to authenticate to.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="credentials"/> is null.</exception>
        /// <exception cref="SecurityTokenValidationException">Thrown when the access token issued by the <see cref="Service"/> is not valid.</exception>
        public async Task<AtProtoHttpResult<bool>> Login(Credentials credentials, Uri? service = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(credentials);

            if (service is null)
            {
                Did? userDid = await ResolveHandle(credentials.Identifier, cancellationToken: cancellationToken).ConfigureAwait(false);

                if (userDid is null || cancellationToken.IsCancellationRequested)
                {
                    return new AtProtoHttpResult<bool>(
                        false,
                        HttpStatusCode.NotFound,
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
                        new AtErrorDetail() { Error = "PdsNotResolvable", Message = $"Could not resolve a PDS for {userDid}." });
                }

                service = pds;
            }

            StopTokenRefreshTimer();

            Logger.CreateSessionCalled(_logger, credentials.Identifier, service);
            AtProtoHttpResult<CreateSessionResponse> createSessionResult =
                await AtProtoServer.CreateSession(
                    credentials,
                    service,
                    httpClient: HttpClient,
                    loggerFactory: LoggerFactory,
                    cancellationToken :cancellationToken).ConfigureAwait(false);
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

                    var sessionCreatedEventArgs = new SessionCreatedEventArgs(
                        createSessionResult.Result.Did,
                        service,
                        createSessionResult.Result.Handle,
                        createSessionResult.Result.AccessJwt,
                        createSessionResult.Result.RefreshJwt);
                    OnSessionCreated(sessionCreatedEventArgs);
                }

                return new AtProtoHttpResult<bool>() {
                    Result = true,
                    StatusCode = createSessionResult.StatusCode,
                    AtErrorDetail = createSessionResult.AtErrorDetail,
                    RateLimit = createSessionResult.RateLimit};
            }
            else
            {
                Logger.CreateSessionFailed(_logger, createSessionResult.StatusCode);

                lock (_session_SyncLock)
                {
                    _sessionRefreshTimer?.Stop();
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

        /// <summary>
        /// Logins into the <paramref name="service"/> with the specified <paramref name="identifier"/> and <paramref name="password"/>.
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

            return await Login(new Credentials(identifier, password, emailAuthFactor), service, cancellationToken).ConfigureAwait(false);
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
                if (Session.RefreshJwt is null)
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

            Uri currentSessionService = Session!.Service!;

            AtProtoHttpResult<EmptyResponse> deleteSessionResult =
                await AtProtoServer.DeleteSession(Session!.RefreshJwt!, currentSessionService, HttpClient, LoggerFactory, cancellationToken).ConfigureAwait(false);

            lock (_session_SyncLock)
            {
                StopTokenRefreshTimer();

                if (deleteSessionResult.Succeeded)
                {
                    if (Session is not null)
                    {
                        var loggedOutEventArgs = new SessionEndedEventArgs(Session.Did, currentSessionService);

                        Session = null;

                        OnSessionEnded(loggedOutEventArgs);
                    }
                    else
                    {
                        Logger.LogoutFailed(_logger, Session!.Did, Session.Service, deleteSessionResult.StatusCode);
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
        /// Refreshes the current authenticated session and updates the access and refresh tokens.
        /// </summary>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="InvalidSessionException">Thrown when the current session does not have enough information to call refresh itself.</exception>
        public async Task<bool> RefreshSession(CancellationToken cancellationToken = default)
        {
            SessionConfigurationErrorType sessionErrorFlags = SessionConfigurationErrorType.None;

            if (Session is null)
            {
                sessionErrorFlags |= SessionConfigurationErrorType.NullSession;
            }

            if (Session is not null && Session.RefreshJwt is null)
            {
                sessionErrorFlags |= SessionConfigurationErrorType.MissingRefreshToken;
            }

            if (Session is not null && Session.Service is null)
            {
                sessionErrorFlags |= SessionConfigurationErrorType.MissingService;
            }

            if (sessionErrorFlags != SessionConfigurationErrorType.None)
            {
                var sessionRefreshFailedEventArgs = new SessionRefreshFailedEventArgs(sessionErrorFlags, Session?.Did, Session?.Service);

                OnSessionRefreshFailed(sessionRefreshFailedEventArgs);

                throw new InvalidSessionException($"The current session does not have enough information to refresh: {sessionErrorFlags}")
                {
                    SessionErrors = sessionErrorFlags
                };
            }

            return await RefreshSession(Session!.RefreshJwt!, Session.Service!, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Refreshes the session specified by the <paramref name="refreshJwt"/> on the <paramref name="service"/>
        /// </summary>
        /// <param name="refreshJwt">The refresh token to use to refresh the session.</param>
        /// <param name="service">The service to refresh the session on.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="refreshJwt"/> is null.</exception>
        /// <exception cref="SecurityTokenValidationException">Thrown when the token issued by <paramref name="service"/> cannot be validated.</exception>
        public async Task<bool> RefreshSession(string refreshJwt, Uri? service = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(refreshJwt);

            service ??= Service;

            if (!IsAuthenticated)
            {
                Logger.RefreshSessionFailedNoSession(_logger, service);
                throw new AuthenticatedSessionRequiredException();
            }

            Logger.RefreshSessionCalled(_logger, Session!.Did, service);

            if (_sessionRefreshTimer is not null)
            {
                lock (_session_SyncLock)
                {
                    StopTokenRefreshTimer();
                }
            }

            AtProtoHttpResult<RefreshSessionResponse> refreshSessionResult;
            try
            {
                refreshSessionResult = await AtProtoServer.RefreshSession(refreshJwt, service, HttpClient, LoggerFactory, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.TokenRefreshApiThrew(_logger, e);
                throw;
            }

            if (!refreshSessionResult.Succeeded || refreshSessionResult.Result.AccessJwt is null || refreshSessionResult.Result.RefreshJwt is null)
            {
                Logger.RefreshSessionApiCallFailed(_logger, Session!.Did, service, refreshSessionResult.StatusCode);

                var sessionRefreshFailedEventArgs = new SessionRefreshFailedEventArgs(
                    SessionConfigurationErrorType.None,
                    null,
                    service,
                    refreshSessionResult.StatusCode,
                    refreshSessionResult.AtErrorDetail);

                OnSessionRefreshFailed(sessionRefreshFailedEventArgs);

                return false;
            }

            if (!await ValidateJwtToken(refreshSessionResult.Result.AccessJwt, refreshSessionResult.Result.Did, service).ConfigureAwait(false))
            {
                Logger.RefreshSessionTokenValidationFailed(_logger, Session!.Did, service);

                throw new SecurityTokenValidationException("The issued access token could not be validated.");
            }

            lock (_session_SyncLock)
            {
                if (Session is not null)
                {
                    Logger.RefreshSessionSucceeded(_logger, Session.Did, service);

                    Session.UpdateAccessTokens(refreshSessionResult.Result.AccessJwt, refreshSessionResult.Result.RefreshJwt);

                    _sessionRefreshTimer ??= new();
                    StartTokenRefreshTimer();

                    var sessionRefreshedEventArgs = new SessionRefreshedEventArgs(
                        Session.Did,
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
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="accessJwt"/> is null or empty.</exception>
        public async Task<AtProtoHttpResult<GetSessionResponse>> GetSession(string accessJwt, Uri? service = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(accessJwt);

            service ??= Session!.Service!;

            return await AtProtoServer.GetSession(accessJwt, service, HttpClient, LoggerFactory, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resumes a session on the <paramref name="service"/> from the <paramref name="refreshJwt"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> the tokens are associated with.</param>
        /// <param name="refreshJwt">The refresh token to restore the session for.</param>
        /// <param name="service">The <see cref="Uri"/> of the PDS that issued the tokens.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/>, <paramref name="refreshJwt"/> or <paramref name="service"/> is null.</exception>
        public async Task<bool> ResumeSession(Did did, string refreshJwt, Uri service, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(did);
            ArgumentException.ThrowIfNullOrWhiteSpace(refreshJwt);
            ArgumentNullException.ThrowIfNull(service);

            return await ResumeSession(did, null, refreshJwt, service, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resumes a session on the <paramref name="service"/> from either the <paramref name="accessJwt"/> or the <paramref name="refreshJwt"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> the tokens are associated with.</param>
        /// <param name="accessJwt">The access token to restore the session for.</param>
        /// <param name="refreshJwt">The refresh token to restore the session for.</param>
        /// <param name="service">The <see cref="Uri"/> of the PDS that issued the tokens.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/>, <paramref name="refreshJwt"/> or <paramref name="service"/> is null.</exception>
        /// <exception cref="SessionRestorationFailedException">Thrown when the session recreated on the PDS does not match the expected <paramref name="did"/>.</exception>
        public async Task<bool> ResumeSession(Did did, string? accessJwt, string refreshJwt, Uri service, CancellationToken cancellationToken = default)
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
                AtProtoHttpResult<GetSessionResponse> getSessionResult = await GetSession(accessJwt, service, cancellationToken).ConfigureAwait(false);
                if (getSessionResult.Succeeded)
                {
                    restoredSession = new Session(service, getSessionResult.Result, accessJwt, refreshJwt);
                }
            }

            // If that failed, try refreshing the session to get a new token, then try again.
            if (restoredSession is null && !cancellationToken.IsCancellationRequested)
            {
                AtProtoHttpResult<RefreshSessionResponse> refreshSessionResult =
                        await AtProtoServer.RefreshSession(
                            refreshJwt,
                            service,
                            httpClient: HttpClient,
                            loggerFactory: LoggerFactory,
                            cancellationToken: cancellationToken).ConfigureAwait(false);

                if (refreshSessionResult.Succeeded)
                {
                    if (!await ValidateJwtToken(refreshSessionResult.Result.AccessJwt, refreshSessionResult.Result.Did, service).ConfigureAwait(false))
                    {
                        throw new SecurityTokenValidationException("The issued access token could not be validated.");
                    }

                    AtProtoHttpResult<GetSessionResponse> getSessionResult = await GetSession(refreshSessionResult.Result.AccessJwt, service, cancellationToken).ConfigureAwait(false);
                    if (getSessionResult.Succeeded)
                    {
                        wereTokensRefreshed = true;
                        restoredSession = new Session(service, getSessionResult.Result, refreshSessionResult.Result.AccessJwt, refreshSessionResult.Result.RefreshJwt);
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

                    Session = restoredSession;
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

            return await AtProtoServer.DescribeServer(server, HttpClient, LoggerFactory, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Apply a batch transaction of repository creates, updates, and deletes. Requires authentication.
        /// </summary>
        /// <param name="writeRequests"><para>A collection of write requests to apply.</para></param>
        /// <param name="repo"><para>The <see cref="AtProto.Did"/> of the repository to write to.</para></param>
        /// <param name="validate">
        ///   <para>Gets a flag indicating what validation will be performed, if any.</para>
        ///   <para>A value of <keyword>true</keyword> requires lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>false</keyword> will skip Lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>null</keyword> to validate record data only for known lexicons.</para>
        ///   <para>Defaults to <keyword>true</keyword>.</para>
        /// </param>
        /// <param name="cid">
        ///   <para>
        ///     Optional commit ID. If provided, the entire operation will fail if the current repo commit CID does not match this value.
        ///     Used to prevent conflicting repo mutations.
        ///   </para>
        ///</param>
        /// <param name="cancellationToken"><para>A cancellation token that can be used by other objects or threads to receive notice of cancellation.</para></param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="writeRequests"/> or <paramref name="repo" /> is null.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="writeRequests"/> is an empty collection.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the current session is not authenticated.</exception>
        public async Task<AtProtoHttpResult<ApplyWritesResponse>> ApplyWrites(
            ICollection<ApplyWritesRequestValueBase> writeRequests,
            Did repo,
            bool? validate,
            Cid? cid = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(writeRequests);
            ArgumentNullException.ThrowIfNull(repo);

            if (!IsAuthenticated)
            {
                Logger.ApplyWritesFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticatedSessionRequiredException();
            }

            AtProtoHttpResult<ApplyWritesResponse> applyWritesResult = await AtProtoServer.ApplyWrites(
                writeRequests,
                repo,
                validate,
                cid,
                Service,
                AccessToken,
                httpClient: HttpClient,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (applyWritesResult.Succeeded)
            {
                Logger.ApplyWritesSucceeded(_logger, applyWritesResult.Result.Commit.Cid, applyWritesResult.Result.Commit.Rev, Service);
            }
            else
            {
                Logger.ApplyWritesApiCallFailed(_logger, applyWritesResult.StatusCode, applyWritesResult.AtErrorDetail?.Error, applyWritesResult.AtErrorDetail?.Message, Service);
            }

            return applyWritesResult;
        }

        /// <summary>
        /// Creates a record in the specified collection belonging to the current user.
        /// </summary>
        /// <typeparam name="TRecord">The type of record to create.</typeparam>
        /// <param name="record"><para>The record to be created.</para></param>
        /// <param name="collection"><para>The collection the record should be created in.</para></param>
        /// <param name="creator"><para>The <see cref="Did"/> of the actor whose collection the record should be created in. Typically this is the Did of the current user.</para></param>
        /// <param name="rkey"><para>An optional <see cref="RecordKey"/> to create the record with.</para></param>
        /// <param name="validate">
        ///   <para>Gets a flag indicating what validation will be performed, if any.</para>
        ///   <para>A value of <keyword>true</keyword> requires lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>false</keyword> will skip Lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>null</keyword> to validate record data only for known lexicons.</para>
        ///   <para>Defaults to <keyword>true</keyword>.</para>
        /// </param>
        /// <param name="swapCommit"><para>Compare and swap with the previous commit by CID.</para></param>
        /// <param name="cancellationToken"><para>A cancellation token that can be used by other objects or threads to receive notice of cancellation.</para></param>
        /// <returns><para>The task object representing the asynchronous operation.</para></returns>
        /// <exception cref="ArgumentNullException"><para>Thrown when <paramref name="record"/>, <paramref name="collection"/> or <paramref name="creator"/> is null.</para></exception>
        /// <exception cref="AuthenticatedSessionRequiredException"><para>Thrown when the current session is not authenticated.</para></exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> CreateRecord<TRecord>(
            TRecord record,
            Nsid collection,
            Did creator,
            RecordKey? rkey = null,
            bool? validate = true,
            Cid? swapCommit = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(creator);

            if (!IsAuthenticated)
            {
                Logger.CreateRecordFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticatedSessionRequiredException();
            }

            AtProtoHttpResult<CreateRecordResponse> result = await AtProtoServer.CreateRecord(
                record: record,
                collection: collection,
                creator: creator,
                rKey: rkey,
                validate: validate,
                swapCommit : swapCommit,
                Service,
                AccessToken,
                httpClient: HttpClient,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Succeeded)
            {
                Logger.CreateRecordSucceeded(_logger, result.Result.Uri, result.Result.Cid, collection, Service);
            }
            else if (result.StatusCode == HttpStatusCode.OK && result.Result is null)
            {
                Logger.CreateRecordSucceededButNullResult(_logger, Service);
            }
            else
            {
                Logger.CreateRecordFailed(_logger, result.StatusCode, collection, result.AtErrorDetail?.Error, result.AtErrorDetail?.Message, Service);
            }

            return result;
        }

        /// <summary>Deletes a record, identified by the repo, collection and rKey from the specified service.</summary>
        /// <param name="collection">The collection the record should be deleted from.</param>
        /// <param name="rKey">The record key, identifying the record to be deleted.</param>
        /// <param name="swapRecord">Specified if the operation should compare and swap with the previous record by cid.</param>
        /// <param name="swapCommit">Specified if the operation should compare and swap with the previous commit by cid.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="collection"/> or <paramref name="rKey"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the current session is not an authenticated session.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteRecord(
            Nsid collection,
            RecordKey rKey,
            Cid? swapRecord = null,
            Cid? swapCommit = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(rKey);

            if (!IsAuthenticated)
            {
                Logger.DeleteRecordFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticatedSessionRequiredException();
            }

            return await DeleteRecord(Did, collection, rKey, swapRecord, swapCommit, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes a record, identified by the repo, collection and rKey from the specified service.</summary>
        /// <param name="strongReference">The <see cref="StrongReference"/> to the record to be deleted.</param>
        /// <param name="swapRecord">Specified if the operation should compare and swap with the previous record by cid.</param>
        /// <param name="swapCommit">Specified if the operation should compare and swap with the previous commit by cid.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is null, </exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="strongReference"/> is not valid.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteRecord(
            StrongReference strongReference,
            Cid? swapRecord = null,
            Cid? swapCommit = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(strongReference);
            ArgumentNullException.ThrowIfNull(strongReference.Uri);
            ArgumentNullException.ThrowIfNull(strongReference.Uri.Repo);
            ArgumentNullException.ThrowIfNull(strongReference.Uri.Collection);
            ArgumentNullException.ThrowIfNull(strongReference.Uri.RecordKey);

            if (!IsAuthenticated)
            {
                Logger.DeleteRecordFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticatedSessionRequiredException();
            }

            return await DeleteRecord(
                strongReference.Uri.Repo,
                strongReference.Uri.Collection,
                strongReference.Uri.RecordKey,
                swapRecord,
                swapCommit,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes a record, identified by the repo, collection and rKey from the specified service.</summary>
        /// <param name="repo">The handle or Did of the repo to delete from. Typically this is the Did of the account that created the record.</param>
        /// <param name="collection">The collection the record should be deleted from.</param>
        /// <param name="rKey">The record key, identifying the record to be deleted.</param>
        /// <param name="swapRecord">Specified if the operation should compare and swap with the previous record by cid.</param>
        /// <param name="swapCommit">Specified if the operation should compare and swap with the previous commit by cid.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticatedSessionRequiredException">Throw when the current session is not an authenticated session.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="repo"/> is null, <paramref name="collection"/> or <paramref name="rKey"/> are null or empty.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteRecord(
            AtIdentifier repo,
            Nsid collection,
            RecordKey rKey,
            Cid? swapRecord = null,
            Cid? swapCommit = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(repo);
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(rKey);

            if (!IsAuthenticated)
            {
                Logger.DeleteRecordFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticatedSessionRequiredException();
            }

            AtProtoHttpResult<Commit> response =
                await AtProtoServer.DeleteRecord(
                    repo,
                    collection,
                    rKey,
                    swapRecord,
                    swapCommit,
                    Service,
                    AccessToken,
                    httpClient: HttpClient,
                    loggerFactory: LoggerFactory,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                Logger.DeleteRecordSucceeded(_logger, repo, collection, rKey, Service, response.Result);
            }
            else
            {
                Logger.DeleteRecordFailed(_logger, response.StatusCode, response.AtErrorDetail?.Error, response.AtErrorDetail?.Message, repo, collection, rKey, Service);
            }

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<Commit>
                {
                    Result = response.Result,
                    AtErrorDetail = response.AtErrorDetail,
                    StatusCode = response.StatusCode,
                    RateLimit = response.RateLimit
                };
            }
            else
            {
                return new AtProtoHttpResult<Commit>
                {
                    Result = null,
                    AtErrorDetail = response.AtErrorDetail,
                    StatusCode = response.StatusCode,
                    RateLimit = response.RateLimit
                };
            }
        }

        /// <summary>
        /// Updates or creates a record in the specified collection belonging to the current user.
        /// </summary>
        /// <param name="record"><para>The record to be updated or created.</para></param>
        /// <param name="collection"><para>The collection the record should be updated or created in.</para></param>
        /// <param name="creator"><para>The <see cref="Did"/> of the actor whose collection the record should be created in. Typically this is the Did of the current user.</para></param>
        /// <param name="rKey"><para>The <see cref="RecordKey"/> of the record to update, otherwise the record key that will be used for the new record.</para></param>
        /// <param name="validate">
        ///   <para>Gets a flag indicating what validation will be performed, if any.</para>
        ///   <para>A value of <keyword>true</keyword> requires lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>false</keyword> will skip Lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>null</keyword> to validate record data only for known lexicons.</para>
        ///   <para>Defaults to <keyword>true</keyword>.</para>
        /// </param>
        /// <param name="swapCommit"><para>Compare and swap with the previous commit by CID.</para></param>
        /// <param name="swapRecord"><para>The <see cref="Cid"/> of the record, if any, to compare and swap with.</para></param>
        /// <param name="cancellationToken"><para>A cancellation token that can be used by other objects or threads to receive notice of cancellation.</para></param>
        /// <returns><para>The task object representing the asynchronous operation.</para></returns>
        /// <exception cref="ArgumentNullException">
        ///     <para>Thrown when <paramref name="record"/>, <paramref name="collection"/>, <paramref name="creator"/> or <paramref name="rKey"/> is null.</para>
        /// </exception>
        /// <exception cref="AuthenticatedSessionRequiredException"><para>Thrown when the current session is not authenticated.</para></exception>
        public async Task<AtProtoHttpResult<PutRecordResponse>> PutRecord(
            object record,
            Nsid collection,
            Did creator,
            RecordKey rKey,
            bool? validate = true,
            Cid? swapCommit = null,
            Cid? swapRecord = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(creator);
            ArgumentNullException.ThrowIfNull(rKey);

            if (!IsAuthenticated)
            {
                Logger.PutRecordFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticatedSessionRequiredException();
            }

            AtProtoHttpResult<PutRecordResponse> result = await AtProtoServer.PutRecord(
                record: record,
                collection: collection,
                creator: creator,
                rKey: rKey,
                validate: validate,
                swapCommit: swapCommit,
                swapRecord: swapRecord,
                Service,
                AccessToken,
                httpClient: HttpClient,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Succeeded)
            {
                Logger.PutRecordSucceeded(_logger, result.Result.Uri, result.Result.Cid, collection, Service);
            }
            else if (result.StatusCode == HttpStatusCode.OK && result.Result is null)
            {
                Logger.PutRecordSucceededButNullResult(_logger, Service);
            }
            else
            {
                Logger.PutRecordFailed(_logger, result.StatusCode, collection, rKey, result.AtErrorDetail?.Error, result.AtErrorDetail?.Message, Service);
            }

            return result;
        }


        /// <summary>
        /// Gets information about a repository, including the list of collections.
        /// </summary>
        /// <param name="repo">The <see cref="AtIdentifier"/> of the repo to retrieve information for.</param>
        /// <param name="service">The service to retrieve the record from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="repo"/> is null.</exception>
        public async Task<AtProtoHttpResult<RepoDescription>> DescribeRepo(AtIdentifier repo, Uri? service = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(repo);

            service ??= Service;

            return await AtProtoServer.DescribeRepo(
                repo,
                service,
                AccessToken,
                httpClient: HttpClient,
                loggerFactory : LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the record specified by the identifying parameters.
        /// </summary>
        /// <typeparam name="T">The type of record to get.</typeparam>
        /// <param name="uri">The <see cref="AtUri"/> of the record to retrieve.</param>
        /// <param name="cid">The CID of the version of the record. If not specified, then return the most recent version.</param>
        /// <param name="service">The service to retrieve the record from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="uri"/> is in an incorrect format.</exception>
        public async Task<AtProtoHttpResult<T>> GetRecord<T>(
            AtUri uri,
            Cid? cid = null,
            Uri? service = null,
            CancellationToken cancellationToken = default) where T : class
        {
            ArgumentNullException.ThrowIfNull(uri);

            if (uri.Repo is null)
            {
                throw new ArgumentException("{uri} does not have a repo.", nameof(uri));
            }

            if (uri.Collection is null)
            {
                throw new ArgumentException("{uri} does not have a collection.", nameof(uri));
            }

            if (uri.RecordKey is null)
            {
                throw new ArgumentException("{uri} does not have an rKey.", nameof(uri));
            }

            return await GetRecord<T>(uri.Repo, uri.Collection, uri.RecordKey, cid, service, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the record specified by the identifying parameters.
        /// </summary>
        /// <typeparam name="T">The type of record to get.</typeparam>
        /// <param name="repo">The <see cref="AtIdentifier"/> of the repo to retrieve the record from.</param>
        /// <param name="collection">The NSID of the collection the record should be retrieved from.</param>
        /// <param name="rKey">The record key, identifying the record to be retrieved.</param>
        /// <param name="cid">The CID of the version of the record. If not specified, then return the most recent version.</param>
        /// <param name="service">The service to retrieve the record from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="repo"/>, <paramref name="collection"/> is null or empty.</exception>
        public async Task<AtProtoHttpResult<T>> GetRecord<T>(
            AtIdentifier repo,
            Nsid collection,
            RecordKey rKey,
            Cid? cid = null,
            Uri? service = null,
            CancellationToken cancellationToken = default) where T:class
        {
            ArgumentNullException.ThrowIfNull(repo);
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(rKey);

            service ??= Service;

            Logger.GetRecordCalled(_logger, repo, collection, rKey, service);

            AtProtoHttpResult<T> result =
                await AtProtoServer.GetRecord<T>(
                    repo,
                    collection,
                    rKey,
                    cid,
                    service,
                    AccessToken,
                    httpClient: HttpClient,
                    loggerFactory: LoggerFactory,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                Logger.GetRecordFailed(_logger, result.StatusCode, repo, collection, rKey, result.AtErrorDetail?.Error, result.AtErrorDetail?.Message, service);
            }
            else if (result.Result is null && result.StatusCode == HttpStatusCode.OK)
            {
                Logger.GetRecordSucceededButReturnedNullResult(_logger, repo, collection, rKey, service);
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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="collection"/> is null or empty.</exception>
        public async Task<AtProtoHttpResult<PagedReadOnlyCollection<T>>> ListRecords<T>(
            Nsid collection,
            int? limit = 50,
            string? cursor = null,
            bool reverse = false,
            Uri? service = null,
            CancellationToken cancellationToken = default) where T : AtProtoRecord
        {
            ArgumentNullException.ThrowIfNull(collection);

            return await ListRecords<T>(
                Session!.Did,
                collection,
                limit,
                cursor,
                reverse,
                service,
                cancellationToken: cancellationToken).ConfigureAwait(false);
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
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="repo"/> or <paramref name="collection"/> is null.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the current session is not an authenticated session.</exception>
        public async Task<AtProtoHttpResult<PagedReadOnlyCollection<T>>> ListRecords<T>(
            AtIdentifier repo,
            Nsid collection,
            int? limit = 50,
            string? cursor = null,
            bool reverse = false,
            Uri? service = null,
            CancellationToken cancellationToken = default) where T : AtProtoRecord
        {
            ArgumentNullException.ThrowIfNull(repo);
            ArgumentNullException.ThrowIfNull(collection);

            service ??= Service;

            Logger.ListRecordsCalled(_logger, repo, collection, service);

            AtProtoHttpResult<PagedReadOnlyCollection<T>> result = await AtProtoServer.ListRecords<T>(
                repo,
                collection,
                limit,
                cursor,
                reverse,
                service,
                AccessToken,
                httpClient: HttpClient,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                Logger.ListRecordsFailed(_logger, result.StatusCode, repo, collection, result.AtErrorDetail?.Error, result.AtErrorDetail?.Message, service);
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
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="blob"/> has a zero length or if <paramref name="mimeType"/> is null or empty.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the current session is not an authenticated session.</exception>
        public async Task<AtProtoHttpResult<Blob>> UploadBlob(
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
                Logger.UploadBlobFailedAsBlobLengthIsZero(_logger, service);
                throw new ArgumentException("blob length cannot be 0.", nameof(blob));
            }

            try
            {
                return await AtProtoServer.UploadBlob(
                    blob,
                    mimeType,
                    service,
                    AccessToken,
                    httpClient: HttpClient,
                    loggerFactory: LoggerFactory,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                Logger.UploadBlobThrewHttpRequestException(_logger, service, ex);
                throw;
            }
        }

        /// <summary>
        /// Find labels relevant to the provided <see cref="AtUri"/> patterns
        /// </summary>
        /// <param name="uriPatterns">List of AT URI patterns to match (boolean 'OR'). Each may be a prefix (ending with ''; will match inclusive of the string leading to ''), or a full URI.</param>
        /// <param name="sources">Optional list of label sources to filter on.</param>
        /// <param name="limit">The number of results to return.</param>
        /// <param name="cursor">An optional cursor for pagination.</param>
        /// <param name="service">The service to find the label information on.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uriPatterns"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="uriPatterns"/> does not contain any patterns, or if <paramref name="limit"/> &lt;1 or &gt;250.</exception>
        public async Task<AtProtoHttpResult<PagedReadOnlyCollection<Label>>> QueryLabels(
            IEnumerable<string> uriPatterns,
            IEnumerable<Did>? sources,
            int? limit,
            string? cursor,
            Uri? service,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uriPatterns);

            if (!uriPatterns.Any())
            {
                throw new ArgumentOutOfRangeException(nameof(uriPatterns), $"{nameof(uriPatterns)} must contain 1 or more patterns.");
            }
            if (limit is not null &&
               (limit < 1 || limit > 250))
            {
                throw new ArgumentOutOfRangeException(nameof(limit), "{limit} must be between 1 and 250.");
            }

            service ??= Service;

            return await AtProtoServer.QueryLabels(
                uriPatterns,
                sources,
                limit,
                cursor,
                service,
                AccessToken,
                httpClient: HttpClient,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a signed token on behalf of the requesting DID for the requested <paramref name="audience"/>.
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
        public async Task<AtProtoHttpResult<string>> GetServiceAuth(
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

                    throw new AuthenticatedSessionRequiredException();
                }

                AtProtoHttpResult<string> result = await AtProtoServer.GetServiceAuth(
                    audience,
                    expiry,
                    lxm,
                    service,
                    AccessToken,
                    httpClient: HttpClient,
                    loggerFactory: LoggerFactory,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (result.Succeeded)
                {
                    TimeSpan expiresIn = GetTimeToJwtTokenExpiry(result.Result);
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
                        result.StatusCode,
                        result.AtErrorDetail?.Error,
                        result.AtErrorDetail?.Message);
                }

                return result;
            }
        }

        /// <summary>
        /// Sets the access and refresh tokens for the current session.
        /// </summary>
        /// <param name="accessJwt">The new access token to use.</param>
        /// <param name="refreshJwt">The new refresh token to use.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <exception cref="ArgumentNullException">Thrown when either the provided <paramref name="accessJwt"/> or <paramref name="refreshJwt"/> is null or empty.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task SetTokens(string accessJwt, string refreshJwt, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(accessJwt);
            ArgumentNullException.ThrowIfNullOrEmpty(refreshJwt);

            if (Session is null)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            if (await ValidateJwtToken(accessJwt, Session.Did, Session.Service!).ConfigureAwait(false) &&
                await ValidateJwtToken(refreshJwt, Session.Did, Session.Service!).ConfigureAwait(false))
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Logger.UpdateTokensCalled(_logger, Session.Did, Session.Service!);
                    Session.UpdateAccessTokens(accessJwt, refreshJwt);
                }
            }
            else
            {
                Logger.UpdateTokensGivenInvalidTokens(_logger, Session.Did, Session.Service!);
                throw new SecurityTokenValidationException("The issued access token could not be validated.");
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private void StartTokenRefreshTimer()
        {
            if (_enableTokenRefresh)
            {
                if (Session is not null && !string.IsNullOrEmpty(AccessToken))
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
            else
            {
                Logger.TokenRefreshTimerStartCalledButRefreshDisabled(_logger);
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

            DateTime validUntil = jsonWebToken.ValidTo.ToUniversalTime();
            DateTime now = DateTime.Now.ToUniversalTime();

            TimeSpan validityPeriod = validUntil - now;

            return validityPeriod;
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
