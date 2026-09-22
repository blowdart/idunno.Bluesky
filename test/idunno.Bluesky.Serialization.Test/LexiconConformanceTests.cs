// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Notifications;
using idunno.Bluesky.Notifications.PreferenceTypes;
using idunno.Bluesky.Unspecced;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class LexiconConformanceTests
{
    [Fact]
    public void FeedGeneratorDescriptionDeserializesWhenLinksIsAbsent()
    {
        const string json = """{"did":"did:web:feed.example","feeds":[{"uri":"at://did:web:feed.example/app.bsky.feed.generator/one"}]}""";

        FeedGeneratorDescription? actual = JsonSerializer.Deserialize<FeedGeneratorDescription>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(actual);
        Assert.Null(actual.Links);
        Assert.Single(actual.Feeds);
    }

    [Fact]
    public void FeedGeneratorDescriptionThrowsWhenAFeedUriIsAbsent()
    {
        const string json = """{"did":"did:web:feed.example","feeds":[{}]}""";

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<FeedGeneratorDescription>(json, BlueskyServer.BlueskyJsonSerializerOptions));
    }

    [Theory]
    [InlineData("\"takendown\"", AccountStatus.Takendown)]
    [InlineData("\"deactivated\"", AccountStatus.Deactivated)]
    [InlineData("\"somethingNewUpstream\"", AccountStatus.Unknown)]
    public void AnAccountStatusDeserializesToItsKnownValueOrToUnknown(string json, AccountStatus expected)
    {
        AccountStatus actual = JsonSerializer.Deserialize<AccountStatus>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("\"mutuals\"", NotificationAllowedFrom.Mutuals)]
    [InlineData("\"somethingNewUpstream\"", NotificationAllowedFrom.Unknown)]
    public void ANotificationAllowedFromDeserializesToItsKnownValueOrToUnknown(string json, NotificationAllowedFrom expected)
    {
        NotificationAllowedFrom actual = JsonSerializer.Deserialize<NotificationAllowedFrom>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("\"accepted\"", ChatNotificationsFrom.Accepted)]
    [InlineData("\"somethingNewUpstream\"", ChatNotificationsFrom.Unknown)]
    public void AChatNotificationsFromDeserializesToItsKnownValueOrToUnknown(string json, ChatNotificationsFrom expected)
    {
        ChatNotificationsFrom actual = JsonSerializer.Deserialize<ChatNotificationsFrom>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("\"follows\"", LimitTo.Follows)]
    [InlineData("\"somethingNewUpstream\"", LimitTo.Unknown)]
    public void ALimitToDeserializesToItsKnownValueOrToUnknown(string json, LimitTo expected)
    {
        LimitTo actual = JsonSerializer.Deserialize<LimitTo>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("\"assured\"", AgeAssuranceStatus.Assured)]
    [InlineData("\"somethingNewUpstream\"", AgeAssuranceStatus.Unknown)]
    public void AnAgeAssuranceStatusDeserializesToItsKnownValueOrToUnknown(string json, AgeAssuranceStatus expected)
    {
        AgeAssuranceStatus actual = JsonSerializer.Deserialize<AgeAssuranceStatus>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AnUnknownEnumValueCannotBeSerialized()
    {
        Assert.Throws<JsonException>(
            () => JsonSerializer.Serialize(NotificationAllowedFrom.Unknown, BlueskyServer.BlueskyJsonSerializerOptions));
        Assert.Throws<JsonException>(
            () => JsonSerializer.Serialize(ChatNotificationsFrom.Unknown, BlueskyServer.BlueskyJsonSerializerOptions));
        Assert.Throws<JsonException>(
            () => JsonSerializer.Serialize(LimitTo.Unknown, BlueskyServer.BlueskyJsonSerializerOptions));
    }
}
