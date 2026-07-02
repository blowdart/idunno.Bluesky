// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Chat;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Enumerates the messages in a conversation.
    /// </summary>
    /// <param name="id">The conversation identifier whose messages should be retrieved.</param>
    /// <param name="limit">An optional limit on the number of messages to retrieve in each page.</param>
    /// <param name="cursor">An optional cursor used for pagination.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see langword="null"/> or white space.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<Messages>> GetMessages(
        string id,
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.GetMessages(
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
