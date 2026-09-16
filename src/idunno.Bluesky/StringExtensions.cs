// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace idunno.Bluesky;

/// <summary>
/// Common, reusable extension methods for <see cref="string"/>.
/// </summary>
public static class StringExtensions
{
    static readonly Encoding s_utf8 = Encoding.UTF8;

    /// <summary>
    /// Gets the length of the string if it was encoded as a UTF8 byte array.
    /// </summary>
    /// <param name="s">The string whose utf8 length to calculate.</param>
    /// <returns>The length of the string if it was encoded as a UTF8 byte array.</returns>
    public static int GetUtf8Length(this string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return 0;
        }

        return s_utf8.GetByteCount(s);
    }
}