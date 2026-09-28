// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo;

namespace idunno.AtProto;

public partial class AtProtoAgent
{
    /// <summary>
    /// Gets the current commit CID and revision of the specified repository, from the personal data server hosting it.
    /// </summary>
    /// <param name="repo">The DID or handle of the repository.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="repo"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The repository identifier cannot be resolved to a DID or personal data server.</exception>
    public async Task<AtProtoHttpResult<Commit>> GetLatestCommit(
        AtIdentifier repo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repo);

        (Did did, Uri pds) = await ResolveSyncRepo(repo, cancellationToken).ConfigureAwait(false);

        return await AtProtoServer.GetLatestCommit(
            did,
            pds,
            HttpClient,
            LoggerFactory,
            MaximumResponseSize,
            cancellationToken).ConfigureAwait(false);
    }
}
