// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed.Model
{
    internal sealed record GetFeedGeneratorsResponse
    {
        [JsonConstructor]
        internal GetFeedGeneratorsResponse(IList<GeneratorView> feeds) => Feeds = feeds;

        [JsonInclude]
        [JsonRequired]
        public IList<GeneratorView> Feeds { get; init; }
    }
}
