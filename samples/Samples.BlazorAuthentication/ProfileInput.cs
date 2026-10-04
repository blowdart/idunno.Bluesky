// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;

using idunno.AtProto;
using idunno.Bluesky;

namespace Samples.BlazorAuthentication;

/// <summary>
/// Validates profile edits before applying them to a repository record.
/// </summary>
public sealed class ProfileInput : IValidatableObject
{
    /// <summary>
    /// Gets or sets the profile version loaded by the editor.
    /// </summary>
    [Required]
    public string Cid { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the profile description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the profile pronouns.
    /// </summary>
    public string Pronouns { get; set; } = string.Empty;

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!idunno.AtProto.Cid.TryParse(Cid, out _))
        {
            yield return new ValidationResult("The profile version is invalid. Reload the editor.", [nameof(Cid)]);
        }

        (string Name, string Value, int Graphemes, int Bytes)[] fields =
        [
            (nameof(DisplayName), DisplayName, Maximum.DisplayNameLengthInGraphemes, Maximum.DisplayNameLengthInBytes),
            (nameof(Description), Description, Maximum.DescriptionLengthInGraphemes, Maximum.DescriptionLengthInBytes),
            (nameof(Pronouns), Pronouns, Maximum.PronounLengthInGraphemes, Maximum.PronounLengthInBytes)
        ];

        foreach (var field in fields)
        {
            if (field.Value.GetGraphemeLength() > field.Graphemes || field.Value.GetUtf8Length() > field.Bytes)
            {
                yield return new ValidationResult(
                    $"{field.Name} must not exceed {field.Graphemes} graphemes or {field.Bytes} UTF-8 bytes.", [field.Name]);
            }
        }
    }
}
