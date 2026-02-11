// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using System.Text.Json;

namespace idunno.Bluesky.RichText
{
    /// <summary>
    /// Annotation of a sub-string within rich text.
    /// </summary>
    [JsonPolymorphic]
    [JsonDerivedType(typeof(Facet), typeDiscriminator: "app.bsky.richtext.facet")]
    public record Facet
    {
        /// <summary>
        /// Creates a new instance of <see cref="Facet"/>.
        /// </summary>
        /// <param name="index">The byte slice the facet refers to.</param>
        /// <param name="features">A list of <see cref="FacetFeature"/>s for the facet.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="index"/> or <paramref name="features"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="features"/> has no <see cref="FacetFeature"/>s.</exception>
        [JsonConstructor]
        public Facet(ByteSlice index, IList<FacetFeature> features)
        {
            ArgumentNullException.ThrowIfNull(index);
            ArgumentNullException.ThrowIfNull(features);
            ArgumentOutOfRangeException.ThrowIfZero(features.Count);

            Index = index;
            Features = features;
        }

        /// <summary>
        /// A list of <see cref="FacetFeature"/>s for the facet.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public IList<FacetFeature> Features { get; init; }

        /// <summary>
        /// The byte slice the facet refers to.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public ByteSlice Index { get; init; }

        /// <summary>
        /// A list of keys and element data that do not map to any strongly typed properties.
        /// </summary>
        [JsonExtensionData]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be writable for json deserialization")]
        public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();
    }
}
