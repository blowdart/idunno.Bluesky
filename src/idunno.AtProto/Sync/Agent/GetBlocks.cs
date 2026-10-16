// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto;

public partial class AtProtoAgent
{
    /// <summary>
    /// Gets data blocks, by their <see cref="Cid"/>s, from the personal data server hosting the specified repository, returned as a CAR file.
    /// </summary>
    /// <param name="repo">The DID or handle of the repository containing the blocks.</param>
    /// <param name="cids">The CIDs of the blocks to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A result containing a stream for the CAR response. The stream owns the response and must be disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="repo"/> or <paramref name="cids"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="cids"/> is empty or contains a <see langword="null"/> entry, or the repository identifier cannot be resolved
    /// to a DID or personal data server.
    /// </exception>
    public async Task<AtProtoHttpResult<Stream>> GetBlocks(
        AtIdentifier repo,
        IEnumerable<Cid> cids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(cids);

        List<Cid> cidList = [.. cids];
        if (cidList.Count == 0)
        {
            throw new ArgumentException("At least one CID must be specified.", nameof(cids));
        }

        (Did did, Uri pds) = await ResolveSyncRepo(repo, cancellationToken).ConfigureAwait(false);

        return await AtProtoServer.GetBlocks(
            did,
            cidList,
            pds,
            HttpClient,
            MaximumResponseSize,
            cancellationToken).ConfigureAwait(false);
    }
}
