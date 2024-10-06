// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace idunno.AtProto.Test
{
    [ExcludeFromCodeCoverage]
    public class RecordKeyTests
    {
        [Theory]
        // Test data from https://github.com/bluesky-social/atproto-interop-tests/blob/main/syntax/recordkey_syntax_valid.txt
        // specs
        [InlineData("self")]
        [InlineData("example.com")]
        [InlineData("~1.2-3_")]
        [InlineData("dHJ1ZQ")]
        [InlineData("_")]
        [InlineData("literal:self")]
        [InlineData("pre:fix")]
        // more corner-cases
        [InlineData(":")]
        [InlineData("-")]
        [InlineData("~")]
        [InlineData("self.")]
        [InlineData("lang:")]
        [InlineData(":lang")]
        // very long: 'o'.repeat(512)
        [InlineData("oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo")]
        void ValidRecordKeysShouldConstructWithoutError(string value)
        {
            RecordKey recordKey = new (value);
            Assert.NotNull(recordKey);
            Assert.Equal(value, recordKey.Value);
        }

        [Theory]
        // Test data from https://github.com/bluesky-social/atproto-interop-tests/blob/main/syntax/recordkey_syntax_invalid.txt
        // specs
        [InlineData("alpha/beta")]
        [InlineData(".")]
        [InlineData("..")]
        // extra
        [InlineData("@handle")]
        [InlineData("any space")]
        [InlineData("any+space")]
        [InlineData("number[3]")]
        [InlineData("number(3)")]
        [InlineData("\"quote\"")]
        [InlineData("dHJ1ZQ==")]
        // too long: 'o'.repeat(513)
        [InlineData("ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo")]
        void InvalidRecordKeysShouldThrowOnConstruction(string value)
        {
            Assert.Throws<RecordKeyFormatException>((Action)(() =>
            {
                _ = new RecordKey(value);
            }));
        }

        [Fact]
        public void ValidStringsCanBeImplicitlyConverted()
        {
            const string value = "example.com";

            RecordKey convertedRecordKey = value;

            Assert.NotNull(convertedRecordKey);
            Assert.Equal(value, convertedRecordKey.ToString());
            Assert.Equal(value, convertedRecordKey.Value);
        }
    }
}
