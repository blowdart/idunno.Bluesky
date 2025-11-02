// Copyright(c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Claims;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using idunno.AtProto.Authentication;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using Blob = idunno.AtProto.Repo.Blob;
using idunno.AtProto.Server.Models;
using idunno.DidPlcDirectory;

namespace idunno.AtProto
{
    /// <summary>
    /// Provides a class for sending requests to and receiving responses from an atproto service, identified by its service URI.
    /// </summary>
    public partial class AtProtoAgent : Agent
    {
        private volatile bool _disposed;
        private readonly ILogger<AtProtoAgent> _logger;

        internal readonly DirectoryAgent _directoryAgent;

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgent"/>
        /// </summary>
        /// <param name="service">The URI of the AtProto service to connect to.</param>
        /// <param name="options">Any <see cref="AtProtoAgentOptions"/> to configure this instance with.</param>
        public AtProtoAgent(
            Uri service,
            AtProtoAgentOptions? options = null) : base(options?.HttpClientOptions, options?.HttpJsonOptions)
        {
            ArgumentNullException.ThrowIfNull(service);

            OriginalService = service;
            Service = service;

            if (options is not null)
            {
                _enableTokenRefresh = options.EnableBackgroundTokenRefresh;

                options.OAuthOptions?.Validate();

                Options = options;

                LoggerFactory = Options.LoggerFactory ?? NullLoggerFactory.Instance;
            }
            else
            {
                LoggerFactory = NullLoggerFactory.Instance;
            }

            _logger = LoggerFactory.CreateLogger<AtProtoAgent>();

            _directoryAgent = new DirectoryAgent(
                    new DirectoryAgentOptions()
                    {
                        PlcDirectoryUri = options?.PlcDirectoryServer ?? DirectoryAgent.s_defaultDirectoryServer,
                        LoggerFactory = LoggerFactory,
                        HttpClientOptions = options?.HttpClientOptions
                    });
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgent"/>
        /// </summary>
        /// <param name="service">The URI of the AtProto service to connect to.</param>
        /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> to use when creating <see cref="HttpClient"/>s.</param>
        /// <param name="options">Any <see cref="AtProtoAgentOptions"/> to configure this instance with.</param>
        public AtProtoAgent(Uri service, IHttpClientFactory httpClientFactory, AtProtoAgentOptions? options = null) : base(httpClientFactory, options?.HttpJsonOptions)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClientFactory);

            OriginalService = service;
            Service = service;

            if (options is not null)
            {
                _enableTokenRefresh = options.EnableBackgroundTokenRefresh;
                LoggerFactory = options.LoggerFactory ?? NullLoggerFactory.Instance;
            }
            else
            {
                LoggerFactory = NullLoggerFactory.Instance;
            }

            _logger = LoggerFactory.CreateLogger<AtProtoAgent>();

            _directoryAgent = new DirectoryAgent(
                httpClientFactory,
                new DirectoryAgentOptions()
                {
                    PlcDirectoryUri = options?.PlcDirectoryServer ?? DirectoryAgent.s_defaultDirectoryServer,
                    LoggerFactory = LoggerFactory,
                });
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgent"/> and sets the agent authentication to
        /// a <see cref="DPoPAccessCredentials"/> derived from the <paramref name="principal"/>.
        /// </summary>
        /// <param name="principal">The <see cref="ClaimsPrincipal"/> to extract authentication properties from.</param>
        /// <param name="options">Any <see cref="AtProtoAgentOptions"/> to configure this instance with.</param>

        public AtProtoAgent(
            ClaimsPrincipal principal,
            AtProtoAgentOptions? options = null) : base(options?.HttpClientOptions, options?.HttpJsonOptions)
        {
            DPoPAccessCredentials credentials = AtProtoCredential.Create(principal);
            OriginalService = credentials.Service;
            Service = credentials.Service;
            _credentials = credentials;

            if (options is not null)
            {
                _enableTokenRefresh = options.EnableBackgroundTokenRefresh;

                options.OAuthOptions?.Validate();

                Options = options;

                LoggerFactory = Options.LoggerFactory ?? NullLoggerFactory.Instance;
            }
            else
            {
                LoggerFactory = NullLoggerFactory.Instance;
            }

            _logger = LoggerFactory.CreateLogger<AtProtoAgent>();

            _directoryAgent = new DirectoryAgent(
                    new DirectoryAgentOptions()
                    {
                        PlcDirectoryUri = options?.PlcDirectoryServer ?? DirectoryAgent.s_defaultDirectoryServer,
                        LoggerFactory = LoggerFactory,
                        HttpClientOptions = options?.HttpClientOptions
                    });
        }

        /// <summary>
        /// Creates a new instance of <see cref="AtProtoAgent"/> and sets the agent authentication to
        /// a <see cref="DPoPAccessCredentials"/> derived from the <paramref name="identity"/>.
        /// </summary>
        /// <param name="identity">The <see cref="ClaimsIdentity"/> to extract authentication properties from.</param>
        /// <param name="options">Any <see cref="AtProtoAgentOptions"/> to configure this instance with.</param>

