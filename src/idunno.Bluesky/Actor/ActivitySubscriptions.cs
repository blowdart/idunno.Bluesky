// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Notifications;

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// A user's subscription preferences for other actors.
    /// </summary>
    /// <param name="allowSubscriptions">A <see cref="NotificationAllowedFrom"/> setting for the actor.</param>
    public sealed record ActivitySubscriptions(NotificationAllowedFrom allowSubscriptions)
    {
    }
}
