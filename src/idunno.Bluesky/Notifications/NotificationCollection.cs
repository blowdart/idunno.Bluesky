// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Notifications
{
    /// <summary>
    /// Presents a list of notifications for the authenticated user.
    /// </summary>
    public class NotificationCollection : PagedReadOnlyCollection<Notification>
    {
        internal NotificationCollection() : base(new List<Notification>().AsReadOnly())
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="Bluesky.Notifications.NotificationCollection"/>.
        /// </summary>
        /// <param name="notifications">The list of notifications to create the list from.</param>
        /// <param name="cursor">An optional pagination cursor.</param>
        /// <param name="priority">A flag indicating whether the notification list is priority or not.</param>
        /// <param name="seenAt">The date and time the list was seen at.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="notifications"/> is <see langword="null"/>.</exception>
        [JsonConstructor]
        public NotificationCollection(IList<Notification> notifications, string? cursor, bool? priority, DateTimeOffset? seenAt) : base(notifications, cursor)
        {
            ArgumentNullException.ThrowIfNull(notifications);

            Priority = priority;
            SeenAt = seenAt;
        }

        /// <summary>
        /// A flag indicating whether a notification list is a priority list.
        /// </summary>
        public bool? Priority { get; internal set; }

        /// <summary>
        /// The date when the view was generated.
        /// </summary>
        [JsonInclude]
        public DateTimeOffset? SeenAt { get; internal set; }
    }
}
