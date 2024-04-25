// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky.Actor
{
    public record ActorSearchResults
    {
        [JsonInclude]
        [JsonRequired]
        public IReadOnlyList<ActorProfile> Actors { get; internal set; } = new List<ActorProfile>();

        [JsonInclude]
        public string? Cursor { get; internal set; }
    }
}
