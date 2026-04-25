// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using PeterO.Cbor;

namespace idunno.AtProto.FireHose;

/// <summary>
/// Encapsulates the contents of the frame body for a sync operation.
/// </summary>
public sealed record SyncPayload : IFramePayload, ICborConvertor<SyncPayload>
{
    /// <summary>
    /// Creates a new instance of <see cref="SyncPayload"/>.
    /// </summary>
    /// <param name="seq">The stream sequence number of this message.</param>
    /// <param name="did">The account this repo event corresponds to. Must match that in the commit object.</param>
    /// <param name="blocks">CAR file containing the commit, as a block. The CAR header must include the commit block CID as the first 'root'.</param>
    /// <param name="rev">The rev of the commit. This value must match that in the commit object.</param>
    /// <param name="time">The <see cref="DateTimeOffset"/> when this message was originally broadcast.</param>
    public SyncPayload(long seq, Did did, byte[] blocks, string rev, DateTimeOffset time)
    {
        Seq = seq;
        Did = did;
        Blocks = blocks.AsReadOnly();
        Rev = rev;
        Time = time;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cborObject"/> is <see langword="null"/>.</exception>
    public static SyncPayload FromCBORObject(CBORObject cborObject)
    {
        ArgumentNullException.ThrowIfNull(cborObject);

        return new SyncPayload(
            cborObject["seq"].AsInt64Value(),
            new Did(cborObject["did"].AsString()),
            cborObject["blocks"].GetByteString(),
            cborObject["rev"].AsString(),
            cborObject["time"].ToDateTimeOffset());
    }

    /// <summary>
    /// Gets the stream sequence number of this message.
    /// </summary>
    public long Seq { get; }

    /// <summary>
    /// Gets the account this repo event corresponds to. Must match that in the commit object.
    /// </summary>
    public Did Did { get; }

    /// <summary>
    /// Gets the CAR file containing the commit, as a block. The CAR header must include the commit block CID as the first 'root'.
    /// </summary>
    public IReadOnlyList<byte> Blocks { get; }

    /// <summary>
    /// Gets the rev of the commit. This value must match that in the commit object.
    /// </summary>
    public string Rev { get; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when this message was originally broadcast.
    /// </summary>
    public DateTimeOffset Time { get; }
}
