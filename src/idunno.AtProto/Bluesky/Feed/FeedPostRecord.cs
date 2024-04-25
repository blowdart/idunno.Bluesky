// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky.Feed
{
    public record FeedPostRecord : FeedRecordBase
    {
        [JsonConstructor]
        public FeedPostRecord(string text, DateTimeOffset createdAt)
        {
            Text = text;
            CreatedAt = createdAt;
        }
       
        [JsonInclude]
        [JsonRequired]
        public string Text { get; internal set; }

        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<Facet> Facets { get; internal set; } = new List<Facet>();

        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<string> Langs { get; internal set; } = new List<string>();

        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public FeedPostReply? Reply { get; internal set; }
    }
}
