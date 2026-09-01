// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Deletes the repost record. If a post <see cref="AtUri"/> is specified, it will delete the repost of that post. If a repost <see cref="AtUri"/> is specified, it will delete that repost record.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the post to delete the repost of, or the direct <see cref="AtUri"/> to a repost record..</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">if <paramref name="uri"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">if the agent is not authenticated.</exception>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="uri"/> does not point to a Bluesky feed repost record, or its RecordKey is <see langword="null"/>.</exception>
    /// <exception cref="BlueskyException">Thrown when the repost record discovery returns an invalid <see cref="AtUri"/> with no record key.</exception>
    public async Task<AtProtoHttpResult<Commit>> DeleteRepost(AtUri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(uri.Collection);
        ArgumentNullException.ThrowIfNull(uri.RecordKey);

        if (uri.Collection != CollectionNsid.Post && uri.Collection != CollectionNsid.Repost)
        {
            throw new ArgumentException($"uri does not point to an {CollectionNsid.Post} or {CollectionNsid.Repost} record", nameof(uri));
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        RecordKey rKey;

        if (uri.Collection == CollectionNsid.Post)
        {
            // Get the post view for the specified post so we can get the repost record uri if one exists.
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
                postViewResult.Result.Viewer.Repost is null)
            {
                return new AtProtoHttpResult<Commit>(
                    null,
                    statusCode: HttpStatusCode.NotFound,
                    httpResponseHeaders: postViewResult.HttpResponseHeaders,
                    atErrorDetail: new AtErrorDetail("RepostNotFound", "No repost record for the was found in {uri}."),
                    rateLimit: postViewResult.RateLimit);
            }
            else if (postViewResult.Result.Viewer.Repost.RecordKey is null)
            {
                throw new BlueskyException("Repost RecordKey is null in post view result for uri:{uri}");
            }

            rKey = postViewResult.Result.Viewer.Repost.RecordKey;
        }
        else
        {
            ArgumentNullException.ThrowIfNull(uri.RecordKey);
            rKey = uri.RecordKey;
        }

        return await DeleteRecord(
             collection: CollectionNsid.Repost,
             rKey: rKey,
             cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the repost record post referenced by <see cref="StrongReference"/>.
    /// </summary>
    /// <param name="strongReference">The <see cref="StrongReference"/> of the post to delete the repost of.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<Commit>> DeleteRepost(StrongReference strongReference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strongReference);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await DeleteRepost(strongReference.Uri, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
