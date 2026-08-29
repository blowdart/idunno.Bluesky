// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Creates a mute relationship for the specified list of accounts. Requires authentication.
    /// </summary>
    /// <param name="listUri">The <see cref="AtUri"/> of the list of actors to mute.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="listUri"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
    public async Task<AtProtoHttpResult<EmptyResponse>> MuteActorList(
        AtUri listUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listUri);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.MuteActorList(
            listUri,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
