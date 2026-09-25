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
    /// <returns>The UTF8 index position of <paramref name="index"/> in <paramref name="s"/>.</returns>
    public static int GetUtf8BytePosition(this string s, int index)
    {
        // Counted over a span rather than a substring, as this is called twice for every facet extracted
        // from a post, and a substring would copy the text leading up to each facet on every call.
        return Encoding.UTF8.GetByteCount(s.AsSpan(0, index));
    }
}