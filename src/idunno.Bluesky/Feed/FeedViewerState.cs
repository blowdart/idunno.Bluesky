// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// Encapsulates metadata about the requesting account's relationship with the subject content. Only has meaningful content for authenticated requests.
    /// </summary>
    public sealed record FeedViewerState
    {
        [JsonConstructor]
        internal FeedViewerState(
            AtUri? repost,
            AtUri? like,
            bool threadMuted,
            bool replyDisabled,
            bool embeddingDisabled,
            bool pinned)
        {
            Repost = repost;
            Like = like;
            ThreadMuted = threadMuted;
            ReplyDisabled = replyDisabled;
            EmbeddingDisabled = embeddingDisabled;
            Pinned = pinned;
        }

        /// <summary>
        /// An <see cref="AtUri"/> to the repost of the post by the requesting account.
        /// </summary>
        public AtUri? Repost { get; init; }

        /// <summary>
        /// An <see cref="AtUri"/> to the like of the post by the requesting account.
        /// </summary>
        public AtUri? Like { get; init; }

        /// <summary>
        /// A flag indicating whether the requesting account has the content muted.
        /// </summary>
        public bool ThreadMuted { get; init; }

        /// <summary>
        /// A flag indicating whether the requesting account can reply to the content.
        /// </summary>
        public bool ReplyDisabled { get; init; }

        /// <summary>
        /// A flag indicating whether embedding is disabled for the requesting account.
        /// </summary>
        public bool EmbeddingDisabled { get; init; }

        /// <summary>
        /// A flag indicating whether the requesting account has pinned the the content.
        /// </summary>
        public bool Pinned { get; init; }
    }
}
