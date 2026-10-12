// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.AgeAssurance;

/// <summary>
/// A platform to which an age-assurance configuration applies.
/// </summary>
[JsonConverter(typeof(AgeAssurancePlatformConverter))]
public enum AgeAssurancePlatform
{
    /// <summary>
    /// The platform is not recognized.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The web platform.
    /// </summary>
    Web,

    /// <summary>
    /// The iOS platform.
    /// </summary>
    Ios,

    /// <summary>
    /// The Android platform.
    /// </summary>
    Android
}
