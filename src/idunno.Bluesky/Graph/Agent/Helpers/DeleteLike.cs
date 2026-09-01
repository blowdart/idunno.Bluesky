// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Deletes the like record for the post referred to by <paramref name="uri"/>.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the like record to delete.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">if <paramref name="uri"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">if the agent is not authenticated.</exception>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="uri"/> does not point to a Bluesky feed repost record, or its RecordKey is <see langword="null"/>.</exception>
    public async Task<AtProtoHttpResult<Commit>> DeleteLike(AtUri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(uri.Collection);
        ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.Like);
        ArgumentNullException.ThrowIfNull(uri.RecordKey);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        // Get the post view for the specified post so we can get the like record uri if one exists.
        AtProtoHttpResult<Feed.PostView> postViewResult = await GetPostView(uri, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (postViewResult.StatusCode != HttpStatusCode.OK)
        {
            return new AtProtoHttpResult<Commit>(
                null,
                statusCode: postViewResult.StatusCode,
                httpResponseHeaders: postViewResult.HttpResponseHeaders,
                atErrorDetail: postViewResult.AtErrorDetail,
                rateLimit: postViewResult.RateLimit);
        }

        if (postViewResult.Result is null)
        {
            return new AtProtoHttpResult<Commit>(
                null,
                statusCode: HttpStatusCode.BadRequest,
                httpResponseHeaders: postViewResult.HttpResponseHeaders,
                atErrorDetail: new AtErrorDetail("RecordNotFound", "Could not locate record:{uri}"),
                rateLimit: postViewResult.RateLimit);
        }
        else if (postViewResult.Result.Viewer is null ||
            postViewResult.Result.Viewer.Like is null)
        {
            return new AtProtoHttpResult<Commit>(
                null,
                statusCode: HttpStatusCode.NotFound,
                httpResponseHeaders: postViewResult.HttpResponseHeaders,
                atErrorDetail: new AtErrorDetail("LikeNotFound", "No like record was found in {uri}."),
                rateLimit: postViewResult.RateLimit);
        }

        return await DeleteRecord(
            collection: CollectionNsid.Like,
            rKey: postViewResult.Result.Viewer.Like.RecordKey!,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes like record for the post specified by <see cref="StrongReference"/>.
    /// </summary>
    /// <param name="strongReference">The <see cref="StrongReference"/> of the post whose like should be deleted.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<Commit>> DeleteLike(StrongReference strongReference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strongReference);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await DeleteLike(strongReference.Uri, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
