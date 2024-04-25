// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto.Bluesky.Actor;

namespace idunno.AtProto.Bluesky.Feed
{
    public record FeedProfiles()
    {
        [JsonInclude]
        public IReadOnlyList<ActorProfile>? Profiles { get; internal set; } = new List<ActorProfile>();
    }
}
