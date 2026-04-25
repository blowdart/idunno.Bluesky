// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.Diagnostics;

using PeterO.Cbor;
using PeterO.Numbers;

namespace idunno.AtProto.FireHose;

/// <summary>
/// An implementation of a Content Addressable aRchive decoder for Firehose CAR blocks.
/// </summary>
/// <remarks>
/// <para>The AtProto implementation of CAR does not require <see href="https://atp.readthedocs.io/en/latest/atproto_core/car.html">coherent DAGs</see>,
/// and does not support arrays of header roots.</para>
/// <para>See https://ipld.io/specs/transport/car/</para>
/// </remarks>
public class AtContentAddressableArchive
{
    private const int ATCidV1BytesLength = 36;

    private static readonly ArrayPool<byte> s_arrayPool = ArrayPool<byte>.Shared;

    private AtContentAddressableArchive(long version, IEnumerable<Cid> roots, ReadOnlyMemory<byte> rawData)
    {
        Version = version;
        Roots = roots;
        RawData = rawData;
    }

    /// <summary>
    /// Gets the version of the CAR file.
    /// </summary>
    public long Version { get; }

    /// <summary>
    /// Gets the root <see cref="Cid"/>s.
    /// </summary>
    public IEnumerable<Cid> Roots { get; }

    /// <summary>
    /// Gets the raw data for the CAR.
    /// </summary>
    public ReadOnlyMemory<byte> RawData { get; }

    /// <summary>
    /// Decodes <paramref name="rawData"/> into a <see cref="AtContentAddressableArchive"/> instance. 
    /// </summary>
    /// <param name="rawData">The raw data to decode.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="ContentAddressableArchiveException">Thrown when <paramref name="rawData"/> cannot be decoded.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="rawData"/> is zero length.</exception>
    public static async Task<AtContentAddressableArchive?> DecodeAsync(ReadOnlyMemory<byte> rawData, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfZero(rawData.Length);

        Debug.WriteLine($"READ>> Decoding {rawData.Length} bytes");

        using (Stream stream = new MemoryStream(rawData.ToArray()))
        {
            // | ---------Header-------- |
            // [ varint | DAG-CBOR block ]

            // | ---------Header-------- |
            // [ varint ]
            long headerSize = await stream.ReadVarint64Async(cancellationToken: cancellationToken).ConfigureAwait(false);

            Debug.WriteLine($"READ>> Header Size {headerSize}");

            // | ---------Header-------- |
            // [        | DAG-CBOR block ]
            byte[] headerBlock = new byte[headerSize];
            await stream.ReadExactlyAsync(headerBlock, cancellationToken: cancellationToken).ConfigureAwait(false);
            CBORObject header = CBORObject.DecodeFromBytes(headerBlock);

            long version = header["version"].AsInt32();
            if (version != 1)
            {
                throw new ContentAddressableArchiveException($"Unexpected CAR version {version}");
            }

            ICollection<CBORObject> roots = header["roots"].Values;
            if (roots is null || roots.Count == 0)
            {
                throw new ContentAddressableArchiveException($"Missing root CID(s)");
            }

            List<Cid> rootCIDs = [];
            foreach (CBORObject root in roots)
            {
                if (root.HasOneTag())
                {
                    EInteger[] tags = root.GetAllTags();
                    int tag = tags[0].ToInt32Checked();

                    if (tag != 42)
                    {
                        throw new ContentAddressableArchiveException($"Header is not valid DAG-CBOR (tag is incorrect, {tag})");
                    }

                    Cid? rootAsCid = root.ToCid() ?? throw new ContentAddressableArchiveException("root cannot be converted to CID");
                    rootCIDs.Add(rootAsCid);
                }
                else
                {
                    throw new ContentAddressableArchiveException($"Header is not valid DAG-CBOR (tag is missing or has multiple tags)");
                }
            }

            if (rootCIDs.Count == 0)
            {
                throw new ContentAddressableArchiveException($"No root entries were convertible to CIDs");
            }
            else if (rootCIDs.Count != 1)
            {
                // The AtProto implementation only has a single data block which may not be valid DagCBOR
                throw new ContentAddressableArchiveException($"Multiple root CIDs were present");
            }

            // |---------------------------------- Data -----------------------------------|
            // [ varint | CID | block ] [ varint | CID | block ] [ varint | CID | block ] …
            long bytesLeft = rawData.Length - headerSize;
            int dataBlockCount = 0;

            byte[] cidBuffer = s_arrayPool.Rent(ATCidV1BytesLength);
            try
            {
                while (bytesLeft > 0)
                {
                    Debug.WriteLine($"Block # {++dataBlockCount}");
                    long sectionSize =
                        await stream.ReadVarint32Async(cancellationToken: cancellationToken).ConfigureAwait(false);

                    Debug.WriteLine($"READ>> Section Size {sectionSize}");

                    await stream.ReadExactlyAsync(cidBuffer, 0, ATCidV1BytesLength, cancellationToken: cancellationToken).ConfigureAwait(false);
                    Cid bodyCid = new(cidBuffer.AsSpan(0, ATCidV1BytesLength));

                    Debug.WriteLine($"READ>> Section cid {bodyCid}");

                    byte[] dataDagCborBuffer = new byte[sectionSize - ATCidV1BytesLength];

                    Debug.WriteLine($"READ>> Getting the rest {dataDagCborBuffer.Length} bytes");

                    await stream.ReadExactlyAsync(dataDagCborBuffer, cancellationToken).ConfigureAwait(false);

                    CBORObject dataBlockCbor = CBORObject.DecodeFromBytes(dataDagCborBuffer);
                    Debug.WriteLine(dataBlockCbor.Count);

                    bytesLeft -= sectionSize;
                }
            }
            finally
            {
                s_arrayPool.Return(cidBuffer);
            }

            AtContentAddressableArchive result = new(version, rootCIDs, rawData);

            return result;
        }
    }
}
