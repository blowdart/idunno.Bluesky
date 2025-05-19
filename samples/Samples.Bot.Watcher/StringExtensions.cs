// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace Samples.Bot.Watcher
{
    internal static class StringExtensions
    {
        public static bool ContainsAny(this string input, IEnumerable<string> values, StringComparison comparisonType = StringComparison.InvariantCulture)
        {
            if (string.IsNullOrEmpty(input))
            {
                return false;
            }

            return values.Any(keyword => input.Contains(keyword, comparisonType));
        }
    }
}
