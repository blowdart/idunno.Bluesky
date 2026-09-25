// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Unmutes the specified account. Requires authentication.
    /// </summary>
    /// <param name="actor">The <see cref="AtIdentifier"/> of the actor to unmute</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
    public async Task<AtProtoHttpResult<EmptyResponse>> UnmuteActor(
        AtIdentifier actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await Unmute(actor, cancellationToken).ConfigureAwait(false);
    }
}
