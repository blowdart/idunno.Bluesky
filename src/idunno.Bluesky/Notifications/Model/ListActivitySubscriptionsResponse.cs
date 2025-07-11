// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Notifications.Model
{
    internal sealed record ListActivitySubscriptionsResponse(IList<ProfileView> Subscriptions, string? Cursor)
    {
    }
}
