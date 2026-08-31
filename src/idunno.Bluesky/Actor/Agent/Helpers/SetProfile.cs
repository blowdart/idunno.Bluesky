// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Updates the current users profile.
    /// </summary>
    /// <param name="profile">The profile update to</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/>, or its Value is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<PutRecordResult>> SetProfile(
        AtProtoRepositoryRecord<Profile> profile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(profile.Value);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await UpdateProfile(profile, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the current users profile.
    /// </summary>
    /// <param name="profile">The profile update to</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public async Task<AtProtoHttpResult<PutRecordResult>> SetProfile(
        AtProtoRepositoryRecord<Profile> profile)
    {
        return await SetProfile(profile, default).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates or updates the current users profile.
    /// </summary>
    /// <param name="profile">The profile to create from or update to</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<PutRecordResult>> SetProfile(
        Profile profile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await UpdateProfile(profile, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates or updates the current users profile.
    /// </summary>
    /// <param name="profile">The profile to create from or update to</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<PutRecordResult>> SetProfile(
        Profile profile)
    {
        return await SetProfile(profile, default).ConfigureAwait(false);
    }
}
