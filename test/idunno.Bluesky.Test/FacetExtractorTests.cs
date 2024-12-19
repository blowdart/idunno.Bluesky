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
        [Fact]
        public async Task HashTagsShouldExtractCorrectly()
        {
            string text = "#one #two #three";

            DefaultFacetExtractor extractor = new(MockResolver);

            IList<Facet> results = await extractor.ExtractFacets(text, TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.NotEmpty(results);
            Assert.Equal(3, results.Count);
        }


        [Fact]
        public async Task LinksShouldExtractCorrectly()
        {
            string text = "http://idunno.org https://idunno.org http//notreal";

            DefaultFacetExtractor extractor = new(MockResolver);

            IList<Facet> results = await extractor.ExtractFacets(text, TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.NotEmpty(results);
            Assert.Equal(2, results.Count);
        }

        [Fact]
        public async Task MentionsShouldExtractAndResolveCorrectly()
        {
            string text = "@blowdart.me hello!, @blowdart.me hello again and hello @handle.invalid";

            DefaultFacetExtractor extractor = new(MockResolver);

            IList<Facet> results = await extractor.ExtractFacets(text, TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.NotEmpty(results);
            Assert.Equal(2, results.Count);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Mocking ResolveHandle() signature.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Mocking ResolveHandle().")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Mocking ResolveHandle() signature.")]
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<Did?> MockResolver(string handle, CancellationToken cancellationToken = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            if (string.Equals(handle, "handle.invalid"))
            {
                return null;
            }

            return new Did("did:plc:hfgp6pj3akhqxntgqwramlbg");
        }
    }
}
