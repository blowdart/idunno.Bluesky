// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed.Model
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in SearchPosts.")]
    internal sealed record SearchPostsResponse
    {
        [JsonConstructor]
        internal SearchPostsResponse(ICollection<PostView> posts, string? cursor, int? hitsTotal)
        {
            Posts = posts;
            Cursor = cursor;
            HitsTotal = hitsTotal;
        }

        [JsonInclude]
        public ICollection<PostView> Posts { get; init; }

        [JsonInclude]
        public string? Cursor { get; init; }

        [JsonInclude]
        public int? HitsTotal { get; init; }
    }
}
