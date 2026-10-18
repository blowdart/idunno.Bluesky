// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Actor;

public partial record Profile : IValidatableObject, IRecordValidatable
{
    private Profile(ProfileJsonValue value)
        : this(null, null, null, value.Website, value.Avatar, value.Banner,
            value.JoinedViaStarterPack, value.PinnedPost, value.Labels, value.CreatedAt)
    {
        _displayName = value.DisplayName;
        _description = value.Description;
        _pronouns = value.Pronouns;
        _links = value.Links is null ? null : Array.AsReadOnly(value.Links.ToArray());
        ExtensionData = value.ExtensionData
            .Where(property => property.Key != "$type")
            .ToDictionary(property => property.Key, property => property.Value.Clone());
    }

    /// <summary>
    /// Parses a JSON document into a validated profile.
    /// </summary>
    /// <param name="document">The JSON document to parse.</param>
    /// <returns>A profile deserialized from the document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The document does not contain a valid profile.</exception>
    public static Profile Parse(JsonDocument document)
    {
        return Parse(document, validate: true);
    }

    /// <summary>
    /// Parses a JSON document into a profile with optional validation.
    /// </summary>
    /// <param name="document">The JSON document to parse.</param>
    /// <param name="validate"><see langword="true"/> to reject profiles that fail validation; otherwise, <see langword="false"/>.</param>
    /// <returns>A profile deserialized from the document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="JsonException">The document cannot be parsed using the selected validation mode.</exception>
    /// <remarks>
    /// <para>Disabling validation preserves over-limit text and links for subsequent validation.
    /// Malformed JSON values and invalid protocol identifiers are still rejected. Public constructors,
    /// property setters and ordinary JSON deserialization retain their existing validation.</para>
    /// </remarks>
    public static Profile Parse(JsonDocument document, bool validate)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Parse(document.RootElement, validate);
    }

    /// <summary>
    /// Parses a JSON element into a validated profile.
    /// </summary>
    /// <param name="element">The JSON element to parse.</param>
    /// <returns>A profile deserialized from the element.</returns>
    /// <exception cref="JsonException">The element does not contain a valid profile.</exception>
    public static Profile Parse(JsonElement element)
    {
        return Parse(element, validate: true);
    }

    /// <summary>
    /// Parses a JSON element into a profile with optional validation.
    /// </summary>
    /// <param name="element">The JSON element to parse.</param>
    /// <param name="validate"><see langword="true"/> to reject profiles that fail validation; otherwise, <see langword="false"/>.</param>
    /// <returns>A profile deserialized from the element.</returns>
    /// <exception cref="JsonException">The element cannot be parsed using the selected validation mode.</exception>
    /// <remarks>
    /// <para>Disabling validation preserves over-limit text and links for subsequent validation.
    /// Malformed JSON values and invalid protocol identifiers are still rejected.</para>
    /// </remarks>
    public static Profile Parse(JsonElement element, bool validate)
    {
        if (TryParse(element, validate, out Profile? result))
        {
            return result;
        }

        throw new JsonException("The JSON value does not contain a profile that passes the selected validation mode.");
    }

    /// <summary>
    /// Tries to parse a JSON document into a validated profile.
    /// </summary>
    /// <param name="document">The JSON document to parse.</param>
    /// <param name="result">When this method returns, contains the profile if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if the document contains a valid profile; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, [MaybeNullWhen(false)] out Profile result)
    {
        return TryParse(document, validate: true, out result);
    }

    /// <summary>
    /// Tries to parse a JSON document into a profile with optional validation.
    /// </summary>
    /// <param name="document">The JSON document to parse.</param>
    /// <param name="validate"><see langword="true"/> to reject profiles that fail validation; otherwise, <see langword="false"/>.</param>
    /// <param name="result">When this method returns, contains the profile if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if the document can be parsed using the selected validation mode; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonDocument? document, bool validate, [MaybeNullWhen(false)] out Profile result)
    {
        result = null;

        return document is not null && TryParse(document.RootElement, validate, out result);
    }

    /// <summary>
    /// Tries to parse a JSON element into a validated profile.
    /// </summary>
    /// <param name="element">The JSON element to parse.</param>
    /// <param name="result">When this method returns, contains the profile if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if the element contains a valid profile; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, [MaybeNullWhen(false)] out Profile result)
    {
        return TryParse(element, validate: true, out result);
    }

    /// <summary>
    /// Tries to parse a JSON element into a profile with optional validation.
    /// </summary>
    /// <param name="element">The JSON element to parse.</param>
    /// <param name="validate"><see langword="true"/> to reject profiles that fail validation; otherwise, <see langword="false"/>.</param>
    /// <param name="result">When this method returns, contains the profile if successful; otherwise, <see langword="null"/>. This parameter is treated as uninitialized.</param>
    /// <returns><see langword="true"/> if the element can be parsed using the selected validation mode; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(JsonElement element, bool validate, [MaybeNullWhen(false)] out Profile result)
    {
        result = null;
        if (element.ValueKind is JsonValueKind.Undefined)
        {
            return false;
        }

        try
        {
            result = validate
                ? JsonSerializer.Deserialize(element, SourceGenerationContext.Default.ActorProfile)
                : DeserializeUnvalidated(element);
            if (result is null || (validate && result.ValidateProfile().Any()))
            {
                result = null;
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            result = null;
            return false;
        }
        catch (ArgumentException)
        {
            result = null;
            return false;
        }
    }

    private static Profile? DeserializeUnvalidated(JsonElement element)
    {
        ProfileJsonValue? value = JsonSerializer.Deserialize(element, SourceGenerationContext.Default.ProfileJsonValue);

        return value is null ? null : new Profile(value);
    }

    /// <summary>
    /// Validates profile text, links, website and image blobs.
    /// </summary>
    /// <param name="validationContext">The context in which the validation is performed.</param>
    /// <returns>A collection of validation errors, or an empty collection when the profile is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateProfile();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidateProfile();

    private IEnumerable<ValidationResult> ValidateProfile()
    {
        foreach (ValidationResult result in RecordValidation.Text(DisplayName, nameof(DisplayName), Maximum.DisplayNameLengthInBytes, Maximum.DisplayNameLengthInGraphemes)
            .Concat(RecordValidation.Text(Description, nameof(Description), Maximum.DescriptionLengthInBytes, Maximum.DescriptionLengthInGraphemes))
            .Concat(RecordValidation.Text(Pronouns, nameof(Pronouns), Maximum.PronounLengthInBytes, Maximum.PronounLengthInGraphemes)))
        {
            yield return result;
        }

        if (Website is not null && !Website.IsAbsoluteUri)
        {
            yield return new ValidationResult("The website must be an absolute URI.", [nameof(Website)]);
        }

        if (Links is not null)
        {
            if (Links.Count > 10)
            {
                yield return new ValidationResult("A profile cannot contain more than ten links.", [nameof(Links)]);
            }

            int index = 0;
            foreach (StrongReference? link in Links)
            {
                if (link is null)
                {
                    yield return new ValidationResult("A profile link cannot be null.", [$"{nameof(Links)}[{index}]"]);
                }

                index++;
            }
        }

        if (Avatar is not null)
        {
            foreach (ValidationResult result in RecordValidation.Blob(Avatar, nameof(Avatar), 1_000_000, "image/png", "image/jpeg"))
            {
                yield return result;
            }
        }

        if (Banner is not null)
        {
            foreach (ValidationResult result in RecordValidation.Blob(Banner, nameof(Banner), 1_000_000, "image/png", "image/jpeg"))
            {
                yield return result;
            }
        }
    }

    internal sealed class ProfileJsonValue
    {
        public string? DisplayName { get; init; }

        public string? Description { get; init; }

        public string? Pronouns { get; init; }

        public Uri? Website { get; init; }

        public Blob? Avatar { get; init; }

        public Blob? Banner { get; init; }

        public StrongReference? JoinedViaStarterPack { get; init; }

        public StrongReference? PinnedPost { get; init; }

        public SelfLabels? Labels { get; init; }

        public DateTimeOffset? CreatedAt { get; init; }

        public IReadOnlyCollection<StrongReference>? Links { get; init; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; } = [];
    }
}
