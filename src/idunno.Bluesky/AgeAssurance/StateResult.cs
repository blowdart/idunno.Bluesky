// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.AgeAssurance;

/// <summary>
/// The <paramref name="State"/> and <paramref name="Metadata"/> returned by the query.
/// </summary>
/// <param name="State">The server-computed value.</param>
/// <param name="Metadata">Additional data used by clients.</param>
public sealed record StateResult(
    [property: JsonRequired] State State,
    [property: JsonRequired] StateMetadata Metadata);
