// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Feed;

namespace idunno.Bluesky.Embed
{
    /// <summary>
    /// An indicator that the view for the specified AT URI is blocked.
    /// </summary>
    public sealed record ViewBlocked : View
    {
        internal ViewBlocked(AtUri uri, BlockedAuthor blockedAuthor)
        {
            Uri = uri;
            BlockedAuthor = blockedAuthor;
        }

        /// <summary>
        /// The <see cref="AtUri"/> of the view that is blocked.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public AtUri Uri { get; init; }

        /// <summary>
        /// Flag indicating the view is blocked.
        /// </summary>
        [JsonIgnore]
        public static bool Blocked => true;

        /// <summary>
        /// Information on the <see cref="BlockedAuthor"/>
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public BlockedAuthor BlockedAuthor { get; init; }
    }
}
