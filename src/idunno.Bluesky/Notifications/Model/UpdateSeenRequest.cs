// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Notifications.Model
{
    internal record UpdateSeenRequest
    {
        public DateTimeOffset SeenAt { get; set; }
    }
}
