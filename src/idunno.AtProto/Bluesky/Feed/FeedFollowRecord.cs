// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky.Feed
{
    public record FeedFollowRecord : FeedRecordBase
    {
        [JsonConstructor]
        public FeedFollowRecord(Did subject, DateTimeOffset createdAt)
        {
            Subject = subject;
            CreatedAt = createdAt;
        }
       
        [JsonInclude]
        [JsonRequired]
        public Did Subject { get; internal set; }

    }
}
