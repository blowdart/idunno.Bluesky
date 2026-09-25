// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class ActorOpenUnionTests
{
    private static readonly JsonSerializerOptions s_options = BlueskyServer.BlueskyJsonSerializerOptions;

    [Theory]
    [InlineData("oldest", ThreadSortingMode.Oldest)]
    [InlineData("newest", ThreadSortingMode.Newest)]
    [InlineData("most-likes", ThreadSortingMode.MostLikes)]
    [InlineData("random", ThreadSortingMode.Random)]
    [InlineData("hotness", ThreadSortingMode.Hotness)]
    [InlineData("somethingNew", ThreadSortingMode.Unknown)]
    public void ThreadViewPreferenceReadsTheSortPropertyTheLexiconDeclares(string sort, ThreadSortingMode expected)
    {
        ThreadViewPreference? preference =
            JsonSerializer.Deserialize<ThreadViewPreference>($$"""{"sort":"{{sort}}"}""", s_options);

        Assert.NotNull(preference);
        Assert.Equal(expected, preference.SortingMode);
    }

    [Theory]
    [InlineData("""{"sort":"hotness","prioritizeFollowedUsers":true}""")]
    [InlineData("""{"sort":"somethingNew"}""")]
    public void ThreadViewPreferenceRoundTripsWhatTheServiceSent(string json)
    {
        ThreadViewPreference? preference = JsonSerializer.Deserialize<ThreadViewPreference>(json, s_options);

        Assert.Equal(json, JsonSerializer.Serialize(preference, s_options));
    }

    [Theory]
    [InlineData("""{"label":"spam","visibility":"hide"}""", LabelVisibility.Hide)]
    [InlineData("""{"label":"spam","visibility":"somethingNew"}""", LabelVisibility.Unknown)]
    public void ContentLabelPreferenceRoundTripsWhatTheServiceSent(string json, LabelVisibility expected)
    {
        ContentLabelPreference? preference = JsonSerializer.Deserialize<ContentLabelPreference>(json, s_options);

        Assert.NotNull(preference);
        Assert.Equal(expected, preference.Visibility);
        Assert.Equal(json, JsonSerializer.Serialize(preference, s_options));
    }

    [Theory]
    [InlineData("""{"id":"1","type":"timeline","value":"following","pinned":true}""", SavedFeedPreferenceType.Timeline)]
    [InlineData("""{"id":"1","type":"somethingNew","value":"following","pinned":true}""", SavedFeedPreferenceType.Unknown)]
    public void SavedFeedRoundTripsWhatTheServiceSent(string json, SavedFeedPreferenceType expected)
    {
        SavedFeed? savedFeed = JsonSerializer.Deserialize<SavedFeed>(json, s_options);

        Assert.NotNull(savedFeed);
        Assert.Equal(expected, savedFeed.Type);
        Assert.Equal(json, JsonSerializer.Serialize(savedFeed, s_options));
    }

    [Theory]
    [InlineData("""{"value":"word","targets":["content","tag"]}""")]
    [InlineData("""{"value":"word","targets":["somethingNew"],"actorTarget":"somethingElse"}""")]
    public void MutedWordRoundTripsWhatTheServiceSent(string json)
    {
        MutedWord? mutedWord = JsonSerializer.Deserialize<MutedWord>(json, s_options);

        Assert.Equal(json, JsonSerializer.Serialize(mutedWord, s_options));
    }

    [Fact]
    public void MutedWordReportsTheLexiconDefaultActorTargetWhenTheServiceSendsNone()
    {
        MutedWord? mutedWord =
            JsonSerializer.Deserialize<MutedWord>("""{"value":"word","targets":["content"]}""", s_options);

        Assert.NotNull(mutedWord);
        Assert.Equal(MutedWordActorTarget.All, mutedWord.ActorTarget);
        Assert.Equal(MutedWordTarget.Content, Assert.Single(mutedWord.Targets));
    }

    [Fact]
    public void MutedWordReportsAnUnrecognizedTargetAsUnknown()
    {
        MutedWord? mutedWord = JsonSerializer.Deserialize<MutedWord>(
            """{"value":"word","targets":["somethingNew"],"actorTarget":"somethingElse"}""", s_options);

        Assert.NotNull(mutedWord);
        Assert.Equal(MutedWordTarget.Unknown, Assert.Single(mutedWord.Targets));
        Assert.Equal(MutedWordActorTarget.Unknown, mutedWord.ActorTarget);
    }

    [Theory]
    [InlineData("none", AllowIncomingChat.None)]
    [InlineData("all", AllowIncomingChat.All)]
    [InlineData("following", AllowIncomingChat.Following)]
    [InlineData("somethingNew", AllowIncomingChat.Unknown)]
    public void ProfileAssociatedChatReportsAnUnrecognizedAllowIncomingChatAsUnknown(string value, AllowIncomingChat expected)
    {
        ProfileAssociatedChat? chat =
            JsonSerializer.Deserialize<ProfileAssociatedChat>($$"""{"allowIncoming":"{{value}}"}""", s_options);

        Assert.NotNull(chat);
        Assert.Equal(expected, chat.AllowIncoming);
    }

    [Fact]
    public void ThreadViewPreferenceCannotBeConstructedFromAnUnknownSortingMode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ThreadViewPreference(ThreadSortingMode.Unknown));
    }

    [Fact]
    public void SavedFeedCannotBeConstructedFromAnUnknownType()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SavedFeed("id", SavedFeedPreferenceType.Unknown, "value", pinned: false));
    }

    [Fact]
    public void ContentLabelPreferenceCannotBeConstructedFromAnUnknownVisibility()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ContentLabelPreference("spam", labelerDid: null, LabelVisibility.Unknown));
    }

    [Fact]
    public void MutedWordCannotBeConstructedFromAnUnknownTarget()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MutedWord(null, "word", [MutedWordTarget.Unknown], MutedWordActorTarget.All, null));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MutedWord(null, "word", [MutedWordTarget.Content], MutedWordActorTarget.Unknown, null));
    }
}
