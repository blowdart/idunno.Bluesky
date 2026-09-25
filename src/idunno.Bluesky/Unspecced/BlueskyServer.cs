// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Converts the accumulated <paramref name="queryStringBuilder"/> into a query string suffix, trimming any trailing
    /// separator and returning <see cref="string.Empty"/> when no parameters were appended.
    /// </summary>
    /// <param name="queryStringBuilder">A <see cref="StringBuilder"/> whose parameters each end with an <c>&amp;</c> separator.</param>
    /// <returns>Either <see cref="string.Empty"/> or a <c>?</c> prefixed query string.</returns>
    private static string BuildQueryString(StringBuilder queryStringBuilder)
    {
        if (queryStringBuilder.Length == 0)
        {
            return string.Empty;
        }

        if (queryStringBuilder[^1] == '&')
        {
            queryStringBuilder.Length--;
        }

        return $"?{queryStringBuilder}";
    }
}