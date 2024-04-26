// Copyright(c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IdentityModel.Tokens.Jwt;
using Timer = System.Timers.Timer;

using Microsoft.IdentityModel.Tokens;

using idunno.AtProto.Repo;
using idunno.AtProto.Server;
using System.Timers;

namespace idunno.AtProto
{
    /// <summary>
    /// Provides a class for sending requests to and receiving responses from an atproto service, identified by its service URI.
    /// </summary>
    public class AtProtoAgent : IDisposable
    {
        private readonly bool _enableTokenRefresh;
        private readonly TimeSpan _refreshAccessTokenInterval = new(1, 0, 0);
        private readonly JwtSecurityTokenHandler _jwtSecurityTokenHandler = new();
        private readonly TokenValidationParameters _defaultTokenValidationParameters = new()
        {
            ValidateActor = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            ValidateIssuer = false,
            ValidateSignatureLast = false,
            ValidateTokenReplay = false,
            ValidateIssuerSigningKey = false,
            ValidateWithLKG = false,
            LogValidationExceptions = false,
            SignatureValidator = (token, parameters) =>
            {
                var jwt = new JwtSecurityToken(token);
                return jwt;
            },
        };

        private Timer? _timer;
        private readonly object _session_SyncLock = new();
        private volatile bool _disposed;
        private Uri? _sessionIssuedBy;

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgent"/>.
        /// </summary>
        public AtProtoAgent()
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgent"/>.
        /// </summary>
        /// <param name="enableTokenRefresh">A flag indicating if the agent should handle token refreshing automatically.</param>
        public AtProtoAgent(bool enableTokenRefresh = true)
        {
            _enableTokenRefresh = enableTokenRefresh;
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgent"/> which will default to the specified <paramref name="service"/>.
        /// </summary>
        /// <param name="service">The service to connect to if no service is specified.</param>
        public AtProtoAgent(Uri service) : this(service, true)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgent"/> which will default to the specified <paramref name="service"/>.
        /// </summary>
        /// <param name="service">The service to connect to if no service is specified.</param>
        /// <param name="enableTokenRefresh">A flag indicating if the agent should handle token refreshing automatically.</param>
        public AtProtoAgent(Uri service, bool enableTokenRefresh = true) : this(enableTokenRefresh)
        {
            DefaultService = service;
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgent"/> which will default to the specified <paramref name="service"/>
        /// and will use the specified <paramref name="httpClientHandler"/> when HttpClients are created to make requests.
        /// </summary>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making requests.</param>
        public AtProtoAgent(HttpClientHandler? httpClientHandler) : this(httpClientHandler, true)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgent"/> which will default to the specified <paramref name="service"/>
        /// and will use the specified <paramref name="httpClientHandler"/> when HttpClients are created to make requests.
        /// </summary>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making requests.</param>
        public AtProtoAgent(HttpClientHandler? httpClientHandler, bool enableTokenRefresh = true) : this(enableTokenRefresh)
        {
            HttpClientHandler = httpClientHandler;
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgent"/> which will default to the specified <paramref name="service"/>
        /// and will use the specified <paramref name="httpClientHandler"/> when HttpClients are created to make requests.
        /// </summary>
        /// <param name="service">The service to connect to if no service is specified.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making requests.</param>
        public AtProtoAgent(Uri service, HttpClientHandler? httpClientHandler) : this(service, httpClientHandler, true)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgent"/> which will default to the specified <paramref name="service"/>
        /// and will use the specified <paramref name="httpClientHandler"/> when HttpClients are created to make requests.
        /// </summary>
        /// <param name="service">The service to connect to if no service is specified.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making requests.</param>
        public AtProtoAgent(Uri service, HttpClientHandler? httpClientHandler, bool enableTokenRefresh = true) : this(enableTokenRefresh)
        {
            DefaultService = service;
            HttpClientHandler = httpClientHandler;
        }

        /// <summary>
        /// Gets the service used to issue commands against.
        /// </summary>
        /// <value>
        /// The service used to issue commands against.
        /// </value>
        public Uri DefaultService { get; set; } = DefaultServices.DefaultService;

        /// <summary>
        /// Gets the service used to issue unauthenticated commands against.
        /// </summary>
        /// <value>
        /// The service used to issue unauthenticated commands against.
        /// </value>
        public Uri DefaultUnauthenticatedService { get; set; } = DefaultServices.ReadOnlyService;

        /// <summary>
        /// Gets or sets an HttpClientHandler to use when creating the HttpClient to make requests and receive responses.
        /// </summary>
        /// <value>An HttpClientHandler to use when creating the HttpClient to make requests and receive responses.</value>
        protected HttpClientHandler? HttpClientHandler { get; set; }

        /// <summary>
        /// Gets or sets an authenticated session to use when making requests that require authentication.
        /// </summary>
        /// <value>
        /// An authenticated session to use when making requests that require authentication.
        /// </value>
        public Session? Session { get; set; }

        /// <summary>
        /// Gets a value indicating whether this instance of <see cref="AtProtoAgent"/> contains an authenticated <see cref="Session"/>.
        /// </summary>
        /// <value>
        /// A value indicating whether this instance of <see cref="AtProtoAgent"/> contains an authenticated <see cref="Session"/>.
        /// </value>
        public bool Authenticated
        {
            get
            {
                return Session is not null && !string.IsNullOrEmpty(Session.AccessJwt);
            }
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="AtProtoAgent"/> and optionally disposes of the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to releases only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                if (_timer is not null)
                {
                    _timer.Stop();
                    _timer.Enabled = false;
                    _timer.Dispose();
                    _timer = null;
                }

                if (HttpClientHandler is not null)
                {
                    HttpClientHandler.Dispose();
                    HttpClientHandler = null;
                }
            }

            _disposed = true;
        }

        /// <summary>
        /// Disposes of any managed and unmanaged resources used by the <see cref="AtProtoAgent"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Logins into the <paramref name="service"/> with the specified <paramref name="credentials"/>.
        /// </summary>
        /// <param name="credentials">The credentials to use when authenticating.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// An <see cref="HttpResult"/> wrapping a flag indication if <paramref name="service"/> created a session,
        /// or any error details returned by the <paramref name="service"/>.
        /// </returns>
        public async Task<HttpResult<bool>> Login(Credentials credentials, CancellationToken cancellationToken = default)
        {
            return await Login(credentials, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Logins into the <paramref name="service"/> with the specified <paramref name="credentials"/>.
        /// </summary>
        /// <param name="credentials">The credentials to use when authenticating.</param>
        /// <param name="service">The service to authenticate to.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// An <see cref="HttpResult"/> wrapping a flag indication if <paramref name="service"/> created a session,
        /// or any error details returned by the <paramref name="service"/>.
        /// </returns>
        public async Task<HttpResult<bool>> Login(Credentials credentials, Uri service, CancellationToken cancellationToken = default)
        {
            HttpResult<Session> result = await AtProtoServer.CreateSession(credentials, service, HttpClientHandler, cancellationToken).ConfigureAwait(false);

            if (result.Succeeded && result.Result is not null)
            {
                lock (_session_SyncLock)
                {
                    Session = result.Result;

                    _sessionIssuedBy = service;
                    _timer ??= new ();
                    StartTokenRefreshTimer();
                }

                return new HttpResult<bool>() { StatusCode = result.StatusCode, Result = true };
            }
            else
            {
                lock (_session_SyncLock)
                {
                    Session = null;
                    if (_timer is not null)
                    {
                        _timer.Stop();
                        _timer.Enabled = false;
                    }
                }

                return new HttpResult<bool>
                {
                    Error = result.Error,
                    StatusCode = result.StatusCode,
                    Result = false
                };
            }
        }

        /// <summary>
        /// Logins in the specified <paramref name="identifier"/> and <paramref name="password"/>.
        /// </summary>
        /// <param name="identifier">The identifier used to authenticate.</param>
        /// <param name="password">The password used to authenticated.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// An <see cref="HttpResult"/> wrapping a flag indication if <paramref name="service"/> created a session,
        /// or any error details returned by the <paramref name="service"/>.
        /// </returns>
        public async Task<HttpResult<bool>> Login(string identifier, string password, CancellationToken cancellationToken = default)
        {
            return await Login(identifier, password, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Logins into the <paramref name="service"/> with the specified <paramref name="identifier"/> and <paramref name="password"/>.
        /// </summary>
        /// <param name="identifier">The identifier used to authenticate.</param>
        /// <param name="password">The password used to authenticated.</param>
        /// <param name="service">The service to authenticate to.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// An <see cref="HttpResult"/> wrapping a flag indication if <paramref name="service"/> created a session,
        /// or any error details returned by the <paramref name="service"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if the identifier or password is null or empty.</exception>
        public async Task<HttpResult<bool>> Login(string identifier, string password, Uri service, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                throw new ArgumentNullException(nameof(identifier));
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException(nameof(password));
            }

            return await Login(new Credentials(identifier, password), service, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Clears the internal session state used by the agent and cancels the session on the service.
        /// </summary>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        public async Task Logout(CancellationToken cancellationToken = default)
        {
            await Logout(DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Clears the internal session state used by the agent and tells the <paramref name="service"/> to cancel the session.
        /// </summary>
        /// <param name="service">The service to authenticate to.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        public async Task Logout(Uri service, CancellationToken cancellationToken = default)
        {
            if (Session is not null && Session.RefreshJwt is not null)
            {
                await AtProtoServer.DeleteSession(Session.RefreshJwt, service, HttpClientHandler, cancellationToken).ConfigureAwait(false);
            }

            lock (_session_SyncLock)
            {
                Session = null;
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        /// <summary>
        /// Refreshes the current authenticated session and updates the access and refresh tokens.
        /// </summary>
        public async Task RefreshSession(CancellationToken cancellationToken = default)
        {
            await RefreshSession(DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Refreshes the current authenticated session and updates the access and refresh tokens.
        /// </summary>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        public async Task<bool> RefreshSession(Uri service, CancellationToken cancellationToken = default)
        {
            if (_timer is not null)
            {
                _timer.Enabled = false;
            }

            if (Session is null || Session.RefreshJwt is null)
            {
                return false;
            }

            HttpResult<Session> refreshSessionResult = await AtProtoServer.RefreshSession(Session.RefreshJwt, service, HttpClientHandler, cancellationToken).ConfigureAwait(false);

            if (refreshSessionResult.Succeeded)
            {
                lock (_session_SyncLock)
                {
                    Session = refreshSessionResult.Result;
                    _timer ??= new();
                    StartTokenRefreshTimer();
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Describes the <paramref name="service"/>'s account creation requirements and capabilities.
        /// </summary>
        /// <param name="service">The service whose account description is to be retrieved.
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// An <see cref="HttpResult{T}"/> wrapping a <see cref="ServerDescription"/> containing the server account creation requirements and capabilities
        /// or any error message that was returned by the <paramref name="service"/>.
        /// </returns>
        public async Task<HttpResult<ServerDescription>> DescribeServer(Uri service, CancellationToken cancellationToken = default)
        {
            return await AtProtoServer.DescribeServer(service, HttpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resolves a handle (domain name) to a DID.
        /// </summary>
        /// <param name="handle">The handle to resolve.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// An <see cref="HttpResult{T}"/> wrapping the <see cref="Did"/> the <paramref name="handle"/> resolves to.
        /// or any error message that was returned by the <paramref name="service"/>.
        public async Task<HttpResult<Did>> ResolveHandle(string handle, CancellationToken cancellationToken = default)
        {
            return await ResolveHandle(handle, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resolves a handle (domain name) to a DID.
        /// </summary>
        /// <param name="handle">The handle to resolve.</param>
        /// <param name="service">The service used to resolve the <paramref name="handle"/>.</param>
        /// <param name="session">An optional <see cref="Session"/> contained authentication information.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>
        /// An <see cref="HttpResult{T}"/> wrapping the <see cref="Did"/> the <paramref name="handle"/> resolves to.
        /// or any error message that was returned by the <paramref name="service"/>.
        /// </returns>
        public async Task<HttpResult<Did>> ResolveHandle(string handle, Uri service, CancellationToken cancellationToken = default)
        {
            string? accessJwt;
            if (Session == null || string.IsNullOrEmpty(Session.AccessJwt))
            {
               accessJwt = null;
            }
            else
            {
                accessJwt = Session.AccessJwt;
            }

            return await AtProtoServer.ResolveHandle(handle, service, accessJwt, HttpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a record in the specified collection belonging to the current user.
        /// </summary>
        /// <param name="record">The record to be created.</param>
        /// <param name="collection">The collection the record should be created in.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="StrongReference"/> containing the Did and Cid of the newly created record, wrapped in an <see cref="HttpResult{T}"/></returns>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the current session is not authenticated.</exception>
        public async Task<HttpResult<StrongReference>> CreateRecord(NewAtProtoRecord record, string collection, CancellationToken cancellationToken = default)
        {
            if (Session == null || string.IsNullOrEmpty(Session.AccessJwt))
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await CreateRecord(record, collection, Session.Did, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a record in the specified collection belonging to the current user.
        /// </summary>
        /// <param name="record">The record to be created.</param>
        /// <param name="creator">The <see cref="Did"/> of the actor whose collection the record should be created in. Typically this is the Did of the current user.</param>
        /// <param name="collection">The collection the record should be created in.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="StrongReference"/> containing the Did and Cid of the newly created record, wrapped in an <see cref="HttpResult{T}"/></returns>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the current session is not authenticated.</exception>
        public async Task<HttpResult<StrongReference>> CreateRecord(NewAtProtoRecord record, string collection, Did creator, CancellationToken cancellationToken = default)
        {
            return await CreateRecord(record, collection, creator, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a record in the specified collection belonging to the current user.
        /// </summary>
        /// <param name="record">The record to be created.</param>
        /// <param name="creator">The <see cref="Did"/> of the actor whose collection the record should be created in. Typically this is the Did of the current user.</param>
        /// <param name="collection">The collection the record should be created in.</param>
        /// <param name="service">The service used to resolve the <paramref name="handle"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="StrongReference"/> containing the Did and Cid of the newly created record, wrapped in an <see cref="HttpResult{T}"/>.</returns>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the current session is not authenticated.</exception>
        public async Task<HttpResult<StrongReference>> CreateRecord(NewAtProtoRecord record, string collection, Did creator, Uri service, CancellationToken cancellationToken = default)
        {
            string? accessJwt;
            if (Session == null || string.IsNullOrEmpty(Session.AccessJwt))
            {
                throw new AuthenticatedSessionRequiredException();
            }
            else
            {
                accessJwt = Session.AccessJwt;
            }

            return await AtProtoServer.CreateRecord(record, collection, creator, service, accessJwt, HttpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes a record, identified by the repo, collection and rkey from the specified service.</summary>
        /// <param name="collection">The collection the record should be deleted from.</param>
        /// <param name="rkey">The record key, identifying the record to be deleted.</param>
        /// <param name="swapRecord">Specified if the operation should compare and swap with the previous record by cid.</param>
        /// <param name="swapCommit">Specified if the operation should compare and swap with the previous commit by cid.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A flag indicating if the record was successfully deleted, wrapped in an <see cref="HttpResult{T}"/>.</returns>
        public async Task<HttpResult<bool>> DeleteRecord(
            string collection,
            string rkey,
            AtCid? swapRecord,
            AtCid? swapCommit,
            CancellationToken cancellationToken = default)
        {
            if (Session == null)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await DeleteRecord(Session.Did, collection, rkey, swapRecord, swapCommit, DefaultService, cancellationToken).ConfigureAwait(false);
        }


        /// <summary>Deletes a record, identified by the repo, collection and rkey from the specified service.</summary>
        /// <param name="repo">The handle or Did of the repo to delete from. Typically this is the Did of the account that created the record.</param>
        /// <param name="collection">The collection the record should be deleted from.</param>
        /// <param name="rkey">The record key, identifying the record to be deleted.</param>
        /// <param name="swapRecord">Specified if the operation should compare and swap with the previous record by cid.</param>
        /// <param name="swapCommit">Specified if the operation should compare and swap with the previous commit by cid.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A flag indicating if the record was successfully deleted, wrapped in an <see cref="HttpResult{T}"/>.</returns>
        public async Task<HttpResult<bool>> DeleteRecord(
            AtIdentifier repo,
            string collection,
            string rkey,
            AtCid? swapRecord,
            AtCid? swapCommit,
            CancellationToken cancellationToken = default)
        {
            return await DeleteRecord(repo, collection, rkey, swapRecord, swapCommit, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes a record, identified by the repo, collection and rkey from the specified service.</summary>
        /// <param name="repo">The handle or Did of the repo to delete from. Typically this is the Did of the account that created the record.</param>
        /// <param name="collection">The collection the record should be deleted from.</param>
        /// <param name="rkey">The record key, identifying the record to be deleted.</param>
        /// <param name="swapRecord">Specified if the operation should compare and swap with the previous record by cid.</param>
        /// <param name="swapCommit">Specified if the operation should compare and swap with the previous commit by cid.</param>
        /// <param name="service">The service to delete the record from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>A flag indicating if the record was successfully deleted, wrapped in an <see cref="HttpResult{T}"/>.</returns>
        public async Task<HttpResult<bool>> DeleteRecord(
            AtIdentifier repo,
            string collection,
            string rkey,
            AtCid? swapRecord,
            AtCid? swapCommit,
            Uri service,
            CancellationToken cancellationToken = default)
        {
            string? accessJwt;
            if (Session == null || string.IsNullOrEmpty(Session.AccessJwt))
            {
                throw new AuthenticatedSessionRequiredException();
            }
            else
            {
                accessJwt = Session.AccessJwt;
            }

            HttpResult<EmptyResponse> response =
                await AtProtoServer.DeleteRecord(repo, collection, rkey, swapRecord, swapCommit, service, accessJwt, HttpClientHandler, cancellationToken).ConfigureAwait(false);

            HttpResult<bool> booleanResponse = new()
            {
                Result = response.Succeeded,
                Error = response.Error,
                StatusCode = response.StatusCode
            };

            return booleanResponse;
        }

        /// <summary>
        /// Gets information about an account and repository, including the list of collections.
        /// </summary>
        /// <param name="atIdentifier">The <see cref="AtIdentifier"/> of the repo to retrieve information for.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult{T}"/> wrapping a <see cref="RepoDescription"/>. The HttpResult status code indicates the success or failure of the operation.</returns>
        public async Task<HttpResult<RepoDescription>> DescribeRepo(AtIdentifier atIdentifier, CancellationToken cancellationToken = default)
        {
            return await DescribeRepo(atIdentifier, DefaultService,  cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets information about an account and repository, including the list of collections.
        /// </summary>
        /// <param name="atIdentifier">The <see cref="AtIdentifier"/> of the repo to retrieve information for.</param>
        /// <param name="service">The service to delete the record from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult{T}"/> wrapping a <see cref="RepoDescription"/>. The HttpResult status code indicates the success or failure of the operation.</returns>
        public async Task<HttpResult<RepoDescription>> DescribeRepo(AtIdentifier atIdentifier, Uri service, CancellationToken cancellationToken = default)
        {
            string? accessJwt = null;
            if (Session is not null && !string.IsNullOrEmpty(Session.AccessJwt))
            {
                accessJwt = Session.AccessJwt;
            }

            return await AtProtoServer.DescribeRepo(atIdentifier, service, accessJwt, HttpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the record specified by the identifying parameters.
        /// </summary>
        /// <param name="repo">The <see cref="AtIdentifier"/> of the repo to retrieve the record from.</param>
        /// <param name="collection">The NSID of the collection the record should be deleted from.</param>
        /// <param name="rkey">The record key, identifying the record to be deleted.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult{T}"/> wrapping a <see cref="AtProtoRecord"/>. The HttpResult status code indicates the success or failure of the operation.</returns>
        public async Task<HttpResult<AtProtoRecord>> GetRecord(
            AtIdentifier repo,
            string collection,
            string rkey,
            CancellationToken cancellationToken = default)
        {
            return await GetRecord(repo, collection, rkey, null, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the record specified by the identifying parameters.
        /// </summary>
        /// <param name="repo">The <see cref="AtIdentifier"/> of the repo to retrieve the record from.</param>
        /// <param name="collection">The NSID of the collection the record should be deleted from.</param>
        /// <param name="rkey">The record key, identifying the record to be deleted.</param>
        /// <param name="cid">The CID of the version of the record. If not specified, then return the most recent version.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult{T}"/> wrapping a <see cref="AtProtoRecord"/>. The HttpResult status code indicates the success or failure of the operation.</returns>
        public async Task<HttpResult<AtProtoRecord>> GetRecord(
            AtIdentifier repo,
            string collection,
            string rkey,
            AtCid? cid,
            CancellationToken cancellationToken = default)
        {
            return await GetRecord(repo, collection, rkey, cid, DefaultService, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the record specified by the identifying parameters.
        /// </summary>
        /// <param name="repo">The <see cref="AtIdentifier"/> of the repo to retrieve the record from.</param>
        /// <param name="collection">The NSID of the collection the record should be deleted from.</param>
        /// <param name="rkey">The record key, identifying the record to be deleted.</param>
        /// <param name="cid">The CID of the version of the record. If not specified, then return the most recent version.</param>
        /// <param name="service">The service to retrieve the record from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult{T}"/> wrapping a <see cref="AtProtoRecord"/>. The HttpResult status code indicates the success or failure of the operation.</returns>
        public async Task<HttpResult<AtProtoRecord>> GetRecord(
            AtIdentifier repo,
            string collection,
            string rkey,
            AtCid? cid,
            Uri service,
            CancellationToken cancellationToken = default)
        {
            string? accessJwt;
            if (Session == null || string.IsNullOrEmpty(Session.AccessJwt))
            {
                throw new AuthenticatedSessionRequiredException();
            }
            else
            {
                accessJwt = Session.AccessJwt;
            }

            return await AtProtoServer.GetRecord(repo, collection, rkey, cid, service, accessJwt, HttpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        private void StartTokenRefreshTimer()
        {
            if (_enableTokenRefresh)
            {
                if (Session is not null && !string.IsNullOrEmpty(Session.AccessJwt))
                {
                    TimeSpan accessTokenExpiresIn = GetTimeToTokenExpiry(Session.AccessJwt);

                    if (accessTokenExpiresIn.TotalSeconds < 60)
                    {
                        // As we're about to expire, go refresh the token
                        RefreshSession(_sessionIssuedBy!).FireAndForget();
                        return;
                    }

                    TimeSpan refreshIn = _refreshAccessTokenInterval;
                    if (accessTokenExpiresIn < _refreshAccessTokenInterval)
                    {
                        refreshIn = accessTokenExpiresIn - new TimeSpan(0, 1, 0);
                    }

                    if (_timer is not null)
                    {
                        _timer.Interval = refreshIn.TotalMilliseconds >= int.MaxValue ? int.MaxValue : refreshIn.TotalMilliseconds;
                        _timer.Elapsed += RefreshToken;
                        _timer.Enabled = true;
                        _timer.Start();
                    }
                }
            }
        }

        private TimeSpan GetTimeToTokenExpiry(string jwt)
        {
            if (string.IsNullOrEmpty(jwt))
            {
                throw new ArgumentNullException(nameof(jwt));
            }

            _jwtSecurityTokenHandler
                .ValidateToken(
                    jwt,
                    _defaultTokenValidationParameters,
                    out SecurityToken token);

            return token.ValidTo.ToUniversalTime() - DateTime.UtcNow;
        }

        private void RefreshToken(object? sender, ElapsedEventArgs e) => RefreshSession(_sessionIssuedBy!).FireAndForget();
    }
}
