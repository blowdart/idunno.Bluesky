// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.AgeAssurance;

/// <summary>
/// The access level granted based on processed age-assurance data.
/// </summary>
[JsonConverter(typeof(AgeAssuranceAccessConverter))]
public enum AgeAssuranceAccess
{
    /// <summary>
    /// The access level is unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// No access is granted.
    /// </summary>
    None,

    /// <summary>
    /// Safe access is granted.
    /// </summary>
    Safe,

    /// <summary>
    /// Full access is granted.
    /// </summary>
    Full
}
