// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky
{
    /// <summary>
    /// An abstract record representing a feature of a <see cref="Facet"/>.
    /// </summary>
    [JsonDerivedType(typeof(HashtagFacetFeature), typeDiscriminator: FacetTypes.HashtagFacet)]
    [JsonDerivedType(typeof(LinkFacetFeature), typeDiscriminator: FacetTypes.LinkFacet)]
    [JsonDerivedType(typeof(MentionFacetFeature), typeDiscriminator: FacetTypes.MentionFacet)]
    public abstract record FacetFeatureBase
    {
    }
}
