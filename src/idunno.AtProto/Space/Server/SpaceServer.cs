// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.AtProto.Repo.Models;
using idunno.AtProto.Space.Models;

using Microsoft.Extensions.Logging;

namespace idunno.AtProto;

public static partial class AtProtoServer
{
    /// <summary>
    /// Creates a space owned by the authenticated account.
    /// </summary>
    /// <param name="spaceType">The NSID of the space type.</param>
    /// <param name="policy">The policy object, including its lexicon <c>$type</c> discriminator.</param>
    /// <param name="skey">The space key, or <see langword="null"/> to let the service generate one.</param>
    /// <param name="service">The service on which to create the space.</param>
    /// <param name="accessCredentials">Credentials for <paramref name="service"/>.</param>
    /// <param name="httpClient">The HTTP client used to make the request.</param>
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="onCredentialsUpdated">The callback for updated credentials.</param>
    /// <param name="loggerFactory">The logger factory used to create a logger.</param>
    /// <param name="maximumResponseSize">The maximum response body size.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The created space reference and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="AccessTokenException">The credentials are not valid for <paramref name="service"/>.</exception>
    /// <exception cref="JsonException">The service returned a record URI instead of a space reference.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>SpaceAlreadyExists</c> or <c>UnsupportedPolicy</c>.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "The request and response types are registered in SourceGenerationContext.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "The request and response types are registered in SourceGenerationContext.")]
    public static async Task<AtProtoHttpResult<SpaceAtUri>> CreateSpace(
        Nsid spaceType,
        JsonNode policy,
        RecordKey? skey,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        string? serviceProxy = null,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spaceType);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);

        EnsureCredentialsMatchService(service, accessCredentials);

        CreateSpaceRequest request = new(spaceType, policy, skey);
        AtProtoHttpClient<CreateSpaceResponse> client = CreateClient<CreateSpaceResponse>(serviceProxy, loggerFactory, maximumResponseSize);

        AtProtoHttpResult<CreateSpaceResponse> response = await client.Post(
            service: service,
            endpoint: "/xrpc/com.atproto.space.createSpace",
            record: request,
            credentials: accessCredentials,
            httpClient: httpClient,
            onCredentialsUpdated: onCredentialsUpdated,
            jsonSerializerOptions: AtProtoJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        SpaceAtUri? createdSpace = response.Succeeded ? response.Result.Space : null;
        if (createdSpace?.IsRecord is true)
        {
            throw new JsonException("The create-space response contained a record URI instead of a space reference.");
        }

        return new AtProtoHttpResult<SpaceAtUri>(
            result: createdSpace,
            statusCode: response.StatusCode,
            httpResponseHeaders: response.HttpResponseHeaders,
            atErrorDetail: response.AtErrorDetail,
            rateLimit: response.RateLimit);
    }

    /// <summary>
    /// Creates a record in a space for the authenticated repository.
    /// </summary>
    /// <typeparam name="TRecord">The type of record to create.</typeparam>
    /// <param name="space">The space reference, not a record URI.</param>
    /// <param name="record">The record to create.</param>
    /// <param name="collection">The collection containing the record.</param>
    /// <param name="rkey">The record key, or <see langword="null"/> to let the service generate one.</param>
    /// <param name="validate">The validation setting, or <see langword="null"/> for known lexicons only.</param>
    /// <param name="service">The service on which to create the record.</param>
    /// <param name="accessCredentials">Credentials for <paramref name="service"/>.</param>
    /// <param name="httpClient">The HTTP client used to make the request.</param>
    /// <param name="jsonSerializerOptions">The options used to serialize <typeparamref name="TRecord"/> and the response.</param>
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="onCredentialsUpdated">The callback for updated credentials.</param>
    /// <param name="loggerFactory">The logger factory used to create a logger.</param>
    /// <param name="maximumResponseSize">The maximum response body size.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The created record result and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="space"/> addresses a record rather than a space.</exception>
    /// <exception cref="AccessTokenException">The credentials are not valid for <paramref name="service"/>.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>SpaceNotFound</c> or <c>RecordAlreadyExists</c>.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    [RequiresDynamicCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
    [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
    public static async Task<AtProtoHttpResult<CreateRecordResult>> CreateSpaceRecord<TRecord>(
        SpaceAtUri space,
        TRecord record,
        Nsid collection,
        RecordKey? rkey,
        bool? validate,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        JsonSerializerOptions jsonSerializerOptions,
        string? serviceProxy = null,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
        where TRecord : AtProtoRecord
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(jsonSerializerOptions);

        ValidateSpaceReference(space);
        EnsureCredentialsMatchService(service, accessCredentials);

        JsonNode serializedRecord = JsonSerializer.SerializeToNode(record, jsonSerializerOptions) ??
            throw new ArgumentException("Record cannot be serialized.", nameof(record));
        SpaceRecordRequest request = new(space, accessCredentials.Did, collection, serializedRecord, rkey, validate);
        AtProtoHttpClient<CreateRecordResponse> client = CreateClient<CreateRecordResponse>(serviceProxy, loggerFactory, maximumResponseSize);

        AtProtoHttpResult<CreateRecordResponse> response = await client.Post(
            service: service,
            endpoint: "/xrpc/com.atproto.space.createRecord",
            record: request,
            credentials: accessCredentials,
            httpClient: httpClient,
            onCredentialsUpdated: onCredentialsUpdated,
            jsonSerializerOptions: jsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new AtProtoHttpResult<CreateRecordResult>(
            result: response.Succeeded ? new CreateRecordResult(response.Result) : null,
            statusCode: response.StatusCode,
            httpResponseHeaders: response.HttpResponseHeaders,
            atErrorDetail: response.AtErrorDetail,
            rateLimit: response.RateLimit);
    }

    /// <summary>
    /// Creates or replaces a record in a space for the authenticated repository.
    /// </summary>
    /// <typeparam name="TRecord">The type of record to write.</typeparam>
    /// <param name="space">The space reference, not a record URI.</param>
    /// <param name="record">The record to write.</param>
    /// <param name="collection">The collection containing the record.</param>
    /// <param name="rkey">The record key.</param>
    /// <param name="validate">The validation setting, or <see langword="null"/> for known lexicons only.</param>
    /// <param name="service">The service on which to write the record.</param>
    /// <param name="accessCredentials">Credentials for <paramref name="service"/>.</param>
    /// <param name="httpClient">The HTTP client used to make the request.</param>
    /// <param name="jsonSerializerOptions">The options used to serialize <typeparamref name="TRecord"/> and the response.</param>
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="onCredentialsUpdated">The callback for updated credentials.</param>
    /// <param name="loggerFactory">The logger factory used to create a logger.</param>
    /// <param name="maximumResponseSize">The maximum response body size.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The written record result and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="space"/> addresses a record rather than a space.</exception>
    /// <exception cref="AccessTokenException">The credentials are not valid for <paramref name="service"/>.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>SpaceNotFound</c>.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    [RequiresDynamicCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
    [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
    public static async Task<AtProtoHttpResult<PutRecordResult>> PutSpaceRecord<TRecord>(
        SpaceAtUri space,
        TRecord record,
        Nsid collection,
        RecordKey rkey,
        bool? validate,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        JsonSerializerOptions jsonSerializerOptions,
        string? serviceProxy = null,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
        where TRecord : AtProtoRecord
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(rkey);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(jsonSerializerOptions);

        ValidateSpaceReference(space);
        EnsureCredentialsMatchService(service, accessCredentials);

        JsonNode serializedRecord = JsonSerializer.SerializeToNode(record, jsonSerializerOptions) ??
            throw new ArgumentException("Record cannot be serialized.", nameof(record));
        SpaceRecordRequest request = new(space, accessCredentials.Did, collection, serializedRecord, rkey, validate);
        AtProtoHttpClient<PutRecordResponse> client = CreateClient<PutRecordResponse>(serviceProxy, loggerFactory, maximumResponseSize);

        AtProtoHttpResult<PutRecordResponse> response = await client.Post(
            service: service,
            endpoint: "/xrpc/com.atproto.space.putRecord",
            record: request,
            credentials: accessCredentials,
            httpClient: httpClient,
            onCredentialsUpdated: onCredentialsUpdated,
            jsonSerializerOptions: jsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new AtProtoHttpResult<PutRecordResult>(
            result: response.Succeeded ? new PutRecordResult(response.Result) : null,
            statusCode: response.StatusCode,
            httpResponseHeaders: response.HttpResponseHeaders,
            atErrorDetail: response.AtErrorDetail,
            rateLimit: response.RateLimit);
    }

    /// <summary>
    /// Deletes a record from a space for the authenticated repository.
    /// </summary>
    /// <param name="space">The space reference, not a record URI.</param>
    /// <param name="collection">The collection containing the record.</param>
    /// <param name="rkey">The record key.</param>
    /// <param name="service">The service on which to delete the record.</param>
    /// <param name="accessCredentials">Credentials for <paramref name="service"/>.</param>
    /// <param name="httpClient">The HTTP client used to make the request.</param>
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="onCredentialsUpdated">The callback for updated credentials.</param>
    /// <param name="loggerFactory">The logger factory used to create a logger.</param>
    /// <param name="maximumResponseSize">The maximum response body size.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The delete result and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="space"/> addresses a record rather than a space.</exception>
    /// <exception cref="AccessTokenException">The credentials are not valid for <paramref name="service"/>.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>SpaceNotFound</c>.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "The request and response types are registered in SourceGenerationContext.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "The request and response types are registered in SourceGenerationContext.")]
    public static async Task<AtProtoHttpResult<DeleteResult>> DeleteSpaceRecord(
        SpaceAtUri space,
        Nsid collection,
        RecordKey rkey,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        string? serviceProxy = null,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(rkey);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);

        ValidateSpaceReference(space);
        EnsureCredentialsMatchService(service, accessCredentials);

        DeleteSpaceRecordRequest request = new(space, accessCredentials.Did, collection, rkey);
        AtProtoHttpClient<EmptyResponse> client = CreateClient<EmptyResponse>(serviceProxy, loggerFactory, maximumResponseSize);

        AtProtoHttpResult<EmptyResponse> response = await client.Post(
            service: service,
            endpoint: "/xrpc/com.atproto.space.deleteRecord",
            record: request,
            credentials: accessCredentials,
            httpClient: httpClient,
            onCredentialsUpdated: onCredentialsUpdated,
            jsonSerializerOptions: AtProtoJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent
            ? new AtProtoHttpResult<DeleteResult>(
                result: new DeleteResult(null),
                statusCode: response.StatusCode,
                httpResponseHeaders: response.HttpResponseHeaders,
                atErrorDetail: response.AtErrorDetail,
                rateLimit: response.RateLimit)
            : new AtProtoHttpResult<DeleteResult>(
                result: null,
                statusCode: response.StatusCode,
                httpResponseHeaders: response.HttpResponseHeaders,
                atErrorDetail: response.AtErrorDetail,
                rateLimit: response.RateLimit);
    }

    private static AtProtoHttpClient<TResponse> CreateClient<TResponse>(
        string? serviceProxy,
        ILoggerFactory? loggerFactory,
        int maximumResponseSize)
        where TResponse : class
    {
        return string.IsNullOrWhiteSpace(serviceProxy)
            ? new(loggerFactory) { MaximumResponseSize = maximumResponseSize }
            : new(serviceProxy, loggerFactory) { MaximumResponseSize = maximumResponseSize };
    }

    private static void EnsureCredentialsMatchService(Uri service, AccessCredentials accessCredentials)
    {
        if (service != accessCredentials.Service)
        {
            throw new AccessTokenException("Credentials not valid for the specified service.");
        }
    }

    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    private static void ValidateSpaceReference(SpaceAtUri space)
    {
        if (space.IsRecord)
        {
            throw new ArgumentException("The space URI must address a space, not a record.", nameof(space));
        }
    }
}
