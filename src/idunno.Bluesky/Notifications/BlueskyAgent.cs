// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.


using idunno.AtProto;
using idunno.Bluesky.Notifications;

namespace idunno.Bluesky
{
    public partial class BlueskyAgent
    {
        /// <summary>
        /// Gets a count the number of unread notifications for the requesting account.
        /// </summary>
        /// <param name="seenAt">An optional <see cref="DateTimeOffset"/> indicating when notifications were last checked.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="AtProtoHttpResult{T}"/> wrapping an integer indicating the unread notification count.</returns>
        public async Task<AtProtoHttpResult<int>> GetNotificationUnreadCount(DateTimeOffset? seenAt = null, CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.GetNotificationUnreadCount(
                seenAt,
                Service,
                AccessToken,
                HttpClient,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the notifications for the requesting account.
        /// </summary>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the current session is not authenticated.</exception>
        public async Task<AtProtoHttpResult<NotificationCollection>> ListNotifications(IEnumerable<Did>? subscribedLabelers = null, CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await ListNotifications(null, null, null, subscribedLabelers, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the notifications for the requesting account.
        /// </summary>
        /// <param name="limit">The maximum number of notifications to return. If specified this should be &gt;=1 and &lt;= 100.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="seenAt">The date and time notifications were last checked.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the current session is not authenticated.</exception>
        public async Task<AtProtoHttpResult<NotificationCollection>> ListNotifications(
            int? limit = null,
            string? cursor = null,
            DateTimeOffset? seenAt = null,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            return await BlueskyServer.ListNotifications(
                limit,
                cursor,
                seenAt,
                Service,
                AccessToken,
                HttpClient,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken:cancellationToken).ConfigureAwait(false);
        }


        /// <summary>
        /// Updates the date and time notifications were last seen for the current user.
        /// </summary>
        /// <param name="seenAt">An optional <see cref="DateTimeOffset"/> indicating when notifications were last checked.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown if the current session is not authenticated.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> UpdateNotificationSeenAt(DateTimeOffset? seenAt = null, CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            seenAt ??= DateTimeOffset.UtcNow;

            return await BlueskyServer.UpdateNotificationSeenAt(
                (DateTimeOffset)seenAt,
                Service,
                AccessToken,
                HttpClient,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
