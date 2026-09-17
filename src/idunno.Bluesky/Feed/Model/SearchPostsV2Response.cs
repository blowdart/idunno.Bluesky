// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed.Model;

internal sealed record SearchPostsV2Response(
    string? Cursor,
    int? HitsTotal,
    [property: JsonRequired] IList<PostView> Posts,
    IList<string>? DetectedQueryLanguages
    )
{
}