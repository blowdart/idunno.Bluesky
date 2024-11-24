// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// Information about a feed generator.
    /// </summary>
    public sealed record FeedGenerator
    {
        [JsonConstructor]
        internal FeedGenerator(GeneratorView view, bool isOnline, bool isValid)
        {
            View = view;
            IsOnline = isOnline;
            IsValid = isValid;
        }

        /// <summary>
        /// Information about the feed generator.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public GeneratorView View { get; init; }

        /// <summary>
        /// Flag indicating whether the feed generator service has been online recently, or else seems to be inactive.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public bool IsOnline { get; init; }

        /// <summary>
        /// Flag indicating whether the feed generator service is compatible with the record declaration.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public bool IsValid { get; init; }
    }
}
