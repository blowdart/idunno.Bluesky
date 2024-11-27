// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// Encapsulates various references about replies to a post in a thread.
    /// </summary>
    public sealed record ReplyReference
    {
        [JsonConstructor]
        internal ReplyReference (PostViewBase root, PostViewBase parent, ProfileViewBasic? grandparentAuthor)
        {
            Root = root;
            Parent = parent;
            GrandparentAuthor = grandparentAuthor;
        }

        /// <summary>
        /// Gets a <see cref="PostViewBase"/> of the post at the root of the thread.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public PostViewBase Root { get; init; }

        /// <summary>
        /// Gets a <see cref="PostViewBase"/> of the immediate parent post in the thread.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public PostViewBase Parent { get; init; }

        /// <summary>
        /// Gets a <see cref="ProfileViewBasic"/> of the author of the grandparent post in the thread, if any.
        /// </summary>
        [JsonInclude]
        public ProfileViewBasic? GrandparentAuthor { get; init; }
    }
}
