// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Sync;

namespace idunno.AtProto;

public partial class AtProtoAgent
{
    /// <summary>
    /// Gets a blob, by its <see cref="Cid"/>, from the personal data server hosting the specified repository.
    /// </summary>
    /// <param name="repo">The DID or handle of the repository that references the blob.</param>
    /// <param name="cid">The CID of the blob to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// A result containing a <see cref="BlobContent"/> for the blob. The <see cref="BlobContent"/> owns the response and must be disposed.
    /// The <see cref="BlobContent.ContentType"/> is supplied by the server and is untrusted.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="repo"/> or <paramref name="cid"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">The repository identifier cannot be resolved to a DID or personal data server.</exception>
    public async Task<AtProtoHttpResult<BlobContent>> GetBlob(
        AtIdentifier repo,
        Cid cid,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(cid);

        (Did did, Uri pds) = await ResolveSyncRepo(repo, cancellationToken).ConfigureAwait(false);

        return await AtProtoServer.GetBlob(
            did,
            cid,
            pds,
            HttpClient,
            MaximumResponseSize,
            cancellationToken).ConfigureAwait(false);
    }
}
