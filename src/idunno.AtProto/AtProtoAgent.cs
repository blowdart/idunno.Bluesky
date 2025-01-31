// Copyright(c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using System.Timers;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using idunno.AtProto.Authentication;
using idunno.AtProto.Events;
using idunno.AtProto.Labels;
using idunno.AtProto.Models;
using idunno.AtProto.Repo;
using idunno.AtProto.Repo.Models;
using idunno.AtProto.Server;

using idunno.DidPlcDirectory;

using Blob = idunno.AtProto.Repo.Blob;
using System.Security.Cryptography;
using System.Text;

namespace idunno.AtProto
{
    /// <summary>
    /// Provides a class for sending requests to and receiving responses from an atproto service, identified by its service URI.
    /// </summary>
    public partial class AtProtoAgent : Agent
    {
        private volatile bool _disposed;

        private readonly DirectoryAgent _directoryAgent;

        private readonly ILogger<AtProtoAgent> _logger;

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgent"/>
        /// </summary>
        /// <param name="service">The URI of the AtProto service to connect to.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/>, if any, to use when creating loggers.</param>
        /// <param name="proxyUri">The proxy URI to use, if any.</param>
        /// <param name="checkCertificateRevocationList">Flag indicating whether certificate revocation lists should be checked. Defaults to <see langword="true" />.</param>
        /// <param name="httpUserAgent">The user agent string to use, if any.</param>
        /// <param name="timeout">The default HTTP timeout to use, if any.</param>
        /// <param name="options">Any <see cref="AtProtoAgentOptions"/> to configure this instance with.</param>
        /// <remarks>
        /// <para>
        /// Settings <paramref name="checkCertificateRevocationList"/> to <see langword="false" /> can introduce security vulnerabilities. Only set this value to
        /// false if you are using a debugging proxy which does not support CRLs.
        /// </para>
        /// </remarks>
        public AtProtoAgent(
            Uri service,
            ILoggerFactory? loggerFactory = default,
            Uri? proxyUri = null,
            bool checkCertificateRevocationList = true,
            string? httpUserAgent = null,
            TimeSpan? timeout = null,
            AtProtoAgentOptions? options = null) : base(
                proxyUri: proxyUri,
                checkCertificateRevocationList: checkCertificateRevocationList,
                httpUserAgent: httpUserAgent,
                timeout: timeout)
        {
            ArgumentNullException.ThrowIfNull(service);

            Service = service;

            if (options is not null)
            {
                _enableTokenRefresh = options.EnableBackgroundTokenRefresh;
            }

            LoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            _logger = LoggerFactory.CreateLogger<AtProtoAgent>();

            _directoryAgent = new DirectoryAgent(
                loggerFactory: loggerFactory,
                proxyUri: proxyUri,
                checkCertificateRevocationList: checkCertificateRevocationList,
                httpUserAgent: httpUserAgent,
                timeout: timeout);
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgent"/>
        /// </summary>
        /// <param name="service">The URI of the AtProto service to connect to.</param>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use when creating <see cref="HttpClient"/>s.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/>, if any, to use when creating loggers.</param>
        /// <param name="options">Any <see cref="AtProtoAgentOptions"/> to configure this instance with.</param>
        public AtProtoAgent(Uri service, IHttpClientFactory httpClientFactory, ILoggerFactory? loggerFactory = default, AtProtoAgentOptions? options = null) : base(httpClientFactory)
        {
            ArgumentNullException.ThrowIfNull(service);

            Service = service;

            if (options is not null)
            {
                _enableTokenRefresh = options.EnableBackgroundTokenRefresh;
            }

            LoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            _logger = LoggerFactory.CreateLogger<AtProtoAgent>();

            _directoryAgent = new DirectoryAgent(httpClientFactory, loggerFactory);
        }

        /// <summary>
        /// Gets the <see cref="Uri"/> for the AT Proto service the agent is issuing requests against.
        /// </summary>
        public Uri Service { get; protected set; }

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
                if (_credentialRefreshTimer is not null)
                {
                    _credentialRefreshTimer.Stop();
                    _credentialRefreshTimer.Enabled = false;
                    _credentialRefreshTimer.Dispose();
                    _credentialRefreshTimer = null;
                }

                Authenticated = null;
                CredentialsUpdated = null;

                _directoryAgent.Dispose();

                _credentialReaderWriterLockSlim.Dispose();
            }

