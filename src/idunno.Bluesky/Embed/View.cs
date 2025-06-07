// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed
{
    /// <summary>
    /// Json Polymorphic base class for embedded record views.
    /// </summary>
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    [JsonDerivedType(typeof(ViewRecord), typeDiscriminator: EmbeddedViewTypeDiscriminators.ViewRecord)]
    [JsonDerivedType(typeof(EmbeddedView), typeDiscriminator: EmbeddedViewTypeDiscriminators.EmbedView)]
    [JsonDerivedType(typeof(ViewNotFound), typeDiscriminator: EmbeddedViewTypeDiscriminators.EmbedViewNotFound)]
    [JsonDerivedType(typeof(ViewBlocked), typeDiscriminator: EmbeddedViewTypeDiscriminators.EmbedViewBlocked)]
    [JsonDerivedType(typeof(ViewDetached), typeDiscriminator: EmbeddedViewTypeDiscriminators.EmbedViewDetached)]
    public record View : Bluesky.View
    {
    }
}
