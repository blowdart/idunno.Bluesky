// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Notifications.Model
{
    internal sealed record NotificationResponse(
        AtUri Uri,
        Cid Cid,
        ProfileViewBasic Author,
        string Reason,
        BlueskyRecord Record,
        bool IsRead,
        DateTimeOffset IndexedAt,
        IReadOnlyCollection<Label>? Labels);
}
