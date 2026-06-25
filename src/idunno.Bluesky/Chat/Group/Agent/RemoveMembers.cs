// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Chat.Group.Model;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Removes the specified <paramref name="members"/> from the conversation identified by <paramref name="conversationId"/>.
    /// </summary>
    /// <param name="conversationId">The id of the group to remove members from.</param>
    /// <param name="members">The <see cref="ICollection{Did}"/> of users to remove from the group.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="conversationId"/> or <paramref name="members"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<RemoveMembersResponse>> RemoveMembers(
        string conversationId,
        ICollection<Did> members,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversationId);
        ArgumentNullException.ThrowIfNull(members);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.RemoveMembers(
            conversationId,
            members,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
