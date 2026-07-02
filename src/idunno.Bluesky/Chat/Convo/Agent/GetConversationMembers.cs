// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets a <see cref="PagedViewReadOnlyCollection{ProfileViewBasic}"/> representing the members of a conversation.
    /// </summary>
    /// <param name="id">The identifier of the conversation.</param>
    /// <param name="limit">An optional limit on the number of members to retrieve in each page.</param>
    /// <param name="cursor">An optional cursor used for pagination.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileViewBasic>>> GetConversationMembers(
        string id,
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.GetConversationMembers(
            id,
            limit,
            cursor,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
