// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;

namespace idunno.AtProto;

/// <summary>
/// Parses AT Protocol datetimes while preserving their offsets.
/// </summary>
internal static class ProtocolTimestamp
{
    // DateTimeOffset holds seven fraction digits, but the protocol permits any number.
    internal static bool TryParse(string value, out DateTimeOffset result)
    {
        result = default;

        const int secondsEnd = 19;
        if (value.Length < secondsEnd + 1 ||
            !IsDigits(value, 0, 4) || value[4] != '-' ||
            !IsDigits(value, 5, 2) || value[7] != '-' ||
            !IsDigits(value, 8, 2) || value[10] != 'T' ||
            !IsDigits(value, 11, 2) || value[13] != ':' ||
            !IsDigits(value, 14, 2) || value[16] != ':' ||
            !IsDigits(value, 17, 2))
        {
            return false;
        }

        int index = secondsEnd;
        int fractionDigits = 0;

        if (value[index] == '.')
        {
            index++;
            while (index < value.Length && char.IsAsciiDigit(value[index]))
            {
                index++;
                fractionDigits++;
            }

            if (fractionDigits == 0)
            {
                return false;
            }
        }

        string timezone = value[index..];
        bool isOffset = timezone.Length == 6 && timezone[0] is '+' or '-' && IsDigits(timezone, 1, 2) && timezone[3] == ':' && IsDigits(timezone, 4, 2);

        if ((timezone != "Z" && !isOffset) || timezone == "-00:00")
        {
            return false;
        }

        int keptDigits = Math.Min(fractionDigits, 7);
        string normalized = fractionDigits == 0
            ? string.Concat(value.AsSpan(0, secondsEnd), timezone)
            : string.Concat(value.AsSpan(0, secondsEnd + 1 + keptDigits), timezone);
        string format = fractionDigits == 0 ? "yyyy-MM-dd'T'HH:mm:ssK" : "yyyy-MM-dd'T'HH:mm:ss." + new string('f', keptDigits) + "K";

        return DateTimeOffset.TryParseExact(normalized, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }

    private static bool IsDigits(string value, int start, int length)
    {
        for (int i = start; i < start + length; i++)
        {
            if (!char.IsAsciiDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }
}
