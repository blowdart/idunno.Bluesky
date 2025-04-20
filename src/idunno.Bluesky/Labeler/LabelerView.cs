// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Labeler
{
    /// <summary>
    /// Encapsulates a detailed view over a labeller.
    /// </summary>
    [JsonDerivedType(typeof(LabelerView), typeDiscriminator: "app.bsky.labeler.defs#labelerView")]
    [JsonDerivedType(typeof(LabelerViewDetailed), typeDiscriminator: "app.bsky.labeler.defs#labelerViewDetailed")]
    public record LabelerView
    {
        /// <summary>
        /// Gets the <see cref="AtUri"/> of the labeler.
        /// </summary>
        public required AtUri Uri { get; init; }

        /// <summary>
        /// Gets the <see cref="Cid">Content Identifier</see> of the labeler.
        /// </summary>
        public required Cid Cid { get; init; }

        /// <summary>
        /// Gets a <see cref="ProfileView"/> of the labeler creator.
        /// </summary>
        public required ProfileView Creator { get; init; }


        /// <summary>
        /// Gets the number of times the labeller has been liked.
        /// </summary>
        public uint LikeCount { get; init; }

        /// <summary>
        /// Gets a view over the relationship between the actor who requested the view and the labeller.
        /// </summary>
        public LabelerViewerState? Viewer { get; init; }

        /// <summary>
        /// Gets the <see cref="DateTimeOffset"/> the labeler was last indexed on.
        /// </summary>
        public required DateTimeOffset IndexedAt { get; init; }

        /// <summary>
        /// Gets the labels applied to the labeller
        /// </summary>
        public ICollection<Label> Labels { get; } = [];
    }
}
