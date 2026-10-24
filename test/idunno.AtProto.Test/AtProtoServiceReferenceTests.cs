// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Test;

public class AtProtoServiceReferenceTests
{
    [Theory]
    [InlineData("atproto_labeler")]
    [InlineData("service-1._~!$&'()*+,;=:@/?")]
    [InlineData("escaped%20fragment")]
    public void PreservesValidServiceReferences(string fragment)
    {
        Did did = new("did:plc:abcdefghijklmnopqrstuvwx");
        AtProtoServiceReference reference = new(did, fragment);
        Assert.Same(did, reference.Did);
        Assert.Equal(fragment, reference.Fragment);
        Assert.Equal($"{did}#{fragment}", reference.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("#atproto_labeler")]
    [InlineData("embedded#fragment")]
    [InlineData("white space")]
    [InlineData("control\r\n")]
    [InlineData("invalid%")]
    [InlineData("invalid%2")]
    [InlineData("invalid%GG")]
    [InlineData("nonasciié")]
    public void RejectsInvalidServiceFragments(string fragment) =>
        Assert.Throws<ArgumentException>(() => new AtProtoServiceReference(new Did("did:plc:abcdefghijklmnopqrstuvwx"), fragment));

    [Fact]
    public void RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new AtProtoServiceReference(null!, "atproto_labeler"));
        Assert.Throws<ArgumentNullException>(() => new AtProtoServiceReference(new Did("did:plc:abcdefghijklmnopqrstuvwx"), null!));
    }
}
