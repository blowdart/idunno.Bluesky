// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky.Notifications
{
    internal record UnreadCountResult
    {
        [JsonInclude]
        [JsonRequired]
        internal int Count { get; set; } = -1;
    }
}
