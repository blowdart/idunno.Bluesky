// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets a <see cref="ProfileViewDetailed"/> for the specified <paramref name="actor"/>.
    /// </summary>
    /// <param name="actor">The actor to retrieve the <see cref="ProfileViewDetailed"/> for.</param>
    /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/> is <see langword="null"/>.</exception>
    public async Task<AtProtoHttpResult<ProfileViewDetailed>> GetProfile(
        AtIdentifier actor,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        return await BlueskyServer.GetProfile(
            actor,
            AuthenticatedOrUnauthenticatedServiceUri,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }


    /// <summary>
    /// Gets a <see cref="Profile"/> for the current authenticated user.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown if the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<Profile>>> GetProfile(CancellationToken cancellationToken)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await GetProfileRecord(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a <see cref="Profile"/> for the current authenticated user.
    /// </summary>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown if the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<Profile>>> GetProfile()
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await GetProfileRecord(default).ConfigureAwait(false);
    }

}
