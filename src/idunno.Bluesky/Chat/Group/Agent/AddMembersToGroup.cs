// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Chat.Group.Model;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Adds members to a group conversation.
    /// </summary>
    /// <param name="conversationId">The ID of the conversation.</param>
    /// <param name="members">The members to add.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result of the add members operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    /// <exception cref="ArgumentException">Thrown when the conversation ID is <see langword="null" /> or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the members collection is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the members collection is empty.</exception>
    public async Task<AtProtoHttpResult<AddMembersResponse>> AddMembersToGroup(
        string conversationId,
        IEnumerable<Did> members,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentOutOfRangeException.ThrowIfZero(members.Count());

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.AddMembersToGroup(
            conversationId: conversationId,
            members: members,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}