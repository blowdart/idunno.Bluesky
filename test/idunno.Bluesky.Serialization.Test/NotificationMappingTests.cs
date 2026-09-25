// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Notifications;
using idunno.Bluesky.Notifications.Model;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class NotificationMappingTests
{
    private const string AuthorJson = """
        {
            "did": "did:plc:hfgp6pj3akhqxntgqwramlbg",
            "handle": "blowdart.me",
            "displayName": "Barry Dorrans",
            "description": "Security Curmudgeon",
            "indexedAt": "2024-08-12T16:22:13.776Z",
            "createdAt": "2023-04-22T22:44:04.316Z"
        }
        """;

    private static string BuildNotificationJson(string reason, string? reasonSubject = null, string labels = "[]")
    {
        string reasonSubjectProperty = reasonSubject is null
            ? string.Empty
            : $"\"reasonSubject\": \"{reasonSubject}\",";

        return $$"""
            {
                "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3l5w6ldfnud2g",
                "cid": "bafyreia4hewie6r5erf2s25wqh6f3k2mcy55ne5jcfgtpvnidt3gpsmtsi",
                "author": {{AuthorJson}},
                "reason": "{{reason}}",
                {{reasonSubjectProperty}}
                "record": {
                    "$type": "app.bsky.feed.post",
                    "createdAt": "2024-10-07T11:15:13.731Z",
                    "text": "Have a new notification"
                },
                "isRead": false,
                "indexedAt": "2024-10-07T11:15:13.731Z",
                "labels": {{labels}}
            }
            """;
    }

    private static Notification Deserialize(string reason, string? reasonSubject = null, string labels = "[]")
    {
        NotificationResponse response = JsonSerializer.Deserialize<NotificationResponse>(
            BuildNotificationJson(reason, reasonSubject, labels),
            BlueskyServer.BlueskyJsonSerializerOptions)!;

        return new Notification(response);
    }

    [Fact]
    public void ReasonSubjectIsCarriedThroughFromTheResponse()
    {
        const string reasonSubject = "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3kqxzemnnc425";

        Notification notification = Deserialize("like", reasonSubject);

        Assert.NotNull(notification.ReasonSubject);
        Assert.Equal(new AtUri(reasonSubject), notification.ReasonSubject);
    }

    [Fact]
    public void ReasonSubjectIsNullWhenTheResponseOmitsIt()
    {
        Notification notification = Deserialize("follow");

        Assert.Null(notification.ReasonSubject);
    }

    [Fact]
    public void AuthorRetainsTheProfileViewProperties()
    {
        Notification notification = Deserialize("mention");

        Assert.Equal("Security Curmudgeon", notification.Author.Description);
        Assert.Equal(new DateTimeOffset(2024, 8, 12, 16, 22, 13, 776, TimeSpan.Zero), notification.Author.IndexedAt);
    }

    [Theory]
    [InlineData("follow", NotificationReason.Follow)]
    [InlineData("like", NotificationReason.Like)]
    [InlineData("mention", NotificationReason.Mention)]
    [InlineData("reply", NotificationReason.Reply)]
    [InlineData("repost", NotificationReason.Repost)]
    [InlineData("quote", NotificationReason.Quote)]
    [InlineData("starterpack-joined", NotificationReason.StarterPackJoined)]
    [InlineData("verified", NotificationReason.Verified)]
    [InlineData("unverified", NotificationReason.Unverified)]
    [InlineData("like-via-repost", NotificationReason.LikeViaRepost)]
    [InlineData("repost-via-repost", NotificationReason.RepostViaRepost)]
    [InlineData("subscribed-post", NotificationReason.SubscribedPost)]
    [InlineData("contact-match", NotificationReason.ContactMatch)]
    public void EveryKnownReasonIsMapped(string reason, NotificationReason expected)
    {
        Notification notification = Deserialize(reason);

        Assert.Equal(expected, notification.Reason);
        Assert.Equal(reason, notification.RawReason);
    }

    [Fact]
    public void AnUnrecognisedReasonMapsToUnknownAndKeepsTheRawValue()
    {
        Notification notification = Deserialize("something-new");

        Assert.Equal(NotificationReason.Unknown, notification.Reason);
        Assert.Equal("something-new", notification.RawReason);
    }

    [Fact]
    public void LabelsAreEmptyRatherThanNullWhenTheResponseOmitsThem()
    {
        NotificationResponse response = JsonSerializer.Deserialize<NotificationResponse>(
            BuildNotificationJson("like").Replace("\"labels\": []", "\"labels\": null", StringComparison.Ordinal),
            BlueskyServer.BlueskyJsonSerializerOptions)!;

        Notification notification = new(response);

        Assert.NotNull(notification.Labels);
        Assert.Empty(notification.Labels);
    }

    [Fact]
    public void LabelsAreDefensivelyCopied()
    {
        List<Label> labels = [new Label(
            version: 1,
            source: new Did("did:plc:hfgp6pj3akhqxntgqwramlbg"),
            uri: "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3l5w6ldfnud2g",
            cid: null,
            value: "spam",
            isNegationLabel: false,
            creationTimestamp: DateTimeOffset.UtcNow,
            signature: [])];

        Notification notification = new(
            new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3l5w6ldfnud2g"),
            new Cid("bafyreia4hewie6r5erf2s25wqh6f3k2mcy55ne5jcfgtpvnidt3gpsmtsi"),
            JsonSerializer.Deserialize<Actor.ProfileView>(AuthorJson, BlueskyServer.BlueskyJsonSerializerOptions)!,
            "like",
            null,
            new Record.BlueskyRecord(),
            null,
            isRead: false,
            DateTimeOffset.UtcNow,
            labels);

        Assert.Single(notification.Labels);

        labels.Clear();

        Assert.Single(notification.Labels);
    }

    [Fact]
    public void SeenAtIsNullWhenTheResponseOmitsIt()
    {
        ListNotificationsResponse response = JsonSerializer.Deserialize<ListNotificationsResponse>(
            """{"notifications":[]}""",
            BlueskyServer.BlueskyJsonSerializerOptions)!;

        Assert.Null(response.SeenAt);
    }

    [Fact]
    public void ActivitySubscriptionIsNullWhenTheResponseOmitsIt()
    {
        SubjectActivitySubscription subscription = JsonSerializer.Deserialize<SubjectActivitySubscription>(
            """{"subject":"did:plc:hfgp6pj3akhqxntgqwramlbg"}""",
            BlueskyServer.BlueskyJsonSerializerOptions)!;

        Assert.Null(subscription.ActivitySubscription);
    }
}
