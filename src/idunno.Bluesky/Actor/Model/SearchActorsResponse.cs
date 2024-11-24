// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor.Model
{
    internal record SearchActorsResponse
    {
        [JsonConstructor]
        public SearchActorsResponse(string? cursor, IReadOnlyCollection<ProfileView> actors)
        {
            Cursor = cursor;
            Actors = actors;
        }

        [JsonInclude]
        public string? Cursor { get; init; }

        [JsonInclude]
        public IReadOnlyCollection<ProfileView> Actors { get; init; }
    }
}
