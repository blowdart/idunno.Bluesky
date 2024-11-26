// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor.Model
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in GetPreferences")]
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
