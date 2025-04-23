// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging;

using idunno.AtProto.Repo;
using idunno.AtProto.Repo.Models;
using idunno.AtProto.Authentication;


namespace idunno.AtProto
{
    /// <summary>
    /// Represents an atproto server and provides methods to send messages and receive responses from the server.
    /// </summary>
    public static partial class AtProtoServer
    {
        // https://docs.bsky.app/docs/api/com-atproto-repo-apply-writes
        internal const string ApplyWritesEndpoint = "/xrpc/com.atproto.repo.applyWrites";

        // https://docs.bsky.app/docs/api/com-atproto-repo-create-record
        internal const string CreateRecordEndpoint = "/xrpc/com.atproto.repo.createRecord";

        // https://docs.bsky.app/docs/api/com-atproto-repo-delete-record
        internal const string DeleteRecordEndpoint = "/xrpc/com.atproto.repo.deleteRecord";

        // https://docs.bsky.app/docs/api/com-atproto-repo-put-record
        internal const string PutRecordEndpoint = "/xrpc/com.atproto.repo.putRecord";

        // https://docs.bsky.app/docs/api/com-atproto-repo-describe-repo
        internal const string DescribeRepoEndpoint = "/xrpc/com.atproto.repo.describeRepo";

        // https://docs.bsky.app/docs/api/com-atproto-repo-get-record
        internal const string GetRecordEndpoint = "/xrpc/com.atproto.repo.getRecord";

        // https://docs.bsky.app/docs/api/com-atproto-repo-list-records
        internal const string ListRecordsEndpoint = "/xrpc/com.atproto.repo.ListRecords";

        // https://docs.bsky.app/docs/api/com-atproto-repo-upload-blob
        internal const string UploadBlobEndpoint = "/xrpc/com.atproto.repo.uploadBlob";

        /// <summary>
        /// Performs a collection of creates, updates, and delete operations within a transaction against the specified repo. Requires authentication.
        /// </summary>
        /// <param name="operations">A collection of write operations to perform in a transaction</param>
        /// <param name="repo">The <see cref="Did"/> of the repo to perform the operations against.</param>
        /// <param name="validate">
        ///     Flag indicating what level of validation the api should perform.
        ///     If false skips lexicon schema validation of record data across all operations.
        ///     If true requires validation
        ///     if null validates only for known lexicons.
        ///</param>
        /// <param name="cid">
        ///   Optional commit ID. If provided, the entire operation will fail if the current repo commit CID does not match this value.
        ///   Used to prevent conflicting repo mutations.
        ///</param>
        /// <param name="service">The service to create the record on.</param>
        /// <param name="accessCredentials">Access credentials for the specified service.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="operations"/>, <paramref name="repo"/>, <paramref name="service"/>,
        /// <paramref name="accessCredentials"/>, or <paramref name="httpClient"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="operations"/> is an empty collection.</exception>
        [RequiresUnreferencedCode("Use a ApplyWrites overload which takes JsonSerializerOptions instead.")]
        [RequiresDynamicCode("Use a ApplyWrites overload which takes JsonSerializerOptions instead.")]
        public static async Task<AtProtoHttpResult<ApplyWritesResults>> ApplyWrites(
            ICollection<WriteOperation> operations,
            Did repo,
            bool? validate,
            Cid? cid,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            string? serviceProxy = null,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operations);
            ArgumentNullException.ThrowIfNull(repo);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (operations.Count == 0)
            {
                throw new ArgumentException("cannot be an empty collection.", nameof(operations));
            }

            ICollection<ApplyWritesRequestValueBase> mappedOperations = [];

            foreach (WriteOperation operation in operations)
            {
                switch (operation)
                {
                    case CreateOperation createOperation:
                        {
                            JsonNode? value = JsonNode.Parse(JsonSerializer.Serialize(createOperation.RecordValue, DefaultJsonSerializerOptionsWithNoTypeResolution));
                            if (value is not null)
                            {
                                mappedOperations.Add(new ApplyWritesCreateRequest(createOperation.Collection, createOperation.RecordKey, value));
                            }
                            break;
                        }

                    case UpdateOperation putOperation:
                        {
                            JsonNode? value = JsonNode.Parse(JsonSerializer.Serialize(putOperation.RecordValue, DefaultJsonSerializerOptionsWithNoTypeResolution));
                            if (value is not null)
                            {
                                mappedOperations.Add(new ApplyWritesUpdateRequest(putOperation.Collection, putOperation.RecordKey!, value));
                            }
                            break;
                        }

                    case DeleteOperation deleteOperation:
                        mappedOperations.Add(new ApplyWritesDeleteRequest(deleteOperation.Collection, deleteOperation.RecordKey!));
                        break;

                }
            }

            ApplyWritesRequest request = new(repo, validate, mappedOperations, cid);

            AtProtoHttpClient<ApplyWritesResponse> client;

            if (string.IsNullOrWhiteSpace(serviceProxy))
            {
                client = new(loggerFactory);
            }
            else
            {
                client = new(serviceProxy, loggerFactory);
            }

