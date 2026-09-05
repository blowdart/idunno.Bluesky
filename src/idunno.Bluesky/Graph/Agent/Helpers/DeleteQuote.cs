// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Deletes the Bluesky quote record specified by its <paramref name="uri" />.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the quote to delete.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<Commit>> DeleteQuote(AtUri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await DeletePost(uri, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the Bluesky quote record specified by its <paramref name="strongReference"/>.
    /// </summary>
    /// <param name="strongReference">The <see cref="StrongReference"/> of the quote to delete.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    /// <remarks>
    /// <para>A quote record is really a post record, so DeletePost() would also work. This method is just here for ease of discover and consistency.</para>
    /// </remarks>
    public async Task<AtProtoHttpResult<Commit>> DeleteQuote(StrongReference strongReference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strongReference);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await DeletePost(strongReference, cancellationToken).ConfigureAwait(false);
    }


}
