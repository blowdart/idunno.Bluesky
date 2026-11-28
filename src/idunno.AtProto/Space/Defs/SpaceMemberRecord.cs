// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto.Repo;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>
/// Represents a published member access record.
/// </summary>
/// <remarks>
/// <para>The record key identifies the member DID. The record is authoritative only in the space authority's repository.</para>
/// </remarks>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(SpaceMemberRecord), "com.atproto.memberspace.member")]
public record SpaceMemberRecord : AtProtoRecord
{
    /// <summary>
    /// Gets or sets a value that indicates whether the member may read the space.
    /// </summary>
    /// <value><see langword="true"/> if reading is allowed; otherwise, <see langword="false"/>.</value>
    [JsonRequired]
    public required bool Read { get; init; }

    /// <summary>
    /// Gets or sets a value that indicates whether the member may write to the space.
    /// </summary>
    /// <value><see langword="true"/> if writing is allowed; otherwise, <see langword="false"/>.</value>
    [JsonRequired]
    public required bool Write { get; init; }
}
