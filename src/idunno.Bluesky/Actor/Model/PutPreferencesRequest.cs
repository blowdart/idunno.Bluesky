// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor.Model
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in PutPreferences.")]
    internal sealed class PutPreferencesRequest
    {
        [JsonConstructor]
        public PutPreferencesRequest(IList<Preference> preferences)
        {
            Preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        }

        public PutPreferencesRequest(Preferences preferences)
        {
            Preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        }

        [JsonInclude]
        public IList<Preference> Preferences { get; init; } = Array.Empty<Preference>();
    }
}
