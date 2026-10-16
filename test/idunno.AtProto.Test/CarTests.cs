// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Formats.Cbor;
using System.Security.Cryptography;

using idunno.AtProto.Repo;

namespace idunno.AtProto.Test;

public class CarTests
{
    [Fact]
    public async Task WritesAndReadsCar()
    {
        Cid root = new("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4");
        Cid blockCid = new("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4");
        byte[] data = [1, 2, 3, 4];
        using MemoryStream stream = new();

        await using (CarWriter writer = await CarWriter.CreateAsync(
            stream,
            new CarHeader([root]),
            leaveOpen: true,
            cancellationToken: TestContext.Current.CancellationToken))
        {
            await writer.WriteBlockAsync(new CarBlock(blockCid, data), TestContext.Current.CancellationToken);
        }

        stream.Position = 0;
        using CarReader reader = new(stream, leaveOpen: true);
        CarHeader header = reader.ReadHeader();
        CarBlock? block = reader.ReadBlock();

        Cid item = Assert.Single(header.Roots);
        Assert.Equal(root, item);
        Assert.NotNull(block);
        Assert.Equal(blockCid, block.Cid);
        Assert.Equal(data, block.Data.ToArray());
        Assert.Null(reader.ReadBlock());
    }

    [Fact]
    public async Task AsynchronouslyWritesAndReadsCar()
    {
        Cid root = new("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4");
        Cid blockCid = new("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4");
        byte[] data = [1, 2, 3, 4];
        using MemoryStream stream = new();

        await using (CarWriter writer = await CarWriter.CreateAsync(
            stream,
            new CarHeader([root]),
            leaveOpen: true,
            cancellationToken: TestContext.Current.CancellationToken))
        {
            await writer.WriteBlockAsync(new CarBlock(blockCid, data), TestContext.Current.CancellationToken);
        }

        stream.Position = 0;
        using CarReader reader = new(stream, leaveOpen: true);
        CarHeader header = reader.ReadHeader();
        CarBlock? block = reader.ReadBlock();

        Assert.Equal(root, Assert.Single(header.Roots));
        Assert.NotNull(block);
        Assert.Equal(blockCid, block.Cid);
        Assert.Equal(data, block.Data.ToArray());
        Assert.Null(reader.ReadBlock());
    }

    [Fact]
    public async Task ValidatesRepositoryCommitSignatureAndReadsNonSeekableCar()
    {
        Did did = new("did:plc:ewvi7nxzyoun6zhxsrcy6jgr");
        using ECDsa signingKey = ECDsa.Create(ECCurve.CreateFromValue("1.3.132.0.10"));
        byte[] compressedKey = GetCompressedSigningKey(signingKey.ExportParameters(true).Q);
        string multibaseKey = $"z{SimpleBase.Base58.Bitcoin.Encode([0xE7, 0x01, .. compressedKey])}";
        DidDocument didDocument = new(
            did,
            context: null,
            alsoKnownAs: null,
            verificationMethods:
            [
                new VerificationMethod($"{did}#atproto", "Multikey", did)
                {
                    PublicKeyMultibase = multibaseKey
                }
            ],
            services: null);

        byte[] commitData = CreateSignedCommit(did, signingKey);
        Cid root = CreateDagCborCid(commitData);
        byte[] car = await CreateCarAsync(root, commitData);
        using NonSeekableReadStream stream = new(car);

        using (CarReader reader = await CarReader.CreateAsync(
            stream,
            (requestedDid, _) => Task.FromResult<DidDocument?>(requestedDid == did ? didDocument : null),
            cancellationToken: TestContext.Current.CancellationToken))
        {
            CarHeader header = reader.ReadHeader();
            CarBlock? block = reader.ReadBlock();
            Assert.Equal(root, Assert.Single(header.Roots));
            Assert.NotNull(block);
            Assert.Equal(root, block.Cid);
            Assert.Equal(commitData, block.Data.ToArray());
            Assert.True(stream.CanRead);
        }

        Assert.False(stream.CanRead);
    }

