// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Feed;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// An unhydrated feed, a page of <see cref="SkeletonFeedPost"/>s returned by a feed generator.
/// </summary>
/// <param name="list">The <see cref="SkeletonFeedPost"/>s in this page of the feed.</param>
/// <param name="cursor">An optional cursor to retrieve the next page of the feed.</param>
/// <param name="requestId">An optional identifier for the request which produced this feed, which can be sent back with interactions.</param>
public sealed class FeedSkeleton(IList<SkeletonFeedPost> list, string? cursor = null, string? requestId = null) : ReadOnlyCollection<SkeletonFeedPost>(list)
{
    /// <summary>
    /// Gets the cursor to retrieve the next page of the feed, if there is one.
    /// </summary>
    public string? Cursor { get; } = cursor;

    /// <summary>
    /// Gets the identifier the feed generator assigned to the request which produced this feed, if it supplied one.
    /// </summary>
    /// <remarks>
    /// <para>Send this back as <see cref="Interaction.RequestId"/> when reporting interactions with posts in this feed.</para>
    /// </remarks>
    public string? RequestId { get; } = requestId;
}
