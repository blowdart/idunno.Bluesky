// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky.Feed
{
    public sealed record NotFoundPost : ThreadViewPostBase
    {
        public NotFoundPost(AtUri uri, bool notFound)
        {
            Uri = uri;
            NotFound = notFound;
        }

        [JsonInclude]
        [JsonRequired]
        public AtUri Uri { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public bool NotFound { get; internal set; }
    }
}
