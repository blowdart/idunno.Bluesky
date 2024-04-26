// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto.Repo;

namespace idunno.AtProto.Bluesky.Feed
{
    public record FeedRepostRecord : FeedRecordBase
    {
        [JsonConstructor]
        public FeedRepostRecord(StrongReference subject, DateTimeOffset createdAt)
        {
            Subject = subject;
            CreatedAt = createdAt;
        }
       
        [JsonInclude]
        [JsonRequired]
        public StrongReference Subject { get; internal set; }
    }
}
