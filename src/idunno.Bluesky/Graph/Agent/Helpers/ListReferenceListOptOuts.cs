// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets a list of reference list opt-outs for the current user.
    /// </summary>
    /// <param name="limit">The maximum number of records to return.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord<ReferenceListOptOut>>>> ListReferenceListOptOuts(int? limit, CancellationToken cancellationToken)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await ListBlueskyRecords<ReferenceListOptOut>(
            repo: Did,
            collection: CollectionNsid.ReferenceListOptOut,
            limit: limit,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a list of reference list opt-outs for the current user.
    /// </summary>
    /// <param name="limit">The maximum number of records to return.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord<ReferenceListOptOut>>>> ListReferenceListOptOuts(int? limit)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await ListReferenceListOptOuts(
            limit: limit,
            cancellationToken: default).ConfigureAwait(false);
    }


    /// <summary>
    /// Gets a list of reference list opt-outs for the current user.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord<ReferenceListOptOut>>>> ListReferenceListOptOuts(CancellationToken cancellationToken)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await ListReferenceListOptOuts(
            limit: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a list of reference list opt-outs for the current user.
    /// </summary>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord<ReferenceListOptOut>>>> ListReferenceListOptOuts()
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await ListReferenceListOptOuts(
            limit: null,
            cancellationToken: default).ConfigureAwait(false);
    }
}
