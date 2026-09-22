// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Removes the block on the actors in the specified moderation list, by deleting the block record the
    /// authenticated user holds against the list. Requires authentication.
    /// </summary>
    /// <param name="listUri">The <see cref="AtUri"/> of the moderation list of actors to unblock.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="listUri"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="listUri"/> does not point to a list record.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
    public async Task<AtProtoHttpResult<DeleteResult>> UnblockModList(
        AtUri listUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listUri);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        if (listUri.Collection != CollectionNsid.List)
        {
            throw new ArgumentException($"listUri does not point to an {CollectionNsid.List} record", nameof(listUri));
        }

        AtProtoHttpResult<ListViewWithItems> listResult =
            await GetList(listUri, limit: 1, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!listResult.Succeeded)
        {
            Logger.UnblockModListFailedAsListCouldNotBeRead(_logger, listUri);

            return new AtProtoHttpResult<DeleteResult>(
                null,
                listResult.StatusCode,
                listResult.HttpResponseHeaders,
                listResult.AtErrorDetail,
                listResult.RateLimit);
        }

        if (listResult.Result.List.Viewer is null || listResult.Result.List.Viewer.Blocked is null)
        {
            Logger.UnblockModListFailedAsUserIsNotBlocking(_logger, listUri);

            return new AtProtoHttpResult<DeleteResult>(
                null,
                HttpStatusCode.NotFound,
                listResult.HttpResponseHeaders,
                null,
                listResult.RateLimit);
        }

        return await DeleteRecord(
            listResult.Result.List.Viewer.Blocked,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
