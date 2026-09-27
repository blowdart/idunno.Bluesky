// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;

namespace idunno.AtProto.Types.Test;

public class CidFromDagCborTests
{
    [Fact]
    public void FromDagCborProducesAVersionOneDagCborCid()
    {
        Cid cid = Cid.FromDagCbor([0x01, 0x02, 0x03]);

        Assert.Equal(1, cid.Version);
        Assert.Equal((ulong)0x71, cid.Codec);
    }

    [Fact]
    public void FromDagCborProducesASha256Multihash()
    {
        byte[] content = [0x0a, 0x0b, 0x0c];

        Cid cid = Cid.FromDagCbor(content);

        Assert.Equal(34, cid.Hash.Count);
        Assert.Equal(0x12, cid.Hash[0]);
        Assert.Equal(32, cid.Hash[1]);
        Assert.Equal(SHA256.HashData(content), cid.Hash.Skip(2));
    }

    [Fact]
    public void FromDagCborProducesACidWhichRoundTripsThroughItsStringRepresentation()
    {
        Cid cid = Cid.FromDagCbor([0x01, 0x02, 0x03]);

        Assert.Equal(cid, new Cid(cid.Value));
    }

    [Fact]
    public void FromDagCborProducesTheSameCidForTheSameContent()
    {
        Assert.Equal(Cid.FromDagCbor([0x01, 0x02]), Cid.FromDagCbor([0x01, 0x02]));
    }

    [Fact]
    public void FromDagCborProducesADifferentCidForDifferentContent()
    {
        Assert.NotEqual(Cid.FromDagCbor([0x01, 0x02]), Cid.FromDagCbor([0x01, 0x03]));
    }

    [Theory]
    // An empty DAG-CBOR map, whose CID is widely used as a test vector.
    [InlineData(new byte[] { 0xa0 }, "bafyreigbtj4x7ip5legnfznufuopl4sg4knzc2cof6duas4b3q2fy6swua")]
    // A DAG-CBOR map of { "hello": "world" }.
    [InlineData(
        new byte[] { 0xa1, 0x65, 0x68, 0x65, 0x6c, 0x6c, 0x6f, 0x65, 0x77, 0x6f, 0x72, 0x6c, 0x64 },
        "bafyreidykglsfhoixmivffc5uwhcgshx4j465xwqntbmu43nb2dzqwfvae")]
    public void FromDagCborMatchesKnownContentIdentifiers(byte[] content, string expected)
    {
        Assert.Equal(expected, Cid.FromDagCbor(content).Value);
    }

    [Fact]
    public void FromDagCborRejectsEmptyContent()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Cid.FromDagCbor([]));
    }
}
