// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto.Repo;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>
/// Describes a space returned when listing spaces.
/// </summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
public sealed record SpaceView : AtProtoObject
{
    /// <summary>
    /// Gets or sets the space reference.
    /// </summary>
    [JsonRequired]
    public required SpaceAtUri Space { get; init; }

    /// <summary>
    /// Gets or sets the repository revision, if available.
    /// </summary>
    public TimestampIdentifier? RepoRev { get; init; }

    /// <summary>
    /// Gets or sets the account's repository commit data, if it has a repository in the space.
    /// </summary>
    public Bytes? Data { get; init; }

    /// <summary>
    /// Gets or sets the current space revision, if the account is the space's authority.
    /// </summary>
    public TimestampIdentifier? SpaceRev { get; init; }
}
