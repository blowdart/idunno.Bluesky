// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed
{
    public record ThreadViewPost : PostViewBase
    {
        [JsonConstructor]
        internal ThreadViewPost(PostView post, PostViewBase? parent, IReadOnlyList<PostViewBase>? replies, ThreadGateView? threadGate)
        {
            Post = post;
            Parent = parent;
            Replies = replies;
            ThreadGate = threadGate;
        }

        [JsonInclude]
        [JsonRequired]
        public PostView Post { get; init; }

        [JsonInclude]
        public PostViewBase? Parent { get; init; }

        [JsonInclude]
        public IReadOnlyList<PostViewBase>? Replies { get; init; }

        [JsonInclude]
        [JsonPropertyName("threadgate")]
        public ThreadGateView? ThreadGate { get; init; }
    }
}
