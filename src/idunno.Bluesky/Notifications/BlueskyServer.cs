// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.Bluesky.Notifications;
using idunno.Bluesky.Notifications.Model;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Actor;

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

        // https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/notification/listActivitySubscriptions.json
        private const string ListActivitySubscriptionsEndpoint = "/xrpc/app.bsky.notification.listActivitySubscriptions";

        // https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/notification/putActivitySubscription.json
        private const string PutActivitySubscriptionEndpoint = "/xrpc/app.bsky.notification.putActivitySubscription";

        // https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/notification/getPreferences.json
        private const string GetNotificationPreferencesEndpoint = "/xrpc/app.bsky.notification.getPreferences";

        // https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/notification/putPreferencesV2.json
        private const string PutNotificationPreferencesV2Endpoint = "/xrpc/app.bsky.notification.putPreferencesV2";

        /// <summary>
        /// Gets the number of unread notifications for the requesting account.
        /// </summary>
        /// <param name="seenAt">The date and time notifications were last checked.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any of <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> are <see langword="null"/>.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<int>> GetNotificationUnreadCount(
            DateTimeOffset? seenAt,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            string queryString = string.Empty;
            if (seenAt is not null)
            {
                queryString = $"{seenAt.Value.UtcDateTime.ToString("o", CultureInfo.InvariantCulture)}";
            }

            AtProtoHttpClient<UnreadCountResponse> request = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<UnreadCountResponse> response = await request.Get(
                service,
                $"{GetUnreadEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
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
        /// Gets the activity subscriptions for the requesting account.
        /// </summary>
        /// <param name="limit">The maximum number of activity subscriptions to return. If specified this should be greater than 1 and less than or equal to 100.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any of <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> are <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is less than 1 or greater than 100.</exception>"
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>> ListActivitySubscriptions(
            int? limit,
            string? cursor,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            StringBuilder queryString = new();
            if (limit is not null)
            {
                queryString.Append(CultureInfo.InvariantCulture, $"limit={limit}&");
            }

            if (!string.IsNullOrEmpty(cursor))
            {
                queryString.Append(CultureInfo.InvariantCulture, $"cursor={Uri.EscapeDataString(cursor)}&");
            }

            if (queryString.Length > 0 && queryString[queryString.Length - 1] == '&')
            {
                queryString.Remove(queryString.Length - 1, 1);
            }
            
            AtProtoHttpClient<ListActivitySubscriptionsResponse> request = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<ListActivitySubscriptionsResponse> response = await request.Get(
                service: service,
                endpoint: $"{ListActivitySubscriptionsEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Transform from DTO and flatten
            if (response.Succeeded)
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>(
                    new (response.Result.Subscriptions, response.Result.Cursor),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>(
                    new(),
                    response.StatusCode,
                    response.HttpResponseHeaders,
                    response.AtErrorDetail,
                    response.RateLimit);
            }
        }

        /// <summary>
        /// Gets the notifications for the requesting account.
        /// </summary>
        /// <param name="limit">The maximum number of notifications to return. If specified this should be greater than 1 and less than or equal to 100.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="seenAt">The date and time notifications were last checked.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any of <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> are <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is less than 1 or greater than 100.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<NotificationCollection>> ListNotifications(
            int? limit,
            string? cursor,
            DateTimeOffset? seenAt,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            IEnumerable<Did>? subscribedLabelers = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
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

            AtProtoHttpClient<ListNotificationsResponse> request = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<ListNotificationsResponse> response = await request.Get(
                service: service,
                endpoint: $"{ListNotificationsEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                subscribedLabelers: subscribedLabelers,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Transform from DTO and flatten
            if (response.Succeeded)
            {
                List<Notification> notifications = [];
                foreach (NotificationResponse notificationResponse in response.Result.Notifications)
                {
                    notifications.Add(new Notification(notificationResponse));
                }

                return new AtProtoHttpResult<NotificationCollection>(
                    new NotificationCollection(notifications, response.Result.Cursor, response.Result.Priority, response.Result.SeenAt),
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
        /// Puts the supplied <paramref name="subscriptionSettings"/> for activity notifications
        /// </summary>
        /// <param name="subscriptionSettings">The <see cref="SubjectActivitySubscription"/> to send.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any of <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> are <see langword="null"/>.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<SubjectActivitySubscription>> PutActivitySubscription(
            SubjectActivitySubscription subscriptionSettings,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(subscriptionSettings);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<SubjectActivitySubscription> request = new(AppViewProxy, loggerFactory);
            return await request.Post(
                service,
                PutActivitySubscriptionEndpoint,
                subscriptionSettings,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                credentials: accessCredentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Updates the time notifications were last seen for the current account.
        /// </summary>
        /// <param name="seenAt">The date and time notifications were last checked.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any of <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> are <see langword="null"/>.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<EmptyResponse>> UpdateNotificationSeenAt(
            DateTimeOffset seenAt,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            UpdateSeenRequest body = new() { SeenAt = seenAt };

            AtProtoHttpClient<EmptyResponse> request = new(AppViewProxy, loggerFactory);
            return await request.Post(
                service,
                $"{UpdateSeenEndpoint}",
                body,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                credentials: accessCredentials,
                httpClient: httpClient,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the notification preferences for the current user.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any of <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> are <see langword="null"/>.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<Notifications.Preferences>> GetNotificationPreferences(
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<Notifications.Model.GetPreferencesResponse> request = new(AppViewProxy, loggerFactory);

            AtProtoHttpResult<GetPreferencesResponse> response = await request.Get(
                service: service,
                endpoint: GetNotificationPreferencesEndpoint,
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<Notifications.Preferences>(
                    response.Result.Preferences,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<Notifications.Preferences>(
                    null,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
        }

        /// <summary>
        /// Puts the notification preferences for the current user.
        /// </summary>
        /// <param name="preferences">The <see cref="Notifications.Preferences"/> to set.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        ///   Thrown when any of <paramref name="preferences"/>, <paramref name="service"/>,
        ///   <paramref name="accessCredentials"/> or <paramref name="httpClient"/> are <see langword="null"/>.
        /// </exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<Notifications.Preferences>> PutNotificationPreferences(
            Notifications.Preferences preferences,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(preferences);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<Notifications.Model.GetPreferencesResponse> request = new(AppViewProxy, loggerFactory);

            PutPreferencesV2Request body = new (preferences);

            AtProtoHttpResult<GetPreferencesResponse> response = await request.Post(
                service: service,
                endpoint: PutNotificationPreferencesV2Endpoint,
                body,
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<Notifications.Preferences>(
                    response.Result.Preferences,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<Notifications.Preferences>(
                    null,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
        }
    }
}
