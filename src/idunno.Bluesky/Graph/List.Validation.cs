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

namespace idunno.Bluesky.Graph;

public partial record List : IValidatableObject, IRecordValidatable
{
    private List(ListJsonValue value) : base(value.CreatedAt)
    {
        _name = value.Name ?? throw new JsonException("A list name is required.");
        _description = value.Description;
        PurposeValue = value.Purpose ?? throw new JsonException("A list purpose is required.");
        DescriptionFacets = value.DescriptionFacets?.Select(Post.DeserializeUnvalidatedFacet).ToArray() ?? [];
        Avatar = value.Avatar;
        Labels = value.Labels;
        ExtensionData = RecordJson.CopyExtensionData(value.ExtensionData);
    }

    /// <summary>Parses a JSON document into a list.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <returns>A validated list.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The value is not a valid list.</exception>
    public static List Parse(JsonDocument document) => Parse(document, validate: true);

    /// <summary>Parses a JSON document into a list with optional validation.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the list; otherwise, <see langword="false"/>.</param>
    /// <returns>A parsed list.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The value cannot be parsed using the selected validation mode.</exception>
    public static List Parse(JsonDocument document, bool validate)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Parse(document.RootElement, validate);
    }

    /// <summary>Parses a JSON element into a list.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <returns>A validated list.</returns>
    /// <exception cref="JsonException">The value is not a valid list.</exception>
    public static List Parse(JsonElement element) => Parse(element, validate: true);

    /// <summary>Parses a JSON element into a list with optional validation.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the list; otherwise, <see langword="false"/>.</param>
    /// <returns>A parsed list.</returns>
    /// <exception cref="JsonException">The value cannot be parsed using the selected validation mode.</exception>
    public static List Parse(JsonElement element, bool validate) =>
        RecordJson.Parse(element, validate, SourceGenerationContext.Default.GraphList, DeserializeUnvalidated);

    /// <summary>Tries to parse a JSON document into a validated list.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <param name="result">When this method returns, contains the list if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, [MaybeNullWhen(false)] out List result) => TryParse(document, validate: true, out result);

    /// <summary>Tries to parse a JSON document into a list with optional validation.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the list; otherwise, <see langword="false"/>.</param>
    /// <param name="result">When this method returns, contains the list if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, bool validate, [MaybeNullWhen(false)] out List result)
    {
        result = null;

        return document is not null && TryParse(document.RootElement, validate, out result);
    }

    /// <summary>Tries to parse a JSON element into a validated list.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <param name="result">When this method returns, contains the list if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, [MaybeNullWhen(false)] out List result) => TryParse(element, validate: true, out result);

    /// <summary>Tries to parse a JSON element into a list with optional validation.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the list; otherwise, <see langword="false"/>.</param>
    /// <param name="result">When this method returns, contains the list if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, bool validate, [MaybeNullWhen(false)] out List result) =>
        RecordJson.TryParse(element, validate, SourceGenerationContext.Default.GraphList, DeserializeUnvalidated, out result);

    private static List? DeserializeUnvalidated(JsonElement element)
    {
        ListJsonValue? value = JsonSerializer.Deserialize(element, SourceGenerationContext.Default.ListJsonValue);

        return value is null ? null : new List(value);
    }

    /// <summary>Validates list text, facets, purpose and avatar.</summary>
    /// <param name="validationContext">The context in which validation is performed.</param>
    /// <returns>A collection of validation failures, or an empty collection when the list is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateList();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidateList();

    private IEnumerable<ValidationResult> ValidateList()
    {
        if (string.IsNullOrEmpty(Name))
        {
            yield return new ValidationResult("A list name is required.", [nameof(Name)]);
        }

        if (PurposeValue is null)
        {
            yield return new ValidationResult("A list purpose is required.", [nameof(PurposeValue)]);
        }

        foreach (ValidationResult result in RecordValidation.Text(Name, nameof(Name), Maximum.ListNameLengthInBytes, int.MaxValue)
            .Concat(RecordValidation.Text(Description, nameof(Description), Maximum.ListDescriptionLengthInBytes, Maximum.ListDescriptionLengthInGraphemes))
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

    internal sealed record ListJsonValue : AtProtoRecord
    {
        [JsonRequired]
        public string? Name { get; init; }

        [JsonRequired]
        public string? Purpose { get; init; }

        [JsonRequired]
        public DateTimeOffset CreatedAt { get; init; }

        public string? Description { get; init; }

        public JsonElement[]? DescriptionFacets { get; init; }

        public Blob? Avatar { get; init; }

        public SelfLabels? Labels { get; init; }
    }
}
