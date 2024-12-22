// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Graph
{
    /// <summary>
    /// Represents a view over a Starter Pack.
    /// </summary>
    public sealed record StarterPackView : StarterPackViewBasic
    {
        /// <summary>
        /// Create a new instance of <see cref="StarterPackViewBasic"/>
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the starter pack.</param>
        /// <param name="cid">The <see cref="AtProto.Cid"/> of the starter pack.</param>
        /// <param name="record">The record for the starter pack.</param>
        /// <param name="creator">The <see cref="ProfileViewBasic"/> of the starter pack creator.</param>
        /// <param name="listItemCount">The number of items in the list.</param>
        /// <param name="joinedWeekCount">The number of users who joined using the list in the last week.</param>
        /// <param name="joinedAllTimeCount">The overall number of users who joined using the list.</param>
        /// <param name="labels">Labels applied to the list.</param>
        /// <param name="indexedAt">The <see cref="DateTimeOffset"/> the list was indexed at.</param>
        /// <param name="list">A <see cref="ListViewBasic"/> over the starter pack.</param>
        /// <param name="listItemsSample">A collection of <see cref="ListItemView"/>s sampling the actors in the list.</param>
        /// <param name="feeds">A collection of <see cref="GeneratorView"/>s over any feeds in the list.</param>
        public StarterPackView(
            AtUri uri,
            Cid cid,
            StarterPackRecordValue record,
            ProfileViewBasic creator,
            int listItemCount,
            int joinedWeekCount,
            int joinedAllTimeCount,
            IReadOnlyCollection<Label>? labels,
            DateTimeOffset indexedAt,
            ListViewBasic list,
            IReadOnlyCollection<ListItemView>? listItemsSample,
            IReadOnlyCollection<GeneratorView>? feeds) : base(uri, cid, record, creator, listItemCount, joinedWeekCount, joinedAllTimeCount, labels, indexedAt)
        {
            if (listItemsSample is not null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(listItemsSample.Count, 12);
            }

            List = list;

            if (listItemsSample is not null)
            {
                ListItemsSample = new List<ListItemView>(listItemsSample).AsReadOnly();
            }
            else
            {
                ListItemsSample = new List<ListItemView>();
            }

            if (feeds is not null)
            {
                Feeds = new List<GeneratorView>(feeds).AsReadOnly();
            }
            else
            {
                Feeds = new List<GeneratorView>().AsReadOnly();
            }

        }

        /// <summary>
        /// Gets a <see cref="ListViewBasic"/> over the starter pack.
        /// </summary>
        public ListViewBasic List { get; init; }

        /// <summary>
        /// Gets a collection of <see cref="ListItemView"/>s sampling the actors in the list.
        /// </summary>
        public IReadOnlyCollection<ListItemView> ListItemsSample { get; init; }

        /// <summary>
        /// Gets a collection of <see cref="GeneratorView"/>s over any feeds in the list
        /// </summary>
        public IReadOnlyCollection<GeneratorView> Feeds { get; init; }
    }

}
