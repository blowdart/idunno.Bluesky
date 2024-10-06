// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace idunno.AtProto.Test
{
    [ExcludeFromCodeCoverage]
    public class NsidTests
    {
        // Test cases from https://github.com/bluesky-social/atproto/blob/290a7e67b8e6417b00352cc1d54bac006c2f6f93/interop-test-files/syntax/nsid_syntax_valid.txt
        [Theory]

        // length checks
        [InlineData("com.ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo.foo")]
        [InlineData("com.example.ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo")]
        [InlineData("com.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.foo")]

        // valid examples
        [InlineData("com.example.fooBar")]
        [InlineData("net.users.bob.ping")]
        [InlineData("a.b.c")]
        [InlineData("m.xn--masekowski-d0b.pl")]
        [InlineData("one.two.three")]
        [InlineData("one.two.three.four-and.FiVe")]
        [InlineData("one.2.three")]
        [InlineData("a-0.b-1.c")]
        [InlineData("a0.b1.cc")]
        [InlineData("test.12345.record")]
        [InlineData("a01.thing.record")]
        [InlineData("a.0.c")]
        [InlineData("xn--fiqs8s.xn--fiqa61au8b7zsevnm8ak20mc4a87e.record.two")]

        //  allows onion (Tor) NSIDs
        [InlineData("onion.expyuzz4wqqyqhjn.spec.getThing")]
        [InlineData("onion.g2zyxa5ihm7nsggfxnu52rck2vv4rvmdlkiu3zzui5du4xyclen53wid.lex.deleteThing")]

        // allows starting-with-numeric segments (same as domains)
        [InlineData("org.4chan.lex.getThing")]
        [InlineData("cn.8.lex.stuff")]
        [InlineData("onion.2gzyxa5ihm7nsggfxnu52rck2vv4rvmdlkiu3zzui5du4xyclen53wid.lex.deleteThing")]
        public void TryParseShouldReturnTrueOnValidNsids(string nsid)
        {
            Assert.True(Nsid.TryParse(nsid, out Nsid? _));
        }

        //Test cases from https://github.com/bluesky-social/atproto/blob/290a7e67b8e6417b00352cc1d54bac006c2f6f93/interop-test-files/syntax/nsid_syntax_invalid.txt
        [Theory]

        // length checks
        [InlineData("com.oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo.foo")]
        [InlineData("com.example.oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo")]
        [InlineData("com.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.middle.foo")]

        // invalid examples
        [InlineData("com.example.foo.*")]
        [InlineData("com.example.foo.blah*")]
        [InlineData("com.example.foo.* blah")]
        [InlineData("com.example.f00")]
        [InlineData("com.exa💩ple.thing")]
        [InlineData("a -0.b-1.c-3")]
        [InlineData("a -0.b-1.c-o")]
        [InlineData("a0.b1.c3")]
        [InlineData("1.0.0.127.record")]
        [InlineData("0two.example.foo")]
        [InlineData("example.com")]
        [InlineData("com.example")]
        [InlineData("a.")]
        [InlineData(".one.two.three")]
        [InlineData("one.two.three ")]
        [InlineData("one.two..three")]
        [InlineData("one .two.three")]
        [InlineData(" one.two.three")]
        [InlineData("com.atproto.feed.p @st")]
        [InlineData("com.atproto.feed.p_st")]
        [InlineData("com.atproto.feed.p* st")]
        [InlineData("com.atproto.feed.po#t")]
        [InlineData("com.atproto.feed.p!ot")]
        [InlineData("com.example-.foo")]
        public void TryParseShouldReturnFalseOnInvalidNsids(string nsid)
        {
            Assert.False(Nsid.TryParse(nsid, out Nsid? _));
        }

        [Theory]
        [InlineData("com.example.foo.*")]
        [InlineData("com.example.foo.blah*")]
        [InlineData("com.example.foo.* blah")]
        public void InstantiatingANewNsidWithAnInvalidStringShouldThrowInvalidNsidException(string s)
        {
            Assert.Throws<NsidFormatException>((Action)(() =>
            {
                _ = new Nsid(s);
            }));
        }

        [Theory]
        [InlineData("com.example.fooBar", "example.com", "fooBar")]
        [InlineData("net.users.bob.ping", "bob.users.net", "ping")]
        public void NsidPropertiesShouldReturnExpectedValues(string nsidString, string authority, string name)
        {
            Assert.True(Nsid.TryParse(nsidString, out Nsid? nsid));
            Assert.NotNull(nsid);
            Assert.Equal(authority, nsid.Authority);
            Assert.Equal(name, nsid.Name);
        }

        [Fact]
        public void EqualityReturnsTrueWhenContentsAreTheSame()
        {
            Nsid lhs = new ("com.example.fooBar");
            Nsid rhs = new ("com.example.fooBar");

            Assert.Equal(lhs, rhs);
        }

        [Fact]
        public void EqualityReturnsFalseWhenContentsAreDifferent()
        {
            Nsid lhs = new("com.example.fooBarBaz");
            Nsid rhs = new("com.example.fooBar");

            Assert.NotEqual(lhs, rhs);
        }

        [Fact]
        public void ValidStringsCanBeImplicitlyConverted()
        {
            const string domainAuthority = "com.example";
            const string name = "foobar";

            string value = $"{domainAuthority}.{name}";

            Nsid convertedNsid = value;

            Assert.NotNull(convertedNsid);
            Assert.Equal(value, convertedNsid.ToString());
            Assert.Equal(domainAuthority, convertedNsid.Authority);
            Assert.Equal(name, convertedNsid.Name);
        }
    }
}
