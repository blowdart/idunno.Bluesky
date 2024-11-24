// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor
{
    public record SavedFeedPreferences2 : Preference
    {
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

        public IReadOnlyList<SavedFeedPreference2> Items { get; init; }
    }

    public record SavedFeedPreference2 : Preference
    {
        [JsonConstructor]
        public SavedFeedPreference2(string id, SavedFeedPreferenceType type, string value, bool pinned)
        {
            Id = id;
            Type = type;
            Value = value;
            Pinned = pinned;
        }

        public string Id { get; init; }

        public SavedFeedPreferenceType Type { get; init; }

        public string Value { get; init; }

        public bool Pinned { get; init; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SavedFeedPreferenceType
    {
        Feed,
        List,
        Timeline
    }
}
