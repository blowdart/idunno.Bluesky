// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace idunno.Bluesky.Labeler;

/// <summary>Validates bounded text shared by immutable label-service contract types.</summary>
/// <remarks>
/// <para>Validation is ordinal and preserves the exact supplied text. It rejects malformed UTF-16 rather than allowing the encoder to replace invalid sequences.</para>
/// <para>Byte limits are measured using strict UTF-8 encoding and U+0000 is rejected so persisted and audited inputs remain unambiguous.</para>
/// </remarks>
internal static class LabelInput
{
    private static readonly UTF8Encoding s_strictUtf8 = new(false, true);

    /// <summary>Validates nonblank text and its strict UTF-8 byte length.</summary>
    /// <param name="value">The text to validate.</param>
    /// <param name="parameterName">The associated parameter name.</param>
    /// <param name="maximumBytes">The maximum encoded UTF-8 byte count.</param>
    /// <returns>The unchanged validated text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank, contains U+0000, or contains invalid UTF-16 that cannot be encoded as UTF-8.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumBytes"/> is negative or the encoded value exceeds it.</exception>
    internal static string ValidateText(string value, string parameterName, int maximumBytes)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Text must not be blank.", parameterName);
        }

        if (value.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("Text must not contain U+0000.", parameterName);
        }

        int byteCount;
        try
        {
            byteCount = s_strictUtf8.GetByteCount(value);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException("Text must be valid Unicode encodable as UTF-8.", parameterName, exception);
        }

        if (byteCount > maximumBytes)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Text exceeds the maximum of {maximumBytes} UTF-8 bytes.");
        }

        return value;
    }
}
