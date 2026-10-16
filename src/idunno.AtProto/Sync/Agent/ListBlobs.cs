// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Sync;

namespace idunno.AtProto;

public partial class AtProtoAgent
{
    /// <summary>
    /// Lists the CIDs of the blobs in the specified repository, optionally since a repository revision,
    /// from the personal data server hosting it.
    /// </summary>
    /// <param name="repo">The DID or handle of the repository.</param>
    /// <param name="since">An optional revision of the repository to list blobs since.</param>
    /// <param name="limit">The maximum number of CIDs to return. Must be between 1 and 1000 if specified. The server default is 500.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="repo"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is less than 1 or greater than 1000.</exception>
    /// <exception cref="ArgumentException">The repository identifier cannot be resolved to a DID or personal data server.</exception>
    /// <remarks>
    /// <para>The service may return a <c>RepoNotFound</c>, <c>RepoTakendown</c>, <c>RepoSuspended</c> or <c>RepoDeactivated</c> error.</para>
    /// </remarks>
    public async Task<AtProtoHttpResult<PagedCidCollection>> ListBlobs(
        AtIdentifier repo,
        TimestampIdentifier? since = null,
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repo);

        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "limit must be between 1 and 1000.");
        }

        (Did did, Uri pds) = await ResolveSyncRepo(repo, cancellationToken).ConfigureAwait(false);

        return await AtProtoServer.ListBlobs(
            did,
            since,
            limit,
            cursor,
            pds,
            HttpClient,
            LoggerFactory,
            MaximumResponseSize,
            cancellationToken).ConfigureAwait(false);
    }
}
