// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed.Model
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in GetSuggestedFeeds")]
    internal sealed record GetSuggestedFeedsResponse
    {
        [JsonConstructor]
        internal GetSuggestedFeedsResponse(ICollection<GeneratorView> feeds, string? cursor)
        {
            Feeds = feeds;
            Cursor = cursor;
        }

        [JsonInclude]
        [JsonRequired]
        public ICollection<GeneratorView> Feeds { get; init; }

        [JsonInclude]
        public string? Cursor { get; init; }
    }
}
