// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Actor;

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Notifications.Model;

internal sealed record ListActivitySubscriptionsResponse([property: JsonRequired] IList<ProfileView> Subscriptions, string? Cursor)
{
}