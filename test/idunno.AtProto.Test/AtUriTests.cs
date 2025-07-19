// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Test
{
    public class AtUriTests
    {
        [Theory]
        [InlineData("at://did:plc:identifier/test.idunno.lexiconType/rkey", "did:plc:identifier", "test.idunno.lexiconType", "rkey")]
        [InlineData("at://did:plc:identifier/test.idunno.lexiconType", "did:plc:identifier", "test.idunno.lexiconType", null)]
        [InlineData("at://did:plc:identifier", "did:plc:identifier", null, null)]
        [InlineData("at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3koaf5bu5kq27", "did:plc:ec72yg6n2sydzjvtovvdlxrk", "app.bsky.feed.post", "3koaf5bu5kq27")]
        [InlineData("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.actor.profile/self", "did:plc:hfgp6pj3akhqxntgqwramlbg", "app.bsky.actor.profile", "self")]
        public void ValidAtUriShouldExtractSemanticPropertiesCorrectly(string value, string repo, string? collection, string? rkey)
        {
            Nsid? collectionAsNsid = null;
            RecordKey? rkeyAsRecordKey = null;

            Assert.True(AtIdentifier.TryParse(repo, out AtIdentifier? repoAsAtIdentifier));
            Assert.NotNull(repoAsAtIdentifier);

            if (collection is not null)
            {
                collectionAsNsid = new(collection);
            }

            if (rkey is not null)
            {
                rkeyAsRecordKey = new(rkey);
            }

            var atUri = new AtUri(value);

            Assert.NotNull(atUri);

            Assert.NotNull(atUri.Repo);
            Assert.Equal(repoAsAtIdentifier, atUri.Repo);

            Assert.Equal(collectionAsNsid, atUri.Collection);
            Assert.Equal(rkeyAsRecordKey, atUri.RecordKey);
        }

        [Theory]
        [InlineData("", "The value cannot be an empty string or composed entirely of whitespace. (Parameter 's')")]
        [InlineData(" ", "The value cannot be an empty string or composed entirely of whitespace. (Parameter 's')")]
        public void EmptyOrWhitespaceAtUriShouldThrowArgumentException(string value, string expectedMessage)
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            {
                _ = new AtUri(value);
            });

            Assert.Equal(expectedMessage, exception.Message);
        }

        [Theory]
        [InlineData("bogus://did:plc:identifier/lexiconType/rkey#fragment", "s has an invalid scheme.")]
        [InlineData("at://", "s contains no authority or path.")]
        [InlineData("at://did:plc:identifier/lexiconType/rkey??query#fragment", "AT URIs cannot contain a query part.")]
        [InlineData("at://did:plc:identifier/lexiconType/rkey#fragment", "AT URIs cannot contain a fragment.")]
        public void InvalidAtUriShouldThrowAtUriFormatException(string value, string expectedMessage)
        {
            AtUriFormatException exception = Assert.Throws<AtUriFormatException>(() =>
            {
                _ = new AtUri(value);
            });

            Assert.Equal(expectedMessage, exception.Message);
        }

        [Theory]
        [InlineData("at://did:plc:identifier")]
        [InlineData("at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3koaf5bu5kq27")]
        [InlineData("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.actor.profile/self")]

        // Test cases from https://github.com/bluesky-social/atproto/blob/main/interop-test-files/syntax/aturi_syntax_valid.txt
        // enforces spec basics
        [InlineData("at://did:plc:asdf123")]
        [InlineData("at://user.bsky.social")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/record")]

        // # very long: 'at://did:plc:asdf123/com.atproto.feed.post/' + 'o'.repeat(512)
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo")]

        // enforces strict paths
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/asdf123")]

        // is very permissive about record keys
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/a")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/asdf-123")]
        [InlineData("at://did:abc:123")]
        [InlineData("at://did:abc:123/io.nsid.someFunc/record-key")]
        [InlineData("at://did:abc:123/io.nsid.someFunc/self.")]
        [InlineData("at://did:abc:123/io.nsid.someFunc/lang:")]
        [InlineData("at://did:abc:123/io.nsid.someFunc/:")]
        [InlineData("at://did:abc:123/io.nsid.someFunc/-")]
        [InlineData("at://did:abc:123/io.nsid.someFunc/_")]
        [InlineData("at://did:abc:123/io.nsid.someFunc/~")]
        [InlineData("at://did:abc:123/io.nsid.someFunc/...")]
        public void TryParseShouldReturnTrueAndAParsedAtUriWhenUriIsValue(string value)
        {
            bool actualParseResult;

            actualParseResult = AtUri.TryParse(value, out AtUri? actualUri);

            Assert.True(actualParseResult);
            Assert.NotNull(actualUri);
            Assert.Equal(value, actualUri.ToString());
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("bogus://did:plc:identifier/lexiconType/rkey#fragment")]
        [InlineData("at://")]
        [InlineData("at://did:plc:identifier/lexiconType/rkey??query#fragment")]
        [InlineData("at://did:plc:identifier/lexiconType/rkey?query##fragment")]

        // Test cases from https://github.com/bluesky-social/atproto/blob/main/interop-test-files/syntax/aturi_syntax_invalid.txt
        // enforces spec basics
        [InlineData("a://did:plc:asdf123")]
        [InlineData("at//did:plc:asdf123")]
        [InlineData("at:/a/did:plc:asdf123")]
        [InlineData("at:/did:plc:asdf123")]
        [InlineData("AT://did:plc:asdf123")]
        [InlineData("http://did:plc:asdf123")]
        [InlineData("://did:plc:asdf123")]
        [InlineData("at:did:plc:asdf123")]
        [InlineData("at:///did:plc:asdf123")]
        [InlineData("at://:/did:plc:asdf123")]
        [InlineData("at:/ /did:plc:asdf123")]
        [InlineData("at://did:plc:asdf123 ")]
        [InlineData("at://did:plc:asdf123/ ")]
        [InlineData(" at://did:plc:asdf123")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post ")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post# ")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post#/ ")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post#/frag ")] 
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post#fr ag")]
        [InlineData("//did:plc:asdf123")]
        [InlineData("at://name")]
        [InlineData("at://name.0")]
        [InlineData("at://diD:plc:asdf123")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.p@st")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.p$st")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.p%st")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.p&st")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.p()t")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed_post")]
        [InlineData("at://did:plc:asdf123/-com.atproto.feed.post")]
        [InlineData("at://did:plc:asdf@123/com.atproto.feed.post")]
        [InlineData("at://DID:plc:asdf123")]
        [InlineData("at://user.bsky.123")]
        [InlineData("at://bsky")]
        [InlineData("at://did:plc: ")]
        [InlineData("at://did:plc:")]
        [InlineData("at://frag")]

        // too long: 'at://did:plc:asdf123/com.atproto.feed.post/' + 'o'.repeat(8200)
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo")]

        // enforces no trailing slashes
        [InlineData("at://did:plc:asdf123/")]
        [InlineData("at://user.bsky.social/")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/record/")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/record/#/frag")]

        // enforces strict paths
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/asdf123/asdf")]

        // # new less permissive about record keys for Lexicon use (with recordkey more specified)
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/%23")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/$@!*)(:,;~.sdf123")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/~'sdf123\")")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/$")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/@")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/!")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/*")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/(")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/,")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/;")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/abc%30123")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/%30")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/%3")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/%")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/%zz")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/%%%")]

        // disallow dot / double-dot
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/.")]
        [InlineData("at://did:plc:asdf123/com.atproto.feed.post/..")]
        public void TryParseShouldReturnFalseAndSetReturnedAtUriToNullWhenUriIsInvalid(string value)
        {
            bool actualParseResult;

            actualParseResult = AtUri.TryParse(value, out AtUri? actualUri);

            Assert.False(actualParseResult);
            Assert.Null(actualUri);
        }

        [Fact]
        public void EqualityWorks()
        {
            AtUri lhs = new("at://did:plc:identifier/test.idunno.lexiconType/rkey");
            AtUri rhs = new("at://did:plc:identifier/test.idunno.lexiconType/rkey");

            Assert.NotNull(lhs);
            Assert.NotNull(rhs);
            Assert.Equal(lhs, rhs);
            Assert.True(lhs.Equals(rhs));
        }

        [Fact]
        public void ImplicitConversionFromValidStringWorks()
        {
            string atUriString = "at://did:plc:identifier/test.idunno.lexiconType/rkey";

            AtUri atUri = (AtUri)atUriString;

            Assert.NotNull(atUri);

            Assert.Equal("did:plc:identifier", atUri.Authority);
            Assert.Equal("test.idunno.lexiconType", atUri.Collection);
            Assert.Equal("rkey", atUri.RecordKey);
        }
    }
}
