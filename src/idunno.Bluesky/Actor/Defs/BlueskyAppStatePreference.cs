// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// A grab bag of state that's specific to the bsky.app program. Third-party apps shouldn't use this
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S2094:Classes should not be empty", Justification = "Needed for json polymorphism, kept empty to discourage use.")]
public sealed record BlueskyAppStatePreference : Preference
{
}