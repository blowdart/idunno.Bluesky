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
            { "bot.idunno.blue", new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk") }
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

                Assert.Equal(typeof(TagFacetFeature), facet.Features[0].GetType());
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
                Assert.Equal(typeof(TagFacetFeature), facet.Features[0].GetType());
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
                Assert.Equal(typeof(TagFacetFeature), facet.Features[0].GetType());

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

                Assert.Equal(typeof(LinkFacetFeature), facet.Features[0].GetType());
            }
        }

        [Theory]
        [InlineData("http://example.org", "http://example.org")]
        [InlineData("http://example.org/path", "http://example.org/path")]
        [InlineData("http://example.org/path/", "http://example.org/path/")]
        [InlineData("http://example.org/path?queryString", "http://example.org/path?queryString")]
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
                Assert.Single(facet.Features);
                Assert.Equal(typeof(LinkFacetFeature), facet.Features[0].GetType());

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
                Assert.Equal(typeof(LinkFacetFeature), facet.Features[0].GetType());
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

                Assert.Equal(typeof(MentionFacetFeature), facet.Features[0].GetType());
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
                Assert.Equal(typeof(MentionFacetFeature), facet.Features[0].GetType());
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
                Assert.Equal(typeof(MentionFacetFeature), facet.Features[0].GetType());

                MentionFacetFeature mentionFeature = (MentionFacetFeature)facet.Features[0];

                Assert.Equal(new Did(expectedDid), mentionFeature.Did);
            }
        }

        [Fact]
        public void DisposeTwiceShouldNotThrow()
        {
            DefaultFacetExtractor extractor = new(MockResolver);

            Exception? assert = null;

            try
            {
                extractor.Dispose();
            }
            catch (Exception ex)
            {
                assert = ex;
            }

            Assert.Null(assert);

            try
            {
                extractor.Dispose();
            }
            catch (Exception ex)
            {
                assert = ex;
            }

            Assert.Null(assert);
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
