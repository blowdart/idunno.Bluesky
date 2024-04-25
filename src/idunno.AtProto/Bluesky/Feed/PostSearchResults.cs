// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky.Feed
{
    public record PostSearchResults
    {
        [JsonInclude]
        public IReadOnlyList<FeedPost> Posts { get; internal set; } = new List<FeedPost>();

        [JsonInclude]
        public string? Cursor { get; internal set; }

        [JsonInclude]
        public int HitsTotal { get; internal set; }
    }
}
