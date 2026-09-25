// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Types.Test;

public class CidValidationTests
{
    [Theory]
    // A version byte followed only by continuation bytes, so the varint never terminates.
    [InlineData(new byte[] { 0x01, 0x80, 0x80, 0x80 }, "Varint is truncated.")]
    // A version byte and a complete codec varint, but no multihash at all.
    [InlineData(new byte[] { 0x01, 0x71 }, "Value contains no multihash.")]
    // A varint longer than the nine byte maximum imposed by the multiformats specification.
    [InlineData(new byte[] { 0x01, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x01, 0x12 }, "Varint is longer than the maximum of 9 bytes.")]
    public void MalformedCidBytesAreRejectedRatherThanProducingAnEmptyHash(byte[] bytes, string expectedMessage)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Cid(bytes));

        Assert.NotNull(exception.InnerException);
        Assert.Contains(expectedMessage, exception.InnerException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedVersionInBytesReportsTheVersionNumber()
    {
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(() => new Cid(new byte[] { 0x42, 0x71, 0x12, 0x20 }));

        Assert.NotNull(exception.InnerException);
        Assert.Contains("Version 66", exception.InnerException.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("00-00", exception.InnerException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConstructorRejectsANullHash()
    {
        Assert.Throws<ArgumentNullException>(() => new Cid(1, 0x71, null!));
    }

    [Fact]
    public void ConstructorRejectsAnEmptyHash()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cid(1, 0x71, []));
    }

    [Theory]
    [InlineData((byte)2)]
    [InlineData((byte)255)]
    public void ConstructorRejectsAnUnsupportedVersion(byte version)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cid(version, 0x71, [0x12, 0x20, 0x01]));
    }

    [Fact]
    public void ConstructorCopiesTheSuppliedHash()
    {
        byte[] hash = [0x12, 0x20, 0x01];
        Cid cid = new(1, 0x71, hash);

        string before = cid.Value;
        hash[2] = 0xFF;

        Assert.Equal(before, cid.Value);
    }

    [Fact]
    public void ValidCidStillRoundTripsThroughBytes()
    {
        Cid cid = new("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4");
        Cid roundTripped = new(cid.ToBytes());

        Assert.Equal(cid, roundTripped);
        Assert.Equal(cid.Value, roundTripped.Value);
    }
}
