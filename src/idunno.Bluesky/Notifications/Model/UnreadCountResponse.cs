// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Notifications.Model
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in GetUnreadCount.")]
    internal sealed record UnreadCountResponse
    {
        [JsonInclude]
        [JsonRequired]
        internal int Count { get; set; } = -1;
    }
}
