// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Feed;

/// <summary>
/// Encapsulates metadata about the requesting account's relationship with the subject content.
/// Only has meaningful content for authenticated requests.
/// </summary>
public sealed record ViewerState
{
    [JsonConstructor]
    internal ViewerState(
        AtUri? repost,
        AtUri? like,
        bool? bookMarked,
        bool? threadMuted,
        bool? replyDisabled,
        bool? embeddingDisabled,
        bool? pinned,
        KnownLikers? knownLikers)
    {
        Repost = repost;
        Like = like;
        ThreadMuted = threadMuted;
        ReplyDisabled = replyDisabled;
        EmbeddingDisabled = embeddingDisabled;
        Pinned = pinned;
        Bookmarked = bookMarked;
        KnownLikers = knownLikers;
    }

    /// <summary>
    /// Gets the <see cref="AtUri"/> to the repost of the post by the requesting account, if any.
    /// </summary>
    public AtUri? Repost { get; init; }

    /// <summary>
    /// Gets the <see cref="AtUri"/> to the like of the post by the requesting account, if any.
    /// </summary>
    public AtUri? Like { get; init; }

    /// <summary>
    /// Gets a flag indicating whether the requesting account has the content muted.
    /// </summary>
    public bool? ThreadMuted { get; init; }

    /// <summary>
    /// Gets a flag indicating whether the requesting account can reply to the content.
    /// </summary>
    public bool? ReplyDisabled { get; init; }

    /// <summary>
    /// Gets a flag indicating whether embedding is disabled for the requesting account.
    /// </summary>
    public bool? EmbeddingDisabled { get; init; }

    /// <summary>
    /// Gets a flag indicating whether the requesting account has pinned the the content.
    /// </summary>
    public bool? Pinned { get; init; }

    /// <summary>
    /// Gets a flag indicating whether the requesting account has bookmarked the content.
    /// </summary>
    public bool? Bookmarked { get; init; }

    /// <summary>
    /// Gets likers of a post who the authenticated user also follows. This property is present only in selected cases, as an optimization.
    /// </summary>
    public KnownLikers? KnownLikers { get; init; }
}