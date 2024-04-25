// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky.Feed
{
    public sealed record ThreadViewPost : ThreadViewPostBase
    {
        [JsonConstructor]
        public ThreadViewPost(FeedPost post) => Post = post;

        [JsonInclude]
        [JsonRequired]
        public FeedPost Post { get; internal set; }

        [JsonInclude]
        public ThreadViewPost? Parent { get; internal set; }

        [JsonInclude]
        public IReadOnlyList<ThreadViewPost>? Replies { get; internal set; }
    }
}
