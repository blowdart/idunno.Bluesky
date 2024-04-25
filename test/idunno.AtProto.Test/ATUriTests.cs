// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace idunno.AtProto.Test
{
    [ExcludeFromCodeCoverage]
    public class ATUriTests
    {
        [Theory]
        [InlineData("at://did:plc:identifier/lexiconType/rkey?query#fragment", "did:plc:identifier", "/lexiconType/rkey", "?query", "#fragment")]
        [InlineData("at://did:plc:identifier/lexiconType?query#fragment", "did:plc:identifier", "/lexiconType", "?query", "#fragment")]
        [InlineData("at://did:plc:identifier/lexiconType/rkey#fragment", "did:plc:identifier", "/lexiconType/rkey", null, "#fragment")]
        [InlineData("at://did:plc:identifier/lexiconType/rkey?query", "did:plc:identifier", "/lexiconType/rkey", "?query", null)]
        [InlineData("at://did:plc:identifier/lexiconType/rkey", "did:plc:identifier", "/lexiconType/rkey", null, null)]
        [InlineData("at://did:plc:identifier", "did:plc:identifier", null, null, null)]
        [InlineData("at://did:plc:identifier?query", "did:plc:identifier", null, "?query", null)]
        [InlineData("at://did:plc:identifier#fragment", "did:plc:identifier", null, null, "#fragment")]
        [InlineData("at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3koaf5bu5kq27", "did:plc:ec72yg6n2sydzjvtovvdlxrk", "/app.bsky.feed.post/3koaf5bu5kq27", null, null)]
        [InlineData("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.actor.profile/self", "did:plc:hfgp6pj3akhqxntgqwramlbg", "/app.bsky.actor.profile/self", null, null)]
        public void ValidATUriShouldParseCorrectly(string value, string authority, string? path, string? query, string? fragment)
        {
            var atUri = new AtUri(value);

            Assert.NotNull(atUri);

            Assert.Equal("at", atUri.Scheme);
            Assert.Equal(authority, atUri.Authority);
            Assert.Equal(path, atUri.AbsolutePath);
            Assert.Equal(query, atUri.Query);
            Assert.Equal(fragment, atUri.Fragment);

            Assert.Equal(value, atUri.ToString());
        }

        [Theory]
        [InlineData("at://did:plc:identifier/lexiconType/rkey?query#fragment", "did:plc:identifier", "lexiconType", "rkey")]
        [InlineData("at://did:plc:identifier/lexiconType?query#fragment", "did:plc:identifier", "lexiconType", null)]
        [InlineData("at://did:plc:identifier/lexiconType/rkey#fragment", "did:plc:identifier", "lexiconType", "rkey")]
        [InlineData("at://did:plc:identifier/lexiconType/rkey?query", "did:plc:identifier", "lexiconType", "rkey")]
        [InlineData("at://did:plc:identifier/lexiconType/rkey", "did:plc:identifier", "lexiconType", "rkey")]
        [InlineData("at://did:plc:identifier", "did:plc:identifier", null, null)]
        [InlineData("at://did:plc:identifier?query", "did:plc:identifier", null, null)]
        [InlineData("at://did:plc:identifier#fragment", "did:plc:identifier", null, null)]
        [InlineData("at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3koaf5bu5kq27", "did:plc:ec72yg6n2sydzjvtovvdlxrk", "app.bsky.feed.post", "3koaf5bu5kq27")]
        [InlineData("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.actor.profile/self", "did:plc:hfgp6pj3akhqxntgqwramlbg", "app.bsky.actor.profile", "self")]
        public void ValidAtUriShouldExtractSemanticPropertiesCorrectly(string value, string? repo, string? collection, string? rkey)
        {
            var atUri = new AtUri(value);

            Assert.NotNull(atUri);

            Assert.NotNull(atUri.Repo);
            Assert.Equal(repo, atUri.Repo.ToString());

            Assert.Equal(collection, atUri.Collection);
            Assert.Equal(rkey, atUri.RKey);
        }

        [Theory]
        [InlineData("", "s is empty or only contains whitespace.")]
        [InlineData(" ", "s is empty or only contains whitespace.")]
        [InlineData("bogus://did:plc:identifier/lexiconType/rkey#fragment", "s has an invalid scheme.")]
        [InlineData("at://", "s contains no authority or path.")]
        [InlineData("at://did:plc:identifier/lexiconType/rkey??query#fragment", "s contains multiple query separators.")]
        [InlineData("at://did:plc:identifier/lexiconType/rkey?query##fragment", "s contains multiple fragment separators.")]
        public void InvalidATUriShouldThrowUriFormatException(string value, string expectedMessage)
        {
            UriFormatException exception = Assert.Throws<UriFormatException>(() =>
            {
                _ = new AtUri(value);
            });

            Assert.Equal(expectedMessage, exception.Message);
        }

        [Theory]
        [InlineData("at://did:plc:identifier/lexiconType/rkey?query#fragment")]
        [InlineData("at://did:plc:identifier/lexiconType?query#fragment")]
        [InlineData("at://did:plc:identifier/lexiconType/rkey#fragment")]
        [InlineData("at://did:plc:identifier/lexiconType/rkey?query")]
        [InlineData("at://did:plc:identifier/lexiconType/rkey")]
        [InlineData("at://did:plc:identifier")]
        [InlineData("at://did:plc:identifier?query")]
        [InlineData("at://did:plc:identifier#fragment")]
        [InlineData("at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3koaf5bu5kq27")]
        [InlineData("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.actor.profile/self")]
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
        public void TryParseShouldReturnFalseAndSetReturnedAtUriToNullWhenUriIsInvalid(string value)
        {
            bool actualParseResult;

            actualParseResult = AtUri.TryParse(value, out AtUri? actualUri);

            Assert.False(actualParseResult);
            Assert.Null(actualUri);
        }
    }
}
