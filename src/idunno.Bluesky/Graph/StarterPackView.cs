// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Feed;

namespace idunno.Bluesky.Graph
{
    public sealed record StarterPackView : StarterPackViewBasic
    {
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

        public ListViewBasic List { get; init; }

        public IReadOnlyCollection<ListItemView> ListItemsSample { get; init; }

        public IReadOnlyCollection<GeneratorView> Feeds { get; init; }
    }

}
