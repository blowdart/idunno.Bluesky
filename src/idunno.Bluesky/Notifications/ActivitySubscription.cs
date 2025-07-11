// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Notifications
{
    /// <summary>
    /// Represents the available settings for activity subscriptions.
    /// </summary>
    /// <param name="Post">A flag indicating whether to subscribe to notifications for posts.</param>
    /// <param name="Reply">A flag indicating whether to subscribe to notifications for replies.</param>
    public sealed record ActivitySubscription(bool Post, bool Reply)
    {
    }
}
