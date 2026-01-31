// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;

namespace idunno.AtProto
{
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
    }
}
