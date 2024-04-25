// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Bluesky.Notifications
{
    internal record UpdateSeenRequest
    {
        public DateTimeOffset SeenAt { get; set; }
    }
}
