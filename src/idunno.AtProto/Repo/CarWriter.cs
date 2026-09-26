// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Formats.Cbor;

namespace idunno.AtProto.Repo;

/// <summary>
/// Writes content-addressed archive version 1 (CARv1) files.
/// </summary>
public sealed class CarWriter : IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private bool _disposed;

    private CarWriter(Stream stream, bool leaveOpen)
    {
        _stream = stream;
        _leaveOpen = leaveOpen;
    }

    /// <summary>
    /// Asynchronously creates a new <see cref="CarWriter"/> and writes the CAR header.
    /// </summary>
    /// <param name="stream">The writable stream.</param>
    /// <param name="header">The CAR header.</param>
    /// <param name="leaveOpen"><see langword="true"/> to leave <paramref name="stream"/> open when this instance is disposed.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A writer for the CAR archive.</returns>
    /// <exception cref="System.ArgumentException">Thrown when <paramref name="stream"/> is not writable.</exception>
    /// <exception cref="System.Exception">Thrown when writing the header fails. If <paramref name="leaveOpen"/> is <see langword="false"/>, the stream is disposed before the exception is rethrown.</exception>
    public static async Task<CarWriter> CreateAsync(
        Stream stream,
        CarHeader header,
        bool leaveOpen = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(header);
        if (!stream.CanWrite)
        {
            throw new ArgumentException("The stream must be writable.", nameof(stream));
        }

        byte[] encodedHeader = EncodeHeader(header);
        try
        {
            await WriteUnsignedVarIntAsync(stream, (ulong)encodedHeader.Length, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(encodedHeader, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (!leaveOpen)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }

        return new CarWriter(stream, leaveOpen);
    }

    /// <summary>
    /// Asynchronously writes a block to the archive.
    /// </summary>
    /// <param name="block">The block to write.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="block"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the writer has been disposed.</exception>
    public async Task WriteBlockAsync(CarBlock block, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(block);

        byte[] cid = GetCarCidBytes(block.Cid);
        ulong sectionLength = checked((ulong)cid.Length + (ulong)block.Data.Length);
        await WriteUnsignedVarIntAsync(_stream, sectionLength, cancellationToken).ConfigureAwait(false);
        await _stream.WriteAsync(cid, cancellationToken).ConfigureAwait(false);
        await _stream.WriteAsync(block.Data, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously releases resources used by this writer.
    /// </summary>
    /// <returns>A value task that represents the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (!_leaveOpen)
            {
                await _stream.DisposeAsync().ConfigureAwait(false);
            }
        }

        GC.SuppressFinalize(this);
    }

    private static byte[] EncodeHeader(CarHeader header)
    {
        CborWriter writer = new(CborConformanceMode.Canonical);
        writer.WriteStartMap(2);
        writer.WriteTextString("roots");
        writer.WriteStartArray(header.Roots.Count);
        foreach (Cid root in header.Roots)
        {
            writer.WriteTag((CborTag)42);
            writer.WriteByteString([0x00, .. GetCarCidBytes(root)]);
        }

        writer.WriteEndArray();
        writer.WriteTextString("version");
        writer.WriteUInt64(CarHeader.Version);
        writer.WriteEndMap();
        return writer.Encode();
    }

    private static byte[] GetCarCidBytes(Cid cid)
    {
        ArgumentNullException.ThrowIfNull(cid);

        return cid.Version == 0 ? [.. cid.Hash] : cid.ToBytes();
    }

    private static async Task WriteUnsignedVarIntAsync(Stream stream, ulong value, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[10];
        int index = 0;
        while (value >= 0x80)
        {
            buffer[index++] = (byte)(value | 0x80);
            value >>= 7;
        }

        buffer[index++] = (byte)value;
        await stream.WriteAsync(buffer.AsMemory(0, index), cancellationToken).ConfigureAwait(false);
    }
}
