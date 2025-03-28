// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Test
{
    [ExcludeFromCodeCoverage]
    public class HandleTests
    {
        [Fact]
        public void InvalidHandleShouldReturnInvalidDotHandle()
        {
            Assert.Equal("handle.invalid", Handle.Invalid.ToString());
        }

        [Theory]
        [InlineData("jay.bsky.social")]
        [InlineData("name.t--t")] //  not a real TLD, but syntax ok
        [InlineData("xn--notarealidn.com")]
        [InlineData("example.t")] //  not a real TLD, but syntax ok

        // Test cases from https://github.com/bluesky-social/atproto/blob/main/interop-test-files/syntax/handle_syntax_valid.txt
        [InlineData("A.ISI.EDU")]
        [InlineData("XX.LCS.MIT.EDU")]
        [InlineData("SRI-NIC.ARPA")]
        [InlineData("john.test")]
        [InlineData("jan.test")]
        [InlineData("a234567890123456789.test")]
        [InlineData("john2.test")]
        [InlineData("john-john.test")]
        [InlineData("john.bsky.app")]
        [InlineData("jo.hn")]
        [InlineData("a.co")]
        [InlineData("a.org")]
        [InlineData("joh.n")]
        [InlineData("j0.h0")]
        [InlineData("jaymome-johnber123456.test")]
        [InlineData("jay.mome-johnber123456.test")]
        [InlineData("john.test.bsky.app")]

        // max over all handle: 'shoooort' + '.loooooooooooooooooooooooooong'.repeat(8) + '.test'
        [InlineData("shoooort.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.test")]

        // max segment: 'short.' + 'o'.repeat(63) + '.test'
        [InlineData("short.ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo.test")]

        // NOTE: this probably isn't ever going to be a real domain, but my read of the RFC is that it would be possible
        [InlineData("john.t")]

        // allows punycode handles
        // 💩.test
        [InlineData("xn--ls8h.test")]
        // bücher.tld
        [InlineData("xn--bcher-kva.tld")]
        [InlineData("xn--3jk.com")]
        [InlineData("xn--w3d.com")]
        [InlineData("xn--vqb.com")]
        [InlineData("xn--ppd.com")]
        [InlineData("xn--cs9a.com")]
        [InlineData("xn--8r9a.com")]
        [InlineData("xn--cfd.com")]
        [InlineData("xn--5jk.com")]
        [InlineData("xn--2lb.com")]

        // allows onion (Tor) handles
        [InlineData("expyuzz4wqqyqhjn.onion")]
        [InlineData("friend.expyuzz4wqqyqhjn.onion")]
        [InlineData("g2zyxa5ihm7nsggfxnu52rck2vv4rvmdlkiu3zzui5du4xyclen53wid.onion")]
        [InlineData("friend.g2zyxa5ihm7nsggfxnu52rck2vv4rvmdlkiu3zzui5du4xyclen53wid.onion")]
        [InlineData("2gzyxa5ihm7nsggfxnu52rck2vv4rvmdlkiu3zzui5du4xyclen53wid.onion")]
        [InlineData("friend.2gzyxa5ihm7nsggfxnu52rck2vv4rvmdlkiu3zzui5du4xyclen53wid.onion")]

        // correctly validates corner cases (modern vs. old RFCs)
        [InlineData("12345.test")]
        [InlineData("8.cn")]
        [InlineData("4chan.org")]
        [InlineData("4chan.o-g")]
        [InlineData("blah.4chan.org")]
        [InlineData("thing.a01")]
        [InlineData("120.0.0.1.com")]
        [InlineData("0john.test")]
        [InlineData("9sta--ck.com")]
        [InlineData("99stack.com")]
        [InlineData("0ohn.test")]
        [InlineData("john.t--t")]
        [InlineData("thing.0aa.thing")]

        // examples from stackoverflow
        [InlineData("stack.com")]
        [InlineData("sta-ck.com")]
        [InlineData("sta---ck.com")]
        [InlineData("sta--ck9.com")]
        [InlineData("stack99.com")]
        [InlineData("sta99ck.com")]
        [InlineData("google.com.uk")]
        [InlineData("google.co.in")]
        [InlineData("google.com")]
        [InlineData("maselkowski.pl")]
        [InlineData("m.maselkowski.pl")]
        [InlineData("xn--masekowski-d0b.pl")]
        [InlineData("xn--fiqa61au8b7zsevnm8ak20mc4a87e.xn--fiqs8s")]
        [InlineData("xn--stackoverflow.com")]
        [InlineData("stackoverflow.xn--com")]
        [InlineData("stackoverflow.co.uk")]
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

        [Fact]
        public void EmptyOrWhiteSpaceHandlesShouldThrowArgumentNullException()
        {
            ArgumentException emptyException = Assert.Throws<ArgumentException>(() =>
            {
                _ = new Handle("");
            });

            ArgumentException whiteSpaceException = Assert.Throws<ArgumentException>(() =>
            {
                _ = new Handle(" ");
            });
        }

        [Theory]
        [InlineData("jay.bsky.social")]
        [InlineData("name.t--t")] //  not a real TLD, but syntax ok
        [InlineData("xn--notarealidn.com")]
        [InlineData("example.t")] //  not a real TLD, but syntax ok

        // Test cases from https://github.com/bluesky-social/atproto/blob/main/interop-test-files/syntax/handle_syntax_valid.txt
        [InlineData("A.ISI.EDU")]
        [InlineData("XX.LCS.MIT.EDU")]
        [InlineData("SRI-NIC.ARPA")]
        [InlineData("john.test")]
        [InlineData("jan.test")]
        [InlineData("a234567890123456789.test")]
        [InlineData("john2.test")]
        [InlineData("john-john.test")]
        [InlineData("john.bsky.app")]
        [InlineData("jo.hn")]
        [InlineData("a.co")]
        [InlineData("a.org")]
        [InlineData("joh.n")]
        [InlineData("j0.h0")]
        [InlineData("jaymome-johnber123456.test")]
        [InlineData("jay.mome-johnber123456.test")]
        [InlineData("john.test.bsky.app")]

        // max over all handle: 'shoooort' + '.loooooooooooooooooooooooooong'.repeat(8) + '.test'
        [InlineData("shoooort.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.test")]

        // max segment: 'short.' + 'o'.repeat(63) + '.test'
        [InlineData("short.ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo.test")]

        // NOTE: this probably isn't ever going to be a real domain, but my read of the RFC is that it would be possible
        [InlineData("john.t")]

        // allows punycode handles
        // 💩.test
        [InlineData("xn--ls8h.test")]
        // bücher.tld
        [InlineData("xn--bcher-kva.tld")]
        [InlineData("xn--3jk.com")]
        [InlineData("xn--w3d.com")]
        [InlineData("xn--vqb.com")]
        [InlineData("xn--ppd.com")]
        [InlineData("xn--cs9a.com")]
        [InlineData("xn--8r9a.com")]
        [InlineData("xn--cfd.com")]
        [InlineData("xn--5jk.com")]
        [InlineData("xn--2lb.com")]

        // allows onion (Tor) handles
        [InlineData("expyuzz4wqqyqhjn.onion")]
        [InlineData("friend.expyuzz4wqqyqhjn.onion")]
        [InlineData("g2zyxa5ihm7nsggfxnu52rck2vv4rvmdlkiu3zzui5du4xyclen53wid.onion")]
        [InlineData("friend.g2zyxa5ihm7nsggfxnu52rck2vv4rvmdlkiu3zzui5du4xyclen53wid.onion")]
        [InlineData("2gzyxa5ihm7nsggfxnu52rck2vv4rvmdlkiu3zzui5du4xyclen53wid.onion")]
        [InlineData("friend.2gzyxa5ihm7nsggfxnu52rck2vv4rvmdlkiu3zzui5du4xyclen53wid.onion")]

        // correctly validates corner cases (modern vs. old RFCs)
        [InlineData("12345.test")]
        [InlineData("8.cn")]
        [InlineData("4chan.org")]
        [InlineData("4chan.o-g")]
        [InlineData("blah.4chan.org")]
        [InlineData("thing.a01")]
        [InlineData("120.0.0.1.com")]
        [InlineData("0john.test")]
        [InlineData("9sta--ck.com")]
        [InlineData("99stack.com")]
        [InlineData("0ohn.test")]
        [InlineData("john.t--t")]
        [InlineData("thing.0aa.thing")]

        // examples from stackoverflow
        [InlineData("stack.com")]
        [InlineData("sta-ck.com")]
        [InlineData("sta---ck.com")]
        [InlineData("sta--ck9.com")]
        [InlineData("stack99.com")]
        [InlineData("sta99ck.com")]
        [InlineData("google.com.uk")]
        [InlineData("google.co.in")]
        [InlineData("google.com")]
        [InlineData("maselkowski.pl")]
        [InlineData("m.maselkowski.pl")]
        [InlineData("xn--masekowski-d0b.pl")]
        [InlineData("xn--fiqa61au8b7zsevnm8ak20mc4a87e.xn--fiqs8s")]
        [InlineData("xn--stackoverflow.com")]
        [InlineData("stackoverflow.xn--com")]
        [InlineData("stackoverflow.co.uk")]
        public void SyntacticallyValidHandlesTryParseShouldReturnTrueAndHandleWithNormalizedValue(string value)
        {
            Assert.True(Handle.TryParse(value, out Handle? actual));
            Assert.NotNull(actual);
            Assert.Equal(value.ToLowerInvariant(), actual.ToString());
        }

        [Theory]
        [InlineData("💩.test")]
        [InlineData("xn--bcher -.tld")]
        [InlineData("name.org.")]
        [InlineData(".name.org")]
        [InlineData(".name.org.")]

        // Test cases from https://github.com/bluesky-social/atproto/blob/main/interop-test-files/syntax/handle_syntax_invalid.txt
        [InlineData("did:thing.test")]
        [InlineData("did:thing")]
        [InlineData("john-test")]
        [InlineData("john.0")]
        [InlineData("john-")]
        [InlineData("xn--bcher-.tld")]
        [InlineData("john..test")]
        [InlineData("jo_hn.test")]
        [InlineData("-john.test")]
        [InlineData(".john.test")]
        [InlineData("jo!hn.test")]
        [InlineData("jo%hn.test")]
        [InlineData("jo&hn.test")]
        [InlineData("jo@hn.test")]
        [InlineData("jo*hn.test")]
        [InlineData("jo|hn.test")]
        [InlineData("jo:hn.test")]
        [InlineData("jo/hn.test")]
        [InlineData("john💩.test")]
        [InlineData("bücher.test")]
        [InlineData("john .test")]
        [InlineData("john.test.")]
        [InlineData("john")]
        [InlineData("john.")]
        [InlineData(".john")]
        [InlineData(" john.test")]
        [InlineData("joh -.test")]
        [InlineData("john.-est")]
        [InlineData("john.tes-")]

        // max over all handle: 'shoooort' + '.loooooooooooooooooooooooooong'.repeat(9) + '.test'
        [InlineData("shoooort.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.loooooooooooooooooooooooooong.test")]

        // max segment: 'short.' + 'o'.repeat(64) + '.test'
        [InlineData("short.oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo.test")]

        // throws on "dotless" TLD handles
        [InlineData("org")]
        [InlineData("ai")]
        [InlineData("gg")]
        [InlineData("io")]

        // correctly validates corner cases (modern vs. old RFCs)
        [InlineData("cn.8")]
        [InlineData("thing.0aa")]

        // does not allow IP addresses as handles
        [InlineData("127.0.0.1")]
        [InlineData("192.168.0.142")]
        [InlineData("fe80::7325:8a97:c100:94b")]
        [InlineData("2600:3c03::f03c:9100:feb0:af1f")]

        // examples from stackoverflow
        [InlineData("-notvalid.at-all")]
        [InlineData("- thing.com")]
        [InlineData("www.masełkowski.pl.com")]
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

        [Fact]
        public void EmptyOrWhiteSpaceHandlesShouldFailTryParse()
        {
            Assert.False(Handle.TryParse("", out Handle? handle));
            Assert.Null(handle);

            Assert.False(Handle.TryParse(" ", out handle));
            Assert.Null(handle);
        }

        [Fact]
        public void EqualityTestIgnoresCase()
        {
            Handle lhs = new("jay.bsky.social");
            Handle rhs = new("jay.BsKy.SoCiAl");

            Assert.True(lhs.Equals(rhs));

            Assert.True(lhs == rhs);
        }

        [Fact]
        public void ImplicitConversionFromValidStringWorks()
        {
            const string value = "jay.bsky.social";

            Handle handle = value;

            Assert.NotNull(handle);
            Assert.Equal(value, handle.Value);
        }

        [Fact]
        public void ExplicitConversionFromValidStringWorks()
        {
            const string value = "jay.bsky.social";

            Handle handle = (Handle)value;

            Assert.NotNull(handle);
            Assert.Equal(value, handle.Value);
        }
    }
}
