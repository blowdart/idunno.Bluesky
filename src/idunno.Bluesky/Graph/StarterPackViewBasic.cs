// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;

// TODO: Update record type when https://github.com/bluesky-social/atproto/issues/2920 is closed.
namespace idunno.Bluesky.Graph
{
    public record StarterPackViewBasic : View
    {
        public StarterPackViewBasic(
            AtUri uri,
            Cid cid,
            StarterPackRecordValue record,
            ProfileViewBasic creator,
            int listItemCount,
            int joinedWeekCount,
            int joinedAllTimeCount,
            IReadOnlyCollection<Label>? labels,
            DateTimeOffset indexedAt)
        {
            Uri = uri;
            Cid = cid;
            StrongReference = new StrongReference(Uri, Cid);


            Record = record;
            Creator = creator;
            ListItemCount = listItemCount;
            JoinedWeekCount = joinedWeekCount;
            JoinedAllTimeCount = joinedAllTimeCount;

            if (labels is not null)
            {
                Labels = labels;
            }
            else
            {
                Labels = new List<Label>().AsReadOnly();
            }

            IndexedAt = indexedAt;
        }

        [JsonInclude]
        [JsonRequired]
        public AtUri Uri { get; init; }

        [JsonInclude]
        [JsonRequired]
        public Cid Cid { get; init; }

        [JsonIgnore]
        [NotNull]
        public StrongReference StrongReference { get; }

        // TODO: Validate this works
        [JsonRequired]
        public StarterPackRecordValue Record { get; init; }

        [JsonRequired]
        public ProfileViewBasic Creator { get; init; }

        public int ListItemCount { get; init; }

        public int JoinedWeekCount { get; init; }

        public int JoinedAllTimeCount { get; init; }

        public IReadOnlyCollection<Label> Labels { get; init; }

        [JsonRequired]
        public DateTimeOffset IndexedAt { get; init; }

    }
}
