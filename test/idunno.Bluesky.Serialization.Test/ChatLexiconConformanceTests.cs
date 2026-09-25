// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Chat;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class ChatLexiconConformanceTests
{
    [Fact]
    public void DeclarationDeserializesWhenAllowGroupInvitesIsAbsent()
    {
        // chat.bsky.actor.declaration only requires allowIncoming, so a record without
        // allowGroupInvites is valid and must round trip rather than yielding a null in a
        // non nullable property.
        Chat.Actor.Declaration? declaration = JsonSerializer.Deserialize<Chat.Actor.Declaration>(
            """{"$type": "chat.bsky.actor.declaration", "allowIncoming": "all"}""",
            BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(declaration);
        Assert.Equal("all", declaration.AllowIncoming);
        Assert.Null(declaration.AllowGroupInvites);
    }

    [Fact]
    public void DeclarationDeserializesWhenAllowGroupInvitesIsPresent()
    {
        Chat.Actor.Declaration? declaration = JsonSerializer.Deserialize<Chat.Actor.Declaration>(
            """{"$type": "chat.bsky.actor.declaration", "allowIncoming": "all", "allowGroupInvites": "following"}""",
            BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(declaration);
        Assert.Equal("following", declaration.AllowGroupInvites);
    }

    [Fact]
    public void DeclarationRejectsARecordWithNoAllowIncoming()
    {
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<Chat.Actor.Declaration>(
                """{"$type": "chat.bsky.actor.declaration", "allowGroupInvites": "all"}""",
                BlueskyServer.BlueskyJsonSerializerOptions));
    }

    [Fact]
    public void ConversationAvailabilityRejectsAResponseWithNoCanChat()
    {
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<ConversationAvailability>("{}", BlueskyServer.BlueskyJsonSerializerOptions));
    }

    [Fact]
    public void ConversationAvailabilityDeserializesCanChat()
    {
        ConversationAvailability? availability = JsonSerializer.Deserialize<ConversationAvailability>(
            """{"canChat": true}""",
            BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(availability);
        Assert.True(availability.CanChat);
        Assert.Null(availability.Conversation);
    }

    [Fact]
    public void ConversationViewDeserializesWhenMembersIsEmpty()
    {
        // convoView does not place a minLength on members, so an empty collection is a valid
        // response and must not throw out of the deserializer.
        ConversationView? conversationView = JsonSerializer.Deserialize<ConversationView>(
            """{"id": "convo", "rev": "1", "members": [], "muted": false, "unreadCount": 0}""",
            BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(conversationView);
        Assert.Empty(conversationView.Members);
    }

    [Fact]
    public void GetMessagesResponseCapturesRelatedProfiles()
    {
        Chat.Convo.Model.GetMessagesResponse? response = JsonSerializer.Deserialize<Chat.Convo.Model.GetMessagesResponse>(
            """
            {
              "messages": [],
              "relatedProfiles": [ { "did": "did:plc:abcdefghijklmnopqrstuvwx", "handle": "example.test" } ]
            }
            """,
            BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(response);
        Assert.NotNull(response.RelatedProfiles);
        Assert.Single(response.RelatedProfiles);
    }

    [Fact]
    public void GetMessagesResponseAllowsAbsentRelatedProfiles()
    {
        Chat.Convo.Model.GetMessagesResponse? response = JsonSerializer.Deserialize<Chat.Convo.Model.GetMessagesResponse>(
            """{"messages": []}""",
            BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(response);
        Assert.Null(response.RelatedProfiles);
    }
}
