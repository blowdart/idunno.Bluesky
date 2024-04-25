// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky.Feed
{
    public record FeedPost
    {
        [JsonConstructor]
        internal FeedPost(AtUri uri, AtCid cid, Author author, FeedPostRecord record)
        {
            Uri = uri;
            Cid = cid;
            Author = author;
            Record = record;
        }

        [JsonInclude]
        [JsonRequired]
        public AtUri Uri { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public AtCid Cid { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public Author Author { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public FeedPostRecord Record { get; internal set; }

        [JsonInclude]
        public int ReplyCount { get; internal set; }

        [JsonInclude]
        public int RepostCount { get; internal set; }

        [JsonInclude]
        public int LikeCount { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public DateTime IndexedAt { get; internal set; }

        // TODO: view, labels, threadgate

    }
}
