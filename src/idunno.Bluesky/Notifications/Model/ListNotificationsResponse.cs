// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Notifications.Model
{
    internal record ListNotificationsResponse(IList<Notification> Notifications, string? Cursor, bool? Priority, DateTimeOffset SeenAt)
    {
    }
}
