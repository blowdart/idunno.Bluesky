// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Formats.Cbor;
using System.Security.Cryptography;

namespace idunno.AtProto.Repo;

/// <summary>
/// Reads content-addressed archive version 1 (CARv1) files.
/// </summary>
public sealed class CarReader : IDisposable
{
    private const int MaximumHeaderSize = 1024 * 1024;
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly Stream? _sourceStreamToDispose;
    private bool _disposed;
    private bool _headerRead;
    private readonly int _maximumBlockSize = int.MaxValue;

    /// <summary>
    /// Creates a new <see cref="CarReader"/>.
    /// </summary>
    /// <param name="stream">The readable stream.</param>
    /// <param name="leaveOpen"><see langword="true"/> to leave <paramref name="stream"/> open when this instance is disposed.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stream"/> is not readable.</exception>
    public CarReader(Stream stream, bool leaveOpen = false)
        : this(stream, leaveOpen, sourceStreamToDispose: null)
    {
    }

    /// <summary>
    /// Creates a new <see cref="CarReader"/> which rejects any block section larger than <paramref name="maximumBlockSize"/> bytes.
    /// </summary>
    /// <param name="stream">The readable stream.</param>
    /// <param name="maximumBlockSize">The largest block section, in bytes, to read.</param>
    /// <remarks>
    /// <para>The limit is checked against the declared section length before any buffer is allocated, so a hostile length
    /// prefix cannot cause a large allocation.</para>
    /// </remarks>
    internal CarReader(Stream stream, int maximumBlockSize) : this(stream, leaveOpen: false, sourceStreamToDispose: null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBlockSize);
        _maximumBlockSize = maximumBlockSize;
    }

    private CarReader(Stream stream, bool leaveOpen, Stream? sourceStreamToDispose)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(stream));
        }

        _stream = stream;
        _leaveOpen = leaveOpen;
        _sourceStreamToDispose = sourceStreamToDispose;
    }

    /// <summary>
    /// Asynchronously creates a new <see cref="CarReader"/>, optionally validating the signature of the root repository commit.
    /// </summary>
    /// <param name="stream">The readable CAR stream.</param>
    /// <param name="validateSignature"><see langword="true"/> to validate the root commit signature using its DID document.</param>
    /// <param name="leaveOpen"><see langword="true"/> to leave <paramref name="stream"/> open when this instance is disposed.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A reader for the CAR archive.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stream"/> is not readable.</exception>
    /// <exception cref="InvalidDataException">Thrown when signature validation is requested and the root commit cannot be verified.</exception>
    /// <exception cref="CryptographicException">Thrown when the platform cannot use the secp256k1 signing key.</exception>
    /// <remarks>
    /// <para>
    /// When validation is requested, the stream must contain a single-root repository CAR. A non-seekable stream is copied to a temporary file
    /// so it can be validated and then read normally; the temporary file is deleted when the reader is disposed.
    /// </para>
    /// </remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overload with a custom DID document resolver.")]
    public static Task<CarReader> CreateAsync(
        Stream stream,
        bool validateSignature = false,
        bool leaveOpen = false,
        CancellationToken cancellationToken = default)
    {
        return CreateAsync(
            stream,
            validateSignature,
            static (did, token) => IdentityResolution.ResolveDidDocumentAsync(did, cancellationToken: token),
            leaveOpen,
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously creates a new <see cref="CarReader"/> and validates the root repository commit signature using the supplied DID-document resolver.
    /// </summary>
    /// <param name="stream">The readable CAR stream.</param>
    /// <param name="didDocumentResolver">A function that resolves a DID document for the repository DID.</param>
    /// <param name="leaveOpen"><see langword="true"/> to leave <paramref name="stream"/> open when this instance is disposed.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A reader for the CAR archive.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stream"/> is not readable.</exception>
    /// <exception cref="InvalidDataException">Thrown when the root commit cannot be verified.</exception>
    /// <exception cref="CryptographicException">Thrown when the platform cannot use the secp256k1 signing key.</exception>
    /// <remarks>
    /// <para>
    /// The stream must contain a single-root repository CAR. A non-seekable stream is copied to a temporary file so it can be validated and then
    /// read normally; the temporary file is deleted when the reader is disposed.
    /// </para>
    /// </remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Overload with a custom DID document resolver.")]
    public static Task<CarReader> CreateAsync(
        Stream stream,
        Func<Did, CancellationToken, Task<DidDocument?>> didDocumentResolver,
        bool leaveOpen = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(didDocumentResolver);
        return CreateAsync(stream, true, didDocumentResolver, leaveOpen, cancellationToken);
    }

    private static async Task<CarReader> CreateAsync(
        Stream stream,
        bool validateSignature,
        Func<Did, CancellationToken, Task<DidDocument?>> didDocumentResolver,
        bool leaveOpen,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(stream));
        }

        Stream readerStream = stream;
        try
        {
            if (validateSignature)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!stream.CanSeek)
                {
                    readerStream = CreateTemporaryStream();
                    await stream.CopyToAsync(readerStream, cancellationToken).ConfigureAwait(false);
                    readerStream.Position = 0;
                }

                long initialPosition = readerStream.Position;
                try
                {
                    await ValidateRootCommitSignatureAsync(readerStream, didDocumentResolver, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    readerStream.Position = initialPosition;
                }
            }

            if (readerStream == stream)
            {
                return new CarReader(readerStream, leaveOpen);
            }

            return new CarReader(readerStream, leaveOpen: false, sourceStreamToDispose: leaveOpen ? null : stream);
        }
        catch
        {
            if (readerStream != stream)
            {
                try
                {
                    await readerStream.DisposeAsync().ConfigureAwait(false);
                }
                finally
                {
                    if (!leaveOpen)
                    {
                        await stream.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
            else if (!leaveOpen)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
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
                    // The declared count is untrusted, so the list grows with the roots actually read rather than being sized from it.
                    List<Cid> parsedRoots = [];
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

        if (sectionLength > int.MaxValue ||
            sectionLength > (ulong)_maximumBlockSize ||
            (_maximumBlockSize != int.MaxValue && _stream.CanSeek && sectionLength > (ulong)(_stream.Length - _stream.Position)))
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
            try
            {
                if (!_leaveOpen)
                {
                    _stream.Dispose();
                }
            }
            finally
            {
                _sourceStreamToDispose?.Dispose();
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
        if (reader.PeekState() != CborReaderState.Tag || (ulong)reader.ReadTag() != 42)
        {
            throw new InvalidDataException("A CAR root must use DAG-CBOR CID tag 42.");
        }

        byte[] cidLink = reader.ReadByteString();
        if (cidLink.Length < 2 || cidLink[0] != 0)
        {
            throw new InvalidDataException("A CAR root CID link has an invalid prefix.");
        }

        return ParseCid(cidLink.AsSpan(1));
    }

    private static FileStream CreateTemporaryStream()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        return new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose | FileOptions.SequentialScan);
    }

    private static async Task ValidateRootCommitSignatureAsync(
        Stream stream,
        Func<Did, CancellationToken, Task<DidDocument?>> didDocumentResolver,
        CancellationToken cancellationToken)
    {
        using CarReader reader = new(stream, leaveOpen: true);
        CarHeader header = reader.ReadHeader();
        if (header.Roots.Count != 1)
        {
            throw new InvalidDataException("A repository CAR must have exactly one root to validate its commit signature.");
        }

        Cid root = header.Roots[0];
        CarBlock? block;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            block = reader.ReadBlock();
        }
        while (block is not null && block.Cid != root);

        if (block is null)
        {
            throw new InvalidDataException("The CAR root commit block is missing.");
        }

        if (root.Version != 1 || root.Codec != 0x71 || root.Hash.Count != 34 ||
            root.Hash[0] != 0x12 || root.Hash[1] != 0x20 ||
            !CryptographicOperations.FixedTimeEquals(
                root.Hash.Skip(2).ToArray(),
                SHA256.HashData(block.Data.Span)))
        {
            throw new InvalidDataException("The CAR root CID does not match the root commit block.");
        }

        (Did did, byte[] unsignedCommit, byte[] signature) = ReadUnsignedCommit(block.Data);
        DidDocument? didDocument = await didDocumentResolver(did, cancellationToken).ConfigureAwait(false);
        SigningKeyVerifier.Verify(didDocument, did, SigningKeyVerifier.RepositorySigningKeyFragment, unsignedCommit, signature, "CAR root repository commit");
    }

    internal static (Did Did, byte[] UnsignedCommit, byte[] Signature) ReadUnsignedCommit(ReadOnlyMemory<byte> data)
    {
        // Checked with the depth limited validator first, as reading a deeply nested field with ReadEncodedValue allocates
        // many times the size of the block.
        Firehose.FirehoseCbor.ValidateDataModel(data);

        CborReader reader = new(data, CborConformanceMode.Canonical);
        int? fieldCount = reader.ReadStartMap();
        if (fieldCount != 6)
        {
            throw new InvalidDataException("The repository commit must contain exactly six fields.");
        }

        CborWriter unsignedWriter = new(CborConformanceMode.Canonical);
        unsignedWriter.WriteStartMap(fieldCount.Value - 1);
        string? didValue = null;
        long? version = null;
        byte[]? signature = null;
        HashSet<string> fields = new(StringComparer.Ordinal);
        for (int index = 0; index < fieldCount; index++)
        {
            string key = reader.ReadTextString();
            if (!fields.Add(key))
            {
                throw new InvalidDataException($"The repository commit contains duplicate field '{key}'.");
            }

            if (key == "sig")
            {
                signature = reader.ReadByteString();
                continue;
            }

            if (key == "did")
            {
                didValue = reader.ReadTextString();
                unsignedWriter.WriteTextString(key);
                unsignedWriter.WriteTextString(didValue);
                continue;
            }

            if (key == "version")
            {
                version = reader.ReadInt64();
                unsignedWriter.WriteTextString(key);
                unsignedWriter.WriteInt64(version.Value);
                continue;
            }

            unsignedWriter.WriteTextString(key);
            unsignedWriter.WriteEncodedValue(reader.ReadEncodedValue().Span);
        }

        reader.ReadEndMap();
        unsignedWriter.WriteEndMap();
        if (reader.BytesRemaining != 0 ||
            didValue is null ||
            version != 3 ||
            signature is null ||
            !fields.SetEquals(["did", "version", "prev", "data", "rev", "sig"]))
        {
            throw new InvalidDataException("The repository commit is missing required fields or contains trailing data.");
        }

        Did did;
        try
        {
            did = new Did(didValue);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The repository commit DID is invalid.", exception);
        }

        return (did, unsignedWriter.Encode(), signature);
    }
}
