// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed.Model
{
    internal sealed record GetAuthorFeedResponse
    {
        [JsonConstructor]
        public GetAuthorFeedResponse(ICollection<FeedViewPost> feed, string? cursor)
        {
            Feed = feed;
            Cursor = cursor;
        }

        [JsonInclude]
        [JsonRequired]
        public ICollection<FeedViewPost> Feed { get; init; }

        [JsonInclude]
        public string? Cursor { get; init; }
    }
}
