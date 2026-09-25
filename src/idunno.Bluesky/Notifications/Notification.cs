// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;

using idunno.Bluesky.Actor;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Notifications.Model;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Notifications;

/// <summary>
/// A record containing information on a Bluesky notification.
/// </summary>
public sealed record Notification : AtProtoRepositoryObject
{
    internal Notification(NotificationResponse notificationResponse) : this(
        notificationResponse.Uri,
        notificationResponse.Cid,
        notificationResponse.Author,
        notificationResponse.Reason,
        notificationResponse.ReasonSubject,
        notificationResponse.Record,
        notificationResponse.StarterPack,
        notificationResponse.IsRead,
        notificationResponse.IndexedAt,
        notificationResponse.Labels)
    {
    }

    internal Notification(
        AtUri uri,
        Cid cid,
        ProfileView author,
        string reason,
        AtUri? reasonSubject,
        BlueskyRecord record,
        StarterPackViewBasic? starterPack,
        bool isRead,
        DateTimeOffset indexedAt,
        IReadOnlyCollection<Label>? labels) : base(uri, cid)
    {
        Author = author;
        RawReason = reason;
        ReasonSubject = reasonSubject;
        Record = record;
        IsRead = isRead;
        IndexedAt = indexedAt;
        StarterPack = starterPack;

        Labels = labels!;

        // As JsonStringEnumConvertor doesn't have the ability to fall back to a default, we have to do this.
        Reason = reason.ToUpperInvariant() switch
        {
            "FOLLOW" => NotificationReason.Follow,
            "LIKE" => NotificationReason.Like,
            "MENTION" => NotificationReason.Mention,
            "REPLY" => NotificationReason.Reply,
            "REPOST" => NotificationReason.Repost,
            "QUOTE" => NotificationReason.Quote,
            "STARTERPACK-JOINED" => NotificationReason.StarterPackJoined,
            "VERIFIED" => NotificationReason.Verified,
            "UNVERIFIED" => NotificationReason.Unverified,
            "LIKE-VIA-REPOST" => NotificationReason.LikeViaRepost,
            "REPOST-VIA-REPOST" => NotificationReason.RepostViaRepost,
            "SUBSCRIBED-POST" => NotificationReason.SubscribedPost,
            "CONTACT-MATCH" => NotificationReason.ContactMatch,
            _ => NotificationReason.Unknown,
        };
    }

    /// <summary>
    /// Gets the <see cref="ProfileView"/> of the actor that triggered the notification.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public ProfileView Author { get; init; }

    /// <summary>
    /// Gets the raw reason for the notification.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("reason")]
    public string RawReason { get; init; }

    /// <summary>
    /// Gets the reason for the notification.
    /// </summary>
    /// <remarks><para>If the reason is <see cref="NotificationReason.Unknown"/> the unparsed reason can be accessed via <see cref="RawReason"/>.</para></remarks>
    [JsonIgnore]
    public NotificationReason Reason { get; init; }

    /// <summary>
    /// Gets the <see cref="AtUri"/> of the subject that triggered the notification, if any.
    /// </summary>
    [JsonInclude]
    public AtUri? ReasonSubject { get; init; }

    /// <summary>
    /// Gets the underlying <see cref="BlueskyRecord"/> for the notification.
    /// </summary>
    [JsonInclude]
    public BlueskyRecord Record { get; init; }

    /// <summary>
    /// Gets the starter pack associated with the notification, if any. Present when the notification is for a follow originating from a starter pack.
    /// </summary>
    public StarterPackViewBasic? StarterPack { get; init; }

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
    public IReadOnlyCollection<Label> Labels
    {
        get;
        init => field = value is null ? ReadOnlyCollection<Label>.Empty : new List<Label>(value).AsReadOnly();
    }
}