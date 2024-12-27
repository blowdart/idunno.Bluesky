// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// Encapsulates feed preferences for an actor
    /// </summary>
    public record SavedFeedPreference : Preference
    {
        /// <summary>
        /// Creates a new instance of <see cref="SavedFeedPreference"/>.
        /// </summary>
        /// <param name="saved">A read only list of <see cref="AtUri"/>s of feeds that the actor has saved.</param>
        /// <param name="pinned">A read only list of <see cref="AtUri"/>s of feeds that the actor has pinned</param>
        /// <param name="timelineIndex">The user's timeline index, if any.</param>
        [JsonConstructor]
        public SavedFeedPreference(IReadOnlyList<AtUri> saved, IReadOnlyList<AtUri> pinned, int? timelineIndex)
        {
            if (saved is null)
            {
                Saved = new List<AtUri>().AsReadOnly();
            }
            else
            {
                Saved = new List<AtUri>(saved).AsReadOnly();
            }

            if (pinned is null)
            {
                Pinned = new List<AtUri>().AsReadOnly();
            }
            else
            {
                Pinned = new List<AtUri>(pinned).AsReadOnly();
            }

            TimelineIndex = timelineIndex;
        }

        /// <summary>
        /// Gets a read only list of <see cref="AtUri"/>s of feeds that the actor has saved.
        /// </summary>
        public IReadOnlyList<AtUri> Saved { get; init; }

        /// <summary>
        /// Gets a read only list of <see cref="AtUri"/>s of feeds that the actor has pinned.
        /// </summary>
        public IReadOnlyList<AtUri> Pinned { get; init; }

        /// <summary>
        /// Gets the user's timeline index, if any.
        /// </summary>
        /// <remarks>
        /// <para>This property is undocumented in the lexicon, so the description is inferred.</para>
        /// </remarks>
        public int? TimelineIndex { get; init; }

    }
}
