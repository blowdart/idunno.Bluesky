// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;

using idunno.AtProto.Authentication;
using idunno.AtProto.Space;
using idunno.AtProto.Space.Models;

using Microsoft.Extensions.Logging;

namespace idunno.AtProto;

public static partial class AtProtoServer
{
    /// <summary>
    /// Adds or updates members of a space in one commit.
    /// </summary>
    /// <param name="space">The space reference.</param>
    /// <param name="members">The member access changes, up to 100 entries.</param>
    /// <param name="service">The personal data server.</param>
    /// <param name="accessCredentials">The space authority's credentials.</param>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="serviceProxy">An optional service proxy.</param>
    /// <param name="onCredentialsUpdated">An optional credential update callback.</param>
    /// <param name="loggerFactory">An optional logger factory.</param>
    /// <param name="maximumResponseSize">The maximum response size.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The endpoint response and HTTP metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The space addresses a record, or the batch contains an invalid entry or more than 100 entries.</exception>
    /// <exception cref="AccessTokenException">The credentials do not match the service.</exception>
    /// <remarks><para>The endpoint may return <c>SpaceNotFound</c>, <c>NotAuthorized</c> or <c>PolicyMismatch</c>.</para></remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The request type is registered in SpaceSourceGenerationContext.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The request type is registered in SpaceSourceGenerationContext.")]
    public static async Task<AtProtoHttpResult<EmptyResponse>> PutSpaceMembers(
        SpaceAtUri space,
        IReadOnlyList<SpaceMember> members,
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
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);
        ValidateSpaceReference(space);
        if (members.Count > 100 || members.Any(member => member is null || member.Did is null))
        {
            throw new ArgumentException("The batch must contain at most 100 members with DIDs.", nameof(members));
        }

        EnsureCredentialsMatchService(service, accessCredentials);
        AtProtoHttpClient<EmptyResponse> client = CreateClient<EmptyResponse>(serviceProxy, loggerFactory, maximumResponseSize);
        return await client.Post(
            service: service,
            endpoint: "/xrpc/com.atproto.memberspace.putMembers",
            record: new PutSpaceMembersRequest(space, members),
            credentials: accessCredentials,
            httpClient: httpClient,
            onCredentialsUpdated: onCredentialsUpdated,
            jsonSerializerOptions: AtProtoJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes members of a space in one commit.
    /// </summary>
    /// <param name="space">The space reference.</param>
    /// <param name="dids">The member DIDs, up to 100 entries.</param>
    /// <param name="service">The personal data server.</param>
    /// <param name="accessCredentials">The space authority's credentials.</param>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="serviceProxy">An optional service proxy.</param>
    /// <param name="onCredentialsUpdated">An optional credential update callback.</param>
    /// <param name="loggerFactory">An optional logger factory.</param>
    /// <param name="maximumResponseSize">The maximum response size.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The endpoint response and HTTP metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The space addresses a record, or the batch contains an invalid entry or more than 100 entries.</exception>
    /// <exception cref="AccessTokenException">The credentials do not match the service.</exception>
    /// <remarks><para>Absent members are ignored. The endpoint may return <c>SpaceNotFound</c>, <c>NotAuthorized</c> or <c>PolicyMismatch</c>.</para></remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The request type is registered in SpaceSourceGenerationContext.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The request type is registered in SpaceSourceGenerationContext.")]
    public static async Task<AtProtoHttpResult<EmptyResponse>> RemoveSpaceMembers(
        SpaceAtUri space,
        IReadOnlyList<Did> dids,
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
        ArgumentNullException.ThrowIfNull(dids);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);
        ValidateSpaceReference(space);
        if (dids.Count > 100 || dids.Any(did => did is null))
        {
            throw new ArgumentException("The batch must contain at most 100 non-null DIDs.", nameof(dids));
        }

        EnsureCredentialsMatchService(service, accessCredentials);
        AtProtoHttpClient<EmptyResponse> client = CreateClient<EmptyResponse>(serviceProxy, loggerFactory, maximumResponseSize);
        return await client.Post(
            service: service,
            endpoint: "/xrpc/com.atproto.memberspace.removeMembers",
            record: new RemoveSpaceMembersRequest(space, dids),
            credentials: accessCredentials,
            httpClient: httpClient,
            onCredentialsUpdated: onCredentialsUpdated,
            jsonSerializerOptions: AtProtoJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists members of a space.
    /// </summary>
    /// <param name="space">The space reference.</param>
    /// <param name="limit">The maximum number of members, from 1 to 1000, or <see langword="null"/> for the server default.</param>
    /// <param name="cursor">An optional pagination cursor.</param>
    /// <param name="service">The personal data server.</param>
    /// <param name="accessCredentials">The space authority's credentials.</param>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="serviceProxy">An optional service proxy.</param>
    /// <param name="onCredentialsUpdated">An optional credential update callback.</param>
    /// <param name="loggerFactory">An optional logger factory.</param>
    /// <param name="maximumResponseSize">The maximum response size.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A page of members and HTTP metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The space addresses a record.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The limit is outside the permitted range.</exception>
    /// <exception cref="AccessTokenException">The credentials do not match the service.</exception>
    /// <exception cref="JsonException">The service returned an invalid member list.</exception>
    /// <remarks><para>The endpoint may return <c>SpaceNotFound</c>, <c>NotAuthorized</c>, <c>PolicyMismatch</c> or <c>InvalidCursor</c>.</para></remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The response types are registered in SpaceSourceGenerationContext.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The response types are registered in SpaceSourceGenerationContext.")]
    public static async Task<AtProtoHttpResult<PagedReadOnlyCollection<SpaceMember>>> ListSpaceMembers(
        SpaceAtUri space,
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
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);
        ValidateSpaceReference(space);
        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "The limit must be between 1 and 1000.");
        }

