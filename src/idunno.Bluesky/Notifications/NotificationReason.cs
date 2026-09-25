// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Notifications;

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
/// <summary>
/// Extension methods for <see cref="NotificationReason"/>.
/// </summary>
internal static class NotificationReasonExtensions
{
    /// <summary>
    /// Returns the value the API uses for the specified <paramref name="reason"/>.
    /// </summary>
    /// <param name="reason">The <see cref="NotificationReason"/> whose API value should be returned.</param>
    /// <returns>The value the API uses for the specified <paramref name="reason"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="reason"/> is not a known reason.</exception>
    internal static string ToNotificationReasonValue(this NotificationReason reason)
    {
        return reason switch
        {
            NotificationReason.Follow => "follow",
            NotificationReason.Like => "like",
            NotificationReason.Mention => "mention",
            NotificationReason.Reply => "reply",
            NotificationReason.Repost => "repost",
            NotificationReason.Quote => "quote",
            NotificationReason.StarterPackJoined => "starterpack-joined",
            NotificationReason.Verified => "verified",
            NotificationReason.Unverified => "unverified",
            NotificationReason.LikeViaRepost => "like-via-repost",
            NotificationReason.RepostViaRepost => "repost-via-repost",
            NotificationReason.SubscribedPost => "subscribed-post",
            NotificationReason.ContactMatch => "contact-match",
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
        };
    }
}
