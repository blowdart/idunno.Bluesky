// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Graph
{
    /// <summary>
    /// A list of suggested follows to a specified actor.
    /// </summary>
    public record SuggestedActors
    {
        internal SuggestedActors(IReadOnlyList<ProfileView> suggestions, bool isFallback)
        {
            Suggestions = suggestions;
            IsFallback = isFallback;
        }

        [JsonConstructor]
        internal SuggestedActors(IReadOnlyList<ProfileView> suggestions, bool? isFallback, long? recId, string? recIdStr)
        {
            Suggestions = suggestions;
            IsFallback = isFallback;
#pragma warning disable 612, 618
            RecId = recId;
#pragma warning restore 612, 618
            RecIdStr = recIdStr;
        }

        /// <summary>
        /// A read-only list of suggested actors to follow
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public IReadOnlyList<ProfileView> Suggestions { get; init; }

        /// <summary>
        /// Flag indicating whether the api fell-back to generic results and is not scoped using relativeToDid.
        /// </summary>
        [JsonInclude]
        public bool? IsFallback { get; init; } = false;

        /// <summary>
        /// Deprecated, use <see cref="RecIdStr"/> instead.
        /// </summary>
        [JsonInclude]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("This property is obsolete. Use RecIdStr instead", false)]
        [SuppressMessage("Info Code Smell", "S1133:Deprecated code should be removed", Justification = "Back compatibility.")]
        public long? RecId { get; init; }

        /// <summary>
        /// Snowflake for this recommendation, use when submitting recommendation events.
        /// </summary>
        [JsonInclude]
        public string? RecIdStr { get; init; }
    }
}
