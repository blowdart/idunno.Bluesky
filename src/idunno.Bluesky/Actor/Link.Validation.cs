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

namespace idunno.Bluesky.Actor;

public partial record Link : IValidatableObject, IRecordValidatable
{
    private Link(LinkJsonValue value) : base(value.CreatedAt)
    {
        Url = value.Url ?? throw new JsonException("A profile link destination is required.");
        _title = value.Title;
        _icon = value.Icon;
        Labels = value.Labels;
        ExtensionData = RecordJson.CopyExtensionData(value.ExtensionData);
    }

    /// <summary>Parses a JSON document into a validated profile link.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <returns>A validated profile link.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The value is not a valid profile link.</exception>
    public static Link Parse(JsonDocument document) => Parse(document, validate: true);

    /// <summary>Parses a JSON document into a profile link with optional validation.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the link; otherwise, <see langword="false"/>.</param>
    /// <returns>A parsed profile link.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The value cannot be parsed using the selected validation mode.</exception>
    public static Link Parse(JsonDocument document, bool validate)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Parse(document.RootElement, validate);
    }

    /// <summary>Parses a JSON element into a validated profile link.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <returns>A validated profile link.</returns>
    /// <exception cref="JsonException">The value is not a valid profile link.</exception>
    public static Link Parse(JsonElement element) => Parse(element, validate: true);

    /// <summary>Parses a JSON element into a profile link with optional validation.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the link; otherwise, <see langword="false"/>.</param>
    /// <returns>A parsed profile link.</returns>
    /// <exception cref="JsonException">The value cannot be parsed using the selected validation mode.</exception>
    /// <remarks><para>Blob metadata is untrusted. Validation does not inspect the icon file.</para></remarks>
    public static Link Parse(JsonElement element, bool validate) =>
        RecordJson.Parse(element, validate, SourceGenerationContext.Default.ActorLink, DeserializeUnvalidated);

    /// <summary>Tries to parse a JSON document into a validated profile link.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <param name="result">When this method returns, contains the link if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, [MaybeNullWhen(false)] out Link result) => TryParse(document, validate: true, out result);

    /// <summary>Tries to parse a JSON document into a profile link with optional validation.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the link; otherwise, <see langword="false"/>.</param>
    /// <param name="result">When this method returns, contains the link if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, bool validate, [MaybeNullWhen(false)] out Link result)
    {
        result = null;

        return document is not null && TryParse(document.RootElement, validate, out result);
    }

    /// <summary>Tries to parse a JSON element into a validated profile link.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <param name="result">When this method returns, contains the link if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, [MaybeNullWhen(false)] out Link result) => TryParse(element, validate: true, out result);

    /// <summary>Tries to parse a JSON element into a profile link with optional validation.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the link; otherwise, <see langword="false"/>.</param>
    /// <param name="result">When this method returns, contains the link if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, bool validate, [MaybeNullWhen(false)] out Link result) =>
        RecordJson.TryParse(element, validate, SourceGenerationContext.Default.ActorLink, DeserializeUnvalidated, out result);

    private static Link? DeserializeUnvalidated(JsonElement element)
    {
        LinkJsonValue? value = JsonSerializer.Deserialize(element, SourceGenerationContext.Default.LinkJsonValue);

        return value is null ? null : new Link(value);
    }

    /// <summary>Validates the profile link destination, title and icon metadata.</summary>
    /// <param name="validationContext">The context in which validation is performed.</param>
    /// <returns>A collection of validation failures, or an empty collection when the link is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    /// <remarks><para>Blob metadata is untrusted. Validation does not inspect the icon file or access the destination.</para></remarks>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateLink();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidateLink();

    private IEnumerable<ValidationResult> ValidateLink()
    {
        if (Url is null || !Url.IsAbsoluteUri || Url.Scheme != Uri.UriSchemeHttps)
        {
            yield return new ValidationResult("The link destination must be an absolute HTTPS URI.", [nameof(Url)]);
        }

        foreach (ValidationResult result in RecordValidation.Text(Title, nameof(Title), 320, 40))
        {
            yield return result;
        }

        if (Icon is not null)
        {
            foreach (ValidationResult result in RecordValidation.Blob(Icon, nameof(Icon), 100_000, "image/png", "image/jpeg", "image/webp"))
            {
                yield return result;
            }
        }
    }

    internal sealed record LinkJsonValue : AtProtoRecord
    {
        [JsonRequired]
        public Uri? Url { get; init; }

        [JsonRequired]
        public DateTimeOffset CreatedAt { get; init; }

        public string? Title { get; init; }

        public Blob? Icon { get; init; }

        public SelfLabels? Labels { get; init; }
    }
}
