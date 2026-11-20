// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Feed;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// The reason a feed generator gives for a post appearing in a <see cref="FeedSkeleton"/>.
/// </summary>
/// <remarks>
/// <para>
/// A feed generator normally returns a <see cref="SkeletonReasonRepost"/> or a <see cref="SkeletonReasonPin"/>.
/// A reason whose <c>$type</c> this library does not recognize is returned as an instance of this base type,
/// with its properties available in <see cref="ExtensionData"/>.
/// </para>
/// </remarks>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(SkeletonReasonRepost), typeDiscriminator: "app.bsky.feed.defs#skeletonReasonRepost")]
[JsonDerivedType(typeof(SkeletonReasonPin), typeDiscriminator: "app.bsky.feed.defs#skeletonReasonPin")]
public record SkeletonReason
{
    /// <summary>
    /// Gets or sets a dictionary of keys and element data that do not map to any strongly typed properties.
    /// </summary>
    [JsonExtensionData]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be writable for json deserialization.")]
    public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();
}
