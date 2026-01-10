// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Test
{
    [ExcludeFromCodeCoverage]
    public class RichTextTests
    {
        [Theory]
        [InlineData("Hello", 5, 5, 5)]
        [InlineData("👨‍👩‍👧‍👧", 11, 1, 25)]
        [InlineData("🤦🏼‍♂️", 7, 1, 17)]
        [InlineData("💩", 2, 1, 4)]
        [InlineData("\"", 1, 1, 1)]
        public void LengthChecks(string text, int expectedLength, int expectedGraphemeLength, int expectedUtf8Length)
        {
            Post postRecord = new(text);

            Assert.Equal(expectedLength, postRecord.Length);
            Assert.Equal(expectedGraphemeLength, postRecord.GraphemeLength);
            Assert.Equal(expectedUtf8Length, postRecord.Utf8Length);
        }
    }
}
