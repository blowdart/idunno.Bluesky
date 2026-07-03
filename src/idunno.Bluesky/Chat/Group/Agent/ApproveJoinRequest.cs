// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Chat.Group.Model;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Approves the join request of a user, specified by <paramref name="member"/>, to a group conversation, specified by <paramref name="conversationId"/>.
    /// </summary>
    /// <param name="conversationId">The ID of the conversation.</param>
    /// <param name="member">The member to approve.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result of the approve join operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    /// <exception cref="ArgumentException">Thrown when the conversation ID is <see langword="null" /> or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the member is <see langword="null" />.</exception>
    public async Task<AtProtoHttpResult<ApproveJoinResponse>> ApproveJoinRequest(
        string conversationId,
        Did member,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentNullException.ThrowIfNull(member);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.ApproveJoinRequest(
            conversationId: conversationId,
            member: member,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}