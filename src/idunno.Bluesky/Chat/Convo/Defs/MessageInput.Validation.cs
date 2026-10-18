// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.Bluesky.Embed;
using idunno.Bluesky.Json;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public sealed partial record MessageInput : IValidatableObject, IRecordValidatable
{
    private MessageInput(MessageInputJsonValue value)
    {
        _text = value.Text ?? throw new JsonException("Message text is required.");
        _facets = value.Facets is null ? null : Array.AsReadOnly(value.Facets.Select(Post.DeserializeUnvalidatedFacet).ToArray());
        Embed = value.Embed is JsonElement embed && embed.ValueKind is not JsonValueKind.Null
            ? JsonSerializer.Deserialize(embed, TolerantMediaJson.EmbeddedBaseTypeInfo)
            : null;
        ReplyTo = value.ReplyTo;
    }

    /// <summary>Parses a JSON document into a validated message input.</summary>
    /// <param name="document">The document containing the message input.</param>
    /// <returns>A validated message input.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The value is not a valid message input.</exception>
    public static MessageInput Parse(JsonDocument document) => Parse(document, validate: true);

    /// <summary>Parses a JSON document into a message input with optional validation.</summary>
    /// <param name="document">The document containing the message input.</param>
    /// <param name="validate"><see langword="true"/> to validate the message input; otherwise, <see langword="false"/>.</param>
    /// <returns>A parsed message input.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The value cannot be parsed using the selected validation mode.</exception>
    public static MessageInput Parse(JsonDocument document, bool validate)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Parse(document.RootElement, validate);
    }

    /// <summary>Parses a JSON element into a validated message input.</summary>
    /// <param name="element">The element containing the message input.</param>
    /// <returns>A validated message input.</returns>
    /// <exception cref="JsonException">The value is not a valid message input.</exception>
    public static MessageInput Parse(JsonElement element) => Parse(element, validate: true);

    /// <summary>Parses a JSON element into a message input with optional validation.</summary>
    /// <param name="element">The element containing the message input.</param>
    /// <param name="validate"><see langword="true"/> to validate the message input; otherwise, <see langword="false"/>.</param>
    /// <returns>A parsed message input.</returns>
    /// <exception cref="JsonException">The value cannot be parsed using the selected validation mode.</exception>
    public static MessageInput Parse(JsonElement element, bool validate) =>
        RecordJson.Parse(element, validate, SourceGenerationContext.Default.MessageInput, DeserializeUnvalidated);

    /// <summary>Tries to parse a JSON document into a validated message input.</summary>
    /// <param name="document">The document containing the message input.</param>
    /// <param name="result">When this method returns, contains the message input if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, [MaybeNullWhen(false)] out MessageInput result) => TryParse(document, validate: true, out result);

    /// <summary>Tries to parse a JSON document into a message input with optional validation.</summary>
    /// <param name="document">The document containing the message input.</param>
    /// <param name="validate"><see langword="true"/> to validate the message input; otherwise, <see langword="false"/>.</param>
    /// <param name="result">When this method returns, contains the message input if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, bool validate, [MaybeNullWhen(false)] out MessageInput result)
    {
        result = null;

        return document is not null && TryParse(document.RootElement, validate, out result);
    }

    /// <summary>Tries to parse a JSON element into a validated message input.</summary>
    /// <param name="element">The element containing the message input.</param>
    /// <param name="result">When this method returns, contains the message input if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, [MaybeNullWhen(false)] out MessageInput result) => TryParse(element, validate: true, out result);

    /// <summary>Tries to parse a JSON element into a message input with optional validation.</summary>
    /// <param name="element">The element containing the message input.</param>
    /// <param name="validate"><see langword="true"/> to validate the message input; otherwise, <see langword="false"/>.</param>
    /// <param name="result">When this method returns, contains the message input if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, bool validate, [MaybeNullWhen(false)] out MessageInput result) =>
        RecordJson.TryParse(element, validate, SourceGenerationContext.Default.MessageInput, DeserializeUnvalidated, out result);

    private static MessageInput? DeserializeUnvalidated(JsonElement element)
    {
        MessageInputJsonValue? value = RecordJson.Read(element, SourceGenerationContext.Default.MessageInputJsonValue);

        return value is null ? null : new MessageInput(value);
    }

    /// <summary>Validates message text, facets, embeds and the reply reference.</summary>
    /// <param name="validationContext">The context in which validation is performed.</param>
    /// <returns>A collection of validation failures, or an empty collection when the message input is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateMessage();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidateMessage();

    private IEnumerable<ValidationResult> ValidateMessage()
    {
        if (Text is null)
        {
            yield return new ValidationResult("Message text is required.", [nameof(Text)]);
        }

        foreach (ValidationResult result in RecordValidation.Text(Text, nameof(Text), Maximum.MessageLengthInBytes, Maximum.MessageLengthInGraphemes)
            .Concat(RecordValidation.Collection(Facets, nameof(Facets), int.MaxValue)))
        {
            yield return result;
        }

        if (Embed is IRecordValidatable validatable)
        {
            foreach (ValidationResult result in RecordValidation.Nested(validatable, nameof(Embed)))
            {
                yield return result;
            }
        }

        if (Embed is not null and not EmbeddedRecord and not Chat.Embed.JoinLink)
        {
            yield return new ValidationResult("A chat message embed must be a record reference or join link.", [nameof(Embed)]);
        }

        if (ReplyTo is { MessageId: null })
        {
            yield return new ValidationResult("A reply message ID is required.", [$"{nameof(ReplyTo)}.{nameof(ReplyReference.MessageId)}"]);
        }
    }

    internal sealed record MessageInputJsonValue(
        [property: JsonRequired] string? Text,
        JsonElement[]? Facets,
        JsonElement? Embed,
        ReplyReference? ReplyTo);
}
