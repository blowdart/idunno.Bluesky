// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace idunno.Bluesky;

internal static class InternalExtensions
{
    /// <summary>
    /// Gets the UTF8 index position of <paramref name="index"/> in <paramref name="s"/>.
    /// </summary>
    /// <param name="s">The string in which the <paramref name="index"/> is based.</param>
    /// <param name="index">The character based index in <paramref name="s"/></param>
    /// <returns></returns>
    public static int GetUtf8BytePosition(this string s, int index)
    {
        string substring = s[..index];

        return Encoding.UTF8.GetByteCount(substring);
    }
}