// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets a count the number of unread notifications for the requesting account. Requires authentication.
    /// </summary>
    /// <param name="seenAt">An optional <see cref="DateTimeOffset"/> indicating when notifications were last checked.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>An <see cref="AtProtoHttpResult{T}"/> wrapping an integer indicating the unread notification count.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    /// <remarks>
    /// <para>The result is <see langword="null"/> if the API call failed, which is distinct from a successful call which returned a count of zero.</para>
    /// </remarks>
    public async Task<AtProtoHttpResult<int?>> GetNotificationUnreadCount(DateTimeOffset? seenAt = null, CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.GetNotificationUnreadCount(
            seenAt,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
