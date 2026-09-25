// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class RequiredResponsePropertyTests
{
    [Fact]
    public void NotificationGetPreferencesResponseRejectsAResponseWithNoPreferences()
    {
        // The lexicon declares preferences as required. Without JsonRequired an absent property deserializes
        // to null, which surfaces to the caller as a failed result carrying an OK status and no error detail.
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<Notifications.Model.GetPreferencesResponse>("{}", BlueskyServer.BlueskyJsonSerializerOptions));
    }

    [Fact]
    public void ChatGetPreferencesResponseRejectsAResponseWithNoPreferences()
    {
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<Chat.Model.GetPreferencesResponse>("{}", BlueskyServer.BlueskyJsonSerializerOptions));
    }

    [Fact]
    public void ChatPutPreferencesResponseRejectsAResponseWithNoPreferences()
    {
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<Chat.Model.PutPreferencesResponse>("{}", BlueskyServer.BlueskyJsonSerializerOptions));
    }

    [Theory]
    [InlineData("uri")]
    [InlineData("cid")]
    [InlineData("author")]
    [InlineData("reason")]
    [InlineData("record")]
    [InlineData("isRead")]
    [InlineData("indexedAt")]
    public void NotificationResponseRejectsAResponseMissingARequiredProperty(string omitted)
    {
        Dictionary<string, string> properties = new()
        {
            ["uri"] = "\"at://did:plc:ar7c4by46qjdydhdevvrndac/app.bsky.feed.post/3kk2bwvyjq52s\"",
            ["cid"] = "\"bafyreid27zk7lbis4zw5fz4podbvbs4fc5ivwji3dmrwa6zggnj4bnd57u\"",
            ["author"] = "{\"did\":\"did:plc:ar7c4by46qjdydhdevvrndac\",\"handle\":\"test.invalid\"}",
            ["reason"] = "\"like\"",
            ["record"] = "{\"$type\":\"app.bsky.feed.post\",\"text\":\"hello\",\"createdAt\":\"2024-02-08T09:00:00Z\"}",
            ["isRead"] = "false",
            ["indexedAt"] = "\"2024-02-08T09:00:00Z\"",
        };

        properties.Remove(omitted);

        string json = $"{{{string.Join(',', properties.Select(p => $"\"{p.Key}\":{p.Value}"))}}}";

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<Notifications.Model.NotificationResponse>(json, BlueskyServer.BlueskyJsonSerializerOptions));
    }
}
