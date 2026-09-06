// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Creates a bookmark on the specified account from the specified <paramref name="uri"/> and <paramref name="cid"/>. Requires authentication.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the post to bookmark</param>
    /// <param name="cid">The <see cref="Cid"/> of the post to bookmark</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> or <paramref name="cid"/>are <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="uri"/> does not point to a post</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown if the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<EmptyResponse>> CreateBookmark(
        AtUri uri,
        Cid cid,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(cid);

        ArgumentNullException.ThrowIfNull(uri.Collection);
        ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.Post);

        return await BlueskyServer.CreateBookmark(
            uri,
            cid,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a bookmark on the specified account from the specified <paramref name="strongReference"/>. Requires authentication.
    /// </summary>
    /// <param name="strongReference">The <see cref="StrongReference"/> of the post to bookmark</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown if the agent is not authenticated.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is <see langword="null"/>.</exception>
    public async Task<AtProtoHttpResult<EmptyResponse>> CreateBookmark(
        StrongReference strongReference,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        ArgumentNullException.ThrowIfNull(strongReference);

        return await BlueskyServer.CreateBookmark(
            strongReference.Uri,
            strongReference.Cid,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
