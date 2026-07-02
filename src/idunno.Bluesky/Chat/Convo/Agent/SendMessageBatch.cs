// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Chat;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Sends the specified <paramref name="batchedMessages"/>.
    /// </summary>
    /// <param name="batchedMessages">The collection of <see cref="BatchedMessage"/>s to send.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="batchedMessages"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="batchedMessages"/> is empty or has greater than the maximum allowed number of messages.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<ICollection<MessageView>>> SendMessageBatch(
        ICollection<BatchedMessage> batchedMessages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batchedMessages);
        ArgumentOutOfRangeException.ThrowIfZero(batchedMessages.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(batchedMessages.Count, Maximum.BatchedMessages);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.SendMessageBatch(
            batchedMessages,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
