// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// Recording indicating the view of the specified post is blocked.
    /// </summary>
    public sealed record BlockedPost : PostViewBase
    {
        [JsonConstructor]
        internal BlockedPost(AtUri uri)
        {
            Uri = uri;
        }

        /// <summary>
        /// Flag indicating the view of the post is blocked.
        /// </summary>
        [JsonIgnore]
        public bool Blocked { get; init; } = true;

        /// <summary>
        /// The <see cref="AtUri"/> of the blocked post.
        /// </summary>
        [JsonRequired]
        public AtUri Uri { get; init; }

    }
}
