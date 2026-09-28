// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Formats.Cbor;
using System.Globalization;

namespace idunno.AtProto.Firehose;

/// <summary>
/// The fields of a DAG-CBOR map, with typed accessors which throw <see cref="InvalidDataException"/> for missing or mistyped values.
/// </summary>
/// <param name="fields">The encoded value of each field, by name.</param>
internal sealed class CborFields(Dictionary<string, ReadOnlyMemory<byte>> fields)
{
    // The single byte encoding of a CBOR null.
    private const byte NullByte = 0xF6;

    /// <summary>
    /// Gets the number of fields in the map.
    /// </summary>
    public int Count => fields.Count;

    /// <summary>
    /// Gets the names of the fields in the map.
    /// </summary>
    public IEnumerable<string> Names => fields.Keys;

    /// <summary>
    /// Gets a value that indicates whether the map contains a field called <paramref name="name"/>, even if its value is null.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <returns><see langword="true"/> if the field is present; otherwise, <see langword="false"/>.</returns>
    public bool Contains(string name) => fields.ContainsKey(name);

    /// <summary>
    /// Gets a value that indicates whether the field called <paramref name="name"/> is absent or null.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <returns><see langword="true"/> if the field is absent or null; otherwise, <see langword="false"/>.</returns>
    public bool IsAbsentOrNull(string name) => !fields.TryGetValue(name, out ReadOnlyMemory<byte> value) || IsNull(value);

    /// <summary>
    /// Gets the encoded value of the field called <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <returns>The encoded value.</returns>
    /// <exception cref="InvalidDataException">The field is absent.</exception>
    public ReadOnlyMemory<byte> GetEncoded(string name) =>
        fields.TryGetValue(name, out ReadOnlyMemory<byte> value) ? value : throw new InvalidDataException($"The required '{name}' field is missing.");

    /// <summary>
    /// Reads the required integer field called <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <returns>The value of the field.</returns>
    /// <exception cref="InvalidDataException">The field is absent or not an integer.</exception>
    public long GetInteger(string name) => Read(name, reader => reader.PeekState() is CborReaderState.UnsignedInteger or CborReaderState.NegativeInteger
        ? reader.ReadInt64()
        : throw new InvalidDataException($"The '{name}' field is not an integer."));

    /// <summary>
    /// Reads the required boolean field called <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <returns>The value of the field.</returns>
    /// <exception cref="InvalidDataException">The field is absent or not a boolean.</exception>
    public bool GetBoolean(string name) => Read(name, reader => reader.PeekState() == CborReaderState.Boolean
        ? reader.ReadBoolean()
        : throw new InvalidDataException($"The '{name}' field is not a boolean."));

    /// <summary>
    /// Reads the required string field called <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <returns>The value of the field.</returns>
    /// <exception cref="InvalidDataException">The field is absent or not a string.</exception>
    public string GetString(string name) => Read(name, reader => reader.PeekState() == CborReaderState.TextString
        ? reader.ReadTextString()
        : throw new InvalidDataException($"The '{name}' field is not a string."));

    /// <summary>
    /// Reads the optional, or nullable, string field called <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <returns>The value of the field, or <see langword="null"/> if it is absent or null.</returns>
    /// <exception cref="InvalidDataException">The field is present but not a string or null.</exception>
    public string? GetOptionalString(string name) => IsAbsentOrNull(name) ? null : GetString(name);

    /// <summary>
    /// Reads the required byte string field called <paramref name="name"/>, into a newly allocated array.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <param name="maximumLength">The largest number of bytes accepted.</param>
    /// <returns>The value of the field.</returns>
    /// <exception cref="InvalidDataException">The field is absent, not a byte string, or longer than <paramref name="maximumLength"/>.</exception>
    public byte[] GetBytes(string name, int maximumLength = int.MaxValue)
    {
        byte[] value = Read(name, reader => reader.PeekState() == CborReaderState.ByteString
            ? reader.ReadByteString()
            : throw new InvalidDataException($"The '{name}' field is not a byte string."));

        if (value.Length > maximumLength)
        {
            throw new InvalidDataException(
                string.Create(CultureInfo.InvariantCulture, $"The '{name}' field is {value.Length} bytes, more than the maximum of {maximumLength}."));
        }

        return value;
    }

    /// <summary>
    /// Reads the required CID link field called <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <returns>The value of the field.</returns>
    /// <exception cref="InvalidDataException">The field is absent or not a CID link.</exception>
    public Cid GetCidLink(string name) => Read(name, FirehoseCbor.ReadCidLink);

    /// <summary>
    /// Reads the optional, or nullable, CID link field called <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <returns>The value of the field, or <see langword="null"/> if it is absent or null.</returns>
    /// <exception cref="InvalidDataException">The field is present but not a CID link or null.</exception>
    public Cid? GetOptionalCidLink(string name) => IsAbsentOrNull(name) ? null : GetCidLink(name);

    /// <summary>
    /// Reads the required DID field called <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <returns>The value of the field.</returns>
    /// <exception cref="InvalidDataException">The field is absent or not a valid DID.</exception>
    public Did GetDid(string name) =>
        Did.TryParse(GetString(name), out Did? did) ? did : throw new InvalidDataException($"The '{name}' field is not a valid DID.");

    /// <summary>
    /// Reads the required datetime field called <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <returns>The value of the field.</returns>
    /// <exception cref="InvalidDataException">The field is absent or not a valid datetime.</exception>
    public DateTimeOffset GetDateTime(string name) =>
        DateTimeOffset.TryParse(GetString(name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset value)
            ? value
            : throw new InvalidDataException($"The '{name}' field is not a valid datetime.");

    /// <summary>
    /// Reads the optional datetime field called <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <returns>The value of the field, or <see langword="null"/> if it is absent or null.</returns>
    /// <exception cref="InvalidDataException">The field is present but not a valid datetime.</exception>
    public DateTimeOffset? GetOptionalDateTime(string name) => IsAbsentOrNull(name) ? null : GetDateTime(name);

    /// <summary>
    /// Reads the items of the required array field called <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <param name="maximumLength">The largest number of items accepted.</param>
    /// <returns>The encoded items of the array.</returns>
    /// <exception cref="InvalidDataException">The field is absent, not an array, or has more than <paramref name="maximumLength"/> items.</exception>
    public IReadOnlyList<ReadOnlyMemory<byte>> GetArray(string name, int maximumLength) => FirehoseCbor.ReadArray(GetEncoded(name), maximumLength, name);

    private static bool IsNull(ReadOnlyMemory<byte> value) => value.Length == 1 && value.Span[0] == NullByte;

    private T Read<T>(string name, Func<CborReader, T> read)
    {
        ReadOnlyMemory<byte> encoded = GetEncoded(name);

        return FirehoseCbor.Wrap(() => read(new CborReader(encoded, CborConformanceMode.Canonical)));
    }
}
