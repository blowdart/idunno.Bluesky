// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;

using idunno.Bluesky.Actor;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Notifications
{
    /// <summary>
    /// A record containing information on a Bluesky notification.
    /// </summary>
    public sealed record Notification : AtProtoRepositoryObject
    {
        [JsonConstructor]
        internal Notification(
            AtUri uri,
            Cid cid,
            ProfileViewBasic author,
            NotificationReason reason,
            BlueskyRecord record,
            bool isRead,
            DateTimeOffset indexedAt,
            IReadOnlyCollection<Label>? labels) : base(uri, cid)
        {
            Author = author;
            Reason = reason;
            Record = record;
            IsRead = isRead;
            IndexedAt = indexedAt;

            if (labels is not null)
            {
                Labels = labels;
            }
            else
            {
                Labels = new List<Label>().AsReadOnly<Label>();
            }
        }

        /// <summary>
        /// Gets the <see cref="ProfileViewBasic"/> of the actor that triggered the notification.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public ProfileViewBasic Author { get; init; }

        /// <summary>
        /// Gets the reason for the notification.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public NotificationReason Reason { get; init; }

        /// <summary>
        /// Gets the <see cref="AtUri"/> of the subject that triggered the notification. if any.
        /// </summary>
        [JsonInclude]
        public AtUri? ReasonSubject { get; init; }

        /// <summary>
        /// Gets the underlying <see cref="BlueskyRecord"/> for the notification.
        /// </summary>
        [JsonInclude]
        public BlueskyRecord Record { get; init; }

        /// <summary>
        /// Gets a flag indicating whether the notification has been read by the authenticated user.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public bool IsRead { get; init; }

        /// <summary>
        /// Gets the <see cref="DateTimeOffset"/> the notification record was indexed on.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public DateTimeOffset IndexedAt { get; init; }

        /// <summary>
        /// Gets a collection of <see cref="Label"/>s for the notification.
        /// </summary>
        [JsonInclude]
        public IReadOnlyCollection<Label> Labels { get; init; }
    }
}
