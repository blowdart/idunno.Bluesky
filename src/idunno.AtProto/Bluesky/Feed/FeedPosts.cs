// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky.Feed
{
    public class FeedPosts
    {
        [JsonInclude]
        public IReadOnlyList<FeedPost>? Posts { get; internal set; }
    }
}
