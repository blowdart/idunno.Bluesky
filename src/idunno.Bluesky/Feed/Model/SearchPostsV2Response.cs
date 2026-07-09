// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Feed.Model;

internal sealed record SearchPostsV2Response(
    string? Cursor,
    int? HitsTotal,
    IList<PostView> Posts,
    IList<string>? DetectedQueryLanguages
    )
{
}