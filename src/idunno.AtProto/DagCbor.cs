// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.Formats.Cbor;
using System.Globalization;
using System.Text.Json;

namespace idunno.AtProto;

/// <summary>
/// Converts DAG-CBOR encoded data, as used by AT Proto repositories and the firehose, to its JSON representation.
/// </summary>
/// <remarks>
/// <para>DAG-CBOR is the binary encoding of the AT Proto data model; JSON is its textual encoding. The two encode the same
/// values, with byte strings written as a <c>$bytes</c> object and CID links written as a <c>$link</c> object.</para>
/// <para>See https://atproto.com/specs/data-model for the specification.</para>
/// </remarks>
public static class DagCbor
{
    // The CBOR tag DAG-CBOR uses to mark a CID.
    private const ulong CidTag = 42;

    // DAG-CBOR CIDs are prefixed with a zero byte, the multibase identity prefix.
    private const byte CidMultibasePrefix = 0x00;

    // Nesting deeper than this is rejected rather than risking a stack overflow on hostile input.
    private const int MaximumDepth = 128;

    /// <summary>
    /// Converts the DAG-CBOR encoded <paramref name="value"/> to its JSON representation.
    /// </summary>
    /// <param name="value">The DAG-CBOR encoded value to convert.</param>
    /// <returns>A <see cref="JsonElement"/> containing the JSON representation of <paramref name="value"/>.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when <paramref name="value"/> is empty, is not valid DAG-CBOR, or contains a value which is not part of the AT Proto data model.
    /// </exception>
    /// <remarks>
    /// <para>The returned <see cref="JsonElement"/> is independent of any <see cref="JsonDocument"/> and does not need disposing.
    /// Use <see cref="ToJsonDocument(ReadOnlyMemory{byte})"/> to avoid the copy this requires.</para>
    /// </remarks>
    public static JsonElement ToJsonElement(ReadOnlyMemory<byte> value)
    {
        Utf8JsonReader reader = new(ToJsonUtf8(value).Span);

        return JsonElement.ParseValue(ref reader);
    }

    /// <summary>
    /// Converts the DAG-CBOR encoded <paramref name="value"/> to its JSON representation.
    /// </summary>
    /// <param name="value">The DAG-CBOR encoded value to convert.</param>
    /// <returns>A <see cref="JsonDocument"/> containing the JSON representation of <paramref name="value"/>.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when <paramref name="value"/> is empty, is not valid DAG-CBOR, or contains a value which is not part of the AT Proto data model.
    /// </exception>
    /// <remarks>
    /// <para>The caller owns the returned <see cref="JsonDocument"/> and must dispose of it.</para>
    /// </remarks>
    public static JsonDocument ToJsonDocument(ReadOnlyMemory<byte> value) => JsonDocument.Parse(ToJsonUtf8(value));

    /// <summary>
    /// Converts the DAG-CBOR encoded <paramref name="value"/> to its UTF-8 encoded JSON representation.
    /// </summary>
    /// <param name="value">The DAG-CBOR encoded value to convert.</param>
    /// <returns>The UTF-8 encoded JSON representation of <paramref name="value"/>.</returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when <paramref name="value"/> is empty, is not valid DAG-CBOR, or contains a value which is not part of the AT Proto data model.
    /// </exception>
    internal static ReadOnlyMemory<byte> ToJsonUtf8(ReadOnlyMemory<byte> value)
    {
        if (value.IsEmpty)
        {
            throw new InvalidDataException("The value contains no data.");
        }

        ArrayBufferWriter<byte> buffer = new();

        using (Utf8JsonWriter writer = new(buffer))
        {
            CborReader reader;

            try
            {
                reader = new CborReader(value, CborConformanceMode.Canonical);
            }
            catch (CborContentException exception)
            {
                throw new InvalidDataException("The value is not valid DAG-CBOR.", exception);
            }

            WriteValue(reader, writer, depth: 0);

            if (reader.BytesRemaining != 0)
            {
                throw new InvalidDataException("The value contains trailing data after the DAG-CBOR value.");
            }

            writer.Flush();
        }

        return buffer.WrittenMemory;
    }

    /// <summary>
    /// Converts the DAG-CBOR encoded <paramref name="value"/> to its JSON representation.
    /// A return value indicates whether the conversion succeeded.
    /// </summary>
    /// <param name="value">The DAG-CBOR encoded value to convert.</param>
    /// <param name="result">
    /// When this method returns contains the JSON representation of <paramref name="value"/>, or the default <see cref="JsonElement"/>
    /// if the conversion failed. This parameter is passed uninitialized; any value originally supplied in result will be overwritten.
    /// </param>
    /// <returns><see langword="true"/> if <paramref name="value"/> was converted successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryToJsonElement(ReadOnlyMemory<byte> value, out JsonElement result)
    {
        try
        {
            result = ToJsonElement(value);
            return true;
        }
        catch (InvalidDataException)
        {
            result = default;
            return false;
        }
    }

