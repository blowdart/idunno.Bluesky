// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Feed;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Labeler;

namespace idunno.Bluesky.Embed;

/// <summary>
/// Json Polymorphic base class for embedded record views.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(ViewRecord), typeDiscriminator: EmbeddedViewTypeDiscriminators.ViewRecord)]
[JsonDerivedType(typeof(ViewNotFound), typeDiscriminator: EmbeddedViewTypeDiscriminators.EmbedViewNotFound)]
[JsonDerivedType(typeof(ViewBlocked), typeDiscriminator: EmbeddedViewTypeDiscriminators.EmbedViewBlocked)]
[JsonDerivedType(typeof(ViewDetached), typeDiscriminator: EmbeddedViewTypeDiscriminators.EmbedViewDetached)]
[JsonDerivedType(typeof(GeneratorView), typeDiscriminator: EmbeddedViewTypeDiscriminators.GeneratorView)]
[JsonDerivedType(typeof(ListView), typeDiscriminator: EmbeddedViewTypeDiscriminators.ListView)]
[JsonDerivedType(typeof(LabelerView), typeDiscriminator: EmbeddedViewTypeDiscriminators.LabelerView)]
[JsonDerivedType(typeof(StarterPackViewBasic), typeDiscriminator: EmbeddedViewTypeDiscriminators.StarterPackViewBasic)]
[JsonDerivedType(typeof(EmbeddedView), typeDiscriminator: EmbeddedViewTypeDiscriminators.EmbedView)]
public record View : Bluesky.View
{
}
