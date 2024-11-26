// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor.Model
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in GetProfiles.")]
    internal sealed record GetProfilesResponse
    {
        [JsonConstructor]
        public GetProfilesResponse(ProfileViewDetailed[] profiles)
        {
            Profiles = profiles;
        }

        [JsonInclude]
        public ProfileViewDetailed[] Profiles { get; init; }
    }
}
