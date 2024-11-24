// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed.Model
{
    internal sealed record GetTimelineResponse
    {
        [JsonConstructor]
        internal GetTimelineResponse(ICollection<FeedViewPost>? feed, string? cursor)
        {
            if (feed is null)
            {
                Feed = new List<FeedViewPost>();
            }
            else
            {
                Feed = feed;
            }

            Cursor = cursor;
        }

        [JsonInclude]
        [NotNull]
        public ICollection<FeedViewPost>? Feed { get; init; }

        [JsonInclude]
        public string? Cursor { get; init; }
    }
}
