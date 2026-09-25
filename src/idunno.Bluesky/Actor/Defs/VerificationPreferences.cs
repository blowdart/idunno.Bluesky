// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

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