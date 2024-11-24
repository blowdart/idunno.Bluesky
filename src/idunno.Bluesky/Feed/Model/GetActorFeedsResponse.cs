// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed.Model
{
    internal sealed record GetActorFeedsResponse
    {
        public GetActorFeedsResponse(ICollection<GeneratorView> feeds, string? cursor)
        {
            Feeds = feeds;
            Cursor = cursor;
        }

        [JsonInclude]
        public string? Cursor { get; init; }

        [JsonInclude]
        [JsonRequired]
        public ICollection<GeneratorView> Feeds { get; init; }
    }
}
