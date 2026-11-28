// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>
/// A policy for a space managed by an application service.
/// </summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ManagedSpacePolicy), "com.atproto.managedspace.policy")]
public class ManagedSpacePolicy : SpacePolicy
{
    /// <summary>
    /// Gets or sets the managing application service reference in <c>did#serviceId</c> form.
    /// </summary>
    [JsonRequired]
    public required string ManagingApp { get; init; }

    /// <summary>
    /// Gets or sets whether the space policy is published.
    /// </summary>
    public bool? PublishPolicy { get; init; }
}
