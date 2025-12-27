// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace idunno.AtProto
{
    internal static class SortableBase32Encoding
    {
        private const string Base32Alphabet = "234567abcdefghijklmnopqrstuvwxyz";

        public static string ToString(double i)
        {
            StringBuilder result = new ();

            while (i > 0)
            {
                int offset = (int)(i % 32);
                i = Math.Floor(i / 32);
                result.Insert(0, Base32Alphabet[offset]);
            }

            return result.ToString();
        }

        public static string ToString(long input)
        {
            return ToString((double)input);
        }

        public static double FromString(string s)
        {
            double i = 0;

            foreach (char c in s)
            {
                int offset = Base32Alphabet.IndexOf(c, StringComparison.InvariantCultureIgnoreCase);

                if (offset == -1)
                {
                    throw new ArgumentException("not a value base32 string", nameof(s));
                }

                i = i * 32 + offset;
            }

            return i;
        }
    }
}
