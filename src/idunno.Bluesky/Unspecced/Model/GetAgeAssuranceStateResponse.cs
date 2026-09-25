// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Unspecced.Model;

internal sealed record GetAgeAssuranceStateResponse(DateTimeOffset? LastInitiatedAt, [property: JsonRequired] AgeAssuranceStatus Status)
{
}