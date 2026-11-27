// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto.Repo;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>
/// Describes a space and its policy.
/// </summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
public sealed record SpaceDescription : AtProtoObject
{
    /// <summary>
    /// Gets or sets the space reference.
    /// </summary>
    [JsonRequired]
    public required SpaceAtUri Space { get; init; }

    /// <summary>
    /// Gets or sets the space's policy.
    /// </summary>
    [JsonRequired]
    public required SpacePolicy Policy { get; init; }
}
