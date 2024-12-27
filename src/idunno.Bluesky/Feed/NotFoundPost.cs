// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// Recording indicating specified post was not found.
    /// </summary>
    public record NotFoundPost : PostViewBase
    {
        [JsonConstructor]
        internal NotFoundPost(AtUri uri)
        {
            Uri = uri;
        }

        /// <summary>
        /// Flag indicating the post was not found.
        /// </summary>
        [JsonIgnore]
        public bool NotFound { get; init; } = true;

        /// <summary>
        /// The <see cref="AtUri"/> of the not found post.
        /// </summary>
        [JsonRequired]
        public AtUri Uri { get; init; }
    }
}
