// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto;

namespace idunno.Bluesky.Actor
{
    public record SavedFeedPreference : Preference
    {
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

        public IReadOnlyList<AtUri> Saved { get; init; }

        public IReadOnlyList<AtUri> Pinned { get; init; }

        public int? TimelineIndex { get; init; }

    }
}
