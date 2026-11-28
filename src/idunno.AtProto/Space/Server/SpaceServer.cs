// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.AtProto.Repo.Models;
using idunno.AtProto.Space;
using idunno.AtProto.Space.Models;

using Microsoft.Extensions.Logging;

namespace idunno.AtProto;

public static partial class AtProtoServer
{
    /// <summary>
    /// Creates a space owned by the authenticated account.
    /// </summary>
    /// <param name="spaceType">The NSID of the space type.</param>
    /// <param name="policy">The typed policy to apply. Its lexicon <c>$type</c> discriminator is serialized automatically.</param>
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
    /// <exception cref="JsonException">The service returned an invalid space reference.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>SpaceAlreadyExists</c> or <c>UnsupportedPolicy</c>.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "The request and response types are registered in SpaceSourceGenerationContext.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "The request and response types are registered in SpaceSourceGenerationContext.")]
    public static async Task<AtProtoHttpResult<SpaceAtUri>> CreateSpace(
        Nsid spaceType,
        SpacePolicy policy,
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
        if (response.Succeeded && (createdSpace is null || createdSpace.IsRecord))
        {
            throw new JsonException("The create-space response contained an invalid space reference.");
        }

        return new AtProtoHttpResult<SpaceAtUri>(
            result: createdSpace,
            statusCode: response.StatusCode,
            httpResponseHeaders: response.HttpResponseHeaders,
            atErrorDetail: response.AtErrorDetail,
            rateLimit: response.RateLimit);
    }

    /// <summary>
    /// Gets a space and its policy.
    /// </summary>
    /// <param name="space">The space reference, not a record URI.</param>
    /// <param name="service">The personal data server to query.</param>
    /// <param name="accessCredentials">Credentials for <paramref name="service"/>.</param>
    /// <param name="httpClient">The HTTP client used to make the request.</param>
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="onCredentialsUpdated">The callback for updated credentials.</param>
    /// <param name="loggerFactory">The logger factory used to create a logger.</param>
    /// <param name="maximumResponseSize">The maximum response body size.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The space description and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="space"/> addresses a record rather than a space.</exception>
    /// <exception cref="AccessTokenException">The credentials are not valid for <paramref name="service"/>.</exception>
    /// <exception cref="JsonException">The service returned an invalid space reference or a <see langword="null"/> policy.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>SpaceNotFound</c> or <c>NotAuthorized</c>.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "The response types are registered in SpaceSourceGenerationContext.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "The response types are registered in SpaceSourceGenerationContext.")]
    public static async Task<AtProtoHttpResult<SpaceDescription>> GetSpace(
        SpaceAtUri space,
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
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);

        ValidateSpaceReference(space);
        EnsureCredentialsMatchService(service, accessCredentials);

        AtProtoHttpClient<GetSpaceResponse> client = CreateClient<GetSpaceResponse>(serviceProxy, loggerFactory, maximumResponseSize);
        AtProtoHttpResult<GetSpaceResponse> response = await client.Get(
            service: service,
            endpoint: $"/xrpc/com.atproto.space.getSpace?space={Uri.EscapeDataString(space.ToString())}",
            credentials: accessCredentials,
            httpClient: httpClient,
            onCredentialsUpdated: onCredentialsUpdated,
            jsonSerializerOptions: AtProtoJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded && (response.Result.Space is null || response.Result.Space.IsRecord || response.Result.Policy is null))
        {
            throw new JsonException("The get-space response contained an invalid space reference or a null policy.");
        }

