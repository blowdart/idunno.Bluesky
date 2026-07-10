// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Chat.Group.Model;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Creates a join link for a group conversation.
    /// </summary>
    /// <param name="conversationId">The id of the conversation to create the link for.</param>
    /// <param name="requireApproval">Indicates whether approval is required to join the conversation.</param>
    /// <param name="joinRule">The rule for joining the conversation. Known values are in <see cref="Chat.Group.JoinRule"/></param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="conversationId"/> or <paramref name="joinRule"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="conversationId"/> or <paramref name="joinRule"/> is empty.</exception>
    public async Task<AtProtoHttpResult<CreateJoinLinkResponse>> CreateJoinGroupLink(
        string conversationId,
        bool requireApproval,
        string joinRule,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);
        ArgumentException.ThrowIfNullOrEmpty(joinRule);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.CreateJoinGroupLink(
            conversationId,
            requireApproval,
            joinRule,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}