// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Formats.Cbor;

namespace idunno.AtProto.Repo;

/// <summary>
/// Reads content-addressed archive version 1 (CARv1) files.
/// </summary>
public sealed class CarReader : IDisposable
{
    private const int MaximumHeaderSize = 1024 * 1024;
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private bool _disposed;
    private bool _headerRead;

    /// <summary>
    /// Creates a new <see cref="CarReader"/>.
    /// </summary>
    /// <param name="stream">The readable stream.</param>
    /// <param name="leaveOpen"><see langword="true"/> to leave <paramref name="stream"/> open when this instance is disposed.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stream"/> is not readable.</exception>
    public CarReader(Stream stream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(stream));
        }

        _stream = stream;
        _leaveOpen = leaveOpen;
    }

    /// <summary>
    /// Reads the CAR header.
    /// </summary>
    /// <returns>The CAR header.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the CAR header has already been read.</exception>
    /// <exception cref="InvalidDataException">Thrown when the CAR header is invalid.</exception>
    public CarHeader ReadHeader()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_headerRead)
        {
            throw new InvalidOperationException("The CAR header has already been read.");
        }

        ulong? headerLength = ReadUnsignedVarInt();
        if (headerLength is null || headerLength == 0 || headerLength > MaximumHeaderSize)
        {
            throw new InvalidDataException("The CAR header length is invalid.");
        }

        byte[] encodedHeader = ReadExactly((int)headerLength.Value);
        CborReader reader = new(encodedHeader, CborConformanceMode.Canonical);
        int? mapLength = reader.ReadStartMap();
        if (mapLength != 2)
        {
            throw new InvalidDataException("The CAR header must contain exactly two fields.");
        }

        IReadOnlyList<Cid>? roots = null;
        ulong? version = null;
        for (int i = 0; i < mapLength; i++)
        {
            string key = reader.ReadTextString();
            switch (key)
            {
                case "roots":
                    int? rootCount = reader.ReadStartArray() ?? throw new InvalidDataException("The CAR roots array must have a definite length.");
                    List<Cid> parsedRoots = new(rootCount.Value);
                    for (int rootIndex = 0; rootIndex < rootCount; rootIndex++)
                    {
                        parsedRoots.Add(ReadCid(reader));
                    }

                    reader.ReadEndArray();
                    roots = parsedRoots;
                    break;
                case "version":
                    version = reader.ReadUInt64();
                    break;
                default:
                    throw new InvalidDataException($"Unknown CAR header field '{key}'.");
            }
        }

        reader.ReadEndMap();
        if (reader.BytesRemaining != 0 || version != CarHeader.Version || roots is null)
        {
            throw new InvalidDataException("The CAR header is invalid.");
        }

        _headerRead = true;
        return new CarHeader(roots);
    }

    /// <summary>
    /// Reads the next block from the archive.
    /// </summary>
    /// <returns>The next block, or <see langword="null"/> at the end of the archive.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the CAR header has not been read.</exception>
    /// <exception cref="InvalidDataException">Thrown when a CAR block is invalid.</exception>
    public CarBlock? ReadBlock()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_headerRead)
        {
            throw new InvalidOperationException("Read the CAR header before reading blocks.");
        }

        ulong? sectionLength = ReadUnsignedVarInt();
        if (sectionLength is null)
        {
            return null;
        }

        if (sectionLength == 0)
        {
            throw new InvalidDataException("A CAR block cannot have a zero length.");
        }

        if (sectionLength > int.MaxValue)
        {
            throw new InvalidDataException("The CAR block is too large.");
        }

        byte[] section = ReadExactly((int)sectionLength.Value);
        int cidLength = GetCidLength(section);
        if (cidLength == section.Length)
        {
            throw new InvalidDataException("A CAR block must contain data after its CID.");
        }

        return new CarBlock(ParseCid(section.AsSpan(0, cidLength)), section.AsMemory(cidLength));
    }

    /// <summary>
    /// Releases resources used by this reader.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (!_leaveOpen)
            {
                _stream.Dispose();
            }
        }
    }

    private ulong? ReadUnsignedVarInt()
    {
        ulong value = 0;
        int shift = 0;
        for (int length = 0; length < 10; length++)
        {
            int next = _stream.ReadByte();
            if (next < 0)
            {
                if (length == 0)
                {
                    return null;
                }

                throw new EndOfStreamException("The CAR block length is truncated.");
            }

            byte current = (byte)next;
            if (length == 9 && current > 1)
            {
                throw new InvalidDataException("The CAR block length is too large.");
            }

            value |= (ulong)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
            {
                return value;
            }

            shift += 7;
        }

        throw new InvalidDataException("The CAR block length varint is too long.");
    }

    private byte[] ReadExactly(int length)
    {
        byte[] result = new byte[length];
        int offset = 0;
        while (offset < result.Length)
        {
            int read = _stream.Read(result, offset, result.Length - offset);
            if (read == 0)
            {
                throw new EndOfStreamException("The CAR block is truncated.");
            }

            offset += read;
        }

        return result;
    }

    private static int GetCidLength(ReadOnlySpan<byte> section)
    {
        if (section.IsEmpty)
        {
            throw new InvalidDataException("The CAR block does not contain a CID.");
        }

        if (section[0] == 0x12)
        {
            if (section.Length < 2 || section[1] != 0x20)
            {
                throw new InvalidDataException("The CARv0 CID is not a SHA-256 multihash.");
            }

            return 34;
        }

        int index = 1;
        ReadUnsignedVarInt(section, ref index);
        ReadUnsignedVarInt(section, ref index);
        ulong digestLength = ReadUnsignedVarInt(section, ref index);
        if (digestLength > int.MaxValue || digestLength > (ulong)(section.Length - index))
        {
            throw new InvalidDataException("The CID multihash is truncated.");
        }

        return checked(index + (int)digestLength);
    }

    private static ulong ReadUnsignedVarInt(ReadOnlySpan<byte> bytes, ref int index)
    {
        ulong value = 0;
        int shift = 0;
        for (int length = 0; length < 10; length++)
        {
            if (index >= bytes.Length)
            {
                throw new InvalidDataException("The CID varint is truncated.");
            }

            byte current = bytes[index++];
            if (length == 9 && current > 1)
            {
                throw new InvalidDataException("The CID varint is too large.");
            }

            value |= (ulong)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
            {
                return value;
            }

            shift += 7;
        }

        throw new InvalidDataException("The CID varint is too long.");
    }

    private static Cid ParseCid(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 34 && bytes[0] == 0x12 && bytes[1] == 0x20)
        {
            return new Cid(0, 0x70, bytes.ToArray());
        }

        return new Cid(bytes.ToArray());
    }

    private static Cid ReadCid(CborReader reader)
    {
        if (reader.PeekState() == CborReaderState.Tag)
        {
            ulong tag = (ulong)reader.ReadTag();
            if (tag != 42)
            {
                throw new InvalidDataException($"Unsupported CID CBOR tag {tag}.");
            }
        }

        return ParseCid(reader.ReadByteString());
    }
}
