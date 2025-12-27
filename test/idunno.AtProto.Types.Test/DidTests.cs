// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Types.Test
{
    public class DidTests
    {
        // Test cases from https://github.com/bluesky-social/atproto/blob/main/interop-test-files/syntax/did_syntax_valid.txt
        [Theory]
        [InlineData("did:method:val")]
        [InlineData("did:method:VAL")]
        [InlineData("did:method:val123")]
        [InlineData("did:method:123")]
        [InlineData("did:method:val-two")]
        [InlineData("did:method:val_two")]
        [InlineData("did:method:val.two")]
        [InlineData("did:method:val:two")]
        [InlineData("did:method:val%BB")]
        [InlineData("did:method:vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv")]
        [InlineData("did:m:v")]
        [InlineData("did:method::::val")]
        [InlineData("did:method:-")]
        [InlineData("did:method:-:_:.:%ab")]
        [InlineData("did:method:.")]
        [InlineData("did:method:_")]
        [InlineData("did:method::.")]

        // Real world DIDs
        [InlineData("did:onion:2gzyxa5ihm7nsggfxnu52rck2vv4rvmdlkiu3zzui5du4xyclen53wid")]
        [InlineData("did:example:123456789abcdefghi")]
        [InlineData("did:plc:7iza6de2dwap2sbkpav7c6c6")]
        [InlineData("did:web:example.com")]
        [InlineData("did:web:localhost%3A1234")]
        [InlineData("did:key:zQ3shZc2QzApp2oymGvQbzP8eKheVshBHbU4ZYjeXqwSKEn6N")]
        [InlineData("did:ethr:0xb9c5714089478a327f09197987f16f9e5d936e8aN")]
        public void ValidDidsShouldConstruct(string input)
        {
            Did did = new(input);

            Assert.NotNull(did);
            Assert.Equal(input, did.Value);
            Assert.Equal(input, did.ToString());
        }

        // Test cases from https://github.com/bluesky-social/atproto/blob/main/interop-test-files/syntax/did_syntax_invalid.txt
        [Theory]
        [InlineData("did")]
        [InlineData("didmethodval")]
        [InlineData("method:did:val")]
        [InlineData("did:method:")]
        [InlineData("didmethod:val")]
        [InlineData("did:methodval")]
        [InlineData(":did:method:val")]
        [InlineData("did.method.val")]
        [InlineData("did:method:val:")]
        [InlineData("did:method:val%")]
        [InlineData("DID:method:val")]
        [InlineData("did:METHOD:val")]
        [InlineData("did:m123:val")]
        [InlineData("did:method:val/two")]
        [InlineData("did:method:val?two")]
        [InlineData("did:method:val#two")]
        [InlineData("did:method:vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv")]
        public void InvalidDidsShouldNotConstruct(string input)
        {
            Exception? exception = Record.Exception(() =>
            {
                Did did = new(input);
            });

            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }

        [Theory]
        [InlineData("did:method:val")]
        [InlineData("did:method:VAL")]
        [InlineData("did:method:val123")]
        [InlineData("did:method:123")]
        [InlineData("did:method:val-two")]
        [InlineData("did:method:val_two")]
        [InlineData("did:method:val.two")]
        [InlineData("did:method:val:two")]
        [InlineData("did:method:val%BB")]
        [InlineData("did:method:vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv")]
        [InlineData("did:m:v")]
        [InlineData("did:method::::val")]
        [InlineData("did:method:-")]
        [InlineData("did:method:-:_:.:%ab")]
        [InlineData("did:method:.")]
        [InlineData("did:method:_")]
        [InlineData("did:method::.")]

        // Real world DIDs
        [InlineData("did:onion:2gzyxa5ihm7nsggfxnu52rck2vv4rvmdlkiu3zzui5du4xyclen53wid")]
        [InlineData("did:example:123456789abcdefghi")]
        [InlineData("did:plc:7iza6de2dwap2sbkpav7c6c6")]
        [InlineData("did:web:example.com")]
        [InlineData("did:web:localhost%3A1234")]
        [InlineData("did:key:zQ3shZc2QzApp2oymGvQbzP8eKheVshBHbU4ZYjeXqwSKEn6N")]
        [InlineData("did:ethr:0xb9c5714089478a327f09197987f16f9e5d936e8aN")]
        public void TryParseOnValidDidsShouldBeSuccessful(string input)
        {
            bool parseResult = Did.TryParse(input, out Did? result);

            Assert.True(parseResult);
            Assert.NotNull(result);
        }

        // Test cases from https://github.com/bluesky-social/atproto/blob/main/interop-test-files/syntax/did_syntax_invalid.txt
        [Theory]
        [InlineData("did")]
        [InlineData("didmethodval")]
        [InlineData("method:did:val")]
        [InlineData("did:method:")]
        [InlineData("didmethod:val")]
        [InlineData("did:methodval")]
        [InlineData(":did:method:val")]
        [InlineData("did.method.val")]
        [InlineData("did:method:val:")]
        [InlineData("did:method:val%")]
        [InlineData("DID:method:val")]
        [InlineData("did:METHOD:val")]
        [InlineData("did:m123:val")]
        [InlineData("did:method:val/two")]
        [InlineData("did:method:val?two")]
        [InlineData("did:method:val#two")]
        [InlineData("did:method:vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv")]
        public void TryParseOnInvalidDidsShouldReturnFalse(string input)
        {
            bool parseResult = Did.TryParse(input, out Did? result);

            Assert.False(parseResult);
            Assert.Null(result);
        }

        [Fact]
        public void ImplicitConversionFromValidStringWorks()
        {
            const string value = "did:method:val";

            Did did = value;

            Assert.NotNull(did);
            Assert.Equal(value, did.Value);
        }

        [Fact]
        public void ExplicitConversionFromValidStringWorks()
        {
            const string value = "did:method:val";

            Did did = (Did)value;

            Assert.NotNull(did);
            Assert.Equal(value, did.Value);

            AtIdentifier did2 = Did.Create(value);
            Assert.NotNull(did2);
            Assert.Equal(value, did2.Value);
            Assert.Equal(did, did2);
        }

        [Fact]
        public void EqualityWorks()
        {
            Did lhs = new("did:method:val");
            Did rhs = new("did:method:val");

            Assert.NotNull(lhs);
            Assert.NotNull(rhs);
            Assert.Equal(lhs, rhs);
            Assert.True(lhs.Equals(rhs));
        }

        [InlineData("did:example:123456789abcdefghi", "example")]
        [InlineData("did:plc:7iza6de2dwap2sbkpav7c6c6", "plc")]
        [InlineData("did:web:example.com", "web")]
        [InlineData("did:web:localhost%3A1234", "web")]
        [InlineData("did:key:zQ3shZc2QzApp2oymGvQbzP8eKheVshBHbU4ZYjeXqwSKEn6N", "key")]
        [InlineData("did:ethr:0xb9c5714089478a327f09197987f16f9e5d936e8aN", "ethr")]
        [Theory]
        public void MethodShouldBeExtractedWithValidDids(string input, string expectedMethod)
        {
            Did did = new(input);

            Assert.NotNull(did);
            Assert.Equal(input, did.Value);
            Assert.Equal(input, did.ToString());
            Assert.Equal(expectedMethod, did.Method);
        }
    }
}
