// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Notifications.Model
{
    internal record UnreadCountResponse
    {
        [JsonInclude]
        [JsonRequired]
        internal int Count { get; set; } = -1;
    }
}
