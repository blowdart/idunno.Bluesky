// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky.Feed
{
    public record Timeline
    {
        [JsonInclude]
        public IReadOnlyList<FeedView> Feed { get; internal set; } = new List<FeedView>();

        [JsonInclude]
        public string? Cursor { get; internal set; }
    }
}
