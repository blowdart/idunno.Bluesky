// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Mutes a thread preventing notifications from the thread and any of its children. Requires authentication.
    /// </summary>
    /// <param name="rootUri">The <see cref="AtUri"/> of the thread to mute</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rootUri"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is unauthenticated.</exception>
    public async Task<AtProtoHttpResult<EmptyResponse>> MuteThread(
        AtUri rootUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rootUri);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.MuteThread(
            rootUri,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
