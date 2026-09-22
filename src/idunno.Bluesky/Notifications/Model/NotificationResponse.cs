// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Notifications.Model;

internal sealed record NotificationResponse(
    [property: JsonRequired] AtUri Uri,
    [property: JsonRequired] Cid Cid,
    [property: JsonRequired] ProfileView Author,
    [property: JsonRequired] string Reason,
    AtUri? ReasonSubject,
    [property: JsonRequired] BlueskyRecord Record,
    StarterPackViewBasic? StarterPack,
    [property: JsonRequired] bool IsRead,
    [property: JsonRequired] DateTimeOffset IndexedAt,
    IReadOnlyCollection<Label>? Labels);
