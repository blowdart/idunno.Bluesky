// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Deletes the starter pack referenced by <paramref name="uri"/> from the current user's repository.
    /// </summary>
    /// <param name="uri">The URI of the starter pack to delete.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="uri"/> or its collection or record key is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="uri"/> does not identify a starter pack owned by the current user.</exception>
    /// <exception cref="AuthenticationRequiredException">The current agent is not authenticated.</exception>
    /// <remarks>
    /// <para>This method deletes only the starter pack record. It does not delete the referenced list or feeds.</para>
    /// </remarks>
    public async Task<AtProtoHttpResult<DeleteResult>> DeleteStarterPack(
        AtUri uri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(uri.Collection);
        ArgumentNullException.ThrowIfNull(uri.RecordKey);
        ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.StarterPack);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        if (!await IsRecordOwnedBy(uri, Did, cancellationToken).ConfigureAwait(false))
        {
            throw new ArgumentException("Starter pack was not created by the current user.", nameof(uri));
        }

        return await DeleteRecord(uri, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
