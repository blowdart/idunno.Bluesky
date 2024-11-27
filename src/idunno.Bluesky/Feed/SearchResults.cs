// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// A collection of <see cref="PostView"/>s returned from <see cref="BlueskyAgent.SearchPosts(string, DateOnly, DateOnly, SearchOrder?, AtProto.AtIdentifier?, AtProto.AtIdentifier?, string?, string?, Uri?, ICollection{string}?, int?, string?, IEnumerable{AtProto.Did}?, CancellationToken)"/>
    /// </summary>
    public sealed class SearchResults : PagedViewReadOnlyCollection<PostView>
    {
        internal SearchResults(IList<PostView> results, int? totalHits, string? cursor = null) : base(results, cursor)
        {
            TotalHits = totalHits;
        }

        internal SearchResults(IEnumerable<PostView> results, int? totalHits, string? cursor = null) : this(new List<PostView>(results), totalHits, cursor)
        {
        }

        internal SearchResults(int? totalHits = null, string? cursor = null) : this(new List<PostView>(), totalHits, cursor)
        {
        }

        /// <summary>
        /// Gets the total number of hits from the search, if any.
        /// </summary>
        public int? TotalHits { get; init; }
    }
}
