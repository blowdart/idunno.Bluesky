// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Chat;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Returns a page of incoming conversation requests for the user.
    /// Direct conversation requests are returned as <see cref="ConversationView"/>;
    /// group join requests made by the user are returned as <see cref="Chat.Group.JoinRequestConversationView"/>.
    ///
    /// The behavior above, from the lexicon does not match the actual API behavior, which returns all requests as <see cref="ConversationView"/>.
    /// See https://github.com/bluesky-social/atproto/issues/5175.
    /// </summary>
    /// <param name="limit">The maximum number of requests to return. Must be between 1 and 100.</param>
    /// <param name="cursor">The cursor to use for pagination.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the limit is less than 1 or greater than 100.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the user is not authenticated.</exception>
    public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ConversationViewBase>>> ListConversationRequests(
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (limit.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfZero(limit.Value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, 100);
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        AtProtoHttpResult<PagedViewReadOnlyCollection<ConversationViewBase>> result = await BlueskyServer.ListConversationRequests(
            limit,
            cursor,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result;
    }
}