// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Graph
{
    // TODO: Guessing at feeds property definition - https://github.com/bluesky-social/atproto/issues/2920

    public record StarterPackRecordValue : BlueskyRecord
    {
        [JsonConstructor]
        public StarterPackRecordValue(
            string name,
            string description,
            AtUri list,
            IReadOnlyList<AtUri> feeds,
            DateTimeOffset createdAt,
            DateTimeOffset? updatedAt) : base(createdAt)
        {
            Name = name;
            Description = description;
            List = list;
            Feeds = feeds;
            UpdatedAt = updatedAt;
        }

        [JsonInclude]
        [JsonRequired]
        public string Name { get; set; }

        [JsonInclude]
        [JsonRequired]
        public string Description { get; init; }

        [JsonInclude]
        public AtUri List { get; set; }

        [JsonInclude]
        public IReadOnlyList<AtUri> Feeds { get; init; }

        [JsonInclude]
        public DateTimeOffset? UpdatedAt { get; init; }
    }
}
