// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto
{
    internal static class InternalStringExtensions
    {
        /// <summary>
        /// Counts the number of times the character <paramref name="c"/> appears in <paramref name="s"/>.
        /// </summary>
        /// <param name="s">The string to search.</param>
        /// <param name="c">The character to count.</param>
        /// <returns>The number of times <paramref name="c"/> appears in <paramref name="s"/>.</returns>
        public static int OccurrenceCount(this string s, char c)
        {
            if (string.IsNullOrEmpty(s))
            {
                return 0;
            }

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
            if (string.IsNullOrEmpty(s))
            {
                return 0;
            }

            ArgumentNullException.ThrowIfNull(c);

            return (s.Length - s.Replace(c, "", StringComparison.InvariantCulture).Length) / c.Length;
        }

        /// <summary>
        /// Gets a flag indicating whether the string only consists of ASCII letters.
        /// </summary>
        /// <param name="s">The string to check.</param>
        /// <returns>True if the string only consists of ASCII letters, otherwise false.</returns>
        public static bool IsOnlyAsciiLetters(this string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return true;
            }

            foreach (char item in s)
            {
                if (item is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
                {
                    continue;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets a flag indicating whether the string only consists of ASCII letters and numbers.
        /// </summary>
        /// <param name="s">The string to check.</param>
        /// <returns>True if the string only consists of ASCII letters, otherwise false.</returns>
        public static bool IsOnlyAsciiLettersAndNumbers(this string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return true;
            }

            foreach (char item in s)
            {
                if (item is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9')
                {
                    continue;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }
    }
}
