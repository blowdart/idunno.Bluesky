// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace idunno.Bluesky.Test;

public class StringExtensionTests
{
    [Fact]
    public void GetUtf8LengthReturnsCorrectLength()
    {
        string input = "Hello, 世界"; // "Hello, World" in Chinese
        int expectedLength = Encoding.UTF8.GetByteCount(input);
        int actualLength = input.GetUtf8Length();
        Assert.Equal(expectedLength, actualLength);
    }

    [Fact]
    public void GetUtf8LengthWithEmptyStringReturnsZero()
    {
        // Arrange
        string input = string.Empty;
        int expectedLength = 0;
        int actualLength = input.GetUtf8Length();
        Assert.Equal(expectedLength, actualLength);
    }

    [Fact]
    public void GetUtf8LengthWithMaximumBlueskyPostLengthReturns300()
    {
        string input = "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890";
        int expectedLength = 300;
        int actualLength = input.GetUtf8Length();
        Assert.Equal(expectedLength, actualLength);
    }
}
