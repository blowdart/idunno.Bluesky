// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto
{
    internal static class StringExtensions
    {
        /// <summary>
        /// Counts the number of times the character <paramref name="c"/> appears in <paramref name="s"/>.
        /// </summary>
        /// <param name="s">The string to search.</param>
        /// <param name="c">The character to count.</param>
        /// <returns>The number of times <paramref name="c"/> appears in <paramref name="s"/>.</returns>
        public static int OccurrenceCount(this string s, char c)
        {
            return s.Length - s.Replace(c.ToString(), "", StringComparison.InvariantCulture).Length;
        }

        /// <summary>
        /// Counts the number of times the string <paramref name="c"/> appears in <paramref name="s"/>.
        /// </summary>
        /// <param name="s">The string to search.</param>
        /// <param name="c">The string to count.</param>
        /// <returns>The number of times <paramref name="c"/> appears in <paramref name="s"/>.</returns>
        public static int OccurrenceCount(this string s, string c)
        {
            return (s.Length - s.Replace(c, "", StringComparison.InvariantCulture).Length) / c.Length;
        }
    }
}
