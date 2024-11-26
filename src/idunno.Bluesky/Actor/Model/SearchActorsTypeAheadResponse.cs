// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor.Model
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in SearchActorsTypeAhead.")]
    internal sealed record SearchActorsTypeAheadResponse
    {
        [JsonConstructor]
        public SearchActorsTypeAheadResponse(IReadOnlyCollection<ProfileViewBasic> actors)
        {
            Actors = actors;
        }

        [JsonInclude]
        public IReadOnlyCollection<ProfileViewBasic> Actors { get; init; }
    }
}
