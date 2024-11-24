// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Graph
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public sealed record ListView : ListViewBasic
    {
        // TODO: Description facets

        [JsonConstructor]
        public ListView(
            AtUri uri,
            Cid cid,
            ProfileView creator,
            string name,
            ListPurpose purpose,
            string description,
            Uri? avatar,
            int? listItemCount,
            IReadOnlyCollection<Label>? labels,
            ListViewerState? viewer,
            DateTimeOffset? indexedAt) : base(uri, cid, name, purpose, avatar, listItemCount, labels, viewer, indexedAt)
        {
            Creator = creator;
            Description = description;
        }

        [JsonRequired]
        public ProfileView Creator { get; init; }

        [JsonRequired]
        public string Description { get; init; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                if (!string.IsNullOrEmpty(Description))
                {
                    return '{' + $"{Name} : {Description} ({Uri})" + "}";
                }
                else
                {
                    return '{' + $"{Name} ({Uri}" + "}";
                }
            }
        }
    }
}
