// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// Encapsulates a view over a post in a thread.
    /// </summary>
    public record ThreadViewPost : PostViewBase
    {
        /// <summary>
        /// Creates a new instance of <see cref="ThreadViewPost"/>.
        /// </summary>
        /// <param name="post">The <see cref="PostView"/> of the post.</param>
        /// <param name="parent">The <see cref="PostViewBase"/> of the parent of the <paramref name="post"/>, if any.</param>
        /// <param name="replies">The collection of <see cref="PostViewBase"/> of replies to the <paramref name="post"/>, if any.</param>
        /// <param name="threadGate">The <see cref="ThreadGateView"/> over the thread gate applied to the post, if any.</param>
        [JsonConstructor]
        internal ThreadViewPost(PostView post, PostViewBase? parent, IReadOnlyList<PostViewBase>? replies, ThreadGateView? threadGate)
        {
            Post = post;
            Parent = parent;
            Replies = replies;
            ThreadGate = threadGate;
        }

        /// <summary>
        /// Gets the <see cref="PostView"/> of the post.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public PostView Post { get; init; }

        /// <summary>
        /// Gets the <see cref="PostViewBase"/> of the parent of the <see cref="Post"/>, if any.
        /// </summary>
        [JsonInclude]
        public PostViewBase? Parent { get; init; }

        /// <summary>
        /// Gets a collection of <see cref="PostViewBase"/> of replies to the <see cref="Post"/>, if any.
        /// </summary>
        [JsonInclude]
        public IReadOnlyList<PostViewBase>? Replies { get; init; }

        /// <summary>
        /// Gets a <see cref="ThreadGateView"/> over the thread gate applied to the post, if any.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("threadgate")]
        public ThreadGateView? ThreadGate { get; init; }
    }
}
