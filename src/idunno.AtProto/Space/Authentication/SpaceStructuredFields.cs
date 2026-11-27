// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>
/// Parses and serializes RFC 9651 dictionaries for the RFC 9421 Spaces profiles, rejecting ambiguous duplicate names.
/// </summary>
/// <param name="text">The bounded structured field to parse.</param>
internal sealed class SpaceStructuredFields(string text)
{
    private int _position;
    private static readonly UTF8Encoding s_utf8 = new(false, true);

    /// <summary>Reads a dictionary while preserving member and parameter order for canonical signature bases.</summary>
    internal static Dictionary<string, Field> Parse(string text)
    {
        if (text.Length is 0 or > 32768)
        {
            throw new InvalidDataException("The structured field is missing or exceeds the supported size.");
        }

        try
        {
            return new SpaceStructuredFields(text).ReadDictionary();
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or DecoderFallbackException)
        {
            throw new InvalidDataException("The structured field is malformed.", exception);
        }
    }

    /// <summary>Serializes a parsed dictionary in its original member order with normalized whitespace and values.</summary>
    internal static string Serialize(Dictionary<string, Field> fields) =>
        string.Join(", ", fields.Select(pair => pair.Key + (pair.Value.Value?.Canonical == "?1" && pair.Value.Items is null
            ? pair.Value.SerializeParameters()
            : "=" + pair.Value.SerializeMember())));

    /// <summary>Reads comma-separated dictionary entries and rejects trailing separators or duplicate keys.</summary>
    private Dictionary<string, Field> ReadDictionary()
    {
        Dictionary<string, Field> result = [with(StringComparer.Ordinal)];
        SkipWhitespace();
        while (_position < text.Length)
        {
            string key = ReadKey();
            Field field = Take('=') ? ReadField() : new Field(new Bare("?1"), null, ReadParameters());
            if (!result.TryAdd(key, field))
            {
                throw new InvalidDataException("A structured field contains duplicate dictionary keys.");
            }

            SkipWhitespace();
            if (_position == text.Length)
            {
                return result;
            }

            Require(',');
            SkipWhitespace();
            if (_position == text.Length)
            {
                throw new InvalidDataException("A structured field has a trailing separator.");
            }
        }

        throw new InvalidDataException("The structured field is empty.");
    }

    /// <summary>Reads either a bare item or an inner list, with its ordered parameters.</summary>
    private Field ReadField()
    {
        if (!Take('('))
        {
            return new Field(ReadBare(), null, ReadParameters());
        }

        List<Field> items = [];
        while (true)
        {
            SkipSpaces();

            if (Take(')'))
            {
                break;
            }

            items.Add(new Field(ReadBare(), null, ReadParameters()));
            if (_position >= text.Length || text[_position] is not (' ' or ')'))
            {
                throw new InvalidDataException("The structured inner list is malformed.");
            }
        }

        return new Field(null, items, ReadParameters());
    }

    /// <summary>Reads ordered parameters, treating omitted values as Boolean true.</summary>
    private Dictionary<string, Bare> ReadParameters()
    {
        Dictionary<string, Bare> result = [with(StringComparer.Ordinal)];
        while (Take(';'))
        {
            SkipSpaces();

            string name = ReadKey();
            if (!result.TryAdd(name, Take('=') ? ReadBare() : new Bare("?1")))
            {
                throw new InvalidDataException("A structured field contains duplicate parameters.");
            }
        }

        return result;
    }

