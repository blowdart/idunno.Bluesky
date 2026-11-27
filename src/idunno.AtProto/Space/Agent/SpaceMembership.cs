// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using idunno.AtProto.Space;

namespace idunno.AtProto;

public partial class AtProtoAgent
{
    /// <summary>
    /// Adds or updates members of a space in one commit.
    /// </summary>
    /// <param name="space">The space reference, not a record URI.</param>
    /// <param name="members">The member access changes, up to 100 entries.</param>
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The endpoint response and HTTP metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The space addresses a record, or the batch contains an invalid entry or more than 100 entries.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    /// <remarks><para>The endpoint may return <c>SpaceNotFound</c>, <c>NotAuthorized</c> or <c>PolicyMismatch</c>.</para></remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    public async Task<AtProtoHttpResult<EmptyResponse>> PutSpaceMembers(
        SpaceAtUri space,
        IReadOnlyList<SpaceMember> members,
        string? serviceProxy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(members);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await AtProtoServer.PutSpaceMembers(
            space: space,
            members: members,
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
    /// Removes members of a space in one commit.
    /// </summary>
    /// <param name="space">The space reference, not a record URI.</param>
    /// <param name="dids">The member DIDs, up to 100 entries.</param>
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>The endpoint response and HTTP metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The space addresses a record, or the batch contains an invalid entry or more than 100 entries.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    /// <remarks><para>Absent members are ignored. The endpoint may return <c>SpaceNotFound</c>, <c>NotAuthorized</c> or <c>PolicyMismatch</c>.</para></remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    public async Task<AtProtoHttpResult<EmptyResponse>> RemoveSpaceMembers(
        SpaceAtUri space,
        IReadOnlyList<Did> dids,
        string? serviceProxy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(dids);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await AtProtoServer.RemoveSpaceMembers(
            space: space,
            dids: dids,
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
    /// Lists members of a space.
    /// </summary>
    /// <param name="space">The space reference, not a record URI.</param>
    /// <param name="limit">The maximum number of members, from 1 to 1000, or <see langword="null"/> for the server default.</param>
    /// <param name="cursor">An optional pagination cursor.</param>
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="cancellationToken">A token that can cancel the request.</param>
    /// <returns>A page of members and HTTP metadata.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="space"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="space"/> addresses a record.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside the permitted range.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    /// <exception cref="JsonException">The service returned an invalid member list.</exception>
    /// <remarks><para>The endpoint may return <c>SpaceNotFound</c>, <c>NotAuthorized</c>, <c>PolicyMismatch</c> or <c>InvalidCursor</c>.</para></remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    public async Task<AtProtoHttpResult<PagedReadOnlyCollection<SpaceMember>>> ListSpaceMembers(
        SpaceAtUri space,
        int? limit = null,
        string? cursor = null,
        string? serviceProxy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(space);
        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "The limit must be between 1 and 1000.");
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await AtProtoServer.ListSpaceMembers(
            space: space,
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
}
