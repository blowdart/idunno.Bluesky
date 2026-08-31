// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Deletes the list referenced by its <paramref name="uri"/>.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the list to delete.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> or its collection property is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="uri"/> does not point to a list.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<Commit>> DeleteList(
        AtUri uri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(uri.Collection);
        ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.List);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await DeleteRecord(
            uri: uri,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
