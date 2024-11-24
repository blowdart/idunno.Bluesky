// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Graph
{
    /// <summary>
    /// A list of suggested follows to a specified actor.
    /// </summary>
    public record SuggestedActors
    {
        [JsonConstructor]
        internal SuggestedActors(IReadOnlyList<ProfileView> suggestions, bool isFallback)
        {
            Suggestions = suggestions;
            IsFallback = isFallback;
        }

        /// <summary>
        /// A read-only list of suggested actors to follow
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public IReadOnlyList<ProfileView> Suggestions { get; init; }

        /// <summary>
        /// Flag indicating whether the api fell-back to generic results.
        /// </summary>
        [JsonInclude]
        public bool IsFallback { get; init; } = false;
    }
}
