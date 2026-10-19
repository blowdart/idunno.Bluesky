// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.Json;

namespace idunno.Bluesky.Feed;

public partial record Generator : IValidatableObject, IRecordValidatable
{
    /// <summary>Parses a JSON document into a validated feed generator record.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <returns>A validated feed generator record.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The value is not a valid feed generator record.</exception>
    public static Generator Parse(JsonDocument document) => Parse(document, validate: true);

    /// <summary>Parses a JSON document into a feed generator record with optional validation.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the generator; otherwise, <see langword="false"/>.</param>
    /// <returns>A parsed feed generator record.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The value cannot be parsed using the selected validation mode.</exception>
    public static Generator Parse(JsonDocument document, bool validate)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Parse(document.RootElement, validate);
    }

    /// <summary>Parses a JSON element into a validated feed generator record.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <returns>A validated feed generator record.</returns>
    /// <exception cref="JsonException">The value is not a valid feed generator record.</exception>
    public static Generator Parse(JsonElement element) => Parse(element, validate: true);

    /// <summary>Parses a JSON element into a feed generator record with optional validation.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the generator; otherwise, <see langword="false"/>.</param>
    /// <returns>A parsed feed generator record.</returns>
    /// <exception cref="JsonException">The value cannot be parsed using the selected validation mode.</exception>
    public static Generator Parse(JsonElement element, bool validate) =>
        RecordJson.Parse(element, validate, SourceGenerationContext.Default.Generator, DeserializeUnvalidated);

    /// <summary>Tries to parse a JSON document into a validated feed generator record.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <param name="result">When this method returns, contains the generator if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, [MaybeNullWhen(false)] out Generator result) => TryParse(document, validate: true, out result);

    /// <summary>Tries to parse a JSON document into a feed generator record with optional validation.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the generator; otherwise, <see langword="false"/>.</param>
    /// <param name="result">When this method returns, contains the generator if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, bool validate, [MaybeNullWhen(false)] out Generator result)
    {
        result = null;

        return document is not null && TryParse(document.RootElement, validate, out result);
    }

    /// <summary>Tries to parse a JSON element into a validated feed generator record.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <param name="result">When this method returns, contains the generator if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, [MaybeNullWhen(false)] out Generator result) => TryParse(element, validate: true, out result);

    /// <summary>Tries to parse a JSON element into a feed generator record with optional validation.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the generator; otherwise, <see langword="false"/>.</param>
    /// <param name="result">When this method returns, contains the generator if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, bool validate, [MaybeNullWhen(false)] out Generator result) =>
        RecordJson.TryParse(element, validate, SourceGenerationContext.Default.Generator, DeserializeUnvalidated, out result);

    private static Generator? DeserializeUnvalidated(JsonElement element)
    {
        GeneratorJsonValue? value = RecordJson.Read(element, SourceGenerationContext.Default.GeneratorJsonValue);
        if (value is null)
        {
            return null;
        }

        return new Generator
        {
            Did = value.Did ?? throw new JsonException("A feed generator service DID is required."),
            DisplayName = value.DisplayName ?? throw new JsonException("A feed generator display name is required."),
            CreatedAt = value.CreatedAt,
            Description = value.Description,
            DescriptionFacets = value.DescriptionFacets is null ? null : Array.AsReadOnly(value.DescriptionFacets.Select(Post.DeserializeUnvalidatedFacet).ToArray()),
            Avatar = value.Avatar,
            AcceptsInteractions = value.AcceptsInteractions,
            ContentMode = value.ContentMode,
            Labels = value.Labels,
            ExtensionData = RecordJson.CopyExtensionData(value.ExtensionData)
        };
    }

    /// <summary>Validates the service DID, text, description facets and avatar metadata.</summary>
    /// <param name="validationContext">The context in which validation is performed.</param>
    /// <returns>A collection of validation failures, or an empty collection when the generator is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    /// <remarks><para>Blob metadata is untrusted. Validation does not inspect the avatar file or contact the service.</para></remarks>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateGenerator();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidateGenerator();

    private IEnumerable<ValidationResult> ValidateGenerator()
    {
        if (Did is null)
        {
            yield return new ValidationResult("A feed generator service DID is required.", [nameof(Did)]);
        }

        if (DisplayName is null)
        {
            yield return new ValidationResult("A feed generator display name is required.", [nameof(DisplayName)]);
        }

        foreach (ValidationResult result in RecordValidation.Text(DisplayName, nameof(DisplayName), 240, 24)
            .Concat(RecordValidation.Text(Description, nameof(Description), 3000, 300))
            .Concat(RecordValidation.Collection(DescriptionFacets, nameof(DescriptionFacets), int.MaxValue)))
        {
            yield return result;
        }

        if (Avatar is not null)
        {
            foreach (ValidationResult result in RecordValidation.Blob(Avatar, nameof(Avatar), 1_000_000, "image/png", "image/jpeg"))
            {
                yield return result;
            }
        }
    }

    internal sealed record GeneratorJsonValue : AtProtoRecord
    {
        [JsonRequired]
        public Did? Did { get; init; }

        [JsonRequired]
        public string? DisplayName { get; init; }

        [JsonRequired]
        public DateTimeOffset CreatedAt { get; init; }

        public string? Description { get; init; }

        public JsonElement[]? DescriptionFacets { get; init; }

        public Blob? Avatar { get; init; }

        public bool? AcceptsInteractions { get; init; }

        public GeneratorContentMode? ContentMode { get; init; }

        public SelfLabels? Labels { get; init; }
    }
}
