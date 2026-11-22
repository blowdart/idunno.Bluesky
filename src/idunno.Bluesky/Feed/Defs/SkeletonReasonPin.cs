// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Feed;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Indicates that a post appears in a <see cref="FeedSkeleton"/> because it is pinned.
/// </summary>
[SuppressMessage("Minor Code Smell", "S2094:Classes should not be empty", Justification = "Needed to discriminate on json type discriminator")]
public sealed record SkeletonReasonPin : SkeletonReason
{
}
