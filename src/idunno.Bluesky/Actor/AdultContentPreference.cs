// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// A user's adult content preferences.
    /// </summary>
    public sealed record AdultContentPreference : Preference
    {
        /// <summary>
        /// Creates a new instance of <see cref="AdultContentPreference"/>
        /// </summary>
        /// <param name="enabled">Flag indicating whether adult content is enabled or not.</param>
        [JsonConstructor]
        public AdultContentPreference(bool enabled) : base()
        {
            Enabled = enabled;
        }

        /// <summary>
        /// Flag indicating whether adult content is enabled or not.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public bool Enabled { get; init; }
    }
}
