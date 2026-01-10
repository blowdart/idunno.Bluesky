// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

namespace idunno.Bluesky
{
    internal static class InternalExtensions
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
}
