// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class FacetExtractorTests
{
    private readonly Dictionary<string, Did?> _resolutionResult = new()
    {
        { "handle.invalid", null },
        { "blowdart.me", new Did("did:plc:hfgp6pj3akhqxntgqwramlbg") },
        { "bot.idunno.blue", new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk") },
        { "sinclairinat0r.com", new Did("did:plc:qkulxlxgznoyw4vdy7nu2mof") }
    };


    [Theory]
    [InlineData("#one", 1)]
    [InlineData("#one ", 1)]
    [InlineData("#one #!two", 2)]
    [InlineData("#one #!", 1)]
    [InlineData("#one #two", 2)]
    [InlineData("#one #two #three", 3)]
    [InlineData("#one #two three", 2)]
    [InlineData("#one #two ##three", 3)]
    [InlineData("#💩", 1)]
    [InlineData("#!", 0)]
    [InlineData("# ", 0)]
    [InlineData("#sixtyFour0123456789012345678901234567890123456789012345678901234", 1)]
    [InlineData("#sixtyFive01234567890123456789012345678901234567890123456789012345", 0)]
    // A tag within the grapheme limit whose UTF-8 encoding is longer than its character count is still a valid
    // tag. Thirty CJK characters are thirty graphemes but ninety UTF-8 bytes.
    [InlineData("#田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田", 1)]
    // Sixty five CJK characters exceed the grapheme limit, so they are still skipped.
    [InlineData("#田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田田", 0)]
    // Twenty six family emoji are twenty six graphemes but six hundred and fifty UTF-8 bytes, which exceeds the byte limit.
    [InlineData("#👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦", 0)]
    // A tag that is skipped must not stop later tags from being extracted.
    [InlineData("#! #realtag", 1)]
    [InlineData("#sixtyFive01234567890123456789012345678901234567890123456789012345 #realtag", 1)]
    // Any whitespace, not just a space, may precede a tag.
    [InlineData("hello\n#world", 1)]
    [InlineData("hello\t#world", 1)]
    public async Task HashTagsShouldCountCorrectly(string text, int expectedCount)
    {
        DefaultFacetExtractor extractor = new(MockResolver);

        IList<Facet> results = await extractor.ExtractFacets(text, TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.Equal(expectedCount, results.Count);

        foreach (Facet facet in results)
        {
            Assert.NotNull(facet.Features);
            FacetFeature facetFeature = Assert.Single(facet.Features);
            Assert.IsType<TagFacetFeature>(facetFeature);
        }
    }

    [Theory]
    [InlineData("aapl text", 0)]
    [InlineData("$aapl", 1)]
    [InlineData("$aapl text", 1)]
    [InlineData("$aapl $msft text", 2)]
    [InlineData("$aapl $msft", 2)]
    [InlineData("$aapl $!msft", 1)]
    [InlineData("$aapl $!", 1)]
    [InlineData("$aapl $msft $abpww", 3)]
    // A single letter ticker at the start of the text is still a cash tag.
    [InlineData("$F", 1)]
    // An opening parenthesis may precede a cash tag and must not become part of it.
    [InlineData("buy ($AAPL) now", 1)]
    public async Task CashTagsShouldCountCorrectly(string text, int expectedCount)
    {
        DefaultFacetExtractor extractor = new(MockResolver);

        IList<Facet> results = await extractor.ExtractFacets(text, TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.Equal(expectedCount, results.Count);

        foreach (Facet facet in results)
        {
            Assert.NotNull(facet.Features);
            FacetFeature featureFeature = Assert.Single(facet.Features);
            Assert.IsType<TagFacetFeature>(featureFeature);
        }
    }

    [Theory]
    [InlineData("#tag", 0, 4)]
    [InlineData("#tag.", 0, 4)]
    [InlineData("#tag!", 0, 4)]
    [InlineData("#tag ", 0, 4)]
    [InlineData(" #tag", 1, 5)]
    [InlineData(" #tag ", 1, 5)]
    [InlineData(" #tag  ", 1, 5)]
    [InlineData("  #tag", 2, 6)]
    [InlineData("  #tag ", 2, 6)]
    [InlineData("  #tag  ", 2, 6)]
    [InlineData("##tag", 0, 5)]
    [InlineData(" ##tag", 1, 6)]
    [InlineData("##tag ", 0, 5)]
    [InlineData(" ##tag ", 1, 6)]
    [InlineData("##tag!", 0, 5)]
    [InlineData(" ##tag!", 1, 6)]
    // The facet must cover the tag only, never the whitespace the pattern consumed before it.
    [InlineData("hello\n#world", 6, 12)]
    [InlineData("hello\t#world", 6, 12)]
    public async Task HashTagsShouldPositionCorrectly(string text, int expectedStartPosition, int expectedEndPosition)
    {
        DefaultFacetExtractor extractor = new(MockResolver);

        IList<Facet> results = await extractor.ExtractFacets(text, TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.Single(results);

        foreach (Facet facet in results)
        {
            Assert.NotNull(facet.Features);
            FacetFeature facetFeature = Assert.Single(facet.Features);
            TagFacetFeature tagFeature = Assert.IsType<TagFacetFeature>(facetFeature);
            Assert.Equal(facet.Index.ByteStart, expectedStartPosition);
            Assert.Equal(facet.Index.ByteEnd, expectedEndPosition);
        }
    }

    [Theory]
    [InlineData("$aapl", 0, 5)]
    [InlineData("$aapl.", 0, 5)]
    [InlineData("$appl!", 0, 5)]
    [InlineData("$appl ", 0, 5)]
    [InlineData(" $aapl", 1, 6)]
    [InlineData(" $aapl ", 1, 6)]
    [InlineData(" $aapl  ", 1, 6)]
    [InlineData("  $aapl", 2, 7)]
    [InlineData("  $aapl ", 2, 7)]
    [InlineData("  $aapl  ", 2, 7)]
    // The facet must cover the tag only, never the parenthesis the pattern consumed before it.
    [InlineData("($AAPL)", 1, 6)]
    [InlineData("buy ($AAPL) now", 5, 10)]
    public async Task CashTagsShouldPositionCorrectly(string text, int expectedStartPosition, int expectedEndPosition)
    {
        DefaultFacetExtractor extractor = new(MockResolver);

        IList<Facet> results = await extractor.ExtractFacets(text, TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.Single(results);

        foreach (Facet facet in results)
        {
            Assert.NotNull(facet.Features);
            FacetFeature facetFeature = Assert.Single(facet.Features);
            Assert.IsType<TagFacetFeature>(facetFeature);
            Assert.Equal(facet.Index.ByteStart, expectedStartPosition);
            Assert.Equal(facet.Index.ByteEnd, expectedEndPosition);
        }
    }

    [Theory]
    [InlineData("#tag", "tag")]
    [InlineData("#tag!", "tag")]
    [InlineData(" #tag!", "tag")]
    [InlineData(" #tag! ", "tag")]
    [InlineData(" #tag ", "tag")]
    [InlineData("#tag ", "tag")]
    [InlineData(" #tag", "tag")]
    [InlineData(" ##tag", "#tag")]
    // The tag value must never include the whitespace the pattern consumed before it.
    [InlineData("hello\n#world", "world")]
    [InlineData("hello\t#world", "world")]
    public async Task HashTagsShouldExtractTheTagCorrectly(string text, string expectedTag)
    {
        DefaultFacetExtractor extractor = new(MockResolver);

        IList<Facet> results = await extractor.ExtractFacets(text, TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.Single(results);

        foreach (Facet facet in results)
        {
            Assert.NotNull(facet.Features);
            FacetFeature facetFeature = Assert.Single(facet.Features);
            Assert.IsType<TagFacetFeature>(facetFeature);

            TagFacetFeature tagFeature = Assert.IsType<TagFacetFeature>(facetFeature);

            Assert.Equal(expectedTag, tagFeature.Tag);
        }
    }

    [Theory]
    // A cash tag keeps its $ prefix in the tag value, unlike a hash tag which has its # stripped.
    // This is deliberate: Bluesky treats #tag and $tag as distinct tags.
    [InlineData("$money", "$money")]
    // The tag value must never include the parenthesis the pattern consumed before it.
    [InlineData("($AAPL)", "$AAPL")]
    [InlineData("buy ($AAPL) now", "$AAPL")]
    public async Task CashTagsShouldExtractTheTagCorrectly(string text, string expectedTag)
    {
        DefaultFacetExtractor extractor = new(MockResolver);

        IList<Facet> results = await extractor.ExtractFacets(text, TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.Single(results);

        foreach (Facet facet in results)
        {
            Assert.NotNull(facet.Features);
            FacetFeature facetFeature = Assert.Single(facet.Features);
            TagFacetFeature tagFeature = Assert.IsType<TagFacetFeature>(facetFeature);
            Assert.Equal(expectedTag, tagFeature.Tag);
        }
    }

    [Theory]
    [InlineData("http://example.org", 1)]
    [InlineData("https://example.org", 1)]
    [InlineData("https//notreal", 0)]
    [InlineData("https://notreal", 0)]
    [InlineData("https://💩", 0)]
    [InlineData("ftp//example.org", 0)]
    [InlineData("http://example.org https://example.org", 2)]
    [InlineData("http://example.org https://example.org https//notreal", 2)]
    [InlineData("http://example.org https://example.org https://example.org", 3)]
    public async Task LinksShouldCountCorrectly(string text, int expectedCount)
    {
        DefaultFacetExtractor extractor = new(MockResolver);

        IList<Facet> results = await extractor.ExtractFacets(text, TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.Equal(expectedCount, results.Count);

        foreach (Facet facet in results)
        {
            Assert.NotNull(facet.Features);
            FacetFeature facetFeature = Assert.Single(facet.Features);

            Assert.IsType<LinkFacetFeature>(facetFeature);
        }
    }

    [Theory]
    [InlineData("http://example.org", "http://example.org")]
    [InlineData("http://example.org/path", "http://example.org/path")]
    [InlineData("http://example.org/path?", "http://example.org/path")]
    [InlineData("http://example.org/path/", "http://example.org/path/")]
    [InlineData("http://example.org/path/?", "http://example.org/path/")]
    [InlineData("http://example.org/path?queryString", "http://example.org/path?queryString")]
    [InlineData("http://example.org/path?queryString?ignore", "http://example.org/path?queryString")]
    [InlineData("http://example.org/path?queryString=1", "http://example.org/path?queryString=1")]
    [InlineData("http://example.org/path?queryString=1&two=2", "http://example.org/path?queryString=1&two=2")]
    [InlineData("https://example.org", "https://example.org")]
    [InlineData("https://example.org ", "https://example.org")]
    [InlineData(" https://example.org", "https://example.org")]
    [InlineData(" https://example.org ", "https://example.org")]

    public async Task LinksExtractTheUrlCorrectly(string text, string expectedLink)
    {
        DefaultFacetExtractor extractor = new(MockResolver);

        IList<Facet> results = await extractor.ExtractFacets(text, TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.Single(results);

        foreach (Facet facet in results)
        {
            Assert.NotNull(facet.Features);
            FacetFeature facetFeature = Assert.Single(facet.Features);
            LinkFacetFeature linkFeature = Assert.IsType<LinkFacetFeature>(facetFeature);
            Assert.Equal(expectedLink, linkFeature.Uri);
        }
    }

    [Theory]
    [InlineData("http://example.org", 0, 18)]
    [InlineData("http://example.org ", 0, 18)]
    [InlineData(" http://example.org", 1, 19)]
    [InlineData(" http://example.org ", 1, 19)]
    [InlineData("http://example.org!", 0, 18)]
    public async Task LinksShouldPositionCorrectly(string text, int expectedStartPosition, int expectedEndPosition)
    {
        DefaultFacetExtractor extractor = new(MockResolver);

        IList<Facet> results = await extractor.ExtractFacets(text, TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.Single(results);

        foreach (Facet facet in results)
        {
            Assert.NotNull(facet.Features);
            FacetFeature facetFeature = Assert.Single(facet.Features);
            Assert.IsType<LinkFacetFeature>(facetFeature);
            Assert.Equal(facet.Index.ByteStart, expectedStartPosition);
            Assert.Equal(facet.Index.ByteEnd, expectedEndPosition);
        }
    }

    [Theory]
    [InlineData("@blowdart.me", 1)]
    [InlineData("@blowdart.me ", 1)]
    [InlineData(" @blowdart.me", 1)]
    [InlineData("@ blowdart.me ", 0)]
    [InlineData(" @blowdart.me ", 1)]
    [InlineData("@blowdart.me @blowdart.me", 2)]
    // A mention must be preceded by the start of the text, whitespace or an opening parenthesis,
    // so the second handle here is not a mention. This is what stops an email address such as
    // bob@example.com from being extracted as a mention of example.com.
    [InlineData("@blowdart.me@blowdart.me", 1)]
    [InlineData("bob@example.com", 0)]
    [InlineData("mail me at bob@blowdart.me please", 0)]
    [InlineData("@blowdart.me @handle.invalid", 1)]
    [InlineData("@blowdart.me @handle.invalid @bot.idunno.blue", 2)]
    public async Task MentionsShouldCountCorrectly(string text, int expectedCount)
    {
        DefaultFacetExtractor extractor = new(MockResolver);

        IList<Facet> results = await extractor.ExtractFacets(text, TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.Equal(expectedCount, results.Count);

        foreach (Facet facet in results)
        {
            Assert.NotNull(facet.Features);
            FacetFeature facetFeature = Assert.Single(facet.Features);

            Assert.IsType<MentionFacetFeature>(facetFeature);
        }
    }

    [Theory]
    [InlineData("@blowdart.me", 0, 12)]
    [InlineData("@blowdart.me ", 0, 12)]
    [InlineData(" @blowdart.me", 1, 13)]
    [InlineData(" @blowdart.me ", 1, 13)]
    [InlineData("@blowdart.me!", 0, 12)]
    public async Task MentionsShouldPositionCorrectly(string text, int expectedStartPosition, int expectedEndPosition)
    {
        DefaultFacetExtractor extractor = new(MockResolver);

        IList<Facet> results = await extractor.ExtractFacets(text, TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.Single(results);

        foreach (Facet facet in results)
        {
            Assert.NotNull(facet.Features);
            FacetFeature facetFeature = Assert.Single(facet.Features);
            Assert.IsType<MentionFacetFeature>(facetFeature);
            Assert.Equal(facet.Index.ByteStart, expectedStartPosition);
            Assert.Equal(facet.Index.ByteEnd, expectedEndPosition);
        }
    }

    [Theory]
    [InlineData("@blowdart.me", "did:plc:hfgp6pj3akhqxntgqwramlbg")]
    [InlineData("@bot.idunno.blue", "did:plc:ec72yg6n2sydzjvtovvdlxrk")]
    public async Task MentionsShouldResolveCorrectly(string text, string expectedDid)
    {
        DefaultFacetExtractor extractor = new(MockResolver);

        IList<Facet> results = await extractor.ExtractFacets(text, TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.Single(results);

        foreach (Facet facet in results)
        {
            Assert.NotNull(facet.Features);
            FacetFeature facetFeature = Assert.Single(facet.Features);
            Assert.IsType<MentionFacetFeature>(facetFeature);

            MentionFacetFeature mentionFeature = Assert.IsType<MentionFacetFeature>(facetFeature);

            Assert.Equal(new Did(expectedDid), mentionFeature.Did);
        }
    }

    [Fact]
    public async Task CombinationsShouldCreateTheCorrectFacets()
    {
        const string text =
            "Hello @sinclairinat0r.com, did you know the Heinz #beans factory is one of the largest food factories in Europe? https://en.wikipedia.org/wiki/H._J._Heinz,_Wigan?";
        //             1         2         3         4         5         6         7         8         9         10        11        12        13        14        15        16
        //   012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901
        DefaultFacetExtractor extractor = new(MockResolver);

        IList<Facet> results = await extractor.ExtractFacets(text, TestContext.Current.CancellationToken);

        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.Equal(3, results.Count);

        int mentionCount = 0;
        int hashTagCount = 0;
        int urlCount = 0;

        foreach (Facet facet in results)
        {
            FacetFeature facetFeature = Assert.Single(facet.Features);
            switch (facetFeature)
            {
                case MentionFacetFeature mentionFacetFeature:
                    mentionCount++;
                    Assert.Equal(1, mentionCount);

                    Assert.Equal("did:plc:qkulxlxgznoyw4vdy7nu2mof", mentionFacetFeature.Did);
                    Assert.Equal(6, facet.Index.ByteStart);
                    Assert.Equal(25, facet.Index.ByteEnd);
                    break;

                case TagFacetFeature tagFacetFeature:
                    hashTagCount++;
                    Assert.Equal(1, hashTagCount);

                    Assert.Equal("beans", tagFacetFeature.Tag);
                    Assert.Equal(50, facet.Index.ByteStart);
                    Assert.Equal(56, facet.Index.ByteEnd);
                    break;

                case LinkFacetFeature linkFacetFeature:
                    urlCount++;
                    Assert.Equal(1, urlCount);

                    Assert.Equal("https://en.wikipedia.org/wiki/H._J._Heinz,_Wigan", linkFacetFeature.Uri);
                    Assert.Equal(113, facet.Index.ByteStart);
                    Assert.Equal(161, facet.Index.ByteEnd);

                    break;

                default:
                    throw new Exception("Unexpected facet feature");
            }
        }
    }


    [Theory]
    // A handle which resolves is only looked up once, however many times it is mentioned.
    [InlineData("@blowdart.me @blowdart.me @blowdart.me", 1, 3)]
    [InlineData("@blowdart.me and @bot.idunno.blue and @blowdart.me", 2, 3)]
    // A handle which does not resolve is also only looked up once, otherwise a handle which does not exist
    // would be looked up again for every time it appears.
    [InlineData("@handle.invalid @handle.invalid @handle.invalid", 1, 0)]
    [InlineData("@blowdart.me @handle.invalid @blowdart.me @handle.invalid", 2, 2)]
    // Resolution is not case sensitive, so handles differing only in case are the same lookup.
    [InlineData("@blowdart.me @BLOWDART.ME", 1, 2)]
    [InlineData("@blowdart.me @bot.idunno.blue @sinclairinat0r.com", 3, 3)]
    public async Task EachDistinctMentionedHandleIsResolvedOnlyOnce(string text, int expectedResolutions, int expectedMentionCount)
    {
        List<string> resolvedHandles = [];

        DefaultFacetExtractor extractor = new((handle, cancellationToken) =>
        {
            resolvedHandles.Add(handle);
            return MockResolver(handle, cancellationToken);
        });

        IList<Facet> results = await extractor.ExtractFacets(text, TestContext.Current.CancellationToken);

        Assert.Equal(expectedResolutions, resolvedHandles.Count);
        Assert.Equal(expectedMentionCount, results.Count);
    }

    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Mocking ResolveHandle() signature.")]
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Mocking ResolveHandle().")]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Mocking ResolveHandle() signature.")]
    private async Task<Did?> MockResolver(string handle, CancellationToken cancellationToken = default)
    {
        if (_resolutionResult.TryGetValue(handle, out Did? value))
        {
            return value;
        }
        else
        {
            return null;
        }
    }
}