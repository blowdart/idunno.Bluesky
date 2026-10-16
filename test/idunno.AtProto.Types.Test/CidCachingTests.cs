// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace idunno.AtProto.Types.Test;

public class CidCachingTests
{
    private const string ValidCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

    private const string ValidCidV0 = "QmbWqxBEKC3P8tqsKc98xmWNzrzDtRLMiMPL8wBuTGsMnR";

    [Theory]
    [InlineData(ValidCid)]
    [InlineData(ValidCidV0)]
    public void ToStringIsCachedAndRoundTrips(string value)
    {
        Cid cid = new(value);

        string first = cid.ToString();

        Assert.Equal(value, first);
        Assert.Same(first, cid.ToString());
    }

    [Theory]
    [InlineData(ValidCid)]
    [InlineData(ValidCidV0)]
    public void HashCannotBeUsedToChangeTheCid(string value)
    {
        Cid cid = new(value);
        int hashCode = cid.GetHashCode();

        Assert.IsNotType<byte[]>(cid.Hash);
        Assert.Throws<NotSupportedException>(() => ((IList<byte>)cid.Hash)[0] = 0);

        Assert.Equal(value, cid.ToString());
        Assert.Equal(hashCode, cid.GetHashCode());
        Assert.Equal(new Cid(value), cid);
    }

    [Fact]
    public void HashCodeIsStableAndMatchesForEqualCids()
    {
        Cid first = new(ValidCid);
        Cid second = new(ValidCid);

        Assert.Equal(first.GetHashCode(), first.GetHashCode());
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first.GetHashCode(), new Cid(ValidCidV0).GetHashCode());
    }

    [Fact]
    public void FromDagCborProducesAnEqualCidToOneParsedFromItsBytes()
    {
        Cid cid = Cid.FromDagCbor(Encoding.UTF8.GetBytes("content"));

        byte[] bytes = cid.ToBytes();
        Cid parsed = new(bytes);

        Assert.Equal(cid, parsed);
        Assert.Equal(cid.GetHashCode(), parsed.GetHashCode());
        Assert.Equal(cid.ToString(), parsed.ToString());
        Assert.Equal(bytes, parsed.ToBytes());
    }

    [Fact]
    public void ToBytesReturnsANewArrayEachCall()
    {
        Cid cid = new(ValidCid);

        byte[] first = cid.ToBytes();
        first[^1] ^= 0xFF;

        Assert.NotEqual(first, cid.ToBytes());
        Assert.Equal(ValidCid, cid.ToString());
    }
}
