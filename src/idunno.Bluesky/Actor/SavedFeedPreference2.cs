// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// Encapsulates an actor's feed preferences.
    /// </summary>
    public record SavedFeedPreferences2 : Preference
    {
        /// <summary>
        /// Creates a new instance of <see cref="SavedFeedPreferences2"/>.
        /// </summary>
        /// <param name="items">A list of feed preferences.</param>
        [JsonConstructor]
        public SavedFeedPreferences2(IReadOnlyList<SavedFeedPreference2> items)
        {
            if (items is null)
            {
                Items = new List<SavedFeedPreference2>().AsReadOnly();
            }
            else
            {
                Items = new List<SavedFeedPreference2>(items).AsReadOnly();
            }
        }

        /// <summary>
        /// Gets a readonly list of an actors saved feed preferences.
        /// </summary>
        public IReadOnlyList<SavedFeedPreference2> Items { get; init; }
    }

    /// <summary>
    /// Encapsulates feed preferences for an actor
    /// </summary>
    public record SavedFeedPreference2 : Preference
    {
        /// <summary>
        /// Creates a new instance of <see cref="SavedFeedPreference2"/>
        /// </summary>
        /// <param name="id">The identifier of the feed preference.</param>
        /// <param name="type">The type of the feed preference.</param>
        /// <param name="value">The value of the feed preference.</param>
        /// <param name="pinned">A flag indicating whether the feed is pinned.</param>
        [JsonConstructor]
        public SavedFeedPreference2(string id, SavedFeedPreferenceType type, string value, bool pinned)
        {
            Id = id;
            Type = type;
            Value = value;
            Pinned = pinned;
        }

        /// <summary>
        /// Gets the identifier of the feed preference.
        /// </summary>
        public string Id { get; init; }

        /// <summary>
        /// Gets the type of the feed preference.
        /// </summary>
        public SavedFeedPreferenceType Type { get; init; }

        /// <summary>
        /// Gets the value of the feed preference.
        /// </summary>
        public string Value { get; init; }

        /// <summary>
        /// Gets a flag indicating whether the feed is pinned.
        /// </summary>
        public bool Pinned { get; init; }
    }

    /// <summary>
    /// Values indicating what type of feed the preference applies to.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SavedFeedPreferenceType
    {
        /// <summary>
        /// The preference applies to a feed.
        /// </summary>
        Feed,

        /// <summary>
        /// The preference applies to a list.
        /// </summary>
        List,

        /// <summary>
        /// The preference applies to the user's timeline.
        /// </summary>
        Timeline
    }
}
