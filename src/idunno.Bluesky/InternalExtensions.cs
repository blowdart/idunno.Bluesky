// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Globalization;
using System.Reflection;
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
        public static long GetUtf8Length(this string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return 0;
            }

            return s_utf8.GetByteCount(s);
        }

        /// <summary>
        /// Gets the length of a string in graphemes.
        /// </summary>
        /// <param name="s">The string whose length to calculate.</param>
        /// <returns></returns>
        public static int GetLengthInGraphemes(this string s)
        {
            StringInfo stringInfo = new(s);

            return stringInfo.LengthInTextElements;
        }

        /// <summary>
        /// Gets the description attribute for an enum value.
        /// </summary>
        /// <param name="enumValue">The value whose description to return.</param>
        /// <returns>The contents of the description attribute applied to the enum value, otherwise the string representation of the enum value.</returns>
        public static string GetDescription(this Enum enumValue)
        {
            FieldInfo? field = enumValue.GetType().GetField(enumValue.ToString());
            if (field is null)
            {
                return enumValue.ToString();
            }

            if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attribute)
            {
                return attribute.Description;
            }

            return enumValue.ToString();
        }
    }
}
