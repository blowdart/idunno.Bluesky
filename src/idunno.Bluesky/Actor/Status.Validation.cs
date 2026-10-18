// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto.Repo;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Json;

namespace idunno.Bluesky.Actor;

public partial record Status : IValidatableObject, IRecordValidatable
{
    private Status(StatusJsonValue value) : base(value.CreatedAt)
    {
        AccountStatus = value.AccountStatus ?? throw new JsonException("An account status is required.");
        _durationMinutes = value.DurationMinutes;
        Embed = value.Embed is JsonElement embed && embed.ValueKind is not JsonValueKind.Null
            ? JsonSerializer.Deserialize(embed, TolerantMediaJson.EmbeddedBaseTypeInfo)
            : null;
        ExtensionData = RecordJson.CopyExtensionData(value.ExtensionData);
    }

    /// <summary>Parses a JSON document into a validated account status.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <returns>A validated account status.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The value is not a valid account status.</exception>
    public static Status Parse(JsonDocument document) => Parse(document, validate: true);

    /// <summary>Parses a JSON document into an account status with optional validation.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the status; otherwise, <see langword="false"/>.</param>
    /// <returns>A parsed account status.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The value cannot be parsed using the selected validation mode.</exception>
    public static Status Parse(JsonDocument document, bool validate)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Parse(document.RootElement, validate);
    }

    /// <summary>Parses a JSON element into a validated account status.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <returns>A validated account status.</returns>
    /// <exception cref="JsonException">The value is not a valid account status.</exception>
    public static Status Parse(JsonElement element) => Parse(element, validate: true);

    /// <summary>Parses a JSON element into an account status with optional validation.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the status; otherwise, <see langword="false"/>.</param>
    /// <returns>A parsed account status.</returns>
    /// <exception cref="JsonException">The value cannot be parsed using the selected validation mode.</exception>
    public static Status Parse(JsonElement element, bool validate) =>
        RecordJson.Parse(element, validate, SourceGenerationContext.Default.ActorStatus, DeserializeUnvalidated);

    /// <summary>Tries to parse a JSON document into a validated account status.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <param name="result">When this method returns, contains the status if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, [MaybeNullWhen(false)] out Status result) => TryParse(document, validate: true, out result);

    /// <summary>Tries to parse a JSON document into an account status with optional validation.</summary>
    /// <param name="document">The document containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the status; otherwise, <see langword="false"/>.</param>
    /// <param name="result">When this method returns, contains the status if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, bool validate, [MaybeNullWhen(false)] out Status result)
    {
        result = null;

        return document is not null && TryParse(document.RootElement, validate, out result);
    }

    /// <summary>Tries to parse a JSON element into a validated account status.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <param name="result">When this method returns, contains the status if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, [MaybeNullWhen(false)] out Status result) => TryParse(element, validate: true, out result);

    /// <summary>Tries to parse a JSON element into an account status with optional validation.</summary>
    /// <param name="element">The element containing the record value.</param>
    /// <param name="validate"><see langword="true"/> to validate the status; otherwise, <see langword="false"/>.</param>
    /// <param name="result">When this method returns, contains the status if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if parsing succeeds; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, bool validate, [MaybeNullWhen(false)] out Status result) =>
        RecordJson.TryParse(element, validate, SourceGenerationContext.Default.ActorStatus, DeserializeUnvalidated, out result);

    private static Status? DeserializeUnvalidated(JsonElement element)
    {
        StatusJsonValue? value = RecordJson.Read(element, SourceGenerationContext.Default.StatusJsonValue);

        return value is null ? null : new Status(value);
    }

    /// <summary>Validates the required status, duration and nested external card.</summary>
    /// <param name="validationContext">The context in which validation is performed.</param>
    /// <returns>A collection of validation failures, or an empty collection when the status is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    /// <remarks><para>Unknown status strings are retained. Validation does not evaluate whether the status has expired.</para></remarks>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateStatus();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidateStatus();

    private IEnumerable<ValidationResult> ValidateStatus()
    {
        if (AccountStatus is null)
        {
            yield return new ValidationResult("An account status is required.", [nameof(AccountStatus)]);
        }

        if (DurationMinutes is < 1)
        {
            yield return new ValidationResult("The status duration must be at least one minute.", [nameof(DurationMinutes)]);
        }

        if (Embed is not null and not EmbeddedExternal and not EmbeddedExternalMedia)
        {
            yield return new ValidationResult("A status embed must be an external card.", [nameof(Embed)]);
        }

        if (Embed is IRecordValidatable validatable)
        {
            foreach (ValidationResult result in RecordValidation.Nested(validatable, nameof(Embed)))
            {
                yield return result;
            }
        }
    }

    internal sealed record StatusJsonValue : AtProtoRecord
    {
        [JsonRequired]
        [JsonPropertyName("status")]
        public string? AccountStatus { get; init; }

        [JsonRequired]
        public DateTimeOffset CreatedAt { get; init; }

        public int? DurationMinutes { get; init; }

        public JsonElement? Embed { get; init; }
    }
}
