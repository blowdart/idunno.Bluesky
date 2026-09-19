// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Feed.Gates;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class ActorRecordValidationTests
{
    // One grapheme, two UTF-16 code units, four UTF-8 bytes.
    private const string FourByteGrapheme = "\U0001F600";

    // One grapheme, eleven UTF-16 code units, twenty five UTF-8 bytes.
    private const string FamilyEmoji = "\U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466";

    private static readonly Did s_did = new("did:plc:identifier");

    private static readonly Handle s_handle = new("test.invalid");

    [Fact]
    public void ProfileViewBasicRejectsADisplayNameOverTheByteLimitEvenWhenItsCharacterLengthIsUnder()
    {
        // 50 family emoji is 1250 UTF-8 bytes but only 550 UTF-16 code units and 50 graphemes, so it is within
        // the grapheme limit and within a .Length check against the byte limit, and is only rejected when the
        // byte limit is measured in actual UTF-8 bytes.
        string displayName = string.Concat(Enumerable.Repeat(FamilyEmoji, 50));

        Assert.True(displayName.Length <= Maximum.DisplayNameLengthInBytes);
        Assert.True(displayName.GetGraphemeLength() <= Maximum.DisplayNameLengthInGraphemes);
        Assert.True(displayName.GetUtf8Length() > Maximum.DisplayNameLengthInBytes);

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateProfileViewBasic(displayName));
    }

    [Fact]
    public void ProfileViewBasicAcceptsADisplayNameWithinBothLimits()
    {
        string displayName = string.Concat(Enumerable.Repeat(FourByteGrapheme, 64));

        Assert.True(displayName.GetUtf8Length() <= Maximum.DisplayNameLengthInBytes);
        Assert.True(displayName.GetGraphemeLength() <= Maximum.DisplayNameLengthInGraphemes);

        ProfileViewBasic profile = CreateProfileViewBasic(displayName);

        Assert.Equal(displayName, profile.DisplayName);
    }

    [Fact]
    public void InterestsPreferenceRejectsTooManyTags()
    {
        List<string> tags = [.. Enumerable.Range(0, Maximum.InterestTags + 1).Select(index => $"tag{index}")];

        Assert.Throws<ArgumentOutOfRangeException>(() => new InterestsPreference(tags));
    }

    [Fact]
    public void InterestsPreferenceAcceptsTheMaximumNumberOfTags()
    {
        List<string> tags = [.. Enumerable.Range(0, Maximum.InterestTags).Select(index => $"tag{index}")];

        InterestsPreference preference = new(tags);

        Assert.Equal(Maximum.InterestTags, preference.Tags.Count);
    }

    [Fact]
    public void InterestsPreferenceRejectsATagOverTheByteLimit()
    {
        // Within the grapheme limit and within a .Length check, but over the limit in real UTF-8 bytes.
        string tag = string.Concat(Enumerable.Repeat(FamilyEmoji, 50));

        Assert.True(tag.Length <= Maximum.TagLengthInBytes);
        Assert.True(tag.GetGraphemeLength() <= Maximum.TagLengthInGraphemes);
        Assert.True(tag.GetUtf8Length() > Maximum.TagLengthInBytes);

        Assert.Throws<ArgumentOutOfRangeException>(() => new InterestsPreference([tag]));
    }

    [Fact]
    public void InterestsPreferenceRejectsATagOverTheGraphemeLimit()
    {
        string tag = new('a', Maximum.TagLengthInGraphemes + 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => new InterestsPreference([tag]));
    }

    [Fact]
    public void MutedWordRejectsAValueOverTheByteLimit()
    {
        // Exactly at the grapheme limit but well over the byte limit, so only the byte check can reject it.
        string value = string.Concat(Enumerable.Repeat(FamilyEmoji, Maximum.MutedWordLengthInGraphemes));

        Assert.True(value.GetGraphemeLength() <= Maximum.MutedWordLengthInGraphemes);
        Assert.True(value.GetUtf8Length() > Maximum.MutedWordLengthInBytes);

        Assert.Throws<ArgumentOutOfRangeException>(() => new MutedWord(null, value, [MutedWordTarget.Content], MutedWordActorTarget.All, null));
    }

    [Fact]
    public void MutedWordRejectsAValueOverTheGraphemeLimit()
    {
        string value = new('a', Maximum.MutedWordLengthInGraphemes + 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => new MutedWord(null, value, [MutedWordTarget.Content], MutedWordActorTarget.All, null));
    }

    [Fact]
    public void MutedWordAcceptsAValueAtTheGraphemeLimit()
    {
        string value = new('a', Maximum.MutedWordLengthInGraphemes);

        MutedWord mutedWord = new(null, value, [MutedWordTarget.Content], MutedWordActorTarget.All, null);

        Assert.Equal(value, mutedWord.Value);
    }

    [Fact]
    public void PostInteractionSettingsPreferencesRejectsTooManyThreadGateRules()
    {
        List<ThreadGateRule> rules = [.. Enumerable.Range(0, Maximum.ThreadGateRules + 1).Select(_ => (ThreadGateRule)new FollowerRule())];

        Assert.Throws<ArgumentOutOfRangeException>(() => new PostInteractionSettingsPreferences(rules, null));
    }

    [Fact]
    public void PostInteractionSettingsPreferencesRejectsTooManyPostGateRules()
    {
        List<PostGateRule> rules = [.. Enumerable.Range(0, Maximum.PostGateRules + 1).Select(_ => (PostGateRule)new DisableEmbeddingRule())];

        Assert.Throws<ArgumentOutOfRangeException>(() => new PostInteractionSettingsPreferences(null, rules));
    }

    [Fact]
    public void PostInteractionSettingsPreferencesCopiesTheRuleCollectionsItIsGiven()
    {
        List<ThreadGateRule> threadGateRules = [new FollowerRule()];
        List<PostGateRule> postGateRules = [new DisableEmbeddingRule()];

        PostInteractionSettingsPreferences preferences = new(threadGateRules, postGateRules);

        threadGateRules.Add(new FollowingRule());
        postGateRules.Add(new DisableEmbeddingRule());

        Assert.Single(preferences.ThreadGateAllowRules!);
        Assert.Single(preferences.PostGateEmbeddingRules!);
    }

    [Fact]
    public void PostInteractionSettingsPreferencesRuleCollectionsCannotBeMutated()
    {
        PostInteractionSettingsPreferences preferences = new([new FollowerRule()], [new DisableEmbeddingRule()]);

        Assert.Throws<NotSupportedException>(() => preferences.ThreadGateAllowRules!.Add(new FollowingRule()));
        Assert.Throws<NotSupportedException>(() => preferences.PostGateEmbeddingRules!.Add(new DisableEmbeddingRule()));
    }

    [Fact]
    public void PreferencesCopiesTheListItIsGiven()
    {
        List<Preference> source = [new AdultContentPreference(true)];

        Preferences preferences = new(source, enableBlueskyModerationLabeler: false);

        source.Add(new ThreadViewPreference());

        Assert.Single(preferences);
    }

    private static ProfileViewBasic CreateProfileViewBasic(string? displayName) =>
        new(s_did, s_handle, displayName, null, null, null, null, null, null, null, null, null);
}
