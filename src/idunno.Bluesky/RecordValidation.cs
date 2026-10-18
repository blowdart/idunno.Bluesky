// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;

using idunno.AtProto;
using idunno.Bluesky.Feed.Gates;

namespace idunno.Bluesky;

internal static class RecordValidation
{
    internal static IEnumerable<ValidationResult> Collection<T>(
        IEnumerable<T>? values, string memberName, int maximumCount, int minimumCount = 0)
    {
        if (values is null)
        {
            if (minimumCount > 0)
            {
                yield return new ValidationResult("A collection is required.", [memberName]);
            }

            yield break;
        }

        int index = 0;
        foreach (T value in values)
        {
            string path = $"{memberName}[{index}]";
            if (value is null)
            {
                yield return new ValidationResult("An entry cannot be null.", [path]);
            }
            else if (value is IRecordValidatable validatable)
            {
                foreach (ValidationResult result in Nested(validatable, path))
                {
                    yield return result;
                }
            }
            else if (value is ListRule { List: null })
            {
                yield return new ValidationResult("A list reference is required.", [$"{path}.{nameof(ListRule.List)}"]);
            }

            index++;
        }

        if (index < minimumCount || index > maximumCount)
        {
            yield return new ValidationResult(
                $"The collection must contain between {minimumCount} and {maximumCount} entries.", [memberName]);
        }
    }

    internal static IEnumerable<ValidationResult> Nested(IRecordValidatable? value, string memberName, bool required = true)
    {
        if (value is null)
        {
            if (required)
            {
                yield return new ValidationResult("A value is required.", [memberName]);
            }

            yield break;
        }

        foreach (ValidationResult result in value.ValidateRecord())
        {
            string[] members = [.. result.MemberNames.Select(member => $"{memberName}.{member}")];
            yield return new ValidationResult(result.ErrorMessage, members.Length == 0 ? [memberName] : members);
        }
    }

    internal static IEnumerable<ValidationResult> Blob(Blob? blob, string memberName, int maximumSize, params string[] acceptedTypes)
    {
        if (blob is null)
        {
            yield return new ValidationResult("A blob is required.", [memberName]);
            yield break;
        }

        if (blob.Reference is null || blob.Reference.Link is null)
        {
            yield return new ValidationResult("A blob reference is required.", [$"{memberName}.{nameof(blob.Reference)}"]);
        }

        if (blob.Size < 0 || blob.Size > maximumSize)
        {
            yield return new ValidationResult(
                $"The blob size must be between 0 and {maximumSize} bytes.",
                [$"{memberName}.{nameof(blob.Size)}"]);
        }

        bool accepted = acceptedTypes.Any(type => type.EndsWith("/*", StringComparison.Ordinal)
            ? blob.MimeType is not null && blob.MimeType.StartsWith(type[..^1], StringComparison.OrdinalIgnoreCase) &&
                blob.MimeType.Length > type.Length - 1
            : string.Equals(type, blob.MimeType, StringComparison.OrdinalIgnoreCase));
        if (!accepted)
        {
            yield return new ValidationResult("The blob MIME type is not supported.", [$"{memberName}.{nameof(blob.MimeType)}"]);
        }
    }

    internal static IEnumerable<ValidationResult> Text(string? value, string memberName, int maximumBytes, int maximumGraphemes)
    {
        if (value is not null && (value.GetUtf8Length() > maximumBytes || value.GetGraphemeLength() > maximumGraphemes))
        {
            yield return new ValidationResult(
                $"Text must not exceed {maximumBytes} UTF-8 bytes or {maximumGraphemes} graphemes.",
                [memberName]);
        }
    }
}
