// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Bluesky.Feed;
using idunno.AtProto.Repo;

namespace idunno.AtProto.Bluesky.Notifications
{
    public class Notification
    {
        public Notification(AtUri uri, AtCid cid, Author author, NotificationReason reason, FeedRecordBase record, bool isRead, DateTimeOffset indexedAt)
        {
            Uri = uri;
            Cid = cid;
            Author = author;
            Reason = reason;
            Record = record;
            IsRead = isRead;
            IndexedAt = indexedAt;
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
        public NotificationReason Reason { get; internal set; }

        [JsonInclude]
        public AtUri? ReasonSubject { get; internal set; }

        [JsonInclude]
        public FeedRecordBase Record { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public bool IsRead { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public DateTimeOffset IndexedAt { get; internal set; }

        [JsonInclude]
        public IReadOnlyCollection<Label> Labels { get; internal set; } = new List<Label>();
    }
}
