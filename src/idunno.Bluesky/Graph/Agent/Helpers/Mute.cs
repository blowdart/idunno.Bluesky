// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Completely mutes the specified account.
    /// If a mute already exists for the account, it is updated in place: the stored scope is replaced with the scope in this request.
    /// Requires authentication.
    /// </summary>
    /// <param name="actor">The <see cref="AtIdentifier"/> of the actor to mute.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
    public async Task<AtProtoHttpResult<EmptyResponse>> Mute(
        AtIdentifier actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await Mute(
            actor,
            onlyReposts: null,
            onlyQuotePosts: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }


    /// <summary>
    /// Creates or updates a mute relationship for the specified account.
    /// If a mute already exists for the account, it is updated in place: the stored scope is replaced with the scope in this request.
    /// Requires authentication.
    /// </summary>
    /// <param name="actor">The <see cref="AtIdentifier"/> of the actor to mute.</param>
    /// <param name="onlyReposts">Flag indicating whether to restrict the mute to the account's reposts. When <see langword="true"/>, just the scoped content is muted; when no scoped mutes are set the account is fully muted.</param>
    /// <param name="onlyQuotePosts">Flag indicating whether to restrict the mute to the account's quotes. When <see langword="true"/>, just the scoped content is muted; when no scoped mutes are set the account is fully muted.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
    public async Task<AtProtoHttpResult<EmptyResponse>> Mute(
        AtIdentifier actor,
        bool? onlyReposts,
        bool? onlyQuotePosts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await MuteActor(
            actor,
            onlyReposts,
            onlyQuotePosts,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
