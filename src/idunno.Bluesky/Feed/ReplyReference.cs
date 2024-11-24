// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Feed
{
    public sealed record ReplyReference
    {
        [JsonConstructor]
        internal ReplyReference (PostViewBase root, PostViewBase parent, ProfileViewBasic? grandparentAuthor)
        {
            Root = root;
            Parent = parent;
            GrandparentAuthor = grandparentAuthor;
        }

        [JsonInclude]
        [JsonRequired]
        public PostViewBase Root { get; init; }

        [JsonInclude]
        [JsonRequired]
        public PostViewBase Parent { get; init; }

        [JsonInclude]
        public ProfileViewBasic? GrandparentAuthor { get; init; }
    }
}
