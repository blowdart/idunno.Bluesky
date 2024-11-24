// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Feed
{
    public sealed record ReasonRepost : ReasonBase
    {
        [JsonConstructor]
        public ReasonRepost(ProfileViewBasic by, DateTimeOffset indexedAt)
        {
            By = by;
            IndexedAt = indexedAt;
        }

        [JsonInclude]
        [JsonRequired]
        public ProfileViewBasic By { get; init; }

        [JsonInclude]
        [JsonRequired]
        public DateTimeOffset IndexedAt { get; init; }
    }
}