        return new AtProtoHttpResult<SpaceDescription>(
            result: response.Succeeded ? new SpaceDescription { Space = response.Result.Space, Policy = response.Result.Policy } : null,
            statusCode: response.StatusCode,
            httpResponseHeaders: response.HttpResponseHeaders,
            atErrorDetail: response.AtErrorDetail,
            rateLimit: response.RateLimit);
    }

    /// <summary>
    /// Lists spaces available to the authenticated account.
    /// </summary>
    /// <param name="spaceDid">An optional space owner DID filter.</param>
    /// <param name="spaceType">An optional space type NSID filter.</param>
    /// <param name="limit">The maximum number of spaces to return. Must be between 1 and 100. Defaults to 50.</param>
    /// <param name="cursor">An optional pagination cursor.</param>
    /// <param name="service">The personal data server to query.</param>
    /// <param name="accessCredentials">Credentials for <paramref name="service"/>.</param>
    /// <param name="httpClient">The HTTP client used to make the request.</param>
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="onCredentialsUpdated">The callback for updated credentials.</param>
    /// <param name="loggerFactory">The logger factory used to create a logger.</param>
    /// <param name="maximumResponseSize">The maximum response body size.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The page of spaces and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is less than 1 or greater than 100.</exception>
    /// <exception cref="AccessTokenException">The credentials are not valid for <paramref name="service"/>.</exception>
    /// <exception cref="JsonException">The service returned an invalid list of space references.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>InvalidCursor</c>.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "The response types are registered in SpaceSourceGenerationContext.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "The response types are registered in SpaceSourceGenerationContext.")]
    public static async Task<AtProtoHttpResult<PagedReadOnlyCollection<SpaceView>>> ListSpaces(
        Did? spaceDid,
        Nsid? spaceType,
        int? limit,
        string? cursor,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        string? serviceProxy = null,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "limit must be between 1 and 100.");
        }

        EnsureCredentialsMatchService(service, accessCredentials);

        StringBuilder endpoint = new("/xrpc/com.atproto.space.listSpaces");
        char separator = '?';

        if (spaceDid is not null)
        {
            endpoint.Append(separator).Append("spaceDid=").Append(Uri.EscapeDataString(spaceDid.ToString()));
            separator = '&';
        }

        if (spaceType is not null)
        {
            endpoint.Append(separator).Append("spaceType=").Append(Uri.EscapeDataString(spaceType.ToString()));
            separator = '&';
        }

        if (limit is not null)
        {
            endpoint.Append(CultureInfo.InvariantCulture, $"{separator}limit={limit}");
            separator = '&';
        }

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            endpoint.Append(separator).Append("cursor=").Append(Uri.EscapeDataString(cursor));
        }

        AtProtoHttpClient<ListSpacesResponse> client = CreateClient<ListSpacesResponse>(serviceProxy, loggerFactory, maximumResponseSize);
        AtProtoHttpResult<ListSpacesResponse> response = await client.Get(
            service: service,
            endpoint: endpoint.ToString(),
            credentials: accessCredentials,
            httpClient: httpClient,
            onCredentialsUpdated: onCredentialsUpdated,
            jsonSerializerOptions: AtProtoJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded &&
            (response.Result.Spaces is null ||
             response.Result.Spaces.Any(item => item is null || item.Space is null || item.Space.IsRecord)))
        {
            throw new JsonException("The list-spaces response contained an invalid space reference.");
        }

        return new AtProtoHttpResult<PagedReadOnlyCollection<SpaceView>>(
            result: response.Succeeded ? new PagedReadOnlyCollection<SpaceView>(response.Result.Spaces, response.Result.Cursor) : null,
            statusCode: response.StatusCode,
            httpResponseHeaders: response.HttpResponseHeaders,
            atErrorDetail: response.AtErrorDetail,
            rateLimit: response.RateLimit);
    }

    /// <summary>
    /// Replaces the policy of a space.
    /// </summary>
    /// <param name="space">The space reference, not a record URI.</param>
    /// <param name="policy">The replacement policy.</param>
    /// <param name="service">The personal data server on which to update the space.</param>
    /// <param name="accessCredentials">Credentials for <paramref name="service"/>.</param>
    /// <param name="httpClient">The HTTP client used to make the request.</param>
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="onCredentialsUpdated">The callback for updated credentials.</param>
    /// <param name="loggerFactory">The logger factory used to create a logger.</param>
    /// <param name="maximumResponseSize">The maximum response body size.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The empty endpoint response and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="space"/> addresses a record rather than a space.</exception>
    /// <exception cref="AccessTokenException">The credentials are not valid for <paramref name="service"/>.</exception>
    /// <remarks>
    /// <para>Changing the policy type discards the previous policy's state. The endpoint may return
    /// <c>SpaceNotFound</c>, <c>NotAuthorized</c> or <c>UnsupportedPolicy</c>.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "The request type is registered in SpaceSourceGenerationContext.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "The request type is registered in SpaceSourceGenerationContext.")]
    public static async Task<AtProtoHttpResult<EmptyResponse>> UpdateSpace(
        SpaceAtUri space,
        SpacePolicy policy,
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
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);

        ValidateSpaceReference(space);
        EnsureCredentialsMatchService(service, accessCredentials);

        UpdateSpaceRequest request = new(space, policy);
        AtProtoHttpClient<EmptyResponse> client = CreateClient<EmptyResponse>(serviceProxy, loggerFactory, maximumResponseSize);
        AtProtoHttpResult<EmptyResponse> response = await client.Post(
            service: service,
            endpoint: "/xrpc/com.atproto.space.updateSpace",
            record: request,
            credentials: accessCredentials,
            httpClient: httpClient,
            onCredentialsUpdated: onCredentialsUpdated,
            jsonSerializerOptions: AtProtoJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return response;
    }

    /// <summary>
    /// Deletes a space and its authority repository.
    /// </summary>
    /// <param name="space">The space reference, not a record URI.</param>
    /// <param name="service">The personal data server on which to delete the space.</param>
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
    /// <para>The endpoint may return <c>SpaceNotFound</c> or <c>NotAuthorized</c>.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "The request type is registered in SpaceSourceGenerationContext.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "The request type is registered in SpaceSourceGenerationContext.")]
    public static async Task<AtProtoHttpResult<DeleteResult>> DeleteSpace(
        SpaceAtUri space,
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
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);

        ValidateSpaceReference(space);
        EnsureCredentialsMatchService(service, accessCredentials);

        DeleteSpaceRequest request = new(space);
        AtProtoHttpClient<EmptyResponse> client = CreateClient<EmptyResponse>(serviceProxy, loggerFactory, maximumResponseSize);
        AtProtoHttpResult<EmptyResponse> response = await client.Post(
            service: service,
            endpoint: "/xrpc/com.atproto.space.deleteSpace",
            record: request,
            credentials: accessCredentials,
            httpClient: httpClient,
            onCredentialsUpdated: onCredentialsUpdated,
            jsonSerializerOptions: AtProtoJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return response.Succeeded
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