    [Fact]
    public async Task RejectsInvalidRepositoryCommitSignature()
    {
        Did did = new("did:plc:ewvi7nxzyoun6zhxsrcy6jgr");
        using ECDsa signingKey = ECDsa.Create(ECCurve.CreateFromValue("1.3.132.0.10"));
        byte[] compressedKey = GetCompressedSigningKey(signingKey.ExportParameters(true).Q);
        string multibaseKey = $"z{SimpleBase.Base58.Bitcoin.Encode([0xE7, 0x01, .. compressedKey])}";
        DidDocument didDocument = new(
            did,
            context: null,
            alsoKnownAs: null,
            verificationMethods:
            [
                new VerificationMethod($"{did}#atproto", "Multikey", did)
                {
                    PublicKeyMultibase = multibaseKey
                }
            ],
            services: null);

        byte[] commitData = CreateSignedCommit(did, signingKey, corruptSignature: true);
        Cid root = CreateDagCborCid(commitData);
        using MemoryStream stream = new(await CreateCarAsync(root, commitData));

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await CarReader.CreateAsync(
                stream,
                (_, _) => Task.FromResult<DidDocument?>(didDocument),
                leaveOpen: true,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ValidatesP256RepositoryCommitSignature()
    {
        Did did = new("did:plc:ewvi7nxzyoun6zhxsrcy6jgr");
        using ECDsa signingKey = ECDsa.Create(ECCurve.CreateFromValue("1.2.840.10045.3.1.7"));
        byte[] compressedKey = GetCompressedSigningKey(signingKey.ExportParameters(true).Q);
        string multibaseKey = $"z{SimpleBase.Base58.Bitcoin.Encode([0x80, 0x24, .. compressedKey])}";
        DidDocument didDocument = new(
            did,
            context: null,
            alsoKnownAs: null,
            verificationMethods:
            [
                new VerificationMethod($"{did}#atproto", "Multikey", did)
                {
                    PublicKeyMultibase = multibaseKey
                }
            ],
            services: null);

        byte[] commitData = CreateSignedCommit(did, signingKey);
        Cid root = CreateDagCborCid(commitData);
        using MemoryStream stream = new(await CreateCarAsync(root, commitData));

        using CarReader reader = await CarReader.CreateAsync(
            stream,
            (_, _) => Task.FromResult<DidDocument?>(didDocument),
            leaveOpen: true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(root, Assert.Single(reader.ReadHeader().Roots));
        CarBlock? block = reader.ReadBlock();
        Assert.NotNull(block);
        Assert.Equal(root, block.Cid);
    }

    [Fact]
    public async Task ReadsCarHeaderBeforeBlocks()
    {
        using MemoryStream stream = new();
        await using CarWriter writer = await CarWriter.CreateAsync(
            stream,
            new CarHeader([]),
            leaveOpen: true,
            cancellationToken: TestContext.Current.CancellationToken);

        stream.Position = 0;
        using CarReader reader = new(stream, leaveOpen: true);

        Assert.Throws<InvalidOperationException>(() => reader.ReadBlock());
    }

    [Fact]
    public async Task RejectsTruncatedBlock()
    {
        Cid cid = new("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4");
        using MemoryStream stream = new();
        await using (CarWriter writer = await CarWriter.CreateAsync(
            stream,
            new CarHeader([]),
            leaveOpen: true,
            cancellationToken: TestContext.Current.CancellationToken))
        {
            await writer.WriteBlockAsync(
                new CarBlock(cid, new byte[] { 1, 2, 3 }),
                TestContext.Current.CancellationToken);
        }

        byte[] bytes = stream.ToArray();
        Array.Resize(ref bytes, bytes.Length - 1);
        using CarReader reader = new(new MemoryStream(bytes));
        reader.ReadHeader();

        Assert.Throws<EndOfStreamException>(() => reader.ReadBlock());
    }

    private static byte[] GetCompressedSigningKey(ECPoint point)
    {
        byte[] compressed = new byte[33];
        byte[] x = point.X ?? throw new CryptographicException("The test key has no x coordinate.");
        byte[] y = point.Y ?? throw new CryptographicException("The test key has no y coordinate.");
        compressed[0] = (y[^1] & 1) == 1 ? (byte)0x03 : (byte)0x02;
        x.CopyTo(compressed, 1);
        return compressed;
    }

    private static byte[] CreateSignedCommit(Did did, ECDsa signingKey, bool corruptSignature = false)
    {
        Cid dataCid = new("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4");
        byte[] unsignedCommit = EncodeCommit(did, dataCid, []);
        byte[] signature = signingKey.SignData(
            unsignedCommit,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        if (corruptSignature)
        {
            signature[0] ^= 1;
        }

        return EncodeCommit(did, dataCid, signature);
    }

    private static byte[] EncodeCommit(Did did, Cid dataCid, byte[] signature)
    {
        CborWriter writer = new(CborConformanceMode.Canonical);
        writer.WriteStartMap(signature.Length == 0 ? 5 : 6);
        writer.WriteTextString("did");
        writer.WriteTextString(did.Value);
        writer.WriteTextString("data");
        WriteCidLink(writer, dataCid);
        writer.WriteTextString("prev");
        writer.WriteNull();
        writer.WriteTextString("rev");
        writer.WriteTextString("3lngovum7vm2k");
        if (signature.Length > 0)
        {
            writer.WriteTextString("sig");
            writer.WriteByteString(signature);
        }

        writer.WriteTextString("version");
        writer.WriteInt64(3);
        writer.WriteEndMap();
        return writer.Encode();
    }

    private static void WriteCidLink(CborWriter writer, Cid cid)
    {
        writer.WriteTag((CborTag)42);
        writer.WriteByteString([0x00, .. cid.ToBytes()]);
    }

    private static Cid CreateDagCborCid(byte[] data)
    {
        byte[] cidBytes = [0x01, 0x71, 0x12, 0x20, .. SHA256.HashData(data)];
        return new Cid(cidBytes);
    }

    private static async Task<byte[]> CreateCarAsync(Cid root, byte[] data)
    {
        using MemoryStream stream = new();
        await using (CarWriter writer = await CarWriter.CreateAsync(
            stream,
            new CarHeader([root]),
            leaveOpen: true,
            cancellationToken: TestContext.Current.CancellationToken))
        {
            await writer.WriteBlockAsync(
                new CarBlock(root, data),
                TestContext.Current.CancellationToken);
        }

        return stream.ToArray();
    }

    private sealed class NonSeekableReadStream(byte[] data) : Stream
    {
        private readonly MemoryStream _innerStream = new(data);

        public override bool CanRead => _innerStream.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => _innerStream.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _innerStream.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerStream.Dispose();
            }

            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            return _innerStream.DisposeAsync();
        }
    }
}
