// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.Bluesky.Feed.Gates;
using idunno.Bluesky.Json;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Drafts;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public partial record Draft : IValidatableObject, IRecordValidatable
{
    private Draft(DraftJsonValue value)
    {
        _posts = TolerantDraftJson.ReadCollection(value.Posts,
            element => DraftPost.DeserializeUnvalidated(element) ?? throw new JsonException("A draft post is required."))
            ?? throw new JsonException("Draft posts are required.");
        DeviceId = value.DeviceId;
        DeviceName = value.DeviceName;
        Langs = value.Langs is null ? null : Array.AsReadOnly(value.Langs.ToArray());
        PostGateEmbeddingRules = value.PostGateEmbeddingRules is null ? null : Array.AsReadOnly(value.PostGateEmbeddingRules.ToArray());
        ThreadGateAllowRules = value.ThreadGateAllowRules is null ? null : Array.AsReadOnly(value.ThreadGateAllowRules.ToArray());
    }

    /// <summary>Parses a JSON document into a validated draft.</summary>
    /// <param name="document">The document containing the draft.</param>
    /// <returns>A validated draft.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The value is not a valid draft.</exception>
    public static Draft Parse(JsonDocument document) => Parse(document, validate: true);

    /// <summary>Parses a JSON document into a draft with optional validation.</summary>
    /// <param name="document">The document containing the draft.</param>
    /// <param name="validate"><see langword="true"/> to validate the draft; otherwise, <see langword="false"/>.</param>
    /// <returns>A parsed draft.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The value cannot be parsed using the selected validation mode.</exception>
    public static Draft Parse(JsonDocument document, bool validate)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Parse(document.RootElement, validate);
    }

    /// <summary>Parses a JSON element into a validated draft.</summary>
    /// <param name="element">The element containing the draft.</param>
    /// <returns>A validated draft.</returns>
    /// <exception cref="JsonException">The value is not a valid draft.</exception>
    public static Draft Parse(JsonElement element) => Parse(element, validate: true);

    /// <summary>Parses a JSON element into a draft with optional validation.</summary>
    /// <param name="element">The element containing the draft.</param>
    /// <param name="validate"><see langword="true"/> to validate the draft; otherwise, <see langword="false"/>.</param>
    /// <returns>A parsed draft.</returns>
    /// <exception cref="JsonException">The value cannot be parsed using the selected validation mode.</exception>
    public static Draft Parse(JsonElement element, bool validate) =>
        RecordJson.Parse(element, validate, SourceGenerationContext.Default.Draft, DeserializeUnvalidated);

    /// <summary>Tries to parse a JSON document into a validated draft.</summary>
    /// <param name="document">The document containing the draft.</param>
    /// <param name="result">When this method returns, contains the draft if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, [MaybeNullWhen(false)] out Draft result) => TryParse(document, validate: true, out result);

    /// <summary>Tries to parse a JSON document into a draft with optional validation.</summary>
    /// <param name="document">The document containing the draft.</param>
    /// <param name="validate"><see langword="true"/> to validate the draft; otherwise, <see langword="false"/>.</param>
    /// <param name="result">When this method returns, contains the draft if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, bool validate, [MaybeNullWhen(false)] out Draft result)
    {
        result = null;

        return document is not null && TryParse(document.RootElement, validate, out result);
    }

    /// <summary>Tries to parse a JSON element into a validated draft.</summary>
    /// <param name="element">The element containing the draft.</param>
    /// <param name="result">When this method returns, contains the draft if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, [MaybeNullWhen(false)] out Draft result) => TryParse(element, validate: true, out result);

    /// <summary>Tries to parse a JSON element into a draft with optional validation.</summary>
    /// <param name="element">The element containing the draft.</param>
    /// <param name="validate"><see langword="true"/> to validate the draft; otherwise, <see langword="false"/>.</param>
    /// <param name="result">When this method returns, contains the draft if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, bool validate, [MaybeNullWhen(false)] out Draft result) =>
        RecordJson.TryParse(element, validate, SourceGenerationContext.Default.Draft, DeserializeUnvalidated, out result);

    private static Draft? DeserializeUnvalidated(JsonElement element)
    {
        TolerantDraftJson.CheckType(element, "app.bsky.draft.defs#draft");
        DraftJsonValue? value = JsonSerializer.Deserialize(element, SourceGenerationContext.Default.DraftJsonValue);

        return value is null ? null : new Draft(value);
    }

    /// <summary>Validates the draft metadata, gate rules and nested posts.</summary>
    /// <param name="validationContext">The context in which validation is performed.</param>
    /// <returns>A collection of validation failures, or an empty collection when the draft is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateDraft();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidateDraft();

    private IEnumerable<ValidationResult> ValidateDraft() =>
        RecordValidation.Collection(Posts, nameof(Posts), Maximum.DraftPosts, minimumCount: 1)
            .Concat(RecordValidation.Text(DeviceId, nameof(DeviceId), Maximum.DraftDeviceIdLengthInBytes, int.MaxValue))
            .Concat(RecordValidation.Text(DeviceName, nameof(DeviceName), Maximum.DraftDeviceNameLengthInBytes, int.MaxValue))
            .Concat(RecordValidation.Collection(Langs, nameof(Langs), Maximum.DraftLangs))
            .Concat(RecordValidation.Collection(PostGateEmbeddingRules, nameof(PostGateEmbeddingRules), Maximum.DraftPostGateEmbeddingRules))
            .Concat(RecordValidation.Collection(ThreadGateAllowRules, nameof(ThreadGateAllowRules), Maximum.DraftThreadGateAllowRules));

    internal sealed record DraftJsonValue(
        [property: JsonRequired] JsonElement[]? Posts,
        string? DeviceId,
        string? DeviceName,
        IReadOnlyList<string>? Langs,
        [property: JsonPropertyName("postgateEmbeddingRules")] IReadOnlyList<PostGateRule>? PostGateEmbeddingRules,
        [property: JsonPropertyName("threadgateAllow")] IReadOnlyList<ThreadGateRule>? ThreadGateAllowRules);
}
