// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Graph
{
    /// <summary>
    /// Represents the 
    /// </summary>
    public class ListViewerState
    {
        /// <summary>
        /// Creates a new instance of <see cref="ListViewerState"/>.
        /// </summary>
        /// <param name="muted">A flag indicating whether the actor has been muted by a list.</param>
        /// <param name="blockedBy">The <see cref="AtUri"/> of the block record if the actor has been blocked by a list.</param>
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

        /// <summary>
        /// Gets a flag indicating whether the actor has been muted by a list.
        /// </summary>
        [NotNull]
        [JsonInclude]
        public bool? Muted { get; init; } = false;

        /// <summary>
        /// Gets the <see cref="AtUri"/> of the block record if the actor has been blocked by a list.
        /// </summary>
        [JsonInclude]
        public AtUri? BlockedBy { get; init; }
    }
}
