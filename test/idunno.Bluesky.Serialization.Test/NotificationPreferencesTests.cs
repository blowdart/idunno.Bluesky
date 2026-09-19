// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using idunno.Bluesky.Notifications;
using idunno.Bluesky.Notifications.PreferenceTypes;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class NotificationPreferencesTests
{
    private const string PreferencesJson = """
        {
          "chat": { "include": "all", "push": true },
          "follow": { "include": "all", "list": true, "push": true },
          "like": { "include": "follows", "list": true, "push": false },
          "likeViaRepost": { "include": "all", "list": true, "push": true },
          "mention": { "include": "all", "list": true, "push": true },
          "quote": { "include": "all", "list": true, "push": true },
          "reply": { "include": "all", "list": true, "push": true },
          "repost": { "include": "all", "list": true, "push": true },
          "repostViaRepost": { "include": "all", "list": true, "push": true },
          "starterpackJoined": { "list": true, "push": true },
          "subscribedPost": { "list": true, "push": true },
          "unverified": { "list": true, "push": true },
          "verified": { "list": false, "push": false }
        }
        """;

    private static Preferences Deserialize() =>
        JsonSerializer.Deserialize<Preferences>(PreferencesJson, BlueskyServer.BlueskyJsonSerializerOptions)!;

    [Fact]
    public void LikePreferenceIsDeserializedRatherThanFallingIntoExtensionData()
    {
        Preferences preferences = Deserialize();

        Assert.NotNull(preferences.Like);
        Assert.Equal(LimitTo.Follows, preferences.Like.Include);
        Assert.True(preferences.Like.List);
        Assert.False(preferences.Like.Push);

        Assert.DoesNotContain("like", preferences.ExtensionData.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public void StarterPackJoinedIsDeserializedRatherThanFallingIntoExtensionData()
    {
        Preferences preferences = Deserialize();

        Assert.NotNull(preferences.StarterPackJoined);
        Assert.True(preferences.StarterPackJoined.List);

        Assert.DoesNotContain("starterpackJoined", preferences.ExtensionData.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public void PreferencesCarryNoUnmappedExtensionData()
    {
        Preferences preferences = Deserialize();

        Assert.Empty(preferences.ExtensionData);
    }

    [Fact]
    public void SerializedPreferencesUseTheLexiconPropertyNames()
    {
        string json = JsonSerializer.Serialize(Deserialize(), BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.Contains("\"like\":", json, StringComparison.Ordinal);
        Assert.Contains("\"starterpackJoined\":", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"starterPackJoined\":", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(LimitTo.All, "all")]
    [InlineData(LimitTo.Follows, "follows")]
    public void FilterablePreferenceIncludeIsSerializedInLowerCase(LimitTo include, string expected)
    {
        string json = JsonSerializer.Serialize(
            new FilterablePreference(include, list: true, push: true),
            BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.Contains($"\"include\":\"{expected}\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ChatNotificationsFrom.All, "all")]
    [InlineData(ChatNotificationsFrom.Accepted, "accepted")]
    public void ChatPreferenceIncludeIsSerializedInLowerCase(ChatNotificationsFrom include, string expected)
    {
#pragma warning disable CS0618 // Type or member is obsolete - the lexicon still returns this property.
        string json = JsonSerializer.Serialize(
            new ChatPreference(include, push: true),
            BlueskyServer.BlueskyJsonSerializerOptions);
#pragma warning restore CS0618 // Type or member is obsolete

        Assert.Contains($"\"include\":\"{expected}\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void PreferencesRoundTripWithoutLoss()
    {
        Preferences original = Deserialize();

        string json = JsonSerializer.Serialize(original, BlueskyServer.BlueskyJsonSerializerOptions);
        Preferences roundTripped = JsonSerializer.Deserialize<Preferences>(json, BlueskyServer.BlueskyJsonSerializerOptions)!;

        Assert.Equal(original.Like.Include, roundTripped.Like.Include);
        Assert.Equal(original.Like.Push, roundTripped.Like.Push);
        Assert.Equal(original.StarterPackJoined.List, roundTripped.StarterPackJoined.List);
        Assert.Equal(original.Verified.List, roundTripped.Verified.List);
        Assert.Empty(roundTripped.ExtensionData);
    }
}
