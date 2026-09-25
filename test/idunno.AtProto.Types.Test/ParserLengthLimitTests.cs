// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Types.Test;

public class ParserLengthLimitTests
{
    [Fact]
    public void AtUriLengthIsCheckedBeforeAnyOtherValidation()
    {
        // The authority is also far too long to be a valid DID, so reporting the length
        // proves the cheap length check runs before the string is scanned and split.
        string value = "at://did:plc:" + new string('a', 9000);

        AtUriFormatException exception = Assert.Throws<AtUriFormatException>(() => new AtUri(value));

        Assert.Contains("too long", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NsidLengthIsCheckedBeforeAnyOtherValidation()
    {
        // The trailing characters are not legal in an NSID either, so reporting the length
        // proves the length check runs before the validation regexes.
        string value = "com.example." + new string('!', 400);

        NsidFormatException exception = Assert.Throws<NsidFormatException>(() => new Nsid(value));

        Assert.Contains("too long", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("at://did:plc:identifier/test.idunno.lexiconType/rkey")]
    [InlineData("at://did:plc:identifier/test.idunno.lexiconType")]
    [InlineData("at://did:plc:identifier")]
    public void AtUrisWithinTheLengthLimitAreStillAccepted(string value)
    {
        Assert.True(AtUri.TryParse(value, out AtUri? _));
    }

    [Theory]
    [InlineData("com.example.status")]
    [InlineData("app.bsky.feed.post")]
    public void NsidsWithinTheLengthLimitAreStillAccepted(string value)
    {
        Assert.True(Nsid.TryParse(value, out Nsid? _));
    }
}
