// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// A preference for an individual labeler.
/// </summary>
/// <param name="Did">The <see cref="AtProto.Did"/> of the labeler this preference applies to.</param>
public record LabelerPreference(
    [property: JsonRequired] Did Did)
{
}