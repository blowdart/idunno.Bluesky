// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.AgeAssurance;

/// <summary>
/// An additional permitted age-assurance verification method.
/// </summary>
[JsonConverter(typeof(AdditionalVerificationMethodConverter))]
public enum AdditionalVerificationMethod
{
    /// <summary>
    /// The verification method is not recognized.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Native on-device age APIs may be used.
    /// </summary>
    Device
}
