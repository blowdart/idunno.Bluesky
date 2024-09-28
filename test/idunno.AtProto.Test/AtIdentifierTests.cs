// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using Xunit;

namespace idunno.AtProto.Test
{
    [ExcludeFromCodeCoverage]
    public class AtIdentifierTests
    {
        // Test cases from https://github.com/bluesky-social/atproto/blob/main/interop-test-files/syntax/atidentifier_syntax_valid.txt
        [Theory]
        // Valid handles
        [InlineData("XX.LCS.MIT.EDU", typeof(Handle))]
        [InlineData("john.test", typeof(Handle))]
        [InlineData("jan.test", typeof(Handle))]
        [InlineData("a234567890123456789.test", typeof(Handle))]
        [InlineData("john2.test", typeof(Handle))]
        [InlineData("john-john.test", typeof(Handle))]

        // Valid DIDs
        [InlineData("did:method:val", typeof(Did))]
        [InlineData("did:method:VAL", typeof(Did))]
        [InlineData("did:method:val123", typeof(Did))]
        [InlineData("did:method:123", typeof(Did))]
        [InlineData("did:method:val-two", typeof(Did))]
        public void ValidAtIdentifiersShouldParseAndShouldHaveCorrectType(string input, Type expectedType)
        {
            bool result = AtIdentifier.TryParse(input, out AtIdentifier? atIdentifier);

            Assert.True(result);
            Assert.NotNull(atIdentifier);
            Assert.Equal(expectedType, atIdentifier.GetType());
        }

        // Test cases from https://github.com/bluesky-social/atproto/blob/main/interop-test-files/syntax/atidentifier_syntax_invalid.txt
        [Theory]
        // Invalid handles
        [InlineData("did:thing.test")]
        [InlineData("did:thing")]
        [InlineData("john -.test")]
        [InlineData("john.0:")]
        [InlineData("john.-")]
        [InlineData("xn--bcher-.tld")]
        [InlineData("john..test")]
        [InlineData("jo_hn.test")]

        // Invalid DIDs
        [InlineData("did")]
        [InlineData("didmethodval")]
        [InlineData("method:did:val")]
        [InlineData("did:method:")]
        [InlineData("didmethod:val")]
        [InlineData("did:methodval)")]
        [InlineData(":did:method:val")]
        [InlineData("did:method:val:")]
        [InlineData("did:method:val%")]
        [InlineData("DID:method:val")]

        // Other invalid stuff
        [InlineData("email@example.com")]
        [InlineData("@handle@example.com")]
        [InlineData("@handle")]
        [InlineData("blah")]
        public void InvalidAtIdentifiersShouldNotParse(string input)
        {
            bool result = AtIdentifier.TryParse(input, out AtIdentifier? atIdentifier);

            Assert.False(result);
            Assert.Null(atIdentifier);
        }
    }
}
