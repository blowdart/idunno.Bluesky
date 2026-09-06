// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Record;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// A declaration of an account's preferences for appearing in content discovery surfaces.
/// </summary>
/// <param name="HideFromAlgorithmicRecommendations">
/// Flag indicating Whether the account requests that its posts be hidden from algorithmic recommendations.
/// Consumers must treat a missing record as <see langword="false"/>
/// </param>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = false, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(ContentVisibilityDeclaration), "app.bsky.actor.contentVisibilityDeclaration")]
public record ContentVisibilityDeclaration(
    [property: JsonRequired] bool HideFromAlgorithmicRecommendations) : BlueskyRecord
{
}
