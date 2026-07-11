// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Chat;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Lists a page of mutual groups as a <see cref="PagedViewReadOnlyCollection{ConversationView}"/> for the specified user.
    /// </summary>
    /// <param name="subject">The <see cref="Did"/> of the user to list mutual groups for.</param>
    /// <param name="limit">An optional limit for the number of items to return.</param>
    /// <param name="cursor">An optional cursor used for pagination.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="subject"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is less than 1 or greater than 100.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the user is not authenticated.</exception>
    public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ConversationView>>> ListMutualGroups(
        Did subject,
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        if (limit is not null && (limit < 1 || limit > 100))
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 100.");
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.ListMutualGroups(
            subject,
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