// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Graph
{
    public class ListViewerState
    {
        [JsonConstructor]
        public ListViewerState(bool? muted, AtUri? blockedBy)
        {
            if (muted is not null)
            {
                Muted = (bool)muted;
            }

            if (blockedBy is not null)
            {
                BlockedBy = blockedBy;
            }
        }

        [NotNull]
        [JsonInclude]
        public bool? Muted { get; init; } = false;

        [JsonInclude]
        public AtUri? BlockedBy { get; init; }
    }
}
