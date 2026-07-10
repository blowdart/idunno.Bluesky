// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Chat.Group.Model;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Edits the join link properties for a conversation
    /// </summary>
    /// <param name="conversationId">The id of the conversation to edit.</param>
    /// <param name="requireApproval">Flag indicating whether the conversation owner needs to approve joins.</param>
    /// <param name="joinRule">The join rule for the conversation. Known values are in <see cref="Chat.Group.JoinRule"/></param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="conversationId"/> is <see langword="null"/>.</exception>
    public async Task<AtProtoHttpResult<EditJoinLinkResponse>> EditJoinGroupLink(
        string conversationId,
        bool? requireApproval = null,
        string? joinRule = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversationId);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.EditJoinGroupLink(
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