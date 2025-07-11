// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Notifications;

namespace idunno.Bluesky
{
    public partial class BlueskyAgent
    {
        /// <summary>
        /// Gets a count the number of unread notifications for the requesting account. Requires authentication.
        /// </summary>
        /// <param name="seenAt">An optional <see cref="DateTimeOffset"/> indicating when notifications were last checked.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>An <see cref="AtProtoHttpResult{T}"/> wrapping an integer indicating the unread notification count.</returns>
        public async Task<AtProtoHttpResult<int>> GetNotificationUnreadCount(DateTimeOffset? seenAt = null, CancellationToken cancellationToken = default)
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
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the notifications for the requesting account. Requires authentication.
        /// </summary>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<NotificationCollection>> ListNotifications(IEnumerable<Did>? subscribedLabelers = null, CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await ListNotifications(null, null, null, subscribedLabelers, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the notifications for the requesting account. Requires authentication.
        /// </summary>
        /// <param name="limit">The maximum number of notifications to return. If specified this should be &gt;=1 and &lt;= 100.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="seenAt">The date and time notifications were last checked.</param>
        /// <param name="subscribedLabelers">A optional list of labeler <see cref="Did"/>s to accept labels from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<NotificationCollection>> ListNotifications(
            int? limit = null,
            string? cursor = null,
            DateTimeOffset? seenAt = null,
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
                seenAt,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                subscribedLabelers: subscribedLabelers,
                cancellationToken:cancellationToken).ConfigureAwait(false);
        }


        /// <summary>
        /// Updates the date and time notifications were last seen for the current user. Requires authentication.
        /// </summary>
        /// <param name="seenAt">An optional <see cref="DateTimeOffset"/> indicating when notifications were last checked.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<EmptyResponse>> UpdateNotificationSeenAt(DateTimeOffset? seenAt = null, CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            seenAt ??= DateTimeOffset.UtcNow;

            return await BlueskyServer.UpdateNotificationSeenAt(
                (DateTimeOffset)seenAt,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the notification declaration record for the current user.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<Declaration>>> GetNotificationDeclaration(CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await GetNotificationDeclaration(Did, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the notification declaration record for the specified <paramref name="did"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> whose notification record should be retrieved.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<Declaration>>> GetNotificationDeclaration(Did did, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(did);

            return
                await GetBlueskyRecord<Declaration>($"at://{did}/{CollectionNsid.NotificationDeclaration}/self", cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the activity subscriptions for the requesting account.
        /// </summary>
        /// <param name="limit">The maximum number of activity subscriptions to return. If specified this should be greater than 1 and less than or equal to 100.</param>
        /// <param name="cursor">An optional cursor. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/>&lt;1 or <paramref name="limit"/>&gt;100.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not authenticated.</exception>
        public async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>> ListActivitySubscriptions(
            int? limit = null,
            string? cursor = null,
            CancellationToken cancellationToken = default)
        {
            if (limit is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
                ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
            }

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await BlueskyServer.ListActivitySubscriptions(
                limit: limit,
                cursor: cursor,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the activity notification settings for the specified <paramref name="subject"/>. Requires authentication.
        /// </summary>
        /// <param name="subject">The <see cref="AtProto.Did"/> of the actor for whom activity notifications should be set.</param>
        /// <param name="posts">Flag indicating whether notifications should be enabled for posts.</param>
        /// <param name="replies">Flag indicating whether notifications should be enabled for replies. <paramref name="posts"/> must also be <see langword="true"/> for this setting to work.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="subject"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="replies"/> is <see langword="true"/> but <paramref name="posts"/> is <see langword="false"/>.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not authenticated.</exception>
        public async Task<AtProtoHttpResult<SubjectActivitySubscription>> SetActivitySubscription(
            Did subject,
            bool posts,
            bool replies,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(subject);

            if (replies && !posts)
            {
                throw new ArgumentException("cannot be true if posts is false", nameof(replies));
            }

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await BlueskyServer.PutActivitySubscription(
                new SubjectActivitySubscription(subject, new ActivitySubscription(posts, replies)),
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a notification declaration record for the current user.Requires authentication.
        /// </summary>
        /// <param name="notificationAllowedFrom">Indicates who will be allowed to subscribe to post notifications for the current user.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not authenticated.</exception>
        public async Task<AtProtoHttpResult<CreateRecordResult>> SetNotificationDeclaration(NotificationAllowedFrom notificationAllowedFrom, CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await CreateBlueskyRecord(
                record: new Notifications.Declaration(notificationAllowedFrom),
                collection: CollectionNsid.NotificationDeclaration,
                rKey: "self",
                validate: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Update a notification declaration record for the current user. Requires authentication.
        /// </summary>
        /// <param name="declaration">The declaration record to update.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="declaration"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not authenticated.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
        public async Task<AtProtoHttpResult<PutRecordResult>> SetNotificationDeclaration(AtProtoRepositoryRecord<Declaration> declaration, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(declaration);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await PutRecord(
                record: declaration.Value,
                jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                collection: CollectionNsid.NotificationDeclaration,
                rKey: "self",
                validate: null,
                swapCommit: null,
                swapRecord: declaration.Cid,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the current user's notification preferences.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Notifications.Preferences>> GetNotificationPreferences(CancellationToken cancellationToken=default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await BlueskyServer.GetNotificationPreferences(
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the current user's notification preferences.
        /// </summary>
        /// <param name="preferences">The notification preferences to set.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="preferences"/> is null.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<Notifications.Preferences>> SetNotificationPreferences(
            Notifications.Preferences preferences,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(preferences);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            return await BlueskyServer.PutNotificationPreferences(
                preferences: preferences,
                service: Service,
                accessCredentials: Credentials,
                httpClient: HttpClient,
                onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
