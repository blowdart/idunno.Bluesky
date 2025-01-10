// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.Bluesky.Notifications;
using idunno.Bluesky.Notifications.Model;

namespace idunno.Bluesky
{
    public static partial class BlueskyServer
    {
        // https://docs.bsky.app/docs/api/app-bsky-notification-get-unread-count
        private const string GetUnreadEndpoint = "/xrpc/app.bsky.notification.getUnreadCount";

        // https://docs.bsky.app/docs/api/app-bsky-notification-list-notifications
        private const string ListNotificationsEndpoint = "/xrpc/app.bsky.notification.listNotifications";

        // https://docs.bsky.app/docs/api/app-bsky-notification-update-seen
        private const string UpdateSeenEndpoint = "/xrpc/app.bsky.notification.updateSeen";

        /// <summary>
        /// Gets the number of unread notifications for the requesting account.
        /// </summary>
        /// <param name="seenAt">The date and time notifications were last checked.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessToken">The access token to use to authenticate against the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/>, <paramref name="accessToken"/> or <paramref name="httpClient"/> is null.</exception>
        public static async Task<AtProtoHttpResult<int>> GetNotificationUnreadCount(
            DateTimeOffset? seenAt,
            Uri service,
            string accessToken,
            HttpClient httpClient,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessToken);
            ArgumentNullException.ThrowIfNull(httpClient);

            string queryString = string.Empty;
            if (seenAt is not null)
            {
                queryString = $"{seenAt.Value.UtcDateTime.ToString("o", CultureInfo.InvariantCulture)}";
            }

            AtProtoHttpClient<UnreadCountResponse> request = new(loggerFactory);
            AtProtoHttpResult<UnreadCountResponse> response = await request.Get(
                service,
                $"{GetUnreadEndpoint}?{queryString}",
                accessToken,
                httpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            int unreadCount = -1;
            if (response.Succeeded)
            {
                unreadCount = response.Result.Count;
            }

            // Flatten the result a little for ease of use.
            return new(
                unreadCount,
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }

        /// <summary>
        /// Gets the notifications for the requesting account.
        /// </summary>
        /// <param name="limit">The maximum number of notifications to return. If specified this should be greater than 1 and less than or equal to 100.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="seenAt">The date and time notifications were last checked.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessToken">The access token to use to authenticate against the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/>, <paramref name="accessToken"/> or <paramref name="httpClient"/> is null.</exception>
        public static async Task<AtProtoHttpResult<NotificationCollection>> ListNotifications(
            int? limit,
            string? cursor,
            DateTimeOffset? seenAt,
            Uri service,
            string accessToken,
            HttpClient httpClient,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessToken);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            StringBuilder queryString = new ();
            if (limit is not null)
            {
                queryString.Append(CultureInfo.InvariantCulture, $"limit={limit}&");
            }

            if (!string.IsNullOrEmpty(cursor))
            {
                queryString.Append(CultureInfo.InvariantCulture, $"cursor={Uri.EscapeDataString(cursor)}&");
            }

            if (seenAt is not null)
            {
                queryString.Append(CultureInfo.InvariantCulture, $"seenAt={seenAt.Value.UtcDateTime.ToString("o", CultureInfo.InvariantCulture)}");
            }

            if (queryString.Length > 0 && queryString[queryString.Length - 1] == '&')
            {
                queryString.Remove(queryString.Length - 1, 1);
            }

            AtProtoHttpClient<ListNotificationsResponse> request = new(loggerFactory);
            AtProtoHttpResult<ListNotificationsResponse> response = await request.Get(
                service: service,
                endpoint: $"{ListNotificationsEndpoint}?{queryString}",
                accessToken: accessToken,
                httpClient: httpClient,
                subscribedLabelers : subscribedLabelers,
                jsonSerializerOptions: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Flatten
            if (response.Succeeded)
            {
                return new AtProtoHttpResult<NotificationCollection>(
                    new NotificationCollection(response.Result.Notifications, response.Result.Cursor, response.Result.Priority, response.Result.SeenAt),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<NotificationCollection>(
                    new(),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Updates the time notifications were last seen for the current account.
        /// </summary>
        /// <param name="seenAt">The date and time notifications were last checked.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessToken">The access token to use to authenticate against the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="seenAt"/>, <paramref name="service"/>, <paramref name="accessToken"/> or <paramref name="httpClient"/> is null.</exception>
        public static async Task<AtProtoHttpResult<EmptyResponse>> UpdateNotificationSeenAt(
            DateTimeOffset seenAt,
            Uri service,
            string accessToken,
            HttpClient httpClient,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessToken);
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentNullException.ThrowIfNull(seenAt);

            UpdateSeenRequest body = new() { SeenAt = seenAt };

            AtProtoHttpClient<EmptyResponse> request = new(loggerFactory);
            return await request.Post(
                service,
                $"{UpdateSeenEndpoint}",
                body,
                accessToken,
                httpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