    private static void WriteValue(CborReader reader, Utf8JsonWriter writer, int depth)
    {
        if (depth > MaximumDepth)
        {
            throw new InvalidDataException(
                string.Create(CultureInfo.InvariantCulture, $"The value is nested more than {MaximumDepth} levels deep."));
        }

        try
        {
            switch (reader.PeekState())
            {
                case CborReaderState.Tag:
                    WriteLink(reader, writer);
                    break;

                case CborReaderState.StartMap:
                    WriteMap(reader, writer, depth);
                    break;

                case CborReaderState.StartArray:
                    WriteArray(reader, writer, depth);
                    break;

                case CborReaderState.TextString:
                    writer.WriteStringValue(reader.ReadTextString());
                    break;

                case CborReaderState.ByteString:
                    WriteBytes(reader, writer);
                    break;

                case CborReaderState.UnsignedInteger:
                    ulong unsignedValue = reader.ReadUInt64();

                    // The AT Proto data model's integers are signed and 64 bit, but CBOR can encode an unsigned integer
                    // larger than that, which has no representation in the data model.
                    if (unsignedValue > long.MaxValue)
                    {
                        throw new InvalidDataException(
                            string.Create(CultureInfo.InvariantCulture, $"The value contains the integer {unsignedValue}, which is larger than the AT Proto data model allows."));
                    }

                    writer.WriteNumberValue((long)unsignedValue);
                    break;

                case CborReaderState.NegativeInteger:
                    writer.WriteNumberValue(reader.ReadInt64());
                    break;

                case CborReaderState.Boolean:
                    writer.WriteBooleanValue(reader.ReadBoolean());
                    break;

                case CborReaderState.Null:
                    reader.ReadNull();
                    writer.WriteNullValue();
                    break;

                case CborReaderState.HalfPrecisionFloat:
                case CborReaderState.SinglePrecisionFloat:
                case CborReaderState.DoublePrecisionFloat:
                    throw new InvalidDataException("The value contains a floating point number, which the AT Proto data model does not allow.");

                default:
                    throw new InvalidDataException(
                        string.Create(CultureInfo.InvariantCulture, $"The value contains an unsupported DAG-CBOR value of '{reader.PeekState()}'."));
            }
        }
        catch (CborContentException exception)
        {
            throw new InvalidDataException("The value is not valid DAG-CBOR.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException("The value is not valid DAG-CBOR.", exception);
        }
    }

    private static void WriteLink(CborReader reader, Utf8JsonWriter writer)
    {
        ulong tag = (ulong)reader.ReadTag();

        if (tag != CidTag)
        {
            throw new InvalidDataException(
                string.Create(CultureInfo.InvariantCulture, $"The value contains an unsupported DAG-CBOR tag of '{tag}'."));
        }

        byte[] cidBytes = reader.ReadByteString();

        if (cidBytes.Length < 2 || cidBytes[0] != CidMultibasePrefix)
        {
            throw new InvalidDataException("The value contains a DAG-CBOR link with an invalid prefix.");
        }

        Cid cid;

        try
        {
            cid = new Cid(cidBytes.AsSpan(1).ToArray());
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The value contains a DAG-CBOR link which is not a valid content identifier.", exception);
        }

        writer.WriteStartObject();
        writer.WriteString("$link", cid.Value);
        writer.WriteEndObject();
    }

    private static void WriteBytes(CborReader reader, Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("$bytes", Convert.ToBase64String(reader.ReadByteString()));
        writer.WriteEndObject();
    }

    private static void WriteMap(CborReader reader, Utf8JsonWriter writer, int depth)
    {
        int length = reader.ReadStartMap() ?? throw new InvalidDataException("The value contains an indefinite length map, which DAG-CBOR does not allow.");
        int read = 0;

        writer.WriteStartObject();

        HashSet<string> keys = new(StringComparer.Ordinal);

        while (read < length)
        {
            if (reader.PeekState() != CborReaderState.TextString)
            {
                throw new InvalidDataException("The value contains a DAG-CBOR map with a key which is not a string.");
            }

            string key = reader.ReadTextString();

            if (!keys.Add(key))
            {
                throw new InvalidDataException(
                    string.Create(CultureInfo.InvariantCulture, $"The value contains a DAG-CBOR map with the duplicate key '{key}'."));
            }

            writer.WritePropertyName(key);
            WriteValue(reader, writer, depth + 1);

            read++;
        }

        reader.ReadEndMap();
        writer.WriteEndObject();
    }

    private static void WriteArray(CborReader reader, Utf8JsonWriter writer, int depth)
    {
        int length = reader.ReadStartArray() ?? throw new InvalidDataException("The value contains an indefinite length array, which DAG-CBOR does not allow.");
        int read = 0;

        writer.WriteStartArray();

        while (read < length)
        {
            WriteValue(reader, writer, depth + 1);
            read++;
        }

        reader.ReadEndArray();
        writer.WriteEndArray();
    }
}
