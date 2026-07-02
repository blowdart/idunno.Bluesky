// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Chat;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Leaves the conversation identified by <paramref name="id"/>.
    /// </summary>
    /// <param name="id">The conversation identifier to leave.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see langword="null"/> or white space.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<ConversationReference>> LeaveConversation(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.LeaveConversation(
            id,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
