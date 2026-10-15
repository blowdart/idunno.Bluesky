// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Serialization.Test;

public class ProfileAssociatedChatTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("all")]
    [InlineData("none")]
    [InlineData("following")]
    [InlineData("future-policy")]
    public void GroupInvitationPolicyPreservesRawWireValues(string? policy)
    {
        string property = policy is null ? string.Empty : $""","allowGroupInvites":"{policy}" """;
        string json = $$"""{"allowIncoming":"following"{{property}}}""";

        var chat = JsonSerializer.Deserialize<ProfileAssociatedChat>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(chat);
        Assert.Equal(AllowIncomingChat.Following, chat.AllowIncoming);
        Assert.Equal(policy, chat.AllowGroupInvites);
        using JsonDocument roundTrip = JsonDocument.Parse(JsonSerializer.Serialize(chat, BlueskyServer.BlueskyJsonSerializerOptions));
        if (policy is null)
        {
            Assert.False(roundTrip.RootElement.TryGetProperty("allowGroupInvites", out _));
        }
        else
        {
            Assert.Equal(policy, roundTrip.RootElement.GetProperty("allowGroupInvites").GetString());
        }
    }

    [Fact]
    public void ExistingConstructorRemainsCompatible()
    {
        ProfileAssociatedChat chat = new(AllowIncomingChat.All);

        Assert.Null(chat.AllowGroupInvites);
        Assert.Equal(AllowIncomingChat.All, chat.AllowIncoming);
        Assert.Equal("future-policy", (chat with { AllowGroupInvites = "future-policy" }).AllowGroupInvites);
        Assert.Null(chat.AllowGroupInvites);
    }
}
