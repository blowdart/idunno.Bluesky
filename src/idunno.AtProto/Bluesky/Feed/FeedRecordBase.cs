// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky.Feed
{
    [JsonDerivedType(typeof(FeedPostRecord), typeDiscriminator: CollectionType.Post)]
    [JsonDerivedType(typeof(FeedLikeRecord), typeDiscriminator: CollectionType.Like)]
    [JsonDerivedType(typeof(FeedFollowRecord), typeDiscriminator: CollectionType.Follow)]
    public abstract record FeedRecordBase
    {
        [JsonInclude]
        [JsonRequired]
        public DateTimeOffset CreatedAt { get; internal set; }
    }
}
