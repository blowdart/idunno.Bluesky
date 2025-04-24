// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// Preferences for how verified accounts appear in an app.
    /// </summary>
    public record class VerificationPreferences : Preference
    {
        /// <summary>
        /// Gets a flag indicating whether the user wants to hide the blue check badges for verified accounts and trusted verifiers.
        /// </summary>
        public bool HideBadges { get; init; }
    }
}
