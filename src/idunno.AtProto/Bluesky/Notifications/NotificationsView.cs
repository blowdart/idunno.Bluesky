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
        /// <summary>
        /// Gets an optional cursor for pagination. See https://atproto.com/specs/xrpc#cursors-and-pagination.</param>
        /// </summary>
        /// <value>
        /// A cursor for api pagination.
        /// </value>
        [JsonInclude]
        public string? Cursor { get; internal set; }

        /// <summary>
        /// Gets the <see cref="Notification"/>s the view contains.
        /// </summary>
        /// <value>
        /// The <see cref="Notification"/>s the view contains.
        /// </value>
        [JsonInclude]
        [JsonRequired]
        public IReadOnlyCollection<Notification> Notifications { get; internal set; } = new List<Notification>();

        /// <summary>
        /// Gets the date when the view was generated.
        /// </summary>
        /// <value>
        /// The date when the view was generated.
        /// </value>
        [JsonInclude]
        public DateTimeOffset SeenAt { get; internal set; }
    }
}
