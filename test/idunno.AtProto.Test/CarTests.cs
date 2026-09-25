// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo;

namespace idunno.AtProto.Test;

public class CarTests
{
    [Fact]
    public void WritesAndReadsCar()
    {
        Cid root = new("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4");
        Cid blockCid = new("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4");
        byte[] data = [1, 2, 3, 4];
        using MemoryStream stream = new();

        using (CarWriter writer = new(stream, new CarHeader([root]), leaveOpen: true))
        {
            writer.WriteBlock(new CarBlock(blockCid, data));
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
    public void ReadsCarHeaderBeforeBlocks()
    {
        using MemoryStream stream = new();
        using CarWriter writer = new(stream, new CarHeader([]), leaveOpen: true);

        stream.Position = 0;
        using CarReader reader = new(stream, leaveOpen: true);

        Assert.Throws<InvalidOperationException>(() => reader.ReadBlock());
    }

    [Fact]
    public void RejectsTruncatedBlock()
    {
        Cid cid = new("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4");
        using MemoryStream stream = new();
        using (CarWriter writer = new(stream, new CarHeader([]), leaveOpen: true))
        {
            writer.WriteBlock(new CarBlock(cid, new byte[] { 1, 2, 3 }));
        }

        byte[] bytes = stream.ToArray();
        Array.Resize(ref bytes, bytes.Length - 1);
        using CarReader reader = new(new MemoryStream(bytes));
        reader.ReadHeader();

        Assert.Throws<EndOfStreamException>(() => reader.ReadBlock());
    }
}