            base.Dispose(disposing);

            _disposed = true;
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
        /// Refreshes the current authenticated session and updates the access and refresh tokens.
        /// </summary>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when agent is not authenticated.</exception>
        public async Task<bool> RefreshSession(CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                Logger.RefreshSessionFailedNoSession(_logger);
                throw new AuthenticationRequiredException();
            }

            return await RefreshSession(Credentials, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Refreshes the session specified by the <paramref name="refreshCredential"/>.
        /// </summary>
        /// <param name="refreshCredential">The refresh token to use to refresh the session.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="refreshCredential"/> is null, or its Uri property is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="refreshCredential"/>'s RefreshToken property is null or whitespace.</exception>
        public async Task<bool> RefreshSession(RefreshCredential refreshCredential, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(refreshCredential);
            ArgumentNullException.ThrowIfNull(refreshCredential.Service);

            ArgumentException.ThrowIfNullOrWhiteSpace(refreshCredential.RefreshToken);

            string tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshCredential.RefreshToken)));

            using (_logger.BeginScope($"RefreshSession() with refresh token #{tokenHash}"))
            {
                Logger.RefreshSessionCalled(_logger, refreshCredential.Service, tokenHash);

                if (_credentialRefreshTimer is not null)
                {
                    StopTokenRefreshTimer();
                }

                AtProtoHttpResult<RefreshSessionResponse> refreshSessionResult;
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
                    Logger.RefreshSessionTokenValidationFailed(_logger, refreshSessionResult.Result.Did, refreshCredential.Service);

                    throw new SecurityTokenValidationException("The issued access token could not be validated.");
                }

                AccessCredentials refreshedCredentials;

                if (refreshCredential is IDPoPBoundCredential dPoPBoundCredential)
                {
                    if (refreshSessionResult.HttpResponseHeaders is null || !refreshSessionResult.HttpResponseHeaders.ContainsDPoPNonce())
                    {
                        var tokenRefreshFailedEventArgs = new TokenRefreshFailedEventArgs(
                            refreshCredential.RefreshToken,
                            refreshCredential.Service,
                            refreshSessionResult.StatusCode,
                            refreshSessionResult.AtErrorDetail);

                        OnTokenRefreshFailed(tokenRefreshFailedEventArgs);

                        throw new AtProtoException("Refresh was for a DPoP token but did not return a DPoP nonce.");
                    }
                    else
                    {
                        refreshedCredentials = new DPoPAccessCredentials(
                            refreshCredential.Service,
                            refreshSessionResult.Result.AccessJwt,
                            refreshSessionResult.Result.RefreshJwt,
                            dPoPBoundCredential.DPoPProofKey,
                            refreshSessionResult.HttpResponseHeaders.DPoPNonce()!);
                    }
                }
                else
                {
                    refreshedCredentials = new AccessCredentials(
                        refreshCredential.Service,
                        refreshSessionResult.Result.AccessJwt,
                        refreshSessionResult.Result.RefreshJwt);
                }


                Logger.RefreshSessionSucceeded(_logger, refreshedCredentials.Did, refreshedCredentials.Service);

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

