// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Formats.Cbor;

namespace idunno.AtProto.Repo;

/// <summary>
/// Writes content-addressed archive version 1 (CARv1) files.
/// </summary>
public sealed class CarWriter : IDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private bool _disposed;

    /// <summary>
    /// Creates a new <see cref="CarWriter"/> and writes the CAR header.
    /// </summary>
    /// <param name="stream">The writable stream.</param>
    /// <param name="header">The CAR header.</param>
    /// <param name="leaveOpen"><see langword="true"/> to leave <paramref name="stream"/> open when this instance is disposed.</param>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="stream"/> is not writable.</exception>
    public CarWriter(Stream stream, CarHeader header, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(header);
        if (!stream.CanWrite)
        {
            throw new ArgumentException("The stream must be writable.", nameof(stream));
        }

        _stream = stream;
        _leaveOpen = leaveOpen;
        WriteHeader(header);
    }

    /// <summary>
    /// Writes a block to the archive.
    /// </summary>
    /// <param name="block">The block to write.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="block"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the writer has been disposed.</exception>
    public void WriteBlock(CarBlock block)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(block);

        byte[] cid = GetCarCidBytes(block.Cid);
        ulong sectionLength = checked((ulong)cid.Length + (ulong)block.Data.Length);
        WriteUnsignedVarInt(sectionLength);
        _stream.Write(cid);
        _stream.Write(block.Data.Span);
    }

    /// <summary>
    /// Releases resources used by this writer.
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

    private void WriteHeader(CarHeader header)
    {
        CborWriter writer = new(CborConformanceMode.Canonical);
        writer.WriteStartMap(2);
        writer.WriteTextString("roots");
        writer.WriteStartArray(header.Roots.Count);
        foreach (Cid root in header.Roots)
        {
            writer.WriteByteString(GetCarCidBytes(root));
        }

        writer.WriteEndArray();
        writer.WriteTextString("version");
        writer.WriteUInt64(CarHeader.Version);
        writer.WriteEndMap();
        byte[] encodedHeader = writer.Encode();
        WriteUnsignedVarInt((ulong)encodedHeader.Length);
        _stream.Write(encodedHeader);
    }

    private static byte[] GetCarCidBytes(Cid cid)
    {
        ArgumentNullException.ThrowIfNull(cid);

        return cid.Version == 0 ? [.. cid.Hash] : cid.ToBytes();
    }

    private void WriteUnsignedVarInt(ulong value)
    {
        Span<byte> buffer = stackalloc byte[10];
        int index = 0;
        while (value >= 0x80)
        {
            buffer[index++] = (byte)(value | 0x80);
            value >>= 7;
        }

        buffer[index++] = (byte)value;
        _stream.Write(buffer[..index]);
    }
}
