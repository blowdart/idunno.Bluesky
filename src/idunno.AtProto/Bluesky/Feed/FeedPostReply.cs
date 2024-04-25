// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto.Repo;

namespace idunno.AtProto.Bluesky.Feed
{
    public record FeedPostReply
    {
        [JsonConstructor]
        public FeedPostReply(StrongReference root, StrongReference parent)
        {
            Root = root;
            Parent = parent;
        }

        public StrongReference Root { get; internal set; }

        public StrongReference Parent { get; internal set; }
    }
}
