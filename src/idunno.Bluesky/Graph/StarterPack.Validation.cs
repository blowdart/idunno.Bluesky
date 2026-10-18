// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Json;

namespace idunno.Bluesky.Graph;

public partial record StarterPack : IValidatableObject, IRecordValidatable
{
    private StarterPack(StarterPackJsonValue value) : base(value.CreatedAt)
    {
        _name = value.Name ?? throw new JsonException("A starter pack name is required.");
        _description = value.Description;
        _list = value.List ?? throw new JsonException("A starter pack list is required.");
        Feeds = value.Feeds is null ? null : Array.AsReadOnly(value.Feeds.ToArray());
        UpdatedAt = value.UpdatedAt;
        ExtensionData = RecordJson.CopyExtensionData(value.ExtensionData);
    }

    /// <summary>Parses a JSON document into a validated starter pack.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <returns>A validated starter pack.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The value is not a valid starter pack.</exception>
    public static StarterPack Parse(JsonDocument document) => Parse(document, validate: true);

    /// <summary>Parses a JSON document into a starter pack with optional validation.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the starter pack; otherwise, <see langword="false"/>.</param>
    /// <returns>A parsed starter pack.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The value cannot be parsed using the selected validation mode.</exception>
    public static StarterPack Parse(JsonDocument document, bool validate)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Parse(document.RootElement, validate);
    }

    /// <summary>Parses a JSON element into a validated starter pack.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <returns>A validated starter pack.</returns>
    /// <exception cref="JsonException">The value is not a valid starter pack.</exception>
    public static StarterPack Parse(JsonElement element) => Parse(element, validate: true);

    /// <summary>Parses a JSON element into a starter pack with optional validation.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the starter pack; otherwise, <see langword="false"/>.</param>
    /// <returns>A parsed starter pack.</returns>
    /// <exception cref="JsonException">The value cannot be parsed using the selected validation mode.</exception>
    public static StarterPack Parse(JsonElement element, bool validate) =>
        RecordJson.Parse(element, validate, SourceGenerationContext.Default.GraphStarterPack, DeserializeUnvalidated);

    /// <summary>Tries to parse a JSON document into a validated starter pack.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <param name="result">When this method returns, contains the starter pack if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, [MaybeNullWhen(false)] out StarterPack result) => TryParse(document, validate: true, out result);

    /// <summary>Tries to parse a JSON document into a starter pack with optional validation.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the starter pack; otherwise, <see langword="false"/>.</param>
    /// <param name="result">When this method returns, contains the starter pack if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, bool validate, [MaybeNullWhen(false)] out StarterPack result)
    {
        result = null;

        return document is not null && TryParse(document.RootElement, validate, out result);
    }

    /// <summary>Tries to parse a JSON element into a validated starter pack.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <param name="result">When this method returns, contains the starter pack if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, [MaybeNullWhen(false)] out StarterPack result) => TryParse(element, validate: true, out result);

    /// <summary>Tries to parse a JSON element into a starter pack with optional validation.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the starter pack; otherwise, <see langword="false"/>.</param>
    /// <param name="result">When this method returns, contains the starter pack if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, bool validate, [MaybeNullWhen(false)] out StarterPack result) =>
        RecordJson.TryParse(element, validate, SourceGenerationContext.Default.GraphStarterPack, DeserializeUnvalidated, out result);

    private static StarterPack? DeserializeUnvalidated(JsonElement element)
    {
        StarterPackJsonValue? value = JsonSerializer.Deserialize(element, SourceGenerationContext.Default.StarterPackJsonValue);

        return value is null ? null : new StarterPack(value);
    }

    /// <summary>Validates starter pack text, list and feed references.</summary>
    /// <param name="validationContext">The context in which validation is performed.</param>
    /// <returns>A collection of validation failures, or an empty collection when the starter pack is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateStarterPack();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidateStarterPack();

    private IEnumerable<ValidationResult> ValidateStarterPack()
    {
        if (string.IsNullOrEmpty(Name))
        {
            yield return new ValidationResult("A starter pack name is required.", [nameof(Name)]);
        }

        if (List is null)
        {
            yield return new ValidationResult("A starter pack list is required.", [nameof(List)]);
        }

        foreach (ValidationResult result in RecordValidation.Text(Name, nameof(Name), Maximum.StarterPackNameLengthInBytes, Maximum.StarterPackNameLengthInGraphemes)
            .Concat(RecordValidation.Text(Description, nameof(Description), Maximum.StarterPackDescriptionLengthInBytes, Maximum.StarterPackDescriptionLengthInGraphemes))
            .Concat(RecordValidation.Collection(Feeds, nameof(Feeds), Maximum.FeedsInStarterPack)))
        {
            yield return result;
        }

        for (int index = 0; index < (Feeds?.Count ?? 0); index++)
        {
            if (Feeds![index] is { Uri: null })
            {
                yield return new ValidationResult("A feed URI is required.", [$"{nameof(Feeds)}[{index}].{nameof(FeedItem.Uri)}"]);
            }
        }
    }

    internal sealed record StarterPackJsonValue : AtProtoRecord
    {
        [JsonRequired]
        public string? Name { get; init; }

        [JsonRequired]
        public AtUri? List { get; init; }

        [JsonRequired]
        public DateTimeOffset CreatedAt { get; init; }

        public string? Description { get; init; }

        public DateTimeOffset? UpdatedAt { get; init; }

        public IReadOnlyList<FeedItem>? Feeds { get; init; }
    }
}
