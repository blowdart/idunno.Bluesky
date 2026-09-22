// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Deletes the like record. If a post <see cref="AtUri"/> is specified, it will delete the like of that post. If a like <see cref="AtUri"/> is specified, it will delete that like record.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the post to delete the like of, or the direct <see cref="AtUri"/> to a like record.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">if <paramref name="uri"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">if the agent is not authenticated.</exception>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="uri"/> does not point to a Bluesky feed post or like record, or its RecordKey is <see langword="null"/>.</exception>
    /// <exception cref="BlueskyException">Thrown when the like record discovery returns an invalid <see cref="AtUri"/> with no record key.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Pre-existing overload set. The return type changed in this release, which makes the analyzer treat these as newly added overloads.")]
    public async Task<AtProtoHttpResult<DeleteResult>> DeleteLike(AtUri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(uri.Collection);
        ArgumentNullException.ThrowIfNull(uri.RecordKey);

        if (uri.Collection != CollectionNsid.Post && uri.Collection != CollectionNsid.Like)
        {
            throw new ArgumentException($"uri does not point to an {CollectionNsid.Post} or {CollectionNsid.Like} record", nameof(uri));
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        RecordKey rKey;

        if (uri.Collection == CollectionNsid.Post)
        {
            // Get the post view for the specified post so we can get the like record uri if one exists.
            AtProtoHttpResult<Feed.PostView> postViewResult = await GetPostView(uri, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (postViewResult.StatusCode != HttpStatusCode.OK)
            {
                return new AtProtoHttpResult<DeleteResult>(
                    null,
                    statusCode: postViewResult.StatusCode,
                    httpResponseHeaders: postViewResult.HttpResponseHeaders,
                    atErrorDetail: postViewResult.AtErrorDetail,
                    rateLimit: postViewResult.RateLimit);
            }

            if (postViewResult.Result is null)
            {
                return new AtProtoHttpResult<DeleteResult>(
                    null,
                    statusCode: HttpStatusCode.BadRequest,
                    httpResponseHeaders: postViewResult.HttpResponseHeaders,
                    atErrorDetail: new AtErrorDetail("RecordNotFound", $"Could not locate record {uri}."),
                    rateLimit: postViewResult.RateLimit);
            }
            else if (postViewResult.Result.Viewer is null ||
                postViewResult.Result.Viewer.Like is null)
            {
                return new AtProtoHttpResult<DeleteResult>(
                    null,
                    statusCode: HttpStatusCode.NotFound,
                    httpResponseHeaders: postViewResult.HttpResponseHeaders,
                    atErrorDetail: new AtErrorDetail("LikeNotFound", $"No like record was found in {uri}."),
                    rateLimit: postViewResult.RateLimit);
            }
            else if (postViewResult.Result.Viewer.Like.RecordKey is null)
            {
                throw new BlueskyException($"Like RecordKey is null in post view result for uri {uri}.");
            }

            rKey = postViewResult.Result.Viewer.Like.RecordKey;
        }
        else
        {
            rKey = uri.RecordKey;
        }

        return await DeleteRecord(
            collection: CollectionNsid.Like,
            rKey: rKey,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the like record for the post referenced by <see cref="StrongReference"/>.
    /// </summary>
    /// <param name="strongReference">The <see cref="StrongReference"/> of the post to delete the like of.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Pre-existing overload set. The return type changed in this release, which makes the analyzer treat these as newly added overloads.")]
    public async Task<AtProtoHttpResult<DeleteResult>> DeleteLike(StrongReference strongReference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strongReference);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await DeleteLike(strongReference.Uri, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
