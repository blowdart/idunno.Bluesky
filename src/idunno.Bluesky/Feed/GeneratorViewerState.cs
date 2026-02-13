// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// Information on the relationship of the current actor to a feed generator.
    /// </summary>
    public record GeneratorViewerState
    {
        /// <summary>
        /// Creates a new instance of <see cref="GeneratorViewerState"/>.
        /// </summary>
        /// <param name="likeUri">An optional <see cref="AtUri"/> to the like record if the actor has liked this feed.</param>
        [JsonConstructor]
        internal GeneratorViewerState(AtUri? likeUri)
        {
            LikeUri = likeUri;
        }

        /// <summary>
        /// An <see cref="AtUri"/> to the like record if the actor has liked this feed,
        /// otherwise <see langword="null"/>.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("like")]
        public AtUri? LikeUri { get; init; }
    }
}
