// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Feed;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Indicates that a post appears in a <see cref="FeedSkeleton"/> because it was reposted.
/// </summary>
public sealed record SkeletonReasonRepost : SkeletonReason
{
    /// <summary>
    /// Creates a new instance of <see cref="SkeletonReasonRepost"/>.
    /// </summary>
    /// <param name="repost">The <see cref="AtUri"/> of the repost record that caused the post to appear in the feed.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="repost"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    public SkeletonReasonRepost(AtUri repost)
    {
        ArgumentNullException.ThrowIfNull(repost);
        Repost = repost;
    }

    /// <summary>
    /// Gets the <see cref="AtUri"/> of the repost record that caused the post to appear in the feed.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public AtUri Repost { get; init; }
}
