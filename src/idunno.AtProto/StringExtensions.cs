// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

namespace idunno.AtProto;

/// <summary>
/// ATProto Helpers for strings
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Gets the length of a string in graphemes.
    /// </summary>
    /// <param name="s">The string whose length to calculate.</param>
    /// <returns>The length of a string in graphemes.</returns>
    public static int GetGraphemeLength(this string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return 0;
        }

        StringInfo stringInfo = new(s);

        return stringInfo.LengthInTextElements;
    }

    /// <summary>
    /// Gets the length of a string when encoded as UTF-8 bytes.
    /// </summary>
    /// <param name="s">The string whose length to calculate.</param>
    /// <returns>The length of a string when encoded as UTF-8 bytes.</returns>
    /// <remarks>
    /// <para>
    /// AT Protocol lexicons count their <c>maxLength</c> constraints in UTF-8 bytes, whereas <see cref="string.Length"/>
    /// counts UTF-16 code units. Use this method when validating a value against a <c>maxLength</c> limit.
    /// </para>
    /// </remarks>
    public static int GetUtf8Length(this string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return 0;
        }

        return Encoding.UTF8.GetByteCount(s);
    }
}