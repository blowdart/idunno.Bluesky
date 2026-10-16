// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Sync;

namespace idunno.AtProto;

public partial class AtProtoAgent
{
    /// <summary>
    /// Gets the hosting status of the specified repository, from the personal data server hosting it.
    /// </summary>
    /// <param name="repo">The DID or handle of the repository.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="repo"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The repository identifier cannot be resolved to a DID or personal data server.</exception>
    /// <remarks>
    /// <para>The service may return a <c>RepoNotFound</c> error.</para>
    /// </remarks>
    public async Task<AtProtoHttpResult<RepoHostingStatus>> GetRepoStatus(
        AtIdentifier repo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repo);

        (Did did, Uri pds) = await ResolveSyncRepo(repo, cancellationToken).ConfigureAwait(false);

        return await AtProtoServer.GetRepoStatus(
            did,
            pds,
            HttpClient,
            LoggerFactory,
            MaximumResponseSize,
            cancellationToken).ConfigureAwait(false);
    }
}