        public AtProtoAgent(
            ClaimsIdentity identity,
            AtProtoAgentOptions? options = null) : base(options?.HttpClientOptions, options?.HttpJsonOptions)
        {
            DPoPAccessCredentials credentials = AtProtoCredential.Create(identity);
            OriginalService = credentials.Service;
            Service = credentials.Service;
            _credentials = credentials;

            if (options is not null)
            {
                _enableTokenRefresh = options.EnableBackgroundTokenRefresh;

                options.OAuthOptions?.Validate();

                Options = options;

                LoggerFactory = Options.LoggerFactory ?? NullLoggerFactory.Instance;
            }
            else
            {
                LoggerFactory = NullLoggerFactory.Instance;
            }

            _logger = LoggerFactory.CreateLogger<AtProtoAgent>();

            _directoryAgent = new DirectoryAgent(
                    new DirectoryAgentOptions()
                    {
                        PlcDirectoryUri = options?.PlcDirectoryServer ?? DirectoryAgent.s_defaultDirectoryServer,
                        LoggerFactory = LoggerFactory,
                        HttpClientOptions = options?.HttpClientOptions
                    });
        }

        /// <summary>
        /// Gets a configured logger factory from which to create loggers.
        /// </summary>
        protected internal ILoggerFactory LoggerFactory { get; init; }

        /// <summary>
        /// Gets the configuration options for the agent.
        /// </summary>
        protected internal AtProtoAgentOptions? Options { get; init; }

        /// <summary>
        /// Gets the <see cref="Uri"/> for the AT Proto service the agent is issuing requests against.
        /// </summary>
        public Uri Service { get; protected internal set; }

        /// <summary>
        /// Gets the <see cref="Uri"/> for the service originally supplied during the construction of this instance.
        /// </summary>
        protected internal Uri OriginalService { get; set; }

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
                Authenticated = null;
                CredentialsUpdated = null;
                TokenRefreshFailed = null;
                Unauthenticated = null;

                if (_credentialRefreshTimer is not null)
                {
                    _credentialRefreshTimer.Stop();
                    _credentialRefreshTimer.Enabled = false;
                    _credentialRefreshTimer.Dispose();
                    _credentialRefreshTimer = null;
                }

                _directoryAgent.Dispose();

                _credentialReaderWriterLockSlim.Dispose();
            }

            base.Dispose(disposing);

