// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using idunno.AtProto.Repo;
using idunno.AtProto.Space;

namespace idunno.AtProto;

public partial class AtProtoAgent
{
    /// <summary>
    /// Creates a space owned by the authenticated account.
    /// </summary>
    /// <param name="spaceType">The NSID of the space type.</param>
    /// <param name="policy">The typed policy to apply. Its lexicon <c>$type</c> discriminator is serialized automatically.</param>
    /// <param name="skey">The space key, or <see langword="null"/> to let the service generate one.</param>
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The created space reference and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spaceType"/> or <paramref name="policy"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    /// <exception cref="JsonException">The service returned an invalid space reference.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>SpaceAlreadyExists</c> or <c>UnsupportedPolicy</c>.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    public async Task<AtProtoHttpResult<SpaceAtUri>> CreateSpace(
        Nsid spaceType,
        SpacePolicy policy,
        RecordKey? skey = null,
        string? serviceProxy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spaceType);
        ArgumentNullException.ThrowIfNull(policy);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await AtProtoServer.CreateSpace(
            spaceType: spaceType,
            policy: policy,
            skey: skey,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            serviceProxy: serviceProxy,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a space and its policy.
    /// </summary>
    /// <param name="space">The space reference, not a record URI.</param>
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The space description and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="space"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="space"/> addresses a record rather than a space.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    /// <exception cref="JsonException">The service returned an invalid space reference or a <see langword="null"/> policy.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>SpaceNotFound</c> or <c>NotAuthorized</c>.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    public async Task<AtProtoHttpResult<SpaceDescription>> GetSpace(
        SpaceAtUri space,
        string? serviceProxy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(space);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await AtProtoServer.GetSpace(
            space: space,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            serviceProxy: serviceProxy,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists spaces available to the authenticated account.
    /// </summary>
    /// <param name="spaceDid">An optional space owner DID filter.</param>
    /// <param name="spaceType">An optional space type NSID filter.</param>
    /// <param name="limit">The maximum number of spaces to return. Must be between 1 and 100. Defaults to 50.</param>
    /// <param name="cursor">An optional pagination cursor.</param>
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The page of spaces and HTTP response metadata.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is less than 1 or greater than 100.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    /// <exception cref="JsonException">The service returned an invalid list of space references.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>InvalidCursor</c>.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    public async Task<AtProtoHttpResult<PagedReadOnlyCollection<SpaceView>>> ListSpaces(
        Did? spaceDid = null,
        Nsid? spaceType = null,
        int? limit = null,
        string? cursor = null,
        string? serviceProxy = null,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "limit must be between 1 and 100.");
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await AtProtoServer.ListSpaces(
            spaceDid: spaceDid,
            spaceType: spaceType,
            limit: limit,
            cursor: cursor,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            serviceProxy: serviceProxy,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Replaces the policy of a space.
    /// </summary>
    /// <param name="space">The space reference, not a record URI.</param>
    /// <param name="policy">The replacement policy.</param>
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The empty endpoint response and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="space"/> addresses a record rather than a space.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    /// <remarks>
    /// <para>Changing the policy type discards the previous policy's state. The endpoint may return
    /// <c>SpaceNotFound</c>, <c>NotAuthorized</c> or <c>UnsupportedPolicy</c>.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    public async Task<AtProtoHttpResult<EmptyResponse>> UpdateSpace(
        SpaceAtUri space,
        SpacePolicy policy,
        string? serviceProxy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(policy);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await AtProtoServer.UpdateSpace(
            space: space,
            policy: policy,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            serviceProxy: serviceProxy,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a space and its authority repository.
    /// </summary>
    /// <param name="space">The space reference, not a record URI.</param>
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The delete result and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="space"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="space"/> addresses a record rather than a space.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>SpaceNotFound</c> or <c>NotAuthorized</c>.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    public async Task<AtProtoHttpResult<DeleteResult>> DeleteSpace(
        SpaceAtUri space,
        string? serviceProxy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(space);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await AtProtoServer.DeleteSpace(
            space: space,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            serviceProxy: serviceProxy,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
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
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The created record result and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="space"/> addresses a record rather than a space.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>SpaceNotFound</c> or <c>RecordAlreadyExists</c>.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    [RequiresDynamicCode("Use a CreateSpaceRecord overload which takes JsonSerializerOptions instead.")]
    [RequiresUnreferencedCode("Use a CreateSpaceRecord overload which takes JsonSerializerOptions instead.")]
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The convenience overload and the explicit serializer-options overload provide equivalent AOT and reflection-based use.")]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "This operation is an instance API that uses the agent's authentication and service context.")]
    public Task<AtProtoHttpResult<CreateRecordResult>> CreateSpaceRecord<TRecord>(
        SpaceAtUri space,
        TRecord record,
        Nsid collection,
        RecordKey? rkey = null,
        bool? validate = true,
        string? serviceProxy = null,
        CancellationToken cancellationToken = default)
        where TRecord : AtProtoRecord
    {
        return CreateSpaceRecord(
            space: space,
            record: record,
            jsonSerializerOptions: AtProtoServer.DefaultJsonSerializerOptionsWithNoTypeResolution,
            collection: collection,
            rkey: rkey,
            validate: validate,
            serviceProxy: serviceProxy,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates a record in a space for the authenticated repository.
    /// </summary>
    /// <typeparam name="TRecord">The type of record to create.</typeparam>
    /// <param name="space">The space reference, not a record URI.</param>
    /// <param name="record">The record to create.</param>
    /// <param name="jsonSerializerOptions">The options used to serialize the record.</param>
    /// <param name="collection">The collection containing the record.</param>
    /// <param name="rkey">The record key, or <see langword="null"/> to let the service generate one.</param>
    /// <param name="validate">The validation setting, or <see langword="null"/> for known lexicons only.</param>
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The created record result and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="space"/> addresses a record rather than a space.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>SpaceNotFound</c> or <c>RecordAlreadyExists</c>.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    [RequiresDynamicCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
    [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The convenience overload and the explicit serializer-options overload provide equivalent AOT and reflection-based use.")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> CreateSpaceRecord<TRecord>(
        SpaceAtUri space,
        TRecord record,
        JsonSerializerOptions jsonSerializerOptions,
        Nsid collection,
        RecordKey? rkey = null,
        bool? validate = true,
        string? serviceProxy = null,
        CancellationToken cancellationToken = default)
        where TRecord : AtProtoRecord
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(jsonSerializerOptions);
        ArgumentNullException.ThrowIfNull(collection);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await AtProtoServer.CreateSpaceRecord(
            space: space,
            record: record,
            collection: collection,
            rkey: rkey,
            validate: validate,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            jsonSerializerOptions: jsonSerializerOptions,
            serviceProxy: serviceProxy,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
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
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The written record result and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="space"/> addresses a record rather than a space.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>SpaceNotFound</c>.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    [RequiresDynamicCode("Use a PutSpaceRecord overload which takes JsonSerializerOptions instead.")]
    [RequiresUnreferencedCode("Use a PutSpaceRecord overload which takes JsonSerializerOptions instead.")]
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The convenience overload and the explicit serializer-options overload provide equivalent AOT and reflection-based use.")]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "This operation is an instance API that uses the agent's authentication and service context.")]
    public Task<AtProtoHttpResult<PutRecordResult>> PutSpaceRecord<TRecord>(
        SpaceAtUri space,
        TRecord record,
        Nsid collection,
        RecordKey rkey,
        bool? validate = true,
        string? serviceProxy = null,
        CancellationToken cancellationToken = default)
        where TRecord : AtProtoRecord
    {
        return PutSpaceRecord(
            space: space,
            record: record,
            jsonSerializerOptions: AtProtoServer.DefaultJsonSerializerOptionsWithNoTypeResolution,
            collection: collection,
            rkey: rkey,
            validate: validate,
            serviceProxy: serviceProxy,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates or replaces a record in a space for the authenticated repository.
    /// </summary>
    /// <typeparam name="TRecord">The type of record to write.</typeparam>
    /// <param name="space">The space reference, not a record URI.</param>
    /// <param name="record">The record to write.</param>
    /// <param name="jsonSerializerOptions">The options used to serialize the record.</param>
    /// <param name="collection">The collection containing the record.</param>
    /// <param name="rkey">The record key.</param>
    /// <param name="validate">The validation setting, or <see langword="null"/> for known lexicons only.</param>
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The written record result and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="space"/> addresses a record rather than a space.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>SpaceNotFound</c>.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    [RequiresDynamicCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
    [RequiresUnreferencedCode("Make sure all required types are preserved in the jsonSerializerOptions parameter.")]
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The convenience overload and the explicit serializer-options overload provide equivalent AOT and reflection-based use.")]
    public async Task<AtProtoHttpResult<PutRecordResult>> PutSpaceRecord<TRecord>(
        SpaceAtUri space,
        TRecord record,
        JsonSerializerOptions jsonSerializerOptions,
        Nsid collection,
        RecordKey rkey,
        bool? validate = true,
        string? serviceProxy = null,
        CancellationToken cancellationToken = default)
        where TRecord : AtProtoRecord
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(jsonSerializerOptions);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(rkey);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await AtProtoServer.PutSpaceRecord(
            space: space,
            record: record,
            collection: collection,
            rkey: rkey,
            validate: validate,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            jsonSerializerOptions: jsonSerializerOptions,
            serviceProxy: serviceProxy,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a record from a space for the authenticated repository.
    /// </summary>
    /// <param name="space">The space reference, not a record URI.</param>
    /// <param name="collection">The collection containing the record.</param>
    /// <param name="rkey">The record key.</param>
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The delete result and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="space"/> addresses a record rather than a space.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>SpaceNotFound</c>.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    public async Task<AtProtoHttpResult<DeleteResult>> DeleteSpaceRecord(
        SpaceAtUri space,
        Nsid collection,
        RecordKey rkey,
        string? serviceProxy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(rkey);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await AtProtoServer.DeleteSpaceRecord(
            space: space,
            collection: collection,
            rkey: rkey,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            serviceProxy: serviceProxy,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
