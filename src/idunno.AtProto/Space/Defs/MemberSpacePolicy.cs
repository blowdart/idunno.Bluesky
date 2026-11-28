// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>
/// A policy controlling public access and membership for a space.
/// </summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(MemberSpacePolicy), "com.atproto.memberspace.policy")]
public class MemberSpacePolicy : SpacePolicy
{
    /// <summary>
    /// Gets or sets whether reads are allowed without membership.
    /// </summary>
    public bool? PublicRead { get; init; }

    /// <summary>
    /// Gets or sets whether writes are allowed without membership.
    /// </summary>
    public bool? PublicWrite { get; init; }

    /// <summary>
    /// Gets or sets whether the space policy is published.
    /// </summary>
    public bool? PublishPolicy { get; init; }

    /// <summary>
    /// Gets or sets whether the space's members are published.
    /// </summary>
    public bool? PublishMembers { get; init; }

    /// <summary>
    /// Gets or sets the applications allowed to access the space.
    /// </summary>
    public IReadOnlyList<Uri>? AllowedApps { get; init; }
}
