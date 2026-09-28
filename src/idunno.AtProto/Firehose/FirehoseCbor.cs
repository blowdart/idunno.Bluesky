// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Formats.Cbor;
using System.Globalization;

namespace idunno.AtProto.Firehose;

/// <summary>
/// Hand written, reflection free, DAG-CBOR helpers for decoding event stream frames.
/// </summary>
internal static class FirehoseCbor
{
    /// <summary>
    /// Nesting deeper than this is rejected rather than risking a stack overflow on hostile input, matching <see cref="DagCbor"/>.
    /// </summary>
    internal const int MaximumDepth = 128;

    private const ulong CidTag = 42;

    private const byte CidMultibasePrefix = 0x00;

    /// <summary>
    /// Checks that <paramref name="value"/> is a single DAG-CBOR value which conforms to the AT Protocol data model.
    /// </summary>
    /// <param name="value">The encoded value.</param>
    /// <exception cref="InvalidDataException"><paramref name="value"/> is not a single valid DAG-CBOR value.</exception>
    /// <remarks>
    /// <para>Canonical conformance rejects indefinite lengths, non-minimal encodings, unsorted or duplicate map keys and invalid UTF-8.
    /// The walk then rejects anything the data model does not allow: floating point numbers, simple values other than booleans and
    /// null, integers outside the signed 64 bit range, tags other than CID links, non-string map keys, and excessive nesting.</para>
    /// </remarks>
    internal static void ValidateDataModel(ReadOnlyMemory<byte> value)
    {
        Wrap(() =>
        {
            CborReader reader = new(value, CborConformanceMode.Canonical);
            ValidateValue(reader, depth: 0);

            if (reader.BytesRemaining != 0)
            {
                throw new InvalidDataException("The value contains trailing data after the DAG-CBOR value.");
            }
        });
    }

    /// <summary>
    /// Reads the next value from <paramref name="reader"/>, checking it conforms to the AT Protocol data model as it goes.
    /// </summary>
    /// <param name="reader">The reader positioned on the value.</param>
    /// <param name="source">The buffer <paramref name="reader"/> was created over.</param>
    /// <returns>The encoded value, as a slice of <paramref name="source"/>.</returns>
    /// <exception cref="InvalidDataException">The value does not conform to the data model, or is nested too deeply.</exception>
    /// <remarks>
    /// <para>Use this, rather than <see cref="CborReader.ReadEncodedValue(bool)"/>, on untrusted data. The validator stops at
    /// <see cref="MaximumDepth"/>, whereas skipping a value first tracks every level of nesting it contains.</para>
    /// </remarks>
    internal static ReadOnlyMemory<byte> ReadValidatedValue(CborReader reader, ReadOnlyMemory<byte> source) => Wrap(() =>
    {
        int start = source.Length - reader.BytesRemaining;
        ValidateValue(reader, depth: 0);

        return source[start..(source.Length - reader.BytesRemaining)];
    });

    /// <summary>
    /// Reads the fields of the DAG-CBOR map in <paramref name="value"/>, keeping each field's value encoded.
    /// </summary>
    /// <param name="value">The encoded map.</param>
    /// <returns>The fields of the map.</returns>
    /// <exception cref="InvalidDataException"><paramref name="value"/> is not a DAG-CBOR map.</exception>
    internal static CborFields ReadFields(ReadOnlyMemory<byte> value) => Wrap(() =>
    {
        CborReader reader = new(value, CborConformanceMode.Canonical);

        if (reader.PeekState() != CborReaderState.StartMap)
        {
            throw new InvalidDataException("The value is not a DAG-CBOR map.");
        }

        int length = reader.ReadStartMap() ?? throw new InvalidDataException("The value is an indefinite length map.");
        Dictionary<string, ReadOnlyMemory<byte>> fields = new(length, StringComparer.Ordinal);

        for (int i = 0; i < length; i++)
        {
            if (reader.PeekState() != CborReaderState.TextString)
            {
                throw new InvalidDataException("The value contains a map key which is not a string.");
            }

            string key = reader.ReadTextString();
            fields[key] = reader.ReadEncodedValue();
        }

        reader.ReadEndMap();

        if (reader.BytesRemaining != 0)
        {
            throw new InvalidDataException("The value contains trailing data after the DAG-CBOR map.");
        }

        return new CborFields(fields);
    });

