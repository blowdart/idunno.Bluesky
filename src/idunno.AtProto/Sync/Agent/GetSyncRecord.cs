// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto;

public partial class AtProtoAgent
{
    /// <summary>
    /// Gets the data blocks needed to prove the existence, or non-existence, of a record in the current version of a repository,
    /// from the personal data server hosting it, returned as a CAR file.
    /// </summary>
    /// <param name="repo">The DID or handle of the repository.</param>
    /// <param name="collection">The NSID of the collection containing the record.</param>
    /// <param name="rKey">The record key of the record.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A result containing a stream for the CAR response. The stream owns the response and must be disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="repo"/>, <paramref name="collection"/>, or <paramref name="rKey"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when the repository identifier cannot be resolved to a DID or personal data server.</exception>
    /// <remarks>
    /// <para>This calls <c>com.atproto.sync.getRecord</c>. To retrieve a record as JSON use <c>GetRecord&lt;TRecord&gt;</c>.</para>
    /// <para>The server may return the following errors: <c>RecordNotFound</c>, <c>RepoNotFound</c>, <c>RepoTakendown</c>,
    /// <c>RepoSuspended</c> and <c>RepoDeactivated</c>.</para>
    /// </remarks>
    public async Task<AtProtoHttpResult<Stream>> GetSyncRecord(
        AtIdentifier repo,
        Nsid collection,
        RecordKey rKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(rKey);

        (Did did, Uri pds) = await ResolveSyncRepo(repo, cancellationToken).ConfigureAwait(false);

        return await AtProtoServer.GetSyncRecord(
            did,
            collection,
            rKey,
            pds,
            HttpClient,
            MaximumResponseSize,
            cancellationToken).ConfigureAwait(false);
    }
}
