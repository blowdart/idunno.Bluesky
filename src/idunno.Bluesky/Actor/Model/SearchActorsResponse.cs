﻿// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor.Model
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in SearchActors.")]
    internal sealed record SearchActorsResponse
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
