// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Feed
{
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

        public int? TotalHits { get; init; }
    }
}
