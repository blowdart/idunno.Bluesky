// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Test
{
    [ExcludeFromCodeCoverage]
    public class StringExtensionTests
    {
        [Theory]
        [InlineData("Hello", 5, 5)]
        [InlineData("👨‍👩‍👧‍👧", 11, 1)]
        [InlineData("🤦🏼‍♂️", 7, 1)]
        [InlineData("💩", 2, 1)]
        [InlineData("\"", 1, 1)]
        public void LengthChecks(string text, int expectedLength, int expectedGraphemeLength)
        {
            Assert.Equal(expectedLength, text.Length);
            Assert.Equal(expectedGraphemeLength, text.GetGraphemeLength());
        }
    }
}