    /// <summary>
    /// Reads the items of the DAG-CBOR array in <paramref name="value"/>, keeping each item encoded.
    /// </summary>
    /// <param name="value">The encoded array.</param>
    /// <param name="maximumLength">The largest number of items accepted.</param>
    /// <param name="name">The name of the field, for error messages.</param>
    /// <returns>The encoded items.</returns>
    /// <exception cref="InvalidDataException"><paramref name="value"/> is not an array, or has more than <paramref name="maximumLength"/> items.</exception>
    internal static IReadOnlyList<ReadOnlyMemory<byte>> ReadArray(ReadOnlyMemory<byte> value, int maximumLength, string name) => Wrap(() =>
    {
        CborReader reader = new(value, CborConformanceMode.Canonical);

        if (reader.PeekState() != CborReaderState.StartArray)
        {
            throw new InvalidDataException($"The '{name}' field is not an array.");
        }

        int length = reader.ReadStartArray() ?? throw new InvalidDataException($"The '{name}' field is an indefinite length array.");

        if (length > maximumLength)
        {
            throw new InvalidDataException(
                string.Create(CultureInfo.InvariantCulture, $"The '{name}' field has {length} items, more than the maximum of {maximumLength}."));
        }

        List<ReadOnlyMemory<byte>> items = new(length);

        for (int i = 0; i < length; i++)
        {
            items.Add(reader.ReadEncodedValue());
        }

        reader.ReadEndArray();

        return (IReadOnlyList<ReadOnlyMemory<byte>>)items.AsReadOnly();
    });

    /// <summary>
    /// Reads a DAG-CBOR CID link from <paramref name="reader"/>.
    /// </summary>
    /// <param name="reader">The reader positioned on the link.</param>
    /// <returns>The linked <see cref="Cid"/>.</returns>
    /// <exception cref="InvalidDataException">The value is not a valid CID link.</exception>
    internal static Cid ReadCidLink(CborReader reader)
    {
        if (reader.PeekState() != CborReaderState.Tag || (ulong)reader.ReadTag() != CidTag)
        {
            throw new InvalidDataException("The value is not a DAG-CBOR CID link.");
        }

        byte[] bytes = reader.ReadByteString();

        if (bytes.Length < 2 || bytes[0] != CidMultibasePrefix)
        {
            throw new InvalidDataException("The DAG-CBOR CID link has an invalid prefix.");
        }

        try
        {
            return new Cid(bytes.AsSpan(1));
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The DAG-CBOR CID link is not a valid content identifier.", exception);
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/>, converting CBOR decoding failures into <see cref="InvalidDataException"/>.
    /// </summary>
    /// <param name="action">The decoding action to run.</param>
    /// <exception cref="InvalidDataException">The data is not valid DAG-CBOR.</exception>
    internal static void Wrap(Action action) => Wrap(() =>
    {
        action();
        return true;
    });

    /// <summary>
    /// Runs <paramref name="func"/>, converting CBOR decoding failures into <see cref="InvalidDataException"/>.
    /// </summary>
    /// <typeparam name="T">The type of value decoded.</typeparam>
    /// <param name="func">The decoding function to run.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="InvalidDataException">The data is not valid DAG-CBOR.</exception>
    internal static T Wrap<T>(Func<T> func)
    {
        try
        {
            return func();
        }
        catch (CborContentException exception)
        {
            throw new InvalidDataException("The value is not valid DAG-CBOR.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException("The value is not valid DAG-CBOR.", exception);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException("The value contains an integer outside the range the AT Protocol data model allows.", exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The value is not valid DAG-CBOR.", exception);
        }
    }

    private static void ValidateValue(CborReader reader, int depth)
    {
        if (depth > MaximumDepth)
        {
            throw new InvalidDataException(
                string.Create(CultureInfo.InvariantCulture, $"The value is nested more than {MaximumDepth} levels deep."));
        }

        switch (reader.PeekState())
        {
            case CborReaderState.Tag:
                ReadCidLink(reader);
                break;

            case CborReaderState.StartMap:
                int mapLength = reader.ReadStartMap() ?? throw new InvalidDataException("The value contains an indefinite length map.");
                for (int i = 0; i < mapLength; i++)
                {
                    if (reader.PeekState() != CborReaderState.TextString)
                    {
                        throw new InvalidDataException("The value contains a map key which is not a string.");
                    }

                    reader.SkipValue();
                    ValidateValue(reader, depth + 1);
                }

                reader.ReadEndMap();
                break;

            case CborReaderState.StartArray:
                int arrayLength = reader.ReadStartArray() ?? throw new InvalidDataException("The value contains an indefinite length array.");
                for (int i = 0; i < arrayLength; i++)
                {
                    ValidateValue(reader, depth + 1);
                }

                reader.ReadEndArray();
                break;

            case CborReaderState.UnsignedInteger:
            case CborReaderState.NegativeInteger:
                reader.ReadInt64();
                break;

            case CborReaderState.TextString:
            case CborReaderState.ByteString:
            case CborReaderState.Boolean:
            case CborReaderState.Null:
                reader.SkipValue();
                break;

            default:
                throw new InvalidDataException(
                    string.Create(CultureInfo.InvariantCulture, $"The value contains a {reader.PeekState()}, which the AT Protocol data model does not allow."));
        }
    }
}