    /// <summary>Reads the RFC 9651 bare item types without coercing strings, tokens or Boolean values.</summary>
    private Bare ReadBare()
    {
        if (_position >= text.Length)
        {
            throw new InvalidDataException("A structured item is missing.");
        }

        char first = text[_position];
        if (first == '"')
        {
            string value = ReadString();
            return new Bare(Canonical: Quote(value), Text: value, IsString: true);
        }

        if (Take(':'))
        {
            int start = _position;
            while (_position < text.Length && text[_position] != ':')
            {
                char character = text[_position++];
                if (!char.IsAsciiLetterOrDigit(character) && character is not ('+' or '/' or '='))
                {
                    throw new InvalidDataException("The structured byte sequence is malformed.");
                }
            }

            string encoded = text[start.._position];
            Require(':');
            byte[] bytes = Convert.FromBase64String(encoded.PadRight((encoded.Length + 3) / 4 * 4, '='));
            return new Bare(":" + Convert.ToBase64String(bytes) + ":", Bytes: bytes);
        }

        if (Take('?'))
        {
            if (_position >= text.Length || text[_position] is not ('0' or '1'))
            {
                throw new InvalidDataException("The structured Boolean value is malformed.");
            }

            return new Bare("?" + text[_position++]);
        }

        bool date = Take('@');
        if (date || first == '-' || char.IsAsciiDigit(first))
        {
            int start = _position;
            Take('-');
            int digits = _position;
            while (_position < text.Length && char.IsAsciiDigit(text[_position]))
            {
                _position++;
            }

            int count = _position - digits;
            bool fractional = Take('.');
            if (fractional)
            {
                int fractionStart = _position;
                while (_position < text.Length && char.IsAsciiDigit(text[_position]))
                {
                    _position++;
                }

                if (date || count is < 1 or > 12 || _position - fractionStart is < 1 or > 3)
                {
                    throw new InvalidDataException("The structured decimal value is malformed.");
                }

                decimal value = decimal.Parse(text[start.._position], CultureInfo.InvariantCulture);
                return new Bare(value.ToString("0.0##", CultureInfo.InvariantCulture));
            }

            if (count is < 1 or > 15)
            {
                throw new InvalidDataException("The structured integer value is malformed.");
            }

            long integer = long.Parse(text[start.._position], CultureInfo.InvariantCulture);
            return new Bare((date ? "@" : "") + integer.ToString(CultureInfo.InvariantCulture));
        }

        if (Take('%'))
        {
            Require('"');
            List<byte> bytes = [];
            while (_position < text.Length && text[_position] != '"')
            {
                char character = text[_position++];
                if (character == '%')
                {
                    if (_position + 2 > text.Length)
                    {
                        throw new InvalidDataException("The display string is malformed.");
                    }

                    bytes.Add(byte.Parse(text.AsSpan(_position, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    _position += 2;
                }
                else if (character is >= ' ' and <= '~')
                {
                    bytes.Add((byte)character);
                }
                else
                {
                    throw new InvalidDataException("The display string is malformed.");
                }
            }

            Require('"');
            _ = s_utf8.GetString([.. bytes]);
            StringBuilder canonical = new("%\"");
            foreach (byte value in bytes)
            {
                if (value is >= 0x20 and <= 0x7e && value is not (0x22 or 0x25))
                {
                    canonical.Append((char)value);
                }
                else
                {
                    canonical.Append('%').Append(value.ToString("x2", CultureInfo.InvariantCulture));
                }
            }

            return new Bare(canonical.Append('"').ToString());
        }

        if (!char.IsAsciiLetter(first) && first != '*')
        {
            throw new InvalidDataException("The structured token is malformed.");
        }

        int tokenStart = _position++;
        while (_position < text.Length &&
            (char.IsAsciiLetterOrDigit(text[_position]) || "!#$%&'*+-.^_`|~:/".Contains(text[_position], StringComparison.Ordinal)))
        {
            _position++;
        }

        return new Bare(text[tokenStart.._position]);
    }

    /// <summary>Reads an ASCII string and its two permitted escape sequences.</summary>
    private string ReadString()
    {
        Require('"');
        StringBuilder value = new();
        while (_position < text.Length)
        {
            char character = text[_position++];
            if (character == '"')
            {
                return value.ToString();
            }

            if (character == '\\')
            {
                if (_position >= text.Length || text[_position] is not ('\\' or '"'))
                {
                    throw new InvalidDataException("The structured string has an invalid escape.");
                }

                character = text[_position++];
            }
            else if (character is < ' ' or > '~')
            {
                throw new InvalidDataException("The structured string is not printable ASCII.");
            }

            value.Append(character);
        }

        throw new InvalidDataException("The structured string is unterminated.");
    }

    /// <summary>Reads a lowercase dictionary or parameter key.</summary>
    private string ReadKey()
    {
        if (_position >= text.Length || text[_position] is not (>= 'a' and <= 'z' or '*'))
        {
            throw new InvalidDataException("The structured field key is malformed.");
        }

        int start = _position++;
        while (_position < text.Length &&
            (text[_position] is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-' or '.' or '*'))
        {
            _position++;
        }

        return text[start.._position];
    }

    /// <summary>Consumes optional HTTP field whitespace between dictionary members.</summary>
    private void SkipWhitespace()
    {
        while (_position < text.Length && text[_position] is ' ' or '\t')
        {
            _position++;
        }
    }

    /// <summary>Consumes the spaces allowed inside inner lists and after parameter separators.</summary>
    private void SkipSpaces()
    {
        while (_position < text.Length && text[_position] == ' ')
        {
            _position++;
        }
    }

    /// <summary>Consumes the expected delimiter if present.</summary>
    private bool Take(char character)
    {
        if (_position < text.Length && text[_position] == character)
        {
            _position++;
            return true;
        }

        return false;
    }

    /// <summary>Requires a delimiter, failing closed on an incomplete field.</summary>
    private void Require(char character)
    {
        if (!Take(character))
        {
            throw new InvalidDataException("The structured field is malformed.");
        }
    }

    /// <summary>Serializes a structured ASCII string.</summary>
    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    /// <summary>Retains a bare item's canonical serialization and its typed string or byte value.</summary>
    /// <param name="Canonical">The normalized serialization.</param>
    /// <param name="Text">The decoded string, token or display-string value.</param>
    /// <param name="Bytes">The byte value, only for structured byte sequences.</param>
    /// <param name="IsString">A value indicating whether this is an ASCII structured string, not a token or display string.</param>
    internal sealed record Bare(string Canonical, string? Text = null, byte[]? Bytes = null, bool IsString = false);

    /// <summary>Retains an item or inner list and its ordered parameters.</summary>
    /// <param name="value">The bare value, when this is an item.</param>
    /// <param name="items">The inner list's items, when this is a list.</param>
    /// <param name="parameters">The ordered parameters.</param>
    internal sealed class Field(Bare? value, List<Field>? items, Dictionary<string, Bare> parameters)
    {
        /// <summary>Gets the bare value for an item.</summary>
        internal Bare? Value { get; } = value;

        /// <summary>Gets the items for an inner list.</summary>
        internal List<Field>? Items { get; } = items;

        /// <summary>Gets the ordered parameters.</summary>
        internal Dictionary<string, Bare> Parameters { get; } = parameters;

        /// <summary>Serializes the item or inner list with all parameters.</summary>
        internal string SerializeMember() =>
            (Items is null ? Value!.Canonical : "(" + string.Join(" ", Items.Select(item => item.SerializeMember())) + ")") + SerializeParameters();

        /// <summary>Serializes parameters while omitting explicit Boolean-true values.</summary>
        internal string SerializeParameters() =>
            string.Concat(Parameters.Select(pair => ";" + pair.Key + (pair.Value.Canonical == "?1" ? "" : "=" + pair.Value.Canonical)));
    }
}