            _disposed = true;
        }

        /// <summary>
        /// Creates a new <see cref="AtProtoAgentBuilder"/>.
        /// </summary>
        /// <returns>A new <see cref="AtProtoAgentBuilder"/></returns>
        public static AtProtoAgentBuilder CreateBuilder() => AtProtoAgentBuilder.Create();

        /// <summary>
        /// Resolves a handle (domain name) to a DID.
        /// </summary>
        /// <param name="handle">The handle to resolve.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="handle"/> is null.</exception>
        public async Task<Did?> ResolveHandle(string handle, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(handle);

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
        /// Resolves the Personal Data Server (PDS) <see cref="Uri"/>for the specified <paramref name="handle"/>.
        /// </summary>
        /// <param name="handle">The handle whose PDS <see cref="Uri"/> should be resolved.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="handle"/> is null or white space.</exception>
        public async Task<Uri?> ResolvePds(string handle, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(handle);

            Did? did = await ResolveHandle(handle, cancellationToken).ConfigureAwait(false) ?? throw new ArgumentException($"{handle} cannot be resolved to a DID", nameof(handle));

            return await ResolvePds(did, cancellationToken).ConfigureAwait(false);
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
        /// <param name="operations"><para>A collection of write requests to apply.</para></param>
        /// <param name="jsonSerializerOptions"><para>The <see cref="JsonSerializerOptions"/> to use when serializing the record values within an create or update operation.</para></param>
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
        /// <param name="serviceProxy"><para>The service the PDS should proxy the call to, if any.</para></param>
        /// <param name="cancellationToken"><para>A cancellation token that can be used by other objects or threads to receive notice of cancellation.</para></param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="operations"/> or <paramref name="repo" /> is null.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="operations"/> is an empty collection.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
        [RequiresDynamicCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        public async Task<AtProtoHttpResult<ApplyWritesResults>> ApplyWrites(
            ICollection<WriteOperation> operations,
            JsonSerializerOptions jsonSerializerOptions,
            Did repo,
            bool? validate,
            Cid? cid = null,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operations);
            ArgumentNullException.ThrowIfNull(repo);

            if (!IsAuthenticated)
            {
                Logger.ApplyWritesFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticationRequiredException();
            }

            AtProtoHttpResult<ApplyWritesResults> applyWritesResult = await AtProtoServer.ApplyWrites(
                operations: operations,
                jsonSerializerOptions: jsonSerializerOptions,
                repo: repo,
                validate: validate,
                cid: cid,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                serviceProxy: serviceProxy,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
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
        /// <param name="rKey"><para>An optional <see cref="RecordKey"/> to create the record with.</para></param>
        /// <param name="validate">
        ///   <para>Gets a flag indicating what validation will be performed, if any.</para>
        ///   <para>A value of <keyword>true</keyword> requires lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>false</keyword> will skip Lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>null</keyword> to validate record data only for known lexicons.</para>
        ///   <para>Defaults to <keyword>true</keyword>.</para>
        /// </param>
        /// <param name="swapCommit"><para>Compare and swap with the previous commit by CID.</para></param>
        /// <param name="serviceProxy"><para>The service the PDS should proxy the call to, if any.</para></param>
        /// <param name="cancellationToken"><para>A cancellation token that can be used by other objects or threads to receive notice of cancellation.</para></param>
        /// <returns><para>The task object representing the asynchronous operation.</para></returns>
        /// <exception cref="ArgumentNullException"><para>Thrown when <paramref name="record"/> or <paramref name="collection"/> is null.</para></exception>
        /// <exception cref="AuthenticationRequiredException"><para>Thrown when the current agent is not authenticated.</para></exception>
        [RequiresDynamicCode("Use a CreateRecord overload which takes JsonSerializerOptions instead.")]
        [RequiresUnreferencedCode("Use a CreateRecord overload which takes JsonSerializerOptions instead.")]
        public async Task<AtProtoHttpResult<CreateRecordResult>> CreateRecord<TRecord>(
            TRecord record,
            Nsid collection,
            RecordKey? rKey = null,
            bool? validate = true,
            Cid? swapCommit = null,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default) where TRecord : AtProtoRecord
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(collection);

            if (!IsAuthenticated)
            {
                Logger.CreateRecordFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticationRequiredException();
            }

            AtProtoHttpResult<CreateRecordResult> result = await AtProtoServer.CreateRecord(
                record: record,
                collection: collection,
                creator: Credentials.Did,
                rKey: rKey,
                validate: validate,
                swapCommit : swapCommit,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                serviceProxy: serviceProxy,
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

        /// <summary>
        /// Creates a record in the specified collection belonging to the current user.
        /// </summary>
        /// <typeparam name="TRecord">The type of record to create.</typeparam>
        /// <param name="record"><para>The record to be created.</para></param>
        /// <param name="jsonSerializerOptions"><para>The <see cref="JsonSerializerOptions"/> to apply during serialization of <typeparamref name="TRecord"/>.</para></param>
        /// <param name="collection"><para>The collection the record should be created in.</para></param>
        /// <param name="rKey"><para>An optional <see cref="RecordKey"/> to create the record with.</para></param>
        /// <param name="validate">
        ///   <para>Gets a flag indicating what validation will be performed, if any.</para>
        ///   <para>A value of <keyword>true</keyword> requires lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>false</keyword> will skip Lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>null</keyword> to validate record data only for known lexicons.</para>
        ///   <para>Defaults to <keyword>true</keyword>.</para>
        /// </param>
        /// <param name="swapCommit"><para>Compare and swap with the previous commit by CID.</para></param>
        /// <param name="serviceProxy"><para>The service the PDS should proxy the call to, if any.</para></param>
        /// <param name="cancellationToken"><para>A cancellation token that can be used by other objects or threads to receive notice of cancellation.</para></param>
        /// <returns><para>The task object representing the asynchronous operation.</para></returns>
        /// <exception cref="ArgumentNullException"><para>Thrown when <paramref name="record"/> or <paramref name="collection"/> is null.</para></exception>
        /// <exception cref="AuthenticationRequiredException"><para>Thrown when the current agent is not authenticated.</para></exception>
        [RequiresDynamicCode("Use a Get overload which takes JsonSerializerOptions instead.")]
        [RequiresUnreferencedCode("Use a Get overload which takes JsonSerializerOptions instead.")]
        public async Task<AtProtoHttpResult<CreateRecordResult>> CreateRecord<TRecord>(
            TRecord record,
            JsonSerializerOptions jsonSerializerOptions,
            Nsid collection,
            RecordKey? rKey = null,
            bool? validate = true,
            Cid? swapCommit = null,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default) where TRecord : AtProtoRecord
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(collection);

            if (!IsAuthenticated)
            {
                Logger.CreateRecordFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticationRequiredException();
            }

            AtProtoHttpResult<CreateRecordResult> result = await AtProtoServer.CreateRecord(
                record: record,
                collection: collection,
                creator: Credentials.Did,
                rKey: rKey,
                validate: validate,
                swapCommit: swapCommit,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                serviceProxy: serviceProxy,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                jsonSerializerOptions: jsonSerializerOptions,
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

        /// <summary>Deletes a record, identified by its <paramref name="uri"/>.</summary>
        /// <param name="uri">The <see cref="AtUri"/> of the record to delete.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, the uri collection or the uri record key is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not an authenticated session.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteRecord(
            AtUri uri,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uri);

            ArgumentNullException.ThrowIfNull(uri.Collection);
            ArgumentNullException.ThrowIfNull(uri.RecordKey);

            if (!IsAuthenticated)
            {
                Logger.DeleteRecordFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticationRequiredException();
            }

            return await DeleteRecord(Did, uri.Collection, uri.RecordKey, null, null, serviceProxy, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes a record, identified by the repo, collection and rKey from the specified service.</summary>
        /// <param name="collection">The collection the record should be deleted from.</param>
        /// <param name="rKey">The record key, identifying the record to be deleted.</param>
        /// <param name="swapRecord">Specified if the operation should compare and swap with the previous record by cid.</param>
        /// <param name="swapCommit">Specified if the operation should compare and swap with the previous commit by cid.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="collection"/> or <paramref name="rKey"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not an authenticated session.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteRecord(
            Nsid collection,
            RecordKey rKey,
            Cid? swapRecord = null,
            Cid? swapCommit = null,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(rKey);

            if (!IsAuthenticated)
            {
                Logger.DeleteRecordFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticationRequiredException();
            }

            return await DeleteRecord(Did, collection, rKey, swapRecord, swapCommit, serviceProxy, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes a record, identified by the repo, collection and rKey from the specified service.</summary>
        /// <param name="strongReference">The <see cref="StrongReference"/> to the record to be deleted.</param>
        /// <param name="swapRecord">Specified if the operation should compare and swap with the previous record by cid.</param>
        /// <param name="swapCommit">Specified if the operation should compare and swap with the previous commit by cid.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is null, </exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="strongReference"/> is not valid.</exception>
        public async Task<AtProtoHttpResult<Commit>> DeleteRecord(
            StrongReference strongReference,
            Cid? swapRecord = null,
            Cid? swapCommit = null,
            string? serviceProxy = null,
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
                serviceProxy,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes a record, identified by the repo, collection and rKey from the specified service.</summary>
        /// <param name="repo">The handle or Did of the repo to delete from. Typically this is the Did of the account that created the record.</param>
        /// <param name="collection">The collection the record should be deleted from.</param>
        /// <param name="rKey">The record key, identifying the record to be deleted.</param>
        /// <param name="swapRecord">Specified if the operation should compare and swap with the previous record by cid.</param>
        /// <param name="swapCommit">Specified if the operation should compare and swap with the previous commit by cid.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
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
            string? serviceProxy = null,
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
                    serviceProxy: serviceProxy,
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
        /// <typeparam name="TRecord">The type of the record value to be updated</typeparam>
        /// <param name="record"><para>The record to be updated or created.</para></param>
        /// <param name="collection"><para>The collection the record should be updated or created in.</para></param>
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
        /// <param name="serviceProxy"><para>The service the PDS should proxy the call to, if any.</para></param>
        /// <param name="cancellationToken"><para>A cancellation token that can be used by other objects or threads to receive notice of cancellation.</para></param>
        /// <returns><para>The task object representing the asynchronous operation.</para></returns>
        /// <exception cref="ArgumentNullException">
        ///     <para>Thrown when <paramref name="record"/>, <paramref name="collection"/> or <paramref name="rKey"/> is null.</para>
        /// </exception>
        /// <exception cref="AuthenticationRequiredException"><para>Thrown when the current agent is not authenticated.</para></exception>
        [RequiresDynamicCode("Use a PutRecord overload which takes JsonSerializerOptions instead.")]
        [RequiresUnreferencedCode("Use a PutRecord overload which takes JsonSerializerOptions instead.")]
        public async Task<AtProtoHttpResult<PutRecordResult>> PutRecord<TRecord>(
            TRecord record,
            Nsid collection,
            RecordKey rKey,
            bool? validate = true,
            Cid? swapCommit = null,
            Cid? swapRecord = null,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default) where TRecord : AtProtoRecord
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(rKey);

            if (!IsAuthenticated)
            {
                Logger.PutRecordFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticationRequiredException();
            }

            AtProtoHttpResult<PutRecordResult> result = await AtProtoServer.PutRecord(
                record: record,
                collection: collection,
                creator: Did,
                rKey: rKey,
                validate: validate,
                swapCommit: swapCommit,
                swapRecord: swapRecord,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                serviceProxy: serviceProxy,
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
        /// Updates or creates a record in the specified collection belonging to the current user.
        /// </summary>
        /// <typeparam name="TRecord">The type of the record value to be updated</typeparam>
        /// <param name="record"><para>The record to be updated or created.</para></param>
        /// <param name="jsonSerializerOptions"><para><see cref="JsonSerializerOptions"/> to use during serialization of <paramref name="record"/>.</para></param>
        /// <param name="collection"><para>The collection the record should be updated or created in.</para></param>
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
        /// <param name="serviceProxy"><para>The service the PDS should proxy the call to, if any.</para></param>
        /// <param name="cancellationToken"><para>A cancellation token that can be used by other objects or threads to receive notice of cancellation.</para></param>
        /// <returns><para>The task object representing the asynchronous operation.</para></returns>
        /// <exception cref="ArgumentNullException">
        ///     <para>Thrown when <paramref name="record"/>, <paramref name="collection"/> or <paramref name="rKey"/> is null.</para>
        /// </exception>
        /// <exception cref="AuthenticationRequiredException"><para>Thrown when the current agent is not authenticated.</para></exception>
        [RequiresDynamicCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        public async Task<AtProtoHttpResult<PutRecordResult>> PutRecord<TRecord>(
            TRecord record,
            JsonSerializerOptions jsonSerializerOptions,
            Nsid collection,
            RecordKey rKey,
            bool? validate = true,
            Cid? swapCommit = null,
            Cid? swapRecord = null,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default) where TRecord : AtProtoRecord
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(rKey);

            if (!IsAuthenticated)
            {
                Logger.PutRecordFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticationRequiredException();
            }

            AtProtoHttpResult<PutRecordResult> result = await AtProtoServer.PutRecord(
                record: record,
                collection: collection,
                creator: Did,
                rKey: rKey,
                validate: validate,
                swapCommit: swapCommit,
                swapRecord: swapRecord,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                serviceProxy: serviceProxy,
                jsonSerializerOptions: jsonSerializerOptions,
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
        /// Updates a record in the specified collection belonging to the current user.
        /// </summary>
        /// <typeparam name="TRecord">The type of the value of record to update.</typeparam>
        /// <param name="repositoryRecord"><para>The record to be updated.</para></param>
        /// <param name="validate">
        ///   <para>Gets a flag indicating what validation will be performed, if any.</para>
        ///   <para>A value of <keyword>true</keyword> requires lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>false</keyword> will skip Lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>null</keyword> to validate record data only for known lexicons.</para>
        ///   <para>Defaults to <keyword>true</keyword>.</para>
        /// </param>
        /// <param name="serviceProxy"><para>The service the PDS should proxy the call to, if any.</para></param>
        /// <param name="cancellationToken"><para>A cancellation token that can be used by other objects or threads to receive notice of cancellation.</para></param>
        /// <returns><para>The task object representing the asynchronous operation.</para></returns>
        /// <exception cref="ArgumentNullException">
        ///     <para>Thrown when <paramref name="repositoryRecord"/> is null.</para>
        /// </exception>
        /// <exception cref="AuthenticationRequiredException"><para>Thrown when the current agent is not authenticated.</para></exception>
        [RequiresDynamicCode("Use a PutRecord overload which takes JsonSerializerOptions instead.")]
        [RequiresUnreferencedCode("Use a PutRecord overload which takes JsonSerializerOptions instead.")]
        public async Task<AtProtoHttpResult<PutRecordResult>> PutRecord<TRecord>(
            AtProtoRepositoryRecord<TRecord> repositoryRecord,
            bool? validate = true,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default) where TRecord : AtProtoRecord
        {
            ArgumentNullException.ThrowIfNull(repositoryRecord);
            ArgumentNullException.ThrowIfNull(repositoryRecord.Value);
            ArgumentNullException.ThrowIfNull(repositoryRecord.Uri.Collection);
            ArgumentNullException.ThrowIfNull(repositoryRecord.Uri.RecordKey);

            if (!IsAuthenticated)
            {
                Logger.PutRecordFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticationRequiredException();
            }

            AtProtoHttpResult<PutRecordResult> result = await AtProtoServer.PutRecord(
                repositoryRecord: repositoryRecord,
                validate: validate,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                serviceProxy: serviceProxy,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Succeeded)
            {
                Logger.PutRecordSucceeded(_logger, result.Result.Uri, result.Result.Cid, repositoryRecord.Uri.Collection, Service);
            }
            else if (result.StatusCode == HttpStatusCode.OK && result.Result is null)
            {
                Logger.PutRecordSucceededButNullResult(_logger, Service);
            }
            else
            {
                Logger.PutRecordFailed(_logger, result.StatusCode, repositoryRecord.Uri.Collection, repositoryRecord.Uri.RecordKey, result.AtErrorDetail?.Error, result.AtErrorDetail?.Message, Service);
            }

            return result;
        }

        /// <summary>
        /// Updates a record in the specified collection belonging to the current user.
        /// </summary>
        /// <typeparam name="TRecord">The type of the value of record to update.</typeparam>
        /// <param name="repositoryRecord"><para>The record to be updated.</para></param>
        /// <param name="jsonSerializerOptions"><para>The <see cref="JsonSerializerOptions"/> to use when serializing <paramref name="repositoryRecord"/></para></param>
        /// <param name="validate">
        ///   <para>Gets a flag indicating what validation will be performed, if any.</para>
        ///   <para>A value of <keyword>true</keyword> requires lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>false</keyword> will skip Lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>null</keyword> to validate record data only for known lexicons.</para>
        ///   <para>Defaults to <keyword>true</keyword>.</para>
        /// </param>
        /// <param name="serviceProxy"><para>The service the PDS should proxy the call to, if any.</para></param>
        /// <param name="cancellationToken"><para>A cancellation token that can be used by other objects or threads to receive notice of cancellation.</para></param>
        /// <returns><para>The task object representing the asynchronous operation.</para></returns>
        /// <exception cref="ArgumentNullException">
        ///     <para>Thrown when <paramref name="repositoryRecord"/> is null.</para>
        /// </exception>
        /// <exception cref="AuthenticationRequiredException"><para>Thrown when the current agent is not authenticated.</para></exception>
        [RequiresDynamicCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        public async Task<AtProtoHttpResult<PutRecordResult>> PutRecord<TRecord>(
            AtProtoRepositoryRecord<TRecord> repositoryRecord,
            JsonSerializerOptions jsonSerializerOptions,
            bool? validate = true,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default) where TRecord : AtProtoRecord
        {
            ArgumentNullException.ThrowIfNull(repositoryRecord);
            ArgumentNullException.ThrowIfNull(repositoryRecord.Value);
            ArgumentNullException.ThrowIfNull(repositoryRecord.Uri.Collection);
            ArgumentNullException.ThrowIfNull(repositoryRecord.Uri.RecordKey);

            if (!IsAuthenticated)
            {
                Logger.PutRecordFailedAsSessionIsAnonymous(_logger);
                throw new AuthenticationRequiredException();
            }

            AtProtoHttpResult<PutRecordResult> result = await AtProtoServer.PutRecord(
                repositoryRecord: repositoryRecord,
                validate: validate,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                serviceProxy: serviceProxy,
                jsonSerializerOptions: jsonSerializerOptions,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Succeeded)
            {
                Logger.PutRecordSucceeded(_logger, result.Result.Uri, result.Result.Cid, repositoryRecord.Uri.Collection, Service);
            }
            else if (result.StatusCode == HttpStatusCode.OK && result.Result is null)
            {
                Logger.PutRecordSucceededButNullResult(_logger, Service);
            }
            else
            {
                Logger.PutRecordFailed(_logger, result.StatusCode, repositoryRecord.Uri.Collection, repositoryRecord.Uri.RecordKey, result.AtErrorDetail?.Error, result.AtErrorDetail?.Message, Service);
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
        /// <typeparam name="TRecord">The type of record to get.</typeparam>
        /// <param name="uri">The <see cref="AtUri"/> of the record to retrieve.</param>
        /// <param name="cid">The CID of the version of the record. If not specified, then return the most recent version.</param>
        /// <param name="service">The service to retrieve the record from.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="uri"/> is in an incorrect format.</exception>
        [RequiresDynamicCode("Use a GetRecord overload which takes JsonSerializerOptions instead.")]
        [RequiresUnreferencedCode("Use a GetRecord overload which takes JsonSerializerOptions instead.")]
        public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<TRecord>>> GetRecord<TRecord>(
            AtUri uri,
            Cid? cid = null,
            Uri? service = null,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default) where TRecord : AtProtoRecord
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

            return await GetRecord<TRecord>(uri.Repo, uri.Collection, uri.RecordKey, cid, service, serviceProxy, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the record specified by the identifying parameters.
        /// </summary>
        /// <typeparam name="TRecord">The type of record to get.</typeparam>
        /// <param name="repo">The <see cref="AtIdentifier"/> of the repo to retrieve the record from.</param>
        /// <param name="collection">The NSID of the collection the record should be retrieved from.</param>
        /// <param name="rKey">The record key, identifying the record to be retrieved.</param>
        /// <param name="cid">The CID of the version of the record. If not specified, then return the most recent version.</param>
        /// <param name="service">The service to retrieve the record from.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="repo"/>, <paramref name="collection"/> is null or empty.</exception>
        [RequiresDynamicCode("Use a GetRecord overload which takes JsonSerializerOptions instead.")]
        [RequiresUnreferencedCode("Use a GetRecord overload which takes JsonSerializerOptions instead.")]
        public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<TRecord>>> GetRecord<TRecord>(
            AtIdentifier repo,
            Nsid collection,
            RecordKey rKey,
            Cid? cid = null,
            Uri? service = null,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default) where TRecord: AtProtoRecord
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

            AtProtoHttpResult<AtProtoRepositoryRecord<TRecord>> result =
                await AtProtoServer.GetRecord<TRecord>(
                    repo: repo,
                    collection: collection,
                    rKey: rKey,
                    cid: cid,
                    service: service,
                    accessCredentials: accessCredentials,
                    httpClient: HttpClient,
                    serviceProxy: serviceProxy,
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
        /// Gets the record specified by the identifying parameters.
        /// </summary>
        /// <typeparam name="TRecord">The type of record to get.</typeparam>
        /// <param name="uri">The <see cref="AtUri"/> of the record to retrieve.</param>
        /// <param name="jsonSerializerOptions">The <see cref="JsonSerializerOptions"/> to use when deserializing <typeparamref name="TRecord"/>.</param>
        /// <param name="cid">The CID of the version of the record. If not specified, then return the most recent version.</param>
        /// <param name="service">The service to retrieve the record from.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="uri"/> is in an incorrect format.</exception>
        [RequiresDynamicCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<TRecord>>> GetRecord<TRecord>(
            AtUri uri,
            JsonSerializerOptions jsonSerializerOptions,
            Cid? cid = null,
            Uri? service = null,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default) where TRecord : AtProtoRecord
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

            return await GetRecord<TRecord>(uri.Repo, uri.Collection, uri.RecordKey, jsonSerializerOptions, cid, service, serviceProxy, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the record specified by the identifying parameters.
        /// </summary>
        /// <typeparam name="TRecord">The type of record to get.</typeparam>
        /// <param name="repo">The <see cref="AtIdentifier"/> of the repo to retrieve the record from.</param>
        /// <param name="collection">The NSID of the collection the record should be retrieved from.</param>
        /// <param name="rKey">The record key, identifying the record to be retrieved.</param>
        /// <param name="cid">The CID of the version of the record. If not specified, then return the most recent version.</param>
        /// <param name="jsonSerializerOptions">The <see cref="JsonSerializerOptions"/> to use when deserializing <typeparamref name="TRecord"/>.</param>
        /// <param name="service">The service to retrieve the record from.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="repo"/>, <paramref name="collection"/> is null or empty.</exception>
        [RequiresDynamicCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<TRecord>>> GetRecord<TRecord>(
            AtIdentifier repo,
            Nsid collection,
            RecordKey rKey,
            JsonSerializerOptions jsonSerializerOptions,
            Cid? cid = null,
            Uri? service = null,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default) where TRecord : AtProtoRecord
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

            AtProtoHttpResult<AtProtoRepositoryRecord<TRecord>> result =
                await AtProtoServer.GetRecord<TRecord>(
                    repo: repo,
                    collection: collection,
                    rKey: rKey,
                    cid: cid,
                    service: service,
                    accessCredentials: accessCredentials,
                    httpClient: HttpClient,
                    serviceProxy: serviceProxy,
                    onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                    jsonSerializerOptions: jsonSerializerOptions,
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
        /// <typeparam name="TRecord">The type of the record value to get.</typeparam>
        /// <param name="collection">The NSID of the collection the records should be retrieved from.</param>
        /// <param name="limit">The number of records to return in each page.</param>
        /// <param name="cursor">The cursor position to start retrieving records from.</param>
        /// <param name="reverse">A flag indicating if records should be listed in reverse order.</param>
        /// <param name="service">The service to retrieve the record from.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="collection"/> is null or empty.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        [RequiresDynamicCode("Use a ListRecords overload which takes JsonSerializerOptions instead.")]
        [RequiresUnreferencedCode("Use a ListRecords overload which takes JsonSerializerOptions instead.")]
        public async Task<AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord<TRecord>>>> ListRecords<TRecord>(
            Nsid collection,
            int? limit = 50,
            string? cursor = null,
            bool reverse = false,
            Uri? service = null,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default) where TRecord : AtProtoRecord
        {
            ArgumentNullException.ThrowIfNull(collection);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await ListRecords<TRecord>(
                repo: Credentials.Did,
                collection: collection,
                limit: limit,
                cursor: cursor,
                reverse: reverse,
                service: service,
                serviceProxy: serviceProxy,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a page of records in the specified <paramref name="collection"/>.
        /// </summary>
        /// <typeparam name="TRecord">The type of the record value to get.</typeparam>
        /// <param name="repo">The <see cref="AtIdentifier"/> of the repo to retrieve the records from.</param>
        /// <param name="collection">The NSID of the collection the records should be retrieved from.</param>
        /// <param name="limit">The number of records to return in each page.</param>
        /// <param name="cursor">The cursor position to start retrieving records from.</param>
        /// <param name="reverse">A flag indicating if records should be listed in reverse order.</param>
        /// <param name="service">The service to retrieve the record from.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="repo"/> or <paramref name="collection"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not an authenticated session.</exception>
        [RequiresDynamicCode("Use a ListRecords overload which takes JsonSerializerOptions instead.")]
        [RequiresUnreferencedCode("Use a ListRecords overload which takes JsonSerializerOptions instead.")]
        public async Task<AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord<TRecord>>>> ListRecords<TRecord>(
            AtIdentifier repo,
            Nsid collection,
            int? limit = 50,
            string? cursor = null,
            bool reverse = false,
            Uri? service = null,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default) where TRecord : AtProtoRecord
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

            AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord<TRecord>>> result = await AtProtoServer.ListRecords<TRecord>(
                repo: repo,
                collection: collection,
                limit: limit,
                cursor: cursor,
                reverse: reverse,
                service: service,
                accessCredentials: accessCredentials,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                httpClient: HttpClient,
                serviceProxy: serviceProxy,
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
        /// Gets a page of records in the specified <paramref name="collection"/> for the current user.
        /// </summary>
        /// <typeparam name="TRecord">The type of the record value to get.</typeparam>
        /// <param name="collection">The NSID of the collection the records should be retrieved from.</param>
        /// <param name="jsonSerializerOptions">The <see cref="JsonSerializerOptions"/> to use when deserializing <typeparamref name="TRecord"/>.</param>
        /// <param name="limit">The number of records to return in each page.</param>
        /// <param name="cursor">The cursor position to start retrieving records from.</param>
        /// <param name="reverse">A flag indicating if records should be listed in reverse order.</param>
        /// <param name="service">The service to retrieve the record from.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="collection"/> is null or empty.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        [RequiresDynamicCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        public async Task<AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord<TRecord>>>> ListRecords<TRecord>(
            Nsid collection,
            JsonSerializerOptions jsonSerializerOptions,
            int? limit = 50,
            string? cursor = null,
            bool reverse = false,
            Uri? service = null,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default) where TRecord : AtProtoRecord
        {
            ArgumentNullException.ThrowIfNull(collection);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await ListRecords<TRecord>(
                repo: Credentials.Did,
                collection: collection,
                jsonSerializerOptions: jsonSerializerOptions,
                limit: limit,
                cursor: cursor,
                reverse: reverse,
                service: service,
                serviceProxy: serviceProxy,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a page of records in the specified <paramref name="collection"/>.
        /// </summary>
        /// <typeparam name="TRecord">The type of the record value to get.</typeparam>
        /// <param name="repo">The <see cref="AtIdentifier"/> of the repo to retrieve the records from.</param>
        /// <param name="collection">The NSID of the collection the records should be retrieved from.</param>
        /// <param name="jsonSerializerOptions">The <see cref="JsonSerializerOptions"/> to use when deserializing <typeparamref name="TRecord"/>.</param>
        /// <param name="limit">The number of records to return in each page.</param>
        /// <param name="cursor">The cursor position to start retrieving records from.</param>
        /// <param name="reverse">A flag indicating if records should be listed in reverse order.</param>
        /// <param name="service">The service to retrieve the record from.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="repo"/> or <paramref name="collection"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not an authenticated session.</exception>
        [RequiresDynamicCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        public async Task<AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord<TRecord>>>> ListRecords<TRecord>(
            AtIdentifier repo,
            Nsid collection,
            JsonSerializerOptions jsonSerializerOptions,
            int? limit = 50,
            string? cursor = null,
            bool reverse = false,
            Uri? service = null,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default) where TRecord : AtProtoRecord
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

            AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord<TRecord>>> result = await AtProtoServer.ListRecords<TRecord>(
                repo: repo,
                collection: collection,
                limit: limit,
                cursor: cursor,
                reverse: reverse,
                service: service,
                accessCredentials: accessCredentials,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                httpClient: HttpClient,
                jsonSerializerOptions: jsonSerializerOptions,
                serviceProxy: serviceProxy,
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
        /// <param name="fileName">The name of the file to be uploaded as a blob.</param>
        /// <param name="mimeType">The mime type of the blob to be created.</param>
        /// <param name="service">The service to upload the blob to.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="fileName"/> or if <paramref name="mimeType"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="fileName"/> or if <paramref name="mimeType"/> is empty.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not an authenticated session.</exception>
        public async Task<AtProtoHttpResult<Blob>> UploadBlob(
            string fileName,
            string mimeType,
            Uri? service = null,
            string? serviceProxy = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(fileName);
            ArgumentException.ThrowIfNullOrEmpty(mimeType);

            service ??= Service;

            if (!IsAuthenticated)
            {
                Logger.UploadBlobFailedAsSessionIsAnonymous(_logger, service);

                throw new AuthenticationRequiredException();
            }

            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException("File not found", fileName: fileName);
            }

            byte[] blob = await File.ReadAllBytesAsync(fileName, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (blob.Length == 0)
            {
                Logger.UploadBlobFailedAsFileLengthIsZero(_logger, service, fileName);
                throw new ArgumentException("file length cannot be 0.", nameof(fileName));
            }

            try
            {
                return await AtProtoServer.UploadBlob(
                    blob: blob,
                    mimeType: mimeType,
                    service: service,
                    accessCredentials: Credentials,
                    httpClient: HttpClient,
                    serviceProxy: serviceProxy,
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
        /// Uploads a blob, to be referenced from a repository record.
        /// </summary>
        /// <param name="blob">The blob to upload.</param>
        /// <param name="mimeType">The mime type of the blob to upload.</param>
        /// <param name="service">The service to upload the blob to.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="blob"/> has a zero length or if <paramref name="mimeType"/> is null or empty.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not an authenticated session.</exception>
        public async Task<AtProtoHttpResult<Blob>> UploadBlob(
            byte[] blob,
            string mimeType,
            Uri? service = null,
            string? serviceProxy = null,
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
                    serviceProxy: serviceProxy,
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
    }
}
