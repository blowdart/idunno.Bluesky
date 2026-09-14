// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Types.Test;

public class AtCidTests
{
    const string ValidCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

    const string ValidCidV0 = "QmbWqxBEKC3P8tqsKc98xmWNzrzDtRLMiMPL8wBuTGsMnR";

    [Fact]
    public void ValidAtCidValuesConstructCorrectly()
    {
        Cid atCid = new(ValidCid);

        Assert.Equal(ValidCid, atCid.Value);
    }

    [Fact]
    public void EmptyValueConstructionShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new Cid(string.Empty));
    }

    [Fact]
    public void ImplicitConversionFromValidStringWorks()
    {
        Cid atCid = ValidCid;

        Assert.NotNull(atCid);
        Assert.Equal(ValidCid, atCid.Value);
    }

    [Fact]
    public void ExplicitConversionFromValidStringWorks()
    {
        Cid atCid = (Cid)ValidCid;

        Assert.NotNull(atCid);
        Assert.Equal(ValidCid, atCid.Value);
    }

    [Fact]
    public void EqualityWorks()
    {
        Cid lhs = (Cid)ValidCid;
        Cid rhs = new(ValidCid);

        Assert.NotNull(lhs);
        Assert.NotNull(rhs);
        Assert.Equal(lhs, rhs);
        Assert.True(lhs.Equals(rhs));
    }

    [Fact]
    public void ConstructingFromPropertiesProducesSameStringRepresentation()
    {
        Cid atCidFromString = ValidCid;

        Cid atCidFromProperties = new(atCidFromString.Version, atCidFromString.Codec, [.. atCidFromString.Hash]);

        Assert.NotNull(atCidFromString);
        Assert.NotNull(atCidFromProperties);

        Assert.Equal(atCidFromString.Version, atCidFromProperties.Version);
        Assert.Equal(atCidFromString.Codec, atCidFromProperties.Codec);
        Assert.Equal(atCidFromString.Hash, atCidFromProperties.Hash);
        Assert.Equal(atCidFromString.ToString(), atCidFromProperties.ToString());
    }

    [Theory]
    [InlineData(ValidCid)]
    [InlineData(ValidCidV0)]
    public void TryParseReturnsTrueAndTheSameValueAsTheConstructorForAValidCid(string value)
    {
        Assert.True(Cid.TryParse(value, out Cid? result));

        Assert.NotNull(result);
        Assert.Equal(new Cid(value), result);
    }

    [Theory]
    [InlineData(ValidCid)]
    [InlineData(ValidCidV0)]
    public void TryParseRoundTripsToTheOriginalString(string value)
    {
        Assert.True(Cid.TryParse(value, out Cid? result));

        Assert.NotNull(result);
        Assert.Equal(value, result.Value);
    }

    [Theory]
    [InlineData(ValidCid)]
    [InlineData(ValidCidV0)]
    public void TheStringRepresentationOfACidParsesBackToAnEqualCid(string value)
    {
        // CIDv0 is base58btc, which is case sensitive, so case normalizing it here would produce a value
        // which no longer round trips.
        Cid cid = new(value);

        Assert.True(Cid.TryParse(cid.Value, out Cid? reparsed));

        Assert.NotNull(reparsed);
        Assert.Equal(cid, reparsed);
        Assert.Equal(value, reparsed.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TryParseReturnsFalseAndANullResultForANullOrEmptyValue(string? value)
    {
        Assert.False(Cid.TryParse(value, out Cid? result));

        Assert.Null(result);
    }

    [Theory]
    [InlineData("not a cid")]
    [InlineData("!!!!")]
    [InlineData(" ")]
    [InlineData("Qm")]
    [InlineData("QmTooShortToBeAVersionZeroCid")]
    [InlineData("Qm0OIl000000000000000000000000000000000000000")]
    public void TryParseReturnsFalseAndANullResultForAMalformedValue(string value)
    {
        Assert.False(Cid.TryParse(value, out Cid? result));

        Assert.Null(result);
    }

    [Theory]
    [InlineData("not a cid")]
    [InlineData("!!!!")]
    [InlineData("Qm0OIl000000000000000000000000000000000000000")]
    public void TryParseReturnsFalseWhereTheConstructorThrows(string value)
    {
        // Without this the malformed value tests would still pass if TryParse stopped catching, as long as
        // the value happened not to be one the constructor rejects.
        Assert.ThrowsAny<ArgumentException>(() => new Cid(value));

        Assert.False(Cid.TryParse(value, out _));
    }
}