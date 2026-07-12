// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Chat.Group;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Lists a page of request as a <see cref="PagedViewReadOnlyCollection{JoinRequestView}"/> to join a group (via join link) the user owns. Shows the data from the owner's point of view.
    /// </summary>
    /// <param name="conversationIds">The ids of the conversations to list join requests for.</param>
    /// <param name="cursor">An optional cursor used for pagination.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="conversationIds"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="conversationIds"/> is empty or contains more than 100 items.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the user is not authenticated.</exception>
    public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<JoinRequestConversationView>>> ListJoinGroupRequests(
        ICollection<string> conversationIds,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversationIds);
        ArgumentOutOfRangeException.ThrowIfZero(conversationIds.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(conversationIds.Count, 100);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.ListJoinGroupRequests(
            conversationIds,
            cursor,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}