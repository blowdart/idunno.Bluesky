// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Repo;

/// <summary>
/// A content-addressed block in a CAR version 1 file.
/// </summary>
public sealed class CarBlock
{
    /// <summary>
    /// Creates a new <see cref="CarBlock"/>.
    /// </summary>
    /// <param name="cid">The content identifier of the block.</param>
    /// <param name="data">The block data.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cid"/> or <paramref name="data"/> is <see langword="null"/>.</exception>
    public CarBlock(Cid cid, ReadOnlyMemory<byte> data)
    {
        ArgumentNullException.ThrowIfNull(cid);

        Cid = cid;
        Data = data;
    }

    /// <summary>
    /// Gets the content identifier of the block.
    /// </summary>
    public Cid Cid { get; }

    /// <summary>
    /// Gets the block data.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; }
}
