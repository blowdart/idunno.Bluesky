// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky.Feed
{
    public record Viewer
    {
        [JsonConstructor]
        public Viewer(bool blockedBy, bool muted)
        {
            BlockedBy = blockedBy;
            Muted = muted;
        }

        [JsonInclude]
        public bool Muted { get; internal set; }

        [JsonInclude]
        public bool BlockedBy { get; internal set; }

        [JsonInclude]
        public AtUri? Following { get; internal set; }

        [JsonInclude]
        public AtUri? FollowedBy { get; internal set; }

    }
}
