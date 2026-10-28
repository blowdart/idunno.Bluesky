// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.RichText;

/// <summary>
/// Annotation of a sub-string within rich text.
/// </summary>
[JsonPolymorphic]
[JsonDerivedType(typeof(Facet), typeDiscriminator: "app.bsky.richtext.facet")]
public record Facet : IValidatableObject, IRecordValidatable
{
    /// <summary>
    /// Creates a new instance of <see cref="Facet"/>.
    /// </summary>
    /// <param name="index">The byte slice the facet refers to.</param>
    /// <param name="features">A list of <see cref="FacetFeature"/>s for the facet.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="index"/> or <paramref name="features"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="features"/> contains a <see langword="null"/> entry.</exception>
    [JsonConstructor]
    public Facet(ByteSlice index, IList<FacetFeature> features)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(features);

        Index = index;
        Features = features;
    }

    /// <summary>
    /// A list of <see cref="FacetFeature"/>s for the facet.
    /// </summary>
    /// <exception cref="ArgumentNullException">The collection is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The collection contains a <see langword="null"/> entry.</exception>
    /// <remarks>
    /// <para>The collection is copied when it is set, so later changes to the collection the caller supplied do
    /// not alter the facet, and the returned collection is read only.</para>
    /// </remarks>
    [JsonInclude]
    [JsonRequired]
    public IList<FacetFeature> Features
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            List<FacetFeature> features = [.. value];
            if (features.Any(feature => feature is null))
            {
                throw new ArgumentException("Facet features cannot contain null entries.", nameof(value));
            }

            field = features.AsReadOnly();
        }
    } = null!;

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

    /// <summary>
    /// Validates the byte range and features in the facet.
    /// </summary>
    /// <param name="validationContext">The context in which the validation is performed.</param>
    /// <returns>A collection of validation errors, or an empty collection when the facet is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateFacet();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord()
    {
        return ValidateFacet();
    }

    private IEnumerable<ValidationResult> ValidateFacet()
    {
        foreach (ValidationResult result in RecordValidation.Nested(Index, nameof(Index))
            .Concat(RecordValidation.Collection(Features, nameof(Features), int.MaxValue)))
        {
            yield return result;
        }
    }
}
