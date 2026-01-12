// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky.Test
{
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
        public async Task HashTagsShouldCountCorrectly(string text, int expectedCount)
        {
            DefaultFacetExtractor extractor = new(MockResolver);

            IList<Facet> results = await extractor.ExtractFacets(text, TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.Equal(expectedCount, results.Count);

            foreach (Facet facet in results)
            {
                Assert.NotNull(facet.Features);
                Assert.Single(facet.Features);

                Assert.IsType<TagFacetFeature>(facet.Features[0]);
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
        public async Task CashTagsShouldCountCorrectly(string text, int expectedCount)
        {
            DefaultFacetExtractor extractor = new(MockResolver);

            IList<Facet> results = await extractor.ExtractFacets(text, TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.Equal(expectedCount, results.Count);

            foreach (Facet facet in results)
            {
                Assert.NotNull(facet.Features);
                Assert.Single(facet.Features);

                Assert.IsType<TagFacetFeature>(facet.Features[0]);
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
                Assert.Single(facet.Features);
                Assert.IsType<TagFacetFeature>(facet.Features[0]);
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
                Assert.Single(facet.Features);
                Assert.IsType<TagFacetFeature>(facet.Features[0]);
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
                Assert.Single(facet.Features);
                Assert.IsType<TagFacetFeature>(facet.Features[0]);

                TagFacetFeature tagFeature = (TagFacetFeature)facet.Features[0];

                Assert.Equal(expectedTag, tagFeature.Tag);
            }
        }

        [Theory]
        [InlineData("$money", "$money")]
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
                Assert.Single(facet.Features);
                Assert.IsType<TagFacetFeature>(facet.Features[0]);

                TagFacetFeature tagFeature = (TagFacetFeature)facet.Features[0];

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
                Assert.Single(facet.Features);

                Assert.IsType<LinkFacetFeature>(facet.Features[0]);
            }
        }

        [Theory]
        [InlineData("http://example.org", "http://example.org")]
        [InlineData("http://example.org/path", "http://example.org/path")]
        [InlineData("http://example.org/path/", "http://example.org/path/")]
        [InlineData("http://example.org/path?queryString", "http://example.org/path")]
        [InlineData("http://example.org/path?queryString=1", "http://example.org/path")]
        [InlineData("http://example.org/path?queryString=1&two=2", "http://example.org/path")]
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
                Assert.Single(facet.Features);
                Assert.IsType<LinkFacetFeature>(facet.Features[0]);

                LinkFacetFeature linkFeature = (LinkFacetFeature)facet.Features[0];

                Assert.Equal(new Uri(expectedLink), linkFeature.Uri);
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
                Assert.Single(facet.Features);
                Assert.IsType<LinkFacetFeature>(facet.Features[0]);
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
        [InlineData("@blowdart.me@blowdart.me", 2)]
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
                Assert.Single(facet.Features);

                Assert.IsType<MentionFacetFeature>(facet.Features[0]);
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
                Assert.Single(facet.Features);
                Assert.IsType<MentionFacetFeature>(facet.Features[0]);
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
                Assert.Single(facet.Features);
                Assert.IsType<MentionFacetFeature>(facet.Features[0]);

                MentionFacetFeature mentionFeature = (MentionFacetFeature)facet.Features[0];

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
                Assert.Single(facet.Features);
                switch (facet.Features[0])
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

                        Assert.Equal(new Uri("https://en.wikipedia.org/wiki/H._J._Heinz,_Wigan"), linkFacetFeature.Uri);
                        Assert.Equal(113, facet.Index.ByteStart);
                        Assert.Equal(161, facet.Index.ByteEnd);

                        break;

                    default:
                        throw new Exception("Unexpected facet feature");
                }
            }
        }


        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Mocking ResolveHandle() signature.")]
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Mocking ResolveHandle().")]
        [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Mocking ResolveHandle() signature.")]
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<Did?> MockResolver(string handle, CancellationToken cancellationToken = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
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
}
