// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Deletes the Bluesky follow record specified by its <see cref="Uri"/>.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the follow record to delete.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="uri"/> does not point to a Bluesky follow record, or its RecordKey is <see langword="null"/>.</exception>
    public async Task<AtProtoHttpResult<Commit>> DeleteFollow(AtUri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        if (uri.Collection != CollectionNsid.Follow)
        {
            throw new ArgumentException($"uri does not point to an {CollectionNsid.Follow} record", nameof(uri));
        }

        if (uri.RecordKey is null)
        {
            throw new ArgumentException("uri RecordKey is null", nameof(uri));
        }

        return await DeleteRecord(uri.Collection, uri.RecordKey, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the Bluesky follow record specified by its <see cref="StrongReference"/>.
    /// </summary>
    /// <param name="strongReference">The <see cref="StrongReference"/> of the follow record to delete.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<Commit>> DeleteFollow(StrongReference strongReference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strongReference);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await DeleteFollow(strongReference.Uri, cancellationToken).ConfigureAwait(false);
    }
}
