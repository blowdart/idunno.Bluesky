// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor.Model
{
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
