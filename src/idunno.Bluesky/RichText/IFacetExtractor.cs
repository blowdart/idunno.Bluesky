// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.RichText
{
    /// <summary>
    /// Exposes a method that extracts facets from a text string.
    /// </summary>
    public interface IFacetExtractor
    {
        /// <summary>
        /// Extracts facets from <paramref name="text"/>.
        /// </summary>
        /// <param name="text">The string to extract facets from.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        Task<IList<Facet>> ExtractFacets(string text, CancellationToken cancellationToken = default);
    }
}