            AtProtoHttpResult<ApplyWritesResponse> response = await client.Post(
                service,
                ApplyWritesEndpoint,
                request,
                accessCredentials,
                httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                jsonSerializerOptions: AtProtoJsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<ApplyWritesResults>(
                    result: new ApplyWritesResults(response.Result),
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<ApplyWritesResults>(
                    result: null,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
        }

        /// <summary>
        /// Performs a collection of creates, updates, and delete operations within a transaction against the specified repo. Requires authentication.
        /// </summary>
        /// <param name="operations">A collection of write operations to perform in a transaction</param>
        /// <param name="jsonSerializerOptions">The <see cref="JsonSerializerOptions"/> to use when serializing the record values within an create or update operation.</param>
        /// <param name="repo">The <see cref="Did"/> of the repo to perform the operations against.</param>
        /// <param name="validate">
        ///     Flag indicating what level of validation the api should perform.
        ///     If false skips lexicon schema validation of record data across all operations.
        ///     If true requires validation
        ///     if null validates only for known lexicons.
        ///</param>
        /// <param name="cid">
        ///   Optional commit ID. If provided, the entire operation will fail if the current repo commit CID does not match this value.
        ///   Used to prevent conflicting repo mutations.
        ///</param>
        /// <param name="service">The service to create the record on.</param>
        /// <param name="accessCredentials">Access credentials for the specified service.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="operations"/>, <paramref name="repo"/>, <paramref name="service"/>,
        /// <paramref name="accessCredentials"/>, or <paramref name="httpClient"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="operations"/> is an empty collection.</exception>
        [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        [RequiresDynamicCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        public static async Task<AtProtoHttpResult<ApplyWritesResults>> ApplyWrites(
            ICollection<WriteOperation> operations,
            JsonSerializerOptions jsonSerializerOptions,
            Did repo,
            bool? validate,
            Cid? cid,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            string? serviceProxy = null,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(operations);
            ArgumentNullException.ThrowIfNull(repo);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentNullException.ThrowIfNull(jsonSerializerOptions);

            if (operations.Count == 0)
            {
                throw new ArgumentException("cannot be an empty collection.", nameof(operations));
            }

            ICollection<ApplyWritesRequestValueBase> mappedOperations = [];

            foreach (WriteOperation operation in operations)
            {
                switch (operation)
                {
                    case CreateOperation createOperation:
                        {
                            JsonNode? value = JsonNode.Parse(JsonSerializer.Serialize(createOperation.RecordValue, jsonSerializerOptions));
                            if (value is not null)
                            {
                                mappedOperations.Add(new ApplyWritesCreateRequest(createOperation.Collection, createOperation.RecordKey, value));
                            }
                            break;
                        }

                    case UpdateOperation putOperation:
                        {
                            JsonNode? value = JsonNode.Parse(JsonSerializer.Serialize(putOperation.RecordValue, jsonSerializerOptions));
                            if (value is not null)
                            {
                                mappedOperations.Add(new ApplyWritesUpdateRequest(putOperation.Collection, putOperation.RecordKey!, value));
                            }
                            break;
                        }

                    case DeleteOperation deleteOperation:
                        mappedOperations.Add(new ApplyWritesDeleteRequest(deleteOperation.Collection, deleteOperation.RecordKey!));
                        break;
                }
            }

            ApplyWritesRequest request = new(repo, validate, mappedOperations, cid);

            AtProtoHttpClient<ApplyWritesResponse> client;

            if (string.IsNullOrWhiteSpace(serviceProxy))
            {
                client = new(loggerFactory);
            }
            else
            {
                client = new(serviceProxy, loggerFactory);
            }

            AtProtoHttpResult<ApplyWritesResponse> response = await client.Post(
                service,
                ApplyWritesEndpoint,
                request,
                accessCredentials,
                httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                jsonSerializerOptions: AtProtoJsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<ApplyWritesResults>(
                    result: new ApplyWritesResults(response.Result),
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<ApplyWritesResults>(
                    result: null,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
        }

        /// <summary>
        /// Creates an atproto record in the specified collection. Requires authentication.
        /// </summary>
        /// <typeparam name="TRecord">The type of the record to create.</typeparam>
        /// <param name="record"><para>A json representation of record to be created.</para></param>
        /// <param name="collection"><para>The NSID of collection the record should be created in.</para></param>
        /// <param name="creator"><para>The <see cref="Did"/> of the creating actor.</para></param>
        /// <param name="rKey"><para>The record key, if any, of the record to be created.</para></param>
        /// <param name="validate">
        ///   <para>Flag indicating what validation will be performed, if any.</para>
        ///   <para>A value of <keyword>true</keyword> requires lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>false</keyword> will skip Lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>null</keyword> to validate record data only for known lexicons.</para>
        ///   <para>Defaults to <keyword>true</keyword>.</para>
        /// </param>
        /// <param name="swapCommit"><para>The <see cref="Cid"/>, if any, to compare and swap with.</para></param>
        /// <param name="service"><para>The service to create the record on.</para></param>
        /// <param name="accessCredentials"><para><see cref="AccessCredentials"/> for the specified service.</para></param>
        /// <param name="httpClient"><para>An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</para></param>
        /// <param name="serviceProxy"><para>The service the PDS should proxy the call to, if any.</para></param>
        /// <param name="onCredentialsUpdated"><para>An <see cref="Action{T}" /> to call if the credentials in the request need updating.</para></param>
        /// <param name="loggerFactory"><para>An instance of <see cref="ILoggerFactory"/> to use to create a logger.</para></param>
        /// <param name="cancellationToken"><para>A cancellation token that can be used by other objects or threads to receive notice of cancellation.</para></param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="accessCredentials"/>, or <paramref name="httpClient"/> is null.
        /// </exception>
        [RequiresDynamicCode("Use a CreateRecord overload which takes JsonSerializerOptions instead.")]
        [RequiresUnreferencedCode("Use a CreateRecord overload which takes JsonSerializerOptions instead.")]
        public static async Task<AtProtoHttpResult<CreateRecordResult>> CreateRecord<TRecord>(
            TRecord record,
            Nsid collection,
            Did creator,
            RecordKey? rKey,
            bool? validate,
            Cid? swapCommit,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            string? serviceProxy = null,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default) where TRecord : AtProtoRecordValue
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(creator);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            JsonNode? serializedRecord = JsonSerializer.SerializeToNode(record, DefaultJsonSerializerOptionsWithNoTypeResolution) ?? throw new ArgumentException("Record cannot be serialized.", nameof(record));

            CreateRecordRequest request = new(serializedRecord, collection, creator, validate, rKey, swapCommit);

            AtProtoHttpClient<CreateRecordResponse> client;

            if (string.IsNullOrWhiteSpace(serviceProxy))
            {
                client = new(loggerFactory);
            }
            else
            {
                client = new(serviceProxy, loggerFactory);
            }

            AtProtoHttpResult<CreateRecordResponse> response = await client.Post(
                service,
                CreateRecordEndpoint,
                request,
                accessCredentials,
                httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<CreateRecordResult>(
                    result: new CreateRecordResult(response.Result),
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<CreateRecordResult>(
                    result: null,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
        }

        /// <summary>
        /// Creates an atproto record in the specified collection. Requires authentication.
        /// </summary>
        /// <typeparam name="TRecord">The type of the record to create.</typeparam>
        /// <param name="record"><para>A json representation of record to be created.</para></param>
        /// <param name="jsonSerializerOptions"><para><see cref="JsonSerializerOptions"/> to use when deserializing <typeparamref name="TRecord"/>.</para></param>
        /// <param name="collection"><para>The NSID of collection the record should be created in.</para></param>
        /// <param name="creator"><para>The <see cref="Did"/> of the creating actor.</para></param>
        /// <param name="rKey"><para>The record key, if any, of the record to be created.</para></param>
        /// <param name="validate">
        ///   <para>Flag indicating what validation will be performed, if any.</para>
        ///   <para>A value of <keyword>true</keyword> requires lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>false</keyword> will skip Lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>null</keyword> to validate record data only for known lexicons.</para>
        ///   <para>Defaults to <keyword>true</keyword>.</para>
        /// </param>
        /// <param name="swapCommit"><para>The <see cref="Cid"/>, if any, to compare and swap with.</para></param>
        /// <param name="service"><para>The service to create the record on.</para></param>
        /// <param name="accessCredentials"><para><see cref="AccessCredentials"/> for the specified service.</para></param>
        /// <param name="httpClient"><para>An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</para></param>
        /// <param name="serviceProxy"><para>The service the PDS should proxy the call to, if any.</para></param>
        /// <param name="onCredentialsUpdated"><para>An <see cref="Action{T}" /> to call if the credentials in the request need updating.</para></param>
        /// <param name="loggerFactory"><para>An instance of <see cref="ILoggerFactory"/> to use to create a logger.</para></param>
        /// <param name="cancellationToken"><para>A cancellation token that can be used by other objects or threads to receive notice of cancellation.</para></param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="accessCredentials"/>, or <paramref name="httpClient"/> is null.
        /// </exception>
        [RequiresDynamicCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        public static async Task<AtProtoHttpResult<CreateRecordResult>> CreateRecord<TRecord>(
            TRecord record,
            JsonSerializerOptions jsonSerializerOptions,
            Nsid collection,
            Did creator,
            RecordKey? rKey,
            bool? validate,
            Cid? swapCommit,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            string? serviceProxy = null,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default) where TRecord : AtProtoRecordValue
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(creator);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            // To avoid callers having to json codegen PutRecordRequest<theirRecord> we manually serialize.
            // We don't mutate the parameter value because it will, in turn, be passed down to the AtProtoHttpClient.
            JsonNode? serializedRecord = JsonSerializer.SerializeToNode(record, jsonSerializerOptions) ?? throw new ArgumentException("Record cannot be serialized.", nameof(record));

            CreateRecordRequest request = new(serializedRecord, collection, creator, validate, rKey, swapCommit);

            AtProtoHttpClient<CreateRecordResponse> client;

            if (string.IsNullOrWhiteSpace(serviceProxy))
            {
                client = new(loggerFactory);
            }
            else
            {
                client = new(serviceProxy, loggerFactory);
            }

            AtProtoHttpResult<CreateRecordResponse> response = await client.Post(
                service,
                CreateRecordEndpoint,
                request,
                accessCredentials,
                httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                jsonSerializerOptions: jsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<CreateRecordResult>(
                    result: new CreateRecordResult(response.Result),
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<CreateRecordResult>(
                    result: null,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
        }

        /// <summary>
        /// Deletes an atproto record, specified by its rKey, from specified repo/collection. Requires authentication.
        /// </summary>
        /// <param name="repo">The handle or Did of the repo to delete from. Typically this is the Did of the account that created the record.</param>
        /// <param name="collection">The NSID of the collection the record should be deleted from.</param>
        /// <param name="rKey">The record key, identifying the record to be deleted.</param>
        /// <param name="swapRecord">Specified if the operation should compare and swap with the previous record by cid.</param>
        /// <param name="swapCommit">Specified if the operation should compare and swap with the previous commit by cid.</param>
        /// <param name="service">The service to delete the record from.</param>
        /// <param name="accessCredentials"><see cref="AccessCredentials"/> for the specified <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="repo"/>, <paramref name="collection"/>, <paramref name="rKey"/>, <paramref name="service"/>,
        /// <paramref name="accessCredentials"/>, or <paramref name="httpClient"/> is null.
        /// </exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage(
            "AOT",
            "IL3050:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<Commit>> DeleteRecord(
            AtIdentifier repo,
            Nsid collection,
            RecordKey rKey,
            Cid? swapRecord,
            Cid? swapCommit,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            string? serviceProxy = null,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(repo);
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(rKey);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            DeleteRecordRequest deleteRecordRequest = new(repo, collection, rKey) { SwapRecord = swapRecord, SwapCommit = swapCommit };

            AtProtoHttpClient<DeleteRecordResponse> client;

            if (string.IsNullOrWhiteSpace(serviceProxy))
            {
                client = new(loggerFactory);
            }
            else
            {
                client = new(serviceProxy, loggerFactory);
            }

            AtProtoHttpResult<DeleteRecordResponse> response =  await client.Post(
                service,
                DeleteRecordEndpoint,
                deleteRecordRequest,
                accessCredentials,
                httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                jsonSerializerOptions : AtProtoJsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<Commit>(
                    response.Result.Commit,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<Commit>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Updates the specified atproto record. Requires authentication.
        /// </summary>
        /// <typeparam name="TRecordValue">The type of the value of record to update.</typeparam>
        /// <param name="record"><para>The record to update.</para></param>
        /// <param name="validate">
        ///   <para>Flag indicating what validation will be performed, if any.</para>
        ///   <para>A value of <keyword>true</keyword> requires lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>false</keyword> will skip Lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>null</keyword> to validate record data only for known lexicons.</para>
        ///   <para>Defaults to <keyword>true</keyword>.</para>
        /// </param>
        /// <param name="swapCommit"><para>The <see cref="Cid"/> of the commit, if any, to compare and swap with.</para></param>
        /// <param name="swapRecord"><para>The <see cref="Cid"/> of the record, if any, to compare and swap with.</para></param>
        /// <param name="service"><para>The service to create the record on.</para></param>
        /// <param name="accessCredentials"><para><see cref="AccessCredentials"/> for the specified service</para></param>
        /// <param name="httpClient"><para>An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</para></param>
        /// <param name="serviceProxy"><para>The service the PDS should proxy the call to, if any.</para></param>
        /// <param name="onCredentialsUpdated"><para>An <see cref="Action{T}" /> to call if the credentials in the request need updating.</para></param>
        /// <param name="loggerFactory"><para>An instance of <see cref="ILoggerFactory"/> to use to create a logger.</para></param>
        /// <param name="cancellationToken"><para>A cancellation token that can be used by other objects or threads to receive notice of cancellation.</para></param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="record"/>, <paramref name="service"/>, <paramref name="accessCredentials"/>, or <paramref name="httpClient"/> is null.
        /// </exception>
        [RequiresDynamicCode("Use a PutRecord overload which takes JsonSerializerOptions instead.")]
        [RequiresUnreferencedCode("Use a PutRecord overload which takes JsonSerializerOptions instead.")]
        public static async Task<AtProtoHttpResult<PutRecordResult>> PutRecord<TRecordValue>(
            AtProtoRecord<TRecordValue> record,
            bool? validate,
            Cid? swapCommit,
            Cid? swapRecord,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            string? serviceProxy = null,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
                where TRecordValue : AtProtoRecordValue
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(record.Value);
            ArgumentNullException.ThrowIfNull(record.Uri.Collection);
            ArgumentNullException.ThrowIfNull(record.Uri.RecordKey);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            return await PutRecord(
                recordValue: record.Value,
                collection: record.Uri.Collection,
                creator: record.Uri.Repo,
                rKey: record.Uri.RecordKey,
                validate: validate,
                swapCommit: swapCommit,
                swapRecord: swapRecord,
                service: service,
                accessCredentials: accessCredentials,
                httpClient: httpClient,
                serviceProxy: serviceProxy,
                onCredentialsUpdated: onCredentialsUpdated,
                loggerFactory: loggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates or creates an atproto record in the specified collection. Requires authentication.
        /// </summary>
        /// <typeparam name="TRecordValue">The type of the record to update or create.</typeparam>
        /// <param name="recordValue"><para>A json representation of record to be created.</para></param>
        /// <param name="collection"><para>The NSID of collection the record should be created in.</para></param>
        /// <param name="creator"><para>The <see cref="AtIdentifier"/> of the creating actor.</para></param>
        /// <param name="rKey"><para>The record key, if any, of the record to be created.</para></param>
        /// <param name="validate">
        ///   <para>Flag indicating what validation will be performed, if any.</para>
        ///   <para>A value of <keyword>true</keyword> requires lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>false</keyword> will skip Lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>null</keyword> to validate record data only for known lexicons.</para>
        ///   <para>Defaults to <keyword>true</keyword>.</para>
        /// </param>
        /// <param name="swapCommit"><para>The <see cref="Cid"/> of the commit, if any, to compare and swap with.</para></param>
        /// <param name="swapRecord"><para>The <see cref="Cid"/> of the record, if any, to compare and swap with.</para></param>
        /// <param name="service"><para>The service to create the record on.</para></param>
        /// <param name="accessCredentials"><para><see cref="AccessCredentials"/> for the specified service</para></param>
        /// <param name="httpClient"><para>An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</para></param>
        /// <param name="serviceProxy"><para>The service the PDS should proxy the call to, if any.</para></param>
        /// <param name="onCredentialsUpdated"><para>An <see cref="Action{T}" /> to call if the credentials in the request need updating.</para></param>
        /// <param name="loggerFactory"><para>An instance of <see cref="ILoggerFactory"/> to use to create a logger.</para></param>
        /// <param name="cancellationToken"><para>A cancellation token that can be used by other objects or threads to receive notice of cancellation.</para></param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="recordValue"/>, <paramref name="collection"/>, <paramref name="creator"/>, <paramref name="rKey"/>, <paramref name="service"/>,
        /// <paramref name="accessCredentials"/>, or <paramref name="httpClient"/> is null.
        /// </exception>
        [RequiresDynamicCode("Use a PutRecord overload which takes JsonSerializerOptions instead.")]
        [RequiresUnreferencedCode("Use a PutRecord overload which takes JsonSerializerOptions instead.")]
        public static async Task<AtProtoHttpResult<PutRecordResult>> PutRecord<TRecordValue>(
            TRecordValue recordValue,
            Nsid collection,
            AtIdentifier creator,
            RecordKey rKey,
            bool? validate,
            Cid? swapCommit,
            Cid? swapRecord,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            string? serviceProxy = null,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default) where TRecordValue : AtProtoRecordValue
        {
            ArgumentNullException.ThrowIfNull(recordValue);
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(creator);
            ArgumentNullException.ThrowIfNull(rKey);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            // To avoid callers having to json codegen PutRecordRequest<theirRecord> we manually serialize.
            // We don't mutate the parameter value because it will, in turn, be passed down to the AtProtoHttpClient.

            JsonNode? serializedRecord = JsonSerializer.SerializeToNode(recordValue, DefaultJsonSerializerOptionsWithNoTypeResolution) ?? throw new ArgumentException("Record cannot be serialized.", nameof(recordValue));

            PutRecordRequest request = new(serializedRecord, collection, creator, rKey, validate, swapCommit, swapRecord);

            AtProtoHttpClient<PutRecordResponse> client;

            if (string.IsNullOrWhiteSpace(serviceProxy))
            {
                client = new(loggerFactory);
            }
            else
            {
                client = new(serviceProxy, loggerFactory);
            }

            AtProtoHttpResult<PutRecordResponse> response =await client.Post(
                service,
                PutRecordEndpoint,
                request,
                accessCredentials,
                httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<PutRecordResult>(
                    result: new PutRecordResult(response.Result),
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<PutRecordResult>(
                    result: null,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
        }

        /// <summary>
        /// Updates the specified atproto record. Requires authentication.
        /// </summary>
        /// <typeparam name="TRecordValue">The type of the value of record to update.</typeparam>
        /// <param name="record"><para>The record to update.</para></param>
        /// <param name="validate">
        ///   <para>Flag indicating what validation will be performed, if any.</para>
        ///   <para>A value of <keyword>true</keyword> requires lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>false</keyword> will skip Lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>null</keyword> to validate record data only for known lexicons.</para>
        ///   <para>Defaults to <keyword>true</keyword>.</para>
        /// </param>
        /// <param name="swapCommit"><para>The <see cref="Cid"/> of the commit, if any, to compare and swap with.</para></param>
        /// <param name="swapRecord"><para>The <see cref="Cid"/> of the record, if any, to compare and swap with.</para></param>
        /// <param name="service"><para>The service to create the record on.</para></param>
        /// <param name="accessCredentials"><para><see cref="AccessCredentials"/> for the specified service</para></param>
        /// <param name="httpClient"><para>An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</para></param>
        /// <param name="serviceProxy"><para>The service the PDS should proxy the call to, if any.</para></param>
        /// <param name="onCredentialsUpdated"><para>An <see cref="Action{T}" /> to call if the credentials in the request need updating.</para></param>
        /// <param name="loggerFactory"><para>An instance of <see cref="ILoggerFactory"/> to use to create a logger.</para></param>
        /// <param name="jsonSerializerOptions"><para><see cref="JsonSerializerOptions"/> to use when serializing <typeparamref name="TRecordValue"/>.</para></param>
        /// <param name="cancellationToken"><para>A cancellation token that can be used by other objects or threads to receive notice of cancellation.</para></param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="record"/>, <paramref name="service"/>, <paramref name="accessCredentials"/>, or <paramref name="httpClient"/> is null.
        /// </exception>
        [RequiresDynamicCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        public static async Task<AtProtoHttpResult<PutRecordResult>> PutRecord<TRecordValue>(
            AtProtoRecord<TRecordValue> record,
            JsonSerializerOptions jsonSerializerOptions,
            bool? validate,
            Cid? swapCommit,
            Cid? swapRecord,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            string? serviceProxy = null,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
                where TRecordValue : AtProtoRecordValue
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(record.Value);
            ArgumentNullException.ThrowIfNull(record.Uri.Collection);
            ArgumentNullException.ThrowIfNull(record.Uri.RecordKey);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            return await PutRecord(
                recordValue: record.Value,
                collection: record.Uri.Collection,
                creator: record.Uri.Repo,
                rKey: record.Uri.RecordKey,
                validate: validate,
                swapCommit: swapCommit,
                swapRecord: swapRecord,
                service: service,
                accessCredentials: accessCredentials,
                httpClient: httpClient,
                serviceProxy: serviceProxy,
                onCredentialsUpdated: onCredentialsUpdated,
                loggerFactory: loggerFactory,
                jsonSerializerOptions: jsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates or creates an atproto record in the specified collection. Requires authentication.
        /// </summary>
        /// <typeparam name="TRecordValue">The type of the record to update or create.</typeparam>
        /// <param name="recordValue"><para>A json representation of record to be created.</para></param>
        /// <param name="collection"><para>The NSID of collection the record should be created in.</para></param>
        /// <param name="creator"><para>The <see cref="AtIdentifier"/> of the creating actor.</para></param>
        /// <param name="rKey"><para>The record key, if any, of the record to be created.</para></param>
        /// <param name="validate">
        ///   <para>Flag indicating what validation will be performed, if any.</para>
        ///   <para>A value of <keyword>true</keyword> requires lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>false</keyword> will skip Lexicon schema validation of record data.</para>
        ///   <para>A value of <keyword>null</keyword> to validate record data only for known lexicons.</para>
        ///   <para>Defaults to <keyword>true</keyword>.</para>
        /// </param>
        /// <param name="swapCommit"><para>The <see cref="Cid"/> of the commit, if any, to compare and swap with.</para></param>
        /// <param name="swapRecord"><para>The <see cref="Cid"/> of the record, if any, to compare and swap with.</para></param>
        /// <param name="service"><para>The service to create the record on.</para></param>
        /// <param name="accessCredentials"><para><see cref="AccessCredentials"/> for the specified service</para></param>
        /// <param name="httpClient"><para>An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</para></param>
        /// <param name="serviceProxy"><para>The service the PDS should proxy the call to, if any.</para></param>
        /// <param name="onCredentialsUpdated"><para>An <see cref="Action{T}" /> to call if the credentials in the request need updating.</para></param>
        /// <param name="loggerFactory"><para>An instance of <see cref="ILoggerFactory"/> to use to create a logger.</para></param>
        /// <param name="jsonSerializerOptions"><para><see cref="JsonSerializerOptions"/> to apply during deserialization.</para></param>
        /// <param name="cancellationToken"><para>A cancellation token that can be used by other objects or threads to receive notice of cancellation.</para></param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="recordValue"/>, <paramref name="collection"/>, <paramref name="creator"/>, <paramref name="rKey"/>, <paramref name="service"/>,
        /// <paramref name="accessCredentials"/>, or <paramref name="httpClient"/> is null.
        /// </exception>
        [RequiresDynamicCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        public static async Task<AtProtoHttpResult<PutRecordResult>> PutRecord<TRecordValue>(
            TRecordValue recordValue,
            JsonSerializerOptions jsonSerializerOptions,
            Nsid collection,
            AtIdentifier creator,
            RecordKey rKey,
            bool? validate,
            Cid? swapCommit,
            Cid? swapRecord,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            string? serviceProxy = null,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default) where TRecordValue: AtProtoRecordValue
        {
            ArgumentNullException.ThrowIfNull(recordValue);
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(creator);
            ArgumentNullException.ThrowIfNull(rKey);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            // To avoid callers having to json codegen PutRecordRequest<theirRecord> we manually serialize.
            // We don't mutate the parameter value because it will, in turn, be passed down to the AtProtoHttpClient.
            JsonNode? serializedRecord = JsonSerializer.SerializeToNode(recordValue, jsonSerializerOptions) ?? throw new ArgumentException("Record cannot be serialized.", nameof(recordValue));

            PutRecordRequest request = new(serializedRecord, collection, creator, rKey, validate, swapCommit, swapRecord);

            AtProtoHttpClient<PutRecordResponse> client;
            jsonSerializerOptions ??= AtProtoJsonSerializerOptions;

            if (string.IsNullOrWhiteSpace(serviceProxy))
            {
                client = new(loggerFactory);
            }
            else
            {
                client = new(serviceProxy, loggerFactory);
            }

            AtProtoHttpResult<PutRecordResponse> response = await client.Post(
                service,
                PutRecordEndpoint,
                request,
                accessCredentials,
                httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                jsonSerializerOptions: jsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<PutRecordResult>(
                    result: new PutRecordResult(response.Result),
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<PutRecordResult>(
                    result: null,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
        }

        /// <summary>
        /// Gets the record specified by the identifying parameters. May require authentication.
        /// </summary>
        /// <typeparam name="TRecord">The type of record to get.</typeparam>
        /// <param name="repo">The <see cref="AtIdentifier"/> of the repo to retrieve the record from.</param>
        /// <param name="collection">The NSID of the collection the record should be deleted from.</param>
        /// <param name="rKey">The record key, identifying the record to be deleted.</param>
        /// <param name="cid">The CID of the version of the record. If not specified, then return the most recent version.</param>
        /// <param name="service">The service to retrieve the record from.</param>
        /// <param name="accessCredentials">Optional access credentials for the specified service.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="repo"/>, <paramref name="collection"/>, <paramref name="rKey"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.
        /// </exception>
        [RequiresDynamicCode("Use a Get overload which takes JsonSerializerOptions instead.")]
        [RequiresUnreferencedCode("Use a Get overload which takes JsonSerializerOptions instead.")]
        public static async Task<AtProtoHttpResult<TRecord>> GetRecord<TRecord>(
            AtIdentifier repo,
            Nsid collection,
            RecordKey rKey,
            Cid? cid,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            string? serviceProxy = null,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default) where TRecord : class
        {
            ArgumentNullException.ThrowIfNull(repo);
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(rKey);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<TRecord> client;

            if (string.IsNullOrWhiteSpace(serviceProxy))
            {
                client = new(loggerFactory);
            }
            else
            {
                client = new(serviceProxy, loggerFactory);
            }

            string queryString = $"repo={Uri.EscapeDataString(repo.ToString())}&collection={Uri.EscapeDataString(collection.ToString())}&rkey={Uri.EscapeDataString(rKey.ToString())}";

            if (cid is not null)
            {
                queryString += $"&cid={Uri.EscapeDataString(cid.ToString())}";
            }

            return await client.Get(
                service,
                $"{GetRecordEndpoint}?{queryString}",
                accessCredentials,
                httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the record specified by the identifying parameters. May require authentication.
        /// </summary>
        /// <typeparam name="TRecord">The type of record to get.</typeparam>
        /// <param name="repo">The <see cref="AtIdentifier"/> of the repo to retrieve the record from.</param>
        /// <param name="collection">The NSID of the collection the record should be deleted from.</param>
        /// <param name="rKey">The record key, identifying the record to be deleted.</param>
        /// <param name="cid">The CID of the version of the record. If not specified, then return the most recent version.</param>
        /// <param name="service">The service to retrieve the record from.</param>
        /// <param name="accessCredentials">Optional access credentials for the specified service.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="jsonSerializerOptions"><see cref="JsonSerializerOptions"/> to apply during deserialization.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="repo"/>, <paramref name="collection"/>, <paramref name="rKey"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.
        /// </exception>
        [RequiresDynamicCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
        public static async Task<AtProtoHttpResult<TRecord>> GetRecord<TRecord>(
            AtIdentifier repo,
            Nsid collection,
            RecordKey rKey,
            Cid? cid,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            JsonSerializerOptions jsonSerializerOptions,
            string? serviceProxy = null,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default) where TRecord: class
        {
            ArgumentNullException.ThrowIfNull(repo);
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(rKey);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<TRecord> client;

            if (string.IsNullOrWhiteSpace(serviceProxy))
            {
                client = new(loggerFactory);
            }
            else
            {
                client = new(serviceProxy, loggerFactory);
            }

            string queryString = $"repo={Uri.EscapeDataString(repo.ToString())}&collection={Uri.EscapeDataString(collection.ToString())}&rkey={Uri.EscapeDataString(rKey.ToString())}";

            if (cid is not null)
            {
                queryString += $"&cid={Uri.EscapeDataString(cid.ToString())}";
            }

            return await client.Get(
                service,
                $"{GetRecordEndpoint}?{queryString}",
                accessCredentials,
                httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                jsonSerializerOptions: jsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a page of records in the specified <paramref name="collection"/>. May require authentication.
        /// </summary>
        /// <typeparam name="TRecordValue">The tyepe of the record value to get.</typeparam>
        /// <param name="repo">The <see cref="AtIdentifier"/> of the repo to retrieve the records from.</param>
        /// <param name="collection">The NSID of the collection the records should be retrieved from.</param>
        /// <param name="limit">The number of records to return in each page.</param>
        /// <param name="cursor">The cursor position to start retrieving records from.</param>
        /// <param name="reverse">A flag indicating if records should be listed in reverse order.</param>
        /// <param name="service">The service to retrieve the record from.</param>
        /// <param name="accessCredentials">Optional access credentials for the specified service.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="repo"/>, <paramref name="collection"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is not &gt;0 and &lt;=100.</exception>
        [RequiresDynamicCode("Use a Get overload which takes JsonSerializerOptions instead.")]
        [RequiresUnreferencedCode("Use a Get overload which takes JsonSerializerOptions instead.")]
        public static async Task<AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRecord<TRecordValue>>>> ListRecords<TRecordValue>(
            AtIdentifier repo,
            Nsid collection,
            int? limit,
            string? cursor,
            bool reverse,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            string? serviceProxy = null,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default) where TRecordValue : AtProtoRecordValue
        {
            ArgumentNullException.ThrowIfNull(repo);
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null &&
               (limit < 1 || limit > 100))
            {
                throw new ArgumentOutOfRangeException(nameof(limit), "{limit} must be between 1 and 100.");
            }

            string queryString = $"repo={Uri.EscapeDataString(repo.ToString())}&collection={Uri.EscapeDataString(collection.ToString())}";

            if (limit is not null)
            {
                queryString += $"&limit={limit}";
            }

            if (cursor is not null)
            {
                queryString += $"&cursor={Uri.EscapeDataString(cursor.ToString())}";
            }

            if (reverse)
            {
                queryString += "&reverse=true";
            }

            AtProtoHttpClient<ListRecordsResponse> client;
            if (string.IsNullOrWhiteSpace(serviceProxy))
            {
                client = new(loggerFactory);
            }
            else
            {
                client = new(serviceProxy, loggerFactory);
            }

            AtProtoHttpResult<ListRecordsResponse> response = await client.Get(
                service,
                $"{ListRecordsEndpoint}?{queryString}",
                accessCredentials,
                httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // We're not using a strongly typed response record to avoid callers having to add ListRecordsResponse<T> to their
            // JSON source generation context.
            PagedReadOnlyCollection<AtProtoRecord<TRecordValue>> result;
            if (response.Succeeded)
            {
                List<AtProtoRecord<TRecordValue>> records = [];

                // Run through the nodes and deserialize one by one to the strongly typed PagedReadOnlyCollection<T>, avoiding
                // the need to expose ListRecordsResponse<T>.
                foreach (JsonObject record in response.Result.Records)
                {
                    string jsonString = record.ToJsonString();

                    AtProtoRecord<TRecordValue>? atProtoRecord = JsonSerializer.Deserialize<AtProtoRecord<TRecordValue>>(jsonString, DefaultJsonSerializerOptionsWithNoTypeResolution);

                    if (atProtoRecord is not null)
                    {
                        records.Add(atProtoRecord);
                    }
                }

                result = new(records, response.Result.Cursor);
            }
            else
            {
                string? responseCursor = null;

                if (response.Result is not null && response.Result.Cursor is not null)
                {
                    responseCursor = response.Result.Cursor;
                }

                result = new([], responseCursor);
            }

            return new AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRecord<TRecordValue>>>(
                result,
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }

        /// <summary>
        /// Gets a page of records in the specified <paramref name="collection"/>. May require authentication.
        /// </summary>
        /// <typeparam name="TRecordValue">The tyepe of the record value to get.</typeparam>
        /// <param name="repo">The <see cref="AtIdentifier"/> of the repo to retrieve the records from.</param>
        /// <param name="collection">The NSID of the collection the records should be retrieved from.</param>
        /// <param name="limit">The number of records to return in each page.</param>
        /// <param name="cursor">The cursor position to start retrieving records from.</param>
        /// <param name="reverse">A flag indicating if records should be listed in reverse order.</param>
        /// <param name="service">The service to retrieve the record from.</param>
        /// <param name="accessCredentials">Optional access credentials for the specified service.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="jsonSerializerOptions"><see cref="JsonSerializerOptions"/> to apply during deserialization.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="repo"/>, <paramref name="collection"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is not &gt;0 and &lt;=100.</exception>
        [RequiresDynamicCode("Make sure all the required types are preserved in the jsonSerializerOptions parameter.")]
        [RequiresUnreferencedCode("Make sure all the required types are preserved in the jsonSerializerOptions parameter.")]
        public static async Task<AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRecord<TRecordValue>>>> ListRecords<TRecordValue>(
            AtIdentifier repo,
            Nsid collection,
            int? limit,
            string? cursor,
            bool reverse,
            Uri service,
            AccessCredentials? accessCredentials,
            HttpClient httpClient,
            JsonSerializerOptions jsonSerializerOptions,
            string? serviceProxy = null,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default) where TRecordValue : AtProtoRecordValue
        {
            ArgumentNullException.ThrowIfNull(repo);
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null &&
               (limit < 1 || limit > 100))
            {
                throw new ArgumentOutOfRangeException(nameof(limit), "{limit} must be between 1 and 100.");
            }

            string queryString = $"repo={Uri.EscapeDataString(repo.ToString())}&collection={Uri.EscapeDataString(collection.ToString())}";

            if (limit is not null)
            {
                queryString += $"&limit={limit}";
            }

            if (cursor is not null)
            {
                queryString += $"&cursor={Uri.EscapeDataString(cursor.ToString())}";
            }

            if (reverse)
            {
                queryString += "&reverse=true";
            }

            AtProtoHttpClient<ListRecordsResponse> client;
            if (string.IsNullOrWhiteSpace(serviceProxy))
            {
                client = new(loggerFactory);
            }
            else
            {
                client = new(serviceProxy, loggerFactory);
            }

            AtProtoHttpResult<ListRecordsResponse> response = await client.Get(
                service,
                $"{ListRecordsEndpoint}?{queryString}",
                accessCredentials,
                httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                jsonSerializerOptions: jsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // We're not using a strongly typed response record to avoid callers having to add ListRecordsResponse<T> to their
            // JSON source generation context.
            PagedReadOnlyCollection<AtProtoRecord<TRecordValue>> result;
            if (response.Succeeded)
            {
                jsonSerializerOptions ??= DefaultJsonSerializerOptionsWithNoTypeResolution;

                List<AtProtoRecord<TRecordValue>> records = [];

                // Run through the nodes and deserialize one by one to the strongly typed PagedReadOnlyCollection<T>, avoiding
                // the need to expose ListRecordsResponse<T>.
                foreach (JsonObject record in response.Result.Records)
                {
                    string jsonString = record.ToJsonString();

                    AtProtoRecord<TRecordValue>? atProtoRecord = JsonSerializer.Deserialize(
                        jsonString,
                        typeof(AtProtoRecord <TRecordValue>),
                        jsonSerializerOptions) as AtProtoRecord<TRecordValue>;

                    if (atProtoRecord is not null)
                    {
                        records.Add(atProtoRecord);
                    }
                }

                result = new (records, response.Result.Cursor);
            }
            else
            {
                string? responseCursor = null;

                if (response.Result is not null && response.Result.Cursor is not null)
                {
                    responseCursor = response.Result.Cursor;
                }

                result = new([], responseCursor);
            }

            return new AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRecord<TRecordValue>>>(
                result,
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }

        /// <summary>
        /// Upload a new blob, to be referenced from a repository record.Requires authentication.
        /// </summary>
        /// <remarks>
        /// <para>
        ///   The blob will be deleted if it is not referenced within a time window (eg, minutes).
        ///   Blob restrictions (mime type, size, etc) are enforced when the reference is created.
        /// </para>
        /// </remarks>
        /// <param name="blob">The blob to upload.</param>
        /// <param name="mimeType">The mime type of the blob to upload.</param>
        /// <param name="service">The service to upload the blob to.</param>
        /// <param name="accessCredentials">Access credentials for the specified service.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="serviceProxy">The service the PDS should proxy the call to, if any.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="blob"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="blob"/> is a zero length array.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="mimeType"/> is empty or not in the type/subtype format.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to PostBlob().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to PostBlob().")]

        public static async Task<AtProtoHttpResult<Blob>> UploadBlob(
            byte[] blob,
            string mimeType,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            string? serviceProxy = null,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(blob);
            ArgumentOutOfRangeException.ThrowIfZero(blob.Length);

            ArgumentException.ThrowIfNullOrEmpty(mimeType);
            if (!mimeType.Contains('/', StringComparison.Ordinal) || mimeType.Count(c => c == '/') != 1)
            {
                throw new ArgumentException("Mime type must be in the format 'type/subtype'.", nameof(mimeType));
            }

            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            List<NameValueHeaderValue> contentHeaders =
            [
                new NameValueHeaderValue("Content-Type", mimeType)
            ];

            AtProtoHttpClient<CreateBlobResponse> client;

            if (string.IsNullOrWhiteSpace(serviceProxy))
            {
                client = new(loggerFactory);
            }
            else
            {
                client = new(serviceProxy, loggerFactory);
            }

            AtProtoHttpResult<CreateBlobResponse> response =
                await client.PostBlob(
                    service: service,
                    endpoint: UploadBlobEndpoint,
                    blob: blob,
                    requestHeaders: null,
                    contentHeaders: contentHeaders,
                    credentials: accessCredentials,
                    httpClient: httpClient,
                    onCredentialsUpdated: onCredentialsUpdated,
                    jsonSerializerOptions : SourceGenerationContext.Default.Options,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<Blob>(
                    response.Result.Blob,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<Blob>(
                    null,
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
             }
        }

        /// <summary>
        /// Gets information about an account and repository, including the list of collections.
        /// </summary>
        /// <param name="repo">The <see cref="AtIdentifier"/> of the repo to retrieve information for.</param>
        /// <param name="service">The service to delete the record from.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="repo"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.
        /// </exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<RepoDescription>> DescribeRepo(
            AtIdentifier repo,
            Uri service,
            HttpClient httpClient,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(repo);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<RepoDescription> request = new(loggerFactory);

            return await request.Get(
                service: service,
                endpoint: $"{DescribeRepoEndpoint}?repo={Uri.EscapeDataString(repo.ToString())}",
                httpClient: httpClient,
                jsonSerializerOptions: SourceGenerationContext.Default.Options,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
