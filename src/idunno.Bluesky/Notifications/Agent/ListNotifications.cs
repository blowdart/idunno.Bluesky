// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.Bluesky.Notifications;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets the notifications for the requesting account. Requires authentication.
    /// </summary>
    /// <param name="limit">The maximum number of notifications to return. If specified this should be &gt;=1 and &lt;= 100.</param>
    /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
    /// <param name="reasons">An optional collection of <see cref="NotificationReason"/>s to limit the results to.</param>
    /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is less than 1 or greater than 100.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="reasons"/> contains <see cref="NotificationReason.Unknown"/>.</exception>
    public async Task<AtProtoHttpResult<NotificationCollection>> ListNotifications(
        int? limit = null,
        string? cursor = null,
        IEnumerable<NotificationReason>? reasons = null,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.ListNotifications(
            limit,
            cursor,
            reasons,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            subscribedLabelers: subscribedLabelers,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}