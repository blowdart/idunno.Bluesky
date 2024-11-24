// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor.Model
{
    internal sealed record GetPreferencesResponse
    {
        [JsonConstructor]
        public GetPreferencesResponse(Preference[] preferences)
        {
            Preferences = preferences;
        }

        [JsonInclude]
        public Preference[] Preferences { get; init; }
    }
}
