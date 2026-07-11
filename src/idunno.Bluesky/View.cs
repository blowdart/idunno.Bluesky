// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.Bluesky;

/// <summary>
/// Base class for views
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(Feed.GeneratorView), typeDiscriminator: EmbeddedViewTypeDiscriminators.GeneratorView)]
[JsonDerivedType(typeof(Graph.ListView), typeDiscriminator: EmbeddedViewTypeDiscriminators.ListView)]
[JsonDerivedType(typeof(Labeler.LabelerView), typeDiscriminator: EmbeddedViewTypeDiscriminators.LabelerView)]
[JsonDerivedType(typeof(Graph.StarterPackViewBasic), typeDiscriminator: EmbeddedViewTypeDiscriminators.StarterPackViewBasic)]
[JsonDerivedType(typeof(Embed.ViewRecord), typeDiscriminator: EmbeddedViewTypeDiscriminators.ViewRecord)]
[JsonDerivedType(typeof(Embed.EmbeddedView), typeDiscriminator: EmbeddedViewTypeDiscriminators.EmbedView)]
[JsonDerivedType(typeof(Chat.Group.JoinLinkView), typeDiscriminator: "chat.bsky.group.defs#joinLinkView")]
[JsonDerivedType(typeof(Chat.Group.JoinLinkView), typeDiscriminator: "chat.bsky.group.defs#joinLinkView")]

public record View
{
    /// <summary>
    /// Creates a new instance of <see cref="View"/>.
    /// </summary>
    public View() { }

    /// <summary>
    /// A list of keys and element data that do not map to any strongly typed properties.
    /// </summary>
    [NotNull]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Must be writable for JSON deserialization.")]
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();
}