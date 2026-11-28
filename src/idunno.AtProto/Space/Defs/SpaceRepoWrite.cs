// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto.Repo;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>
/// Describes a repository write included in a space synchronization event.
/// </summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
public sealed record SpaceRepoWrite : AtProtoObject
{
    /// <summary>
    /// Gets or sets the repository DID.
    /// </summary>
    [JsonRequired]
    public required Did Repo { get; init; }

    /// <summary>
    /// Gets or sets the repository revision.
    /// </summary>
    [JsonRequired]
    public required TimestampIdentifier RepoRev { get; init; }

    /// <summary>
    /// Gets or sets the serialized write data.
    /// </summary>
    [JsonRequired]
    public required Bytes Data { get; init; }

    /// <summary>
    /// Gets or sets the current space revision.
    /// </summary>
    [JsonRequired]
    public required TimestampIdentifier SpaceRev { get; init; }

    /// <summary>
    /// Gets or sets the previous space revision, if any.
    /// </summary>
    public TimestampIdentifier? PrevSpaceRev { get; init; }
}
