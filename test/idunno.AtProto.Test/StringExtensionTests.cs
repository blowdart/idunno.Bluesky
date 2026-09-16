// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class StringExtensionTests
{
    [Theory]
    [InlineData("Hello", 5, 5, 5)]
    [InlineData("👨‍👩‍👧‍👧", 11, 1, 25)]
    [InlineData("🤦🏼‍♂️", 7, 1, 17)]
    [InlineData("💩", 2, 1, 4)]
    [InlineData("\"", 1, 1, 1)]
    [InlineData("é", 1, 1, 2)]
    [InlineData("€", 1, 1, 3)]
    [InlineData("世界", 2, 2, 6)]
    [InlineData("", 0, 0, 0)]
    public void LengthChecks(string text, int expectedLength, int expectedGraphemeLength, int expectedUtf8Length)
    {
        Assert.Equal(expectedLength, text.Length);
        Assert.Equal(expectedGraphemeLength, text.GetGraphemeLength());
        Assert.Equal(expectedUtf8Length, text.GetUtf8Length());

        // The UTF-8 byte count is never smaller than the UTF-16 character count, which is why measuring a
        // maxLength limit with string.Length is always too permissive and never too strict.
        Assert.True(text.GetUtf8Length() >= text.Length);
    }
}