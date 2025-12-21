// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Notifications
{
    /// <summary>
    /// The reason why a notification was delivered - e.g. your post was liked, or you received a new follower.
    /// </summary>
    public enum NotificationReason
    {
        /// <summary>
        /// The notification reason is unknown.
        /// </summary>
        Unknown,

        /// <summary>
        /// A user followed the notification subject.
        /// </summary>
        Follow,

        /// <summary>
        /// A post created by the notification subject was liked.
        /// </summary>
        Like,

        /// <summary>
        /// A post mentioned the notification subject.
        /// </summary>
        Mention,

        /// <summary>
        /// A reply mentioned a post by the notification subject.
        /// </summary>
        Reply,

        /// <summary>
        /// A post created by the notification subject was reposted.
        /// </summary>
        Repost,

        /// <summary>
        /// A quote post quoted a post by the notification subject.
        /// </summary>
        Quote,

        /// <summary>
        /// A user followed the notification subject from a starter pack.
        /// </summary>
        [JsonStringEnumMemberName("starterpack-joined")]
        StarterPackJoined,

        /// <summary>
        /// The user's account has been verified.
        /// </summary>
        Verified,

        /// <summary>
        /// The user's account has been unverified.
        /// </summary>
        Unverified,

        /// <summary>
        /// A repost created by the notification subject was liked.
        /// </summary>
        [JsonStringEnumMemberName("like-via-repost")]
        LikeViaRepost,

        /// <summary>
        /// A repost created by the notification subject was reposted.
        /// </summary>
        [JsonStringEnumMemberName("repost-via-repost")]
        RepostViaRepost,

        /// <summary>
        /// The post is from a user the current user subscribed to.
        /// </summary>
        [JsonStringEnumMemberName("subscribed-post")]
        SubscribedPost,

        /// <summary>
        /// The notification is from a matching contact.
        /// </summary>
        [JsonStringEnumMemberName("contact-match")]
        ContactMatch
    }
}
