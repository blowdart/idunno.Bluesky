// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto.Repo;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>
/// Represents a published space policy record.
/// </summary>
/// <remarks>
/// <para>This record is authoritative only in the space authority's repository and uses the record key <c>self</c>.</para>
/// </remarks>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(SpacePolicyRecord), "com.atproto.space.policy")]
public record SpacePolicyRecord : AtProtoRecord
{
    /// <summary>
    /// Gets or sets the published policy.
    /// </summary>
    /// <value>The published policy.</value>
    [JsonRequired]
    public required SpacePolicy Policy { get; init; }
}
