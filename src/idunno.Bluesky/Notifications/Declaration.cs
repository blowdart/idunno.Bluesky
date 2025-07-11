// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Notifications
{
    /// <summary>
    /// A declaration of the user's choices related to notifications that can be produced by them
    /// </summary>
    /// <param name="AllowSubscriptions">A declaration of the user's preference for allowing activity subscriptions from other users.A <see langword="null"/> value implies 'followers'.</param>
    public sealed record Declaration(NotificationAllowedFrom? AllowSubscriptions = NotificationAllowedFrom.Followers) : BlueskyRecord
    {
    }

    /// <summary>
    /// Who is allowed to subscribe to notifications about account posts and reposts.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<NotificationAllowedFrom>))]
    public enum NotificationAllowedFrom
    {
        /// <summary>
        /// No-one.
        /// </summary>
        [JsonStringEnumMemberName("none")]
        None,

        /// <summary>
        /// Users who follow the account.
        /// </summary>
        [JsonStringEnumMemberName("followers")]
        Followers,

        /// <summary>
        /// Users who follow the account and the account follows.
        /// </summary>
        [JsonStringEnumMemberName("mutuals")]
        Mutuals
    }
}
