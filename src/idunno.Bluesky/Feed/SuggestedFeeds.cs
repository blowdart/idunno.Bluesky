// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// A readonly collection of <see cref="GeneratorView"/>s of suggested feeds for the current user.
    /// </summary>
    public sealed class SuggestedFeeds : PagedViewReadOnlyCollection<GeneratorView>
    {
        internal SuggestedFeeds() : this(new List<GeneratorView>(), null)
        {
        }

        internal SuggestedFeeds(IList<GeneratorView> list, string? cursor = null) : base(list, cursor)
        {
        }

        internal SuggestedFeeds(ICollection<GeneratorView> collection, string? cursor = null) : this(new List<GeneratorView>(collection), cursor)
        {
        }
    }
}
