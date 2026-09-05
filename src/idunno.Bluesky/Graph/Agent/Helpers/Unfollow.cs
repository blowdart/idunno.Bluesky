// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Unfollows the specified <paramref name="handle"/>.
    /// </summary>
    /// <param name="handle">The <see cref="Handle"/> of the actor to unfollow.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<Commit>> Unfollow(Handle handle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handle);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        ArgumentNullException.ThrowIfNull(handle);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        Did? didResolutionResult = await ResolveHandle(handle, cancellationToken).ConfigureAwait(false);

        if (didResolutionResult is null)
        {
            Logger.UnfollowFailedAsHandleCouldNotResolve(_logger, handle);
        }

        if (didResolutionResult is null || cancellationToken.IsCancellationRequested)
        {
            return new AtProtoHttpResult<Commit>(
                null,
                HttpStatusCode.NotFound,
                null,
                null);
        }

        return await Unfollow(didResolutionResult, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Unfollows the specified <paramref name="did"/>.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> of the actor to unfollow.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<Commit>> Unfollow(Did did, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(did);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        AtProtoHttpResult<ProfileViewDetailed> userProfileResult = await GetProfile(did, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!userProfileResult.Succeeded)
        {
            Logger.UnfollowFailedAsHandleCouldNotGetUserProfile(_logger, did);

            return new AtProtoHttpResult<Commit>(
                null,
                userProfileResult.StatusCode,
                userProfileResult.HttpResponseHeaders,
                userProfileResult.AtErrorDetail,
                userProfileResult.RateLimit);
        }

        // Now check to see if the current user has a follow relationship with the did

        if (userProfileResult.Result.Viewer is null || userProfileResult.Result.Viewer.Following is null)
        {
            Logger.UnfollowFailedAsHandleCouldNotGetUserIsNotFollowing(_logger, did);

            return new AtProtoHttpResult<Commit>(
                null,
                HttpStatusCode.NotFound,
                userProfileResult.HttpResponseHeaders,
                null,
                userProfileResult.RateLimit);
        }

        return await DeleteFollow(userProfileResult.Result.Viewer.Following, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
