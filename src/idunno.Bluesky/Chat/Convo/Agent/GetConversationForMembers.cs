// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Chat;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Get or create a 1-1 conversation for the given members. Always returns the same direct (non-group) conversation. To create a group conversation, use <see cref="CreateGroup(ICollection{Did}, string, CancellationToken)"/>.
    /// </summary>
    /// <param name="members">The <see cref="Did"/>s of the conversation members.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="members"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="members"/> is empty or has greater than the maximum number of conversation members.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<ConversationView>> GetConversationForMembers(
        ICollection<Did> members,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(members);

        ArgumentOutOfRangeException.ThrowIfZero(members.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(members.Count, Maximum.ConversationMembers);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.GetConversationForMembers(
            members,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
