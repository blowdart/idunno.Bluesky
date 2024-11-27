// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// A grab bag of state that's specific to the bsky.app program. Third-party apps shouldn't use this
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S2094:Classes should not be empty", Justification = "Needed for json polymorphism, kept empty to discourage use.")]
    public sealed record BlueskyAppStatePreference : Preference
    {
    }
}
