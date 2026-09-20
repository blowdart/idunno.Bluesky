// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Unspecced;

/// <summary>
/// An accounts age assurance status.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AgeAssuranceStatus>))]
public enum AgeAssuranceStatus
{
    /// <summary>
    /// The age assurance status is unknown.
    /// </summary>
    [JsonStringEnumMemberName("unknown")]
    Unknown = 0,

    /// <summary>
    /// The age assurance process has started, but has not completed.
    /// </summary>
    [JsonStringEnumMemberName("pending")]
    Pending,

    /// <summary>
    /// The account belongs to a user of an appropriate age for their location.
    /// </summary>
    [JsonStringEnumMemberName("assured")]
    Assured,

    /// <summary>
    /// The age assurance status indicates its blocked.
    /// </summary>
    [JsonStringEnumMemberName("blocked")]
    Blocked
}