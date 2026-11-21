// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed.Model;

/// <summary>
/// The wire format of the response from <c>app.bsky.feed.getFeedSkeleton</c>.
/// </summary>
/// <param name="Feed">The skeleton posts in the feed.</param>
/// <param name="Cursor">An optional cursor for the next page.</param>
/// <param name="ReqId">An optional identifier for the request, which clients can return with interactions.</param>
[SuppressMessage("Performance", "CA1812", Justification = "Used in GetFeedSkeleton.")]
internal sealed record GetFeedSkeletonResponse([property: JsonRequired] ICollection<SkeletonFeedPost> Feed, string? Cursor, string? ReqId);
