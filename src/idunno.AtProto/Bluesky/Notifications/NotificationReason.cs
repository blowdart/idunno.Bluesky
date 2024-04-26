// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky.Notifications
{
    /// <summary>
    /// Reasons for a notification.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<NotificationReason>))]
    public enum NotificationReason
    {
        Unknown = 0,
        Follow,
        Like,
        Mention,
        Quote,
        Repost,
        Reply
    }
}
