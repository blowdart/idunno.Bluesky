// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Unspecced;

namespace idunno.Bluesky.AgeAssurance;

/// <summary>
/// The computed age-assurance state for an account.
/// </summary>
/// <param name="Status">The current age-assurance status.</param>
/// <param name="Access">The access level granted based on processed age-assurance data.</param>
/// <param name="LastInitiatedAt">The timestamp when the state was last updated, if available.</param>
public sealed record State(
    [property: JsonRequired] AgeAssuranceStatus Status,
    [property: JsonRequired] AgeAssuranceAccess Access,
    DateTimeOffset? LastInitiatedAt);
