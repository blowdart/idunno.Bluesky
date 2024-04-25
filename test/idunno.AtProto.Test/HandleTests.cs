// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Xunit;

namespace idunno.AtProto.Test
{
    [ExcludeFromCodeCoverage]
    public class HandleTests
    {
        [Fact]
        public void InvalidHandleShouldReturnInvalidDotHandle()
        {
            Assert.Equal("handle.invalid", Handle.InvalidHandle.ToString());
        }

        [Theory]
        [InlineData("jay.bsky.social")]
        [InlineData("8.cn")]
        [InlineData("name.t--t")] //  not a real TLD, but syntax ok
        [InlineData("XX.LCS.MIT.EDU")]
        [InlineData("a.co")]
        [InlineData("xn--notarealidn.com")]
        [InlineData("xn--fiqa61au8b7zsevnm8ak20mc4a87e.xn--fiqs8s")]
        [InlineData("xn--ls8h.test")]
        [InlineData("example.t")] //  not a real TLD, but syntax ok
        public void SyntacticallyValidHandlesShouldConstructWithoutErrorsWithANormalizedValue(string value)
        {
            Handle actual = new(value);
            Assert.NotNull(actual);
            Assert.Equal (value.ToLowerInvariant(), actual.ToString());
        }

        [Theory]
        [InlineData("jo@hn.test", "\"jo@hn.test\" does not validate as a handle. (Parameter 's')")]
        [InlineData("💩.test", "\"💩.test\" does not validate as a handle. (Parameter 's')")]
        [InlineData("john..test", "\"john..test\" does not validate as a handle. (Parameter 's')")]
        [InlineData("xn--bcher -.tld", "\"xn--bcher -.tld\" does not validate as a handle. (Parameter 's')")]
        [InlineData("john.0", "\"john.0\" does not validate as a handle. (Parameter 's')")]
        [InlineData("cn.8", "\"cn.8\" does not validate as a handle. (Parameter 's')")]
        [InlineData("www.masełkowski.pl.com", "\"www.masełkowski.pl.com\" does not validate as a handle. (Parameter 's')")]
        [InlineData("org", "\"org\" is not a valid hostname. (Parameter 's')")]
        [InlineData("name.org.", "\"name.org.\" cannot begin or end with '.'. (Parameter 's')")]
        [InlineData(".name.org", "\".name.org\" cannot begin or end with '.'. (Parameter 's')")]
        [InlineData(".name.org.", "\".name.org.\" cannot begin or end with '.'. (Parameter 's')")]
        public void SyntacticallyInvalidHandlesShouldNotConstruct(string value, string expectedMessage)
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            {
                _ = new Handle(value);
            });

            Assert.Equal(expectedMessage, exception.Message);
        }

        [Theory]
        [InlineData("test.alt")]
        [InlineData("test.arpa")]
        [InlineData("test.example")]
        [InlineData("test.internal")]
        [InlineData("test.invalid")]
        [InlineData("test.local")]
        [InlineData("test.localhost")]
        [InlineData("test.onion")]
        public void InvalidTldHandlesShouldNotConstruct(string value)
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            {
                _ = new Handle(value);
            });

            string tld = value.Substring(value.LastIndexOf('.')+1);

            Assert.Equal($"\".{tld}\" is a disallowed TLD. (Parameter 's')", exception.Message);
        }

        [Fact]
        public void EmptyOrWhiteSpaceHandlesShouldThrowArgumentNullException()
        {
            ArgumentNullException emptyException = Assert.Throws<ArgumentNullException>(() =>
            {
                _ = new Handle("");
            });

            ArgumentNullException whiteSpaceException = Assert.Throws<ArgumentNullException>(() =>
            {
                _ = new Handle(" ");
            });
        }

        [Theory]
        [InlineData("jay.bsky.social")]
        [InlineData("8.cn")]
        [InlineData("name.t--t")] //  not a real TLD, but syntax ok
        [InlineData("XX.LCS.MIT.EDU")]
        [InlineData("a.co")]
        [InlineData("xn--notarealidn.com")]
        [InlineData("xn--fiqa61au8b7zsevnm8ak20mc4a87e.xn--fiqs8s")]
        [InlineData("xn--ls8h.test")]
        [InlineData("example.t")] //  not a real TLD, but syntax ok
        public void SyntacticallyValidHandlesTryParseShouldReturnTrueAndHandleWithNormalizedValue(string value)
        {
            Assert.True(Handle.TryParse(value, out Handle? actual));
            Assert.NotNull(actual);
            Assert.Equal(value.ToLowerInvariant(), actual.ToString());
        }

        [Theory]
        [InlineData("jo@hn.test")]
        [InlineData("💩.test")]
        [InlineData("john..test")]
        [InlineData("xn--bcher -.tld")]
        [InlineData("john.0")]
        [InlineData("cn.8")]
        [InlineData("www.masełkowski.pl.com")]
        [InlineData("org")]
        [InlineData("name.org.")]
        [InlineData(".name.org")]
        [InlineData(".name.org.")]
        public void SyntacticallyInvalidHandlesTryParseShouldReturnFalseAndNullHandle(string value)
        {
            Assert.False(Handle.TryParse(value, out Handle? actual));
            Assert.Null(actual);
        }

        [Fact]
        public void TooLongHandlesShouldNotConstruct()
        {
            string handle = new string('a', 251) + ".co";
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            {
                _ = new Handle(handle);
            });


            Assert.Equal($"\"{handle}\" length is greater than 253. (Parameter 's')", exception.Message);
        }

        [Fact]
        public void TooLongHandlesShouldFailTryParse()
        {
            string longString = new string('a', 251) + ".co";

            Assert.False(Handle.TryParse(longString, out Handle? handle));
            Assert.Null(handle);
        }

        [Theory]
        [InlineData("test.alt")]
        [InlineData("test.arpa")]
        [InlineData("test.example")]
        [InlineData("test.internal")]
        [InlineData("test.invalid")]
        [InlineData("test.local")]
        [InlineData("test.localhost")]
        [InlineData("test.onion")]
        public void InvalidTldHandlesShouldFailTryParse(string value)
        {
            Assert.False(Handle.TryParse(value, out Handle? handle));
            Assert.Null(handle);
        }

        [Fact]
        public void EmptyOrWhiteSpaceHandlesShouldFailTryParse()
        {
            Assert.False(Handle.TryParse("", out Handle? handle));
            Assert.Null(handle);

            Assert.False(Handle.TryParse(" ", out handle));
            Assert.Null(handle);
        }
    }
}
