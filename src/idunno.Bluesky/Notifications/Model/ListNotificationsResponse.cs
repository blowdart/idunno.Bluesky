// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Notifications.Model;

internal sealed record ListNotificationsResponse([property: JsonRequired] IList<NotificationResponse> Notifications, string? Cursor, bool? Priority, DateTimeOffset? SeenAt)
{
}