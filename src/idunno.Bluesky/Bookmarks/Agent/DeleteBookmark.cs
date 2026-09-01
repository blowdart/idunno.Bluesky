// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Deletes a bookmark on the specified account for the specified <paramref name="uri"/>. Requires authentication.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the post to delete the bookmark for.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown if the agent is not authenticated.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, or its Collection property is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="uri"/>'s Collection property does not point to a post.</exception>
    public async Task<AtProtoHttpResult<EmptyResponse>> DeleteBookmark(
        AtUri uri,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        ArgumentNullException.ThrowIfNull(uri);

        ArgumentNullException.ThrowIfNull(uri.Collection);
        ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.Post);

        return await BlueskyServer.DeleteBookmark(
            uri,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