        /// <summary>
        /// Gets information about the session associated with the access token provided.
        /// </summary>
        /// <param name="accessCredentials">The access credentials to authenticate with.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="accessCredentials"/> is null.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the <paramref name="accessCredentials"/>'s AccessJwt is null or empty.</exception>
        public async Task<AtProtoHttpResult<GetSessionResponse>> GetSession(
            AccessCredentials accessCredentials,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentException.ThrowIfNullOrEmpty(accessCredentials.AccessJwt);

            return await AtProtoServer.GetSession(accessCredentials, HttpClient, InternalOnCredentialsUpdatedCallBack, LoggerFactory, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resumes a session on the <paramref name="service"/> from the <paramref name="refreshCredential"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> the tokens are associated with.</param>
        /// <param name="service">The <see cref="Uri"/> of the PDS that issued the tokens.</param>
        /// <param name="refreshCredential">The refresh token to restore the session for.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/>, <paramref name="refreshCredential"/> or <paramref name="service"/> is null.</exception>
        public async Task<bool> ResumeSession(
            Did did,
            Uri service,
            RefreshCredential refreshCredential,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(did);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(refreshCredential);

            ArgumentException.ThrowIfNullOrWhiteSpace(refreshCredential.RefreshToken);

            return await ResumeSession(did, refreshCredential, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resumes a session from the provided <paramref name="refreshCredential"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> the tokens are associated with.</param>
        /// <param name="refreshCredential">The credentials to restore the session for.</param>
        /// <param name="cancellationToken">An optional cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="refreshCredential"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the RefreshJwt or Service properties in <paramref name="refreshCredential"/> is null or whitespace.</exception>
        /// <exception cref="SessionRestorationFailedException">Thrown when the session recreated on the PDS does not match the expected <paramref name="did"/>.</exception>
        public async Task<bool> ResumeSession(Did did, RefreshCredential refreshCredential, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(refreshCredential);
            ArgumentNullException.ThrowIfNull(refreshCredential.Service);

            ArgumentException.ThrowIfNullOrWhiteSpace(refreshCredential.RefreshToken);

            Logger.RestoreSessionCalled(_logger, did, refreshCredential.Service);

            AtProtoHttpResult<RefreshSessionResponse> refreshSessionResult =
                await AtProtoServer.RefreshSession(
                    refreshCredential,
                    httpClient: HttpClient,
                    InternalOnCredentialsUpdatedCallBack,
                    loggerFactory: LoggerFactory,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

            if (refreshSessionResult.Succeeded)
            {
                if (!await ValidateJwtToken(refreshSessionResult.Result.AccessJwt, refreshSessionResult.Result.Did, refreshCredential.Service).ConfigureAwait(false))
                {
                    throw new SecurityTokenValidationException("The issued access token could not be validated.");
                }

                AccessCredentials refreshedAccessCredentials;

                if (refreshCredential is not IDPoPBoundCredential dPoPRefreshCredential)
                {
                    refreshedAccessCredentials = new AccessCredentials(
                        refreshCredential.Service,
                        refreshSessionResult.Result.AccessJwt,
                        refreshSessionResult.Result.RefreshJwt);
                }
                else
                {
                    if (refreshSessionResult.HttpResponseHeaders is null)
                    {
                        throw new SessionRestorationFailedException("Refresh token was DPoPBound, but no http response headers were returned");
                    }

                    string? dPoPNonce = refreshSessionResult.HttpResponseHeaders.DPoPNonce() ??
                        throw new SessionRestorationFailedException("Refresh token was DPoPBound, but no nonce was present in the response headers.");

                    refreshedAccessCredentials = new DPoPAccessCredentials(
                        refreshCredential.Service,
                        refreshSessionResult.Result.AccessJwt,
                        refreshSessionResult.Result.RefreshJwt,
                        dPoPRefreshCredential.DPoPProofKey,
                        dPoPNonce);
                }

                Credentials = refreshedAccessCredentials;

                Logger.RestoreSessionSucceeded(_logger, did, refreshedAccessCredentials.Service);

                _credentialRefreshTimer ??= new();
                StartTokenRefreshTimer();

                var sessionRefreshedEventArgs = new CredentialsUpdatedEventArgs(
                    refreshedAccessCredentials.Did,
                    refreshedAccessCredentials.Service,
                    Credentials);

                OnCredentialsUpdated(sessionRefreshedEventArgs);

                return true;
            }
            else
            {
                StopTokenRefreshTimer(true);
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
        /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not authenticated.</exception>
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
                throw new AuthenticationRequiredException();
            }

            AtProtoHttpResult<ApplyWritesResponse> applyWritesResult = await AtProtoServer.ApplyWrites(
                writeRequests,
                repo,
                validate,
                cid,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated : InternalOnCredentialsUpdatedCallBack,
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
        /// <exception cref="ArgumentNullException"><para>Thrown when <paramref name="record"/> or <paramref name="collection"/> is null.</para></exception>
        /// <exception cref="AuthenticationRequiredException"><para>Thrown when the current session is not authenticated.</para></exception>
        public async Task<AtProtoHttpResult<CreateRecordResponse>> CreateRecord<TRecord>(
            TRecord record,
            Nsid collection,
            RecordKey? rkey = null,
            bool? validate = true,
            Cid? swapCommit = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(collection);

            if (!IsAuthenticated)
            {
                Logger.CreateRecordFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticationRequiredException();
            }

            AtProtoHttpResult<CreateRecordResponse> result = await AtProtoServer.CreateRecord(
                record: record,
                collection: collection,
                creator: Credentials.Did,
                rKey: rkey,
                validate: validate,
                swapCommit : swapCommit,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
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
        /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not an authenticated session.</exception>
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
                throw new AuthenticationRequiredException();
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
                throw new AuthenticationRequiredException();
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
        /// <exception cref="AuthenticationRequiredException">Throw when the current session is not an authenticated session.</exception>
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
                throw new AuthenticationRequiredException();
            }

            AtProtoHttpResult<Commit> response =
                await AtProtoServer.DeleteRecord(
                    repo,
                    collection,
                    rKey,
                    swapRecord,
                    swapCommit,
                    service: Service,
                    accessCredentials: Credentials,
                    httpClient: HttpClient,
                    onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
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
                    HttpResponseHeaders = response.HttpResponseHeaders,
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
                    HttpResponseHeaders = response.HttpResponseHeaders,
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
        /// <exception cref="AuthenticationRequiredException"><para>Thrown when the current session is not authenticated.</para></exception>
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
                throw new AuthenticationRequiredException();
            }

            AtProtoHttpResult<PutRecordResponse> result = await AtProtoServer.PutRecord(
                record: record,
                collection: collection,
                creator: creator,
                rKey: rKey,
                validate: validate,
                swapCommit: swapCommit,
                swapRecord: swapRecord,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
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
                HttpClient,
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

            AccessCredentials? accessCredentials = null;

            if (IsAuthenticated)
            {
                accessCredentials = Credentials;
            }

            AtProtoHttpResult<T> result =
                await AtProtoServer.GetRecord<T>(
                    repo: repo,
                    collection: collection,
                    rKey: rKey,
                    cid: cid,
                    service: service,
                    accessCredentials: accessCredentials,
                    httpClient: HttpClient,
                    onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
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
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<PagedReadOnlyCollection<T>>> ListRecords<T>(
            Nsid collection,
            int? limit = 50,
            string? cursor = null,
            bool reverse = false,
            Uri? service = null,
            CancellationToken cancellationToken = default) where T : AtProtoRecord
        {
            ArgumentNullException.ThrowIfNull(collection);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await ListRecords<T>(
                Credentials.Did,
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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="repo"/> or <paramref name="collection"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not an authenticated session.</exception>
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

            AccessCredentials? accessCredentials = null;

            if (IsAuthenticated)
            {
                accessCredentials = Credentials;
            }

            Logger.ListRecordsCalled(_logger, repo, collection, service);

            AtProtoHttpResult<PagedReadOnlyCollection<T>> result = await AtProtoServer.ListRecords<T>(
                repo: repo,
                collection: collection,
                limit: limit,
                cursor: cursor,
                reverse: reverse,
                service: service,
                accessCredentials: accessCredentials,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
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
        /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not an authenticated session.</exception>
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

                throw new AuthenticationRequiredException();
            }

            if (blob.Length == 0)
            {
                Logger.UploadBlobFailedAsBlobLengthIsZero(_logger, service);
                throw new ArgumentException("blob length cannot be 0.", nameof(blob));
            }

            try
            {
                return await AtProtoServer.UploadBlob(
                    blob: blob,
                    mimeType: mimeType,
                    service: service,
                    accessCredentials: Credentials,
                    httpClient: HttpClient,
                    onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
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
                uriPatterns: uriPatterns,
                sources: sources,
                limit: limit,
                cursor: cursor,
                service: service,
                accessCredentials: null,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
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
                        RefreshSession().FireAndForget();
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
