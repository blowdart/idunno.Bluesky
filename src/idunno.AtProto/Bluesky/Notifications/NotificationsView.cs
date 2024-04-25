// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky.Notifications
{
    /// <summary>
    /// Presents a view of notifications for the current user.
    /// </summary>
    public record NotificationsView
    {
        [JsonInclude]
        public string? Cursor { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public IReadOnlyCollection<Notification> Notifications { get; internal set; } = new List<Notification>();

        [JsonInclude]
        public DateTimeOffset SeenAt { get; internal set; }
    }
}
