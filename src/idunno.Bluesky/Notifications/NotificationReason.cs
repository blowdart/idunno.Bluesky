// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Notifications
{
    /// <summary>
    /// Reasons for a notification.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<NotificationReason>))]
    public enum NotificationReason
    {
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
        StarterPackJoined
    }
}
