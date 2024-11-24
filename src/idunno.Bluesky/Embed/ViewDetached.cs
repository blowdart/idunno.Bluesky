// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Embed
{
    /// <summary>
    /// An indicator that the view for the specified AT URI is detached.
    /// </summary>
    public record ViewDetached : View
    {
        /// <summary>
        /// Creates a new instance of <see cref="ViewDetached"/>
        /// </summary>
        /// <param name="uri">The <see cref="AtUri"/> of the view that is detached.</param>
        internal ViewDetached(AtUri uri)
        {
            Uri = uri;
        }

        /// <summary>
        /// The <see cref="AtUri"/> of the view that is detached.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public AtUri Uri { get; init; }

        /// <summary>
        /// Flag indicating the view is detached.
        /// </summary>
        [JsonIgnore]
        public static bool Detached => true;
    }
}
