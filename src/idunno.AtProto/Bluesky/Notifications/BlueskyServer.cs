// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

using idunno.AtProto.Bluesky.Notifications;

namespace idunno.AtProto.Bluesky
{
    internal partial class BlueskyServer
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
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult{T}"/> wrapping an integer indicating the unread notification count.</returns>
        public static async Task<HttpResult<int>> GetNotificationUnreadCount(
            DateTimeOffset? seenAt,
            Uri service,
            string accessToken,
            HttpClientHandler? httpClientHandler,
            CancellationToken cancellationToken)
        {
            string queryString = string.Empty;
            if (seenAt is not null)
            {
                queryString = $"{seenAt.Value.UtcDateTime.ToString("o", CultureInfo.InvariantCulture)}";
            }

            AtProtoHttpClient<UnreadCountResult> request = new();
            HttpResult<UnreadCountResult> result = await request.Get(
                service,
                $"{GetUnreadEndpoint}?{queryString}",
                accessToken,
                httpClientHandler,
                cancellationToken).ConfigureAwait(false);

            // Flatten the result a little for ease of use.
            HttpResult<int> flattenedResult = new()
            {
                StatusCode = result.StatusCode
            };

            if (result.Succeeded && result.Result is not null)
            {
                flattenedResult.Result = result.Result.Count;
            }

            if (result.Error is not null)
            {
                flattenedResult.Error = result.Error;
            }

            return flattenedResult;
        }

        /// <summary>
        /// Gets the notifications for the requesting account.
        /// </summary>
        /// <param name="limit">The maximum number of notifications to return. If specified this should be >=1 and <= 100.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="seenAt">The date and time notifications were last checked.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult{T}"/> wrapping a view over the notifications for the account.</returns>
        public static async Task<HttpResult<NotificationsView>> ListNotifications(
            int? limit,
            string? cursor,
            DateTimeOffset? seenAt,
            Uri service,
            string accessToken,
            HttpClientHandler? httpClientHandler,
            CancellationToken cancellationToken)
        {
            if (limit <0 || limit > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be >= 1 and <=100.");
            }

            StringBuilder queryString = new ();
            if (limit is not null)
            {
                queryString.Append(CultureInfo.InvariantCulture, $"limit={limit}&");
            }

            if (!string.IsNullOrEmpty(cursor))
            {
                queryString.Append(CultureInfo.InvariantCulture, $"cursor={cursor}&");
            }

            if (seenAt is not null)
            {
                queryString.Append(CultureInfo.InvariantCulture, $"seenAt={seenAt.Value.UtcDateTime.ToString("o", CultureInfo.InvariantCulture)}");
            }

            if (queryString.Length > 0 && queryString[queryString.Length - 1] == '&')
            {
                queryString.Remove(queryString.Length - 1, 1);
            }

            AtProtoHttpClient<NotificationsView> request = new();
            return await request.Get(
                service,
                $"{ListNotificationsEndpoint}?{queryString}",
                accessToken,
                httpClientHandler,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates the time notifications were last seen for the current account.
        /// </summary>
        /// <param name="seenAt">The date and time notifications were last checked.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessToken">The access token to use to authenticate against the <paramref name="service"/>.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="HttpResult{T}"/> wrapping an empty response.</returns>
        public static async Task<HttpResult<EmptyResponse>> UpdateNotificationSeenAt(
            DateTimeOffset seenAt,
            Uri service,
            string accessToken,
            HttpClientHandler? httpClientHandler,
            CancellationToken cancellationToken)
        {
            UpdateSeenRequest body = new() { SeenAt = seenAt };

            AtProtoHttpClient<EmptyResponse> request = new();
            return await request.Post(
                service,
                $"{UpdateSeenEndpoint}?seenAt={seenAt.UtcDateTime.ToString("o", CultureInfo.InvariantCulture)}",
                body,
                accessToken,
                httpClientHandler,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
