// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky.Feed
{
    [JsonDerivedType(typeof(FeedFollowRecord), typeDiscriminator: CollectionType.Follow)]
    [JsonDerivedType(typeof(FeedLikeRecord), typeDiscriminator: CollectionType.Like)]
    [JsonDerivedType(typeof(FeedPostRecord), typeDiscriminator: CollectionType.Post)]
    [JsonDerivedType(typeof(FeedRepostRecord), typeDiscriminator: CollectionType.Repost)]
    public abstract record FeedRecordBase
    {
        [JsonInclude]
        [JsonRequired]
        public DateTimeOffset CreatedAt { get; internal set; }
    }
}
