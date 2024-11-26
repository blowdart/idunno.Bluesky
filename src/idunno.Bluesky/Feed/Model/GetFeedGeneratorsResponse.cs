// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed.Model
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in GetFeedGenerators")]
    internal sealed record GetFeedGeneratorsResponse
    {
        [JsonConstructor]
        internal GetFeedGeneratorsResponse(IList<GeneratorView> feeds) => Feeds = feeds;

        [JsonInclude]
        [JsonRequired]
        public IList<GeneratorView> Feeds { get; init; }
    }
}