        EnsureCredentialsMatchService(service, accessCredentials);
        StringBuilder endpoint = new($"/xrpc/com.atproto.memberspace.listMembers?space={Uri.EscapeDataString(space.ToString())}");
        if (limit is not null)
        {
            endpoint.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
        }
        if (cursor is not null)
        {
            endpoint.Append("&cursor=").Append(Uri.EscapeDataString(cursor));
        }

        AtProtoHttpClient<ListSpaceMembersResponse> client = CreateClient<ListSpaceMembersResponse>(serviceProxy, loggerFactory, maximumResponseSize);
        AtProtoHttpResult<ListSpaceMembersResponse> response = await client.Get(
            service: service,
            endpoint: endpoint.ToString(),
            credentials: accessCredentials,
            httpClient: httpClient,
            onCredentialsUpdated: onCredentialsUpdated,
            jsonSerializerOptions: AtProtoJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.Succeeded && (response.Result.Members is null || response.Result.Members.Any(member => member is null || member.Did is null)))
        {
            throw new JsonException("The list-members response contained an invalid member.");
        }

        return new AtProtoHttpResult<PagedReadOnlyCollection<SpaceMember>>(
            result: response.Succeeded ? new PagedReadOnlyCollection<SpaceMember>(response.Result.Members, response.Result.Cursor) : null,
            statusCode: response.StatusCode,
            httpResponseHeaders: response.HttpResponseHeaders,
            atErrorDetail: response.AtErrorDetail,
            rateLimit: response.RateLimit);
    }

    /// <summary>
    /// Checks access to a managed space through its managing application.
    /// </summary>
    /// <param name="space">The space reference.</param>
    /// <param name="repo">The requesting account DID, if known.</param>
    /// <param name="clientId">The attested OAuth client ID, if any.</param>
    /// <param name="service">The managing application's endpoint.</param>
    /// <param name="serviceCredential">The service credential intended for the managing application.</param>
    /// <param name="httpClient">The HTTP client.</param>
    /// <param name="loggerFactory">An optional logger factory.</param>
    /// <param name="maximumResponseSize">The maximum response size.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The access decision and HTTP metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The space addresses a record, or the client ID is not an absolute URI.</exception>
    /// <remarks><para>This service-to-service endpoint is called by the space host, not by an account session. The caller must obtain a service credential for the managing application.</para></remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The response type is registered in SpaceSourceGenerationContext.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The response type is registered in SpaceSourceGenerationContext.")]
    public static async Task<AtProtoHttpResult<ManagedSpaceAccess>> CheckManagedSpaceAccess(
        SpaceAtUri space,
        Did? repo,
        Uri? clientId,
        Uri service,
        ServiceCredential serviceCredential,
        HttpClient httpClient,
        ILoggerFactory? loggerFactory = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(serviceCredential);
        ArgumentNullException.ThrowIfNull(httpClient);
        ValidateSpaceReference(space);
        if (clientId is not null && !clientId.IsAbsoluteUri)
        {
            throw new ArgumentException("The client ID must be an absolute URI.", nameof(clientId));
        }

        StringBuilder endpoint = new($"/xrpc/com.atproto.managedspace.checkAccess?space={Uri.EscapeDataString(space.ToString())}");
        if (repo is not null)
        {
            endpoint.Append("&repo=").Append(Uri.EscapeDataString(repo.ToString()));
        }
        if (clientId is not null)
        {
            endpoint.Append("&clientId=").Append(Uri.EscapeDataString(clientId.ToString()));
        }

        AtProtoHttpClient<ManagedSpaceAccess> client = CreateClient<ManagedSpaceAccess>(null, loggerFactory, maximumResponseSize);
        return await client.Get(
            service: service,
            endpoint: endpoint.ToString(),
            credentials: serviceCredential,
            httpClient: httpClient,
            jsonSerializerOptions: AtProtoJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
