// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{

    /// <summary>
    /// Unmutes the specified moderation list. Requires authentication.
    /// </summary>
    /// <param name="listUri">The <see cref="AtUri"/> of the list of actors to unmute.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="listUri"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
    public async Task<AtProtoHttpResult<EmptyResponse>> UnmuteModList(
        AtUri listUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listUri);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await UnmuteActorList(listUri, cancellationToken).ConfigureAwait(false);
    }

}
