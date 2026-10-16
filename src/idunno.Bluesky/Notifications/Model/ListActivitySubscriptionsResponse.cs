// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Notifications.Model;

internal sealed record ListActivitySubscriptionsResponse([property: JsonRequired] IList<ProfileView> Subscriptions, string? Cursor)
{
}