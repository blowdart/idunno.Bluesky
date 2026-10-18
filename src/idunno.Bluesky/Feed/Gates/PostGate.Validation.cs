// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Json;

namespace idunno.Bluesky.Feed.Gates;

public partial record PostGate : IValidatableObject, IRecordValidatable
{
    private PostGate(PostGateJsonValue value)
    {
        Post = value.Post ?? throw new JsonException("A gated post is required.");
        CreatedAt = value.CreatedAt;
        Rules = value.Rules;
        DetachedEmbeddingUris = value.DetachedEmbeddingUris;
        ExtensionData = RecordJson.CopyExtensionData(value.ExtensionData);
    }

    /// <summary>Parses a JSON document into a validated post gate.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <returns>A validated post gate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The value is not a valid post gate.</exception>
    public static PostGate Parse(JsonDocument document) => Parse(document, validate: true);

    /// <summary>Parses a JSON document into a post gate with optional validation.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the post gate; otherwise, <see langword="false"/>.</param>
    /// <returns>A parsed post gate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The value cannot be parsed using the selected validation mode.</exception>
    public static PostGate Parse(JsonDocument document, bool validate)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Parse(document.RootElement, validate);
    }

    /// <summary>Parses a JSON element into a validated post gate.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <returns>A validated post gate.</returns>
    /// <exception cref="JsonException">The value is not a valid post gate.</exception>
    public static PostGate Parse(JsonElement element) => Parse(element, validate: true);

    /// <summary>Parses a JSON element into a post gate with optional validation.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the post gate; otherwise, <see langword="false"/>.</param>
    /// <returns>A parsed post gate.</returns>
    /// <exception cref="JsonException">The value cannot be parsed using the selected validation mode.</exception>
    public static PostGate Parse(JsonElement element, bool validate) =>
        RecordJson.Parse(element, validate, SourceGenerationContext.Default.PostGate, DeserializeUnvalidated);

    /// <summary>Tries to parse a JSON document into a validated post gate.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <param name="result">When this method returns, contains the post gate if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, [MaybeNullWhen(false)] out PostGate result) => TryParse(document, validate: true, out result);

    /// <summary>Tries to parse a JSON document into a post gate with optional validation.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the post gate; otherwise, <see langword="false"/>.</param>
    /// <param name="result">When this method returns, contains the post gate if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, bool validate, [MaybeNullWhen(false)] out PostGate result)
    {
        result = null;

        return document is not null && TryParse(document.RootElement, validate, out result);
    }

    /// <summary>Tries to parse a JSON element into a validated post gate.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <param name="result">When this method returns, contains the post gate if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, [MaybeNullWhen(false)] out PostGate result) => TryParse(element, validate: true, out result);

    /// <summary>Tries to parse a JSON element into a post gate with optional validation.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the post gate; otherwise, <see langword="false"/>.</param>
    /// <param name="result">When this method returns, contains the post gate if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, bool validate, [MaybeNullWhen(false)] out PostGate result) =>
        RecordJson.TryParse(element, validate, SourceGenerationContext.Default.PostGate, DeserializeUnvalidated, out result);

    private static PostGate? DeserializeUnvalidated(JsonElement element)
    {
        PostGateJsonValue? value = RecordJson.Read(element, SourceGenerationContext.Default.PostGateJsonValue);

        return value is null ? null : new PostGate(value);
    }

    /// <summary>Validates the gated post, embedding rules and detached post references.</summary>
    /// <param name="validationContext">The context in which validation is performed.</param>
    /// <returns>A collection of validation failures, or an empty collection when the post gate is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateGate();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidateGate();

    private IEnumerable<ValidationResult> ValidateGate()
    {
        if (Post is null || Post.Collection != CollectionNsid.Post || Post.RecordKey is null)
        {
            yield return new ValidationResult("The gated URI must reference a post.", [nameof(Post)]);
        }

        foreach (ValidationResult result in RecordValidation.Collection(Rules, nameof(Rules), Maximum.PostGateRules)
            .Concat(RecordValidation.Collection(DetachedEmbeddingUris, nameof(DetachedEmbeddingUris), Maximum.PostGateDetachedEmbeddingPosts)))
        {
            yield return result;
        }
    }

    internal sealed record PostGateJsonValue : AtProtoRecord
    {
        [JsonRequired]
        public AtUri? Post { get; init; }

        [JsonRequired]
        public DateTimeOffset CreatedAt { get; init; }

        [JsonPropertyName("embeddingRules")]
        public ICollection<PostGateRule>? Rules { get; init; }

        public ICollection<AtUri>? DetachedEmbeddingUris { get; init; }
    }
}
