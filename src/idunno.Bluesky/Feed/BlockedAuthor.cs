// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// A record indicating that the author specified by <see cref="Did"/> is blocked.
    /// </summary>
    public sealed record BlockedAuthor
    {
        [JsonConstructor]
        internal BlockedAuthor(Did did, ActorViewerState? viewer)
        {
            Did = did;
            Viewer = viewer;
        }

        /// <summary>
        /// The <see cref="Did"/> of the blocked author.
        /// </summary>
        [JsonRequired]
        [JsonInclude]
        public Did Did { get; init; }

        /// <summary>
        /// Metadata about the requesting account's relationship with the author.
        /// </summary>
        [JsonInclude]
        public ActorViewerState? Viewer { get; init; }
    }
}
