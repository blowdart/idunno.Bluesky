// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace idunno.Bluesky.Notifications.Model
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in ListNotifications.")]
    internal sealed record ListNotificationsResponse(IList<Notification> Notifications, string? Cursor, bool? Priority, DateTimeOffset SeenAt)
    {
    }
}
