// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto.Labels;
using idunno.Bluesky.Json;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Drafts;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public partial record DraftPost : IValidatableObject, IRecordValidatable
{
    private DraftPost(DraftPostJsonValue value)
    {
        _text = value.Text ?? throw new JsonException("Draft post text is required.");
        Labels = value.Labels;
        EmbedImages = TolerantDraftJson.ReadCollection(value.EmbedImages, TolerantDraftJson.ReadImage);
        EmbedGallery = value.EmbedGallery is JsonElement gallery && gallery.ValueKind is not JsonValueKind.Null
            ? TolerantDraftJson.ReadGallery(gallery) : null;
        EmbedVideos = TolerantDraftJson.ReadCollection(value.EmbedVideos, TolerantDraftJson.ReadVideo);
        EmbedExternals = value.EmbedExternals is null ? null : Array.AsReadOnly(value.EmbedExternals.ToArray());
        EmbedRecords = value.EmbedRecords is null ? null : Array.AsReadOnly(value.EmbedRecords.ToArray());
    }

    /// <summary>Parses a JSON document into a validated draft post.</summary>
    /// <param name="document">The document containing the draft post.</param>
    /// <returns>A validated draft post.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The value is not a valid draft post.</exception>
    public static DraftPost Parse(JsonDocument document) => Parse(document, validate: true);

    /// <summary>Parses a JSON document into a draft post with optional validation.</summary>
    /// <param name="document">The document containing the draft post.</param>
    /// <param name="validate"><see langword="true"/> to validate the draft post; otherwise, <see langword="false"/>.</param>
    /// <returns>A parsed draft post.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The value cannot be parsed using the selected validation mode.</exception>
    public static DraftPost Parse(JsonDocument document, bool validate)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Parse(document.RootElement, validate);
    }

    /// <summary>Parses a JSON element into a validated draft post.</summary>
    /// <param name="element">The element containing the draft post.</param>
    /// <returns>A validated draft post.</returns>
    /// <exception cref="JsonException">The value is not a valid draft post.</exception>
    public static DraftPost Parse(JsonElement element) => Parse(element, validate: true);

    /// <summary>Parses a JSON element into a draft post with optional validation.</summary>
    /// <param name="element">The element containing the draft post.</param>
    /// <param name="validate"><see langword="true"/> to validate the draft post; otherwise, <see langword="false"/>.</param>
    /// <returns>A parsed draft post.</returns>
    /// <exception cref="JsonException">The value cannot be parsed using the selected validation mode.</exception>
    public static DraftPost Parse(JsonElement element, bool validate) =>
        RecordJson.Parse(element, validate, SourceGenerationContext.Default.DraftPost, DeserializeUnvalidated);

    /// <summary>Tries to parse a JSON document into a validated draft post.</summary>
    /// <param name="document">The document containing the draft post.</param>
    /// <param name="result">When this method returns, contains the draft post if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, [MaybeNullWhen(false)] out DraftPost result) => TryParse(document, validate: true, out result);

    /// <summary>Tries to parse a JSON document into a draft post with optional validation.</summary>
    /// <param name="document">The document containing the draft post.</param>
    /// <param name="validate"><see langword="true"/> to validate the draft post; otherwise, <see langword="false"/>.</param>
    /// <param name="result">When this method returns, contains the draft post if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, bool validate, [MaybeNullWhen(false)] out DraftPost result)
    {
        result = null;

        return document is not null && TryParse(document.RootElement, validate, out result);
    }

    /// <summary>Tries to parse a JSON element into a validated draft post.</summary>
    /// <param name="element">The element containing the draft post.</param>
    /// <param name="result">When this method returns, contains the draft post if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, [MaybeNullWhen(false)] out DraftPost result) => TryParse(element, validate: true, out result);

    /// <summary>Tries to parse a JSON element into a draft post with optional validation.</summary>
    /// <param name="element">The element containing the draft post.</param>
    /// <param name="validate"><see langword="true"/> to validate the draft post; otherwise, <see langword="false"/>.</param>
    /// <param name="result">When this method returns, contains the draft post if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, bool validate, [MaybeNullWhen(false)] out DraftPost result) =>
        RecordJson.TryParse(element, validate, SourceGenerationContext.Default.DraftPost, DeserializeUnvalidated, out result);

    internal static DraftPost? DeserializeUnvalidated(JsonElement element)
    {
        TolerantDraftJson.CheckType(element, "app.bsky.draft.defs#draftPost");
        DraftPostJsonValue? value = JsonSerializer.Deserialize(element, SourceGenerationContext.Default.DraftPostJsonValue);

        return value is null ? null : new DraftPost(value);
    }

    /// <summary>Validates draft post text and all nested draft media.</summary>
    /// <param name="validationContext">The context in which validation is performed.</param>
    /// <returns>A collection of validation failures, or an empty collection when the draft post is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidatePost();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidatePost();

    private IEnumerable<ValidationResult> ValidatePost()
    {
        if (Text is null)
        {
            yield return new ValidationResult("Draft post text is required.", [nameof(Text)]);
        }

        foreach (ValidationResult result in RecordValidation.Text(Text, nameof(Text), Maximum.DraftTextLengthInBytes, Maximum.DraftTextLengthInGraphemes)
            .Concat(RecordValidation.Collection(EmbedImages, nameof(EmbedImages), Maximum.DraftEmbedImages))
            .Concat(RecordValidation.Nested(EmbedGallery, nameof(EmbedGallery), required: false))
            .Concat(RecordValidation.Collection(EmbedVideos, nameof(EmbedVideos), Maximum.DraftEmbedVideos))
            .Concat(RecordValidation.Collection(EmbedExternals, nameof(EmbedExternals), Maximum.DraftEmbedExternals))
            .Concat(RecordValidation.Collection(EmbedRecords, nameof(EmbedRecords), Maximum.DraftEmbedRecords)))
        {
            yield return result;
        }
    }

    internal sealed record DraftPostJsonValue(
        [property: JsonRequired] string? Text,
        SelfLabels? Labels,
        JsonElement[]? EmbedImages,
        JsonElement? EmbedGallery,
        JsonElement[]? EmbedVideos,
        IReadOnlyList<DraftEmbedExternal>? EmbedExternals,
        IReadOnlyList<DraftEmbedRecord>? EmbedRecords);
}
