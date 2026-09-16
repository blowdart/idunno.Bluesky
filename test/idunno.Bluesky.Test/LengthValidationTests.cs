// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Chat;
using idunno.Bluesky.Drafts;
using idunno.Bluesky.RichText;

using Standard.Site;

namespace idunno.Bluesky.Test;

/// <summary>
/// Tests for the lexicon maxLength limits, which are counted in UTF-8 bytes rather than in characters.
/// </summary>
/// <remarks>
/// <para>
/// A UTF-8 byte count is always greater than or equal to the number of UTF-16 characters in the same string, so
/// measuring these limits with <see cref="string.Length"/> was always too permissive. Each test below uses text
/// which sits inside both the character count and the grapheme count, and can only be rejected by the byte count.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public class LengthValidationTests
{
    // A family emoji is four emoji joined by zero width joiners. It is a single grapheme, eleven UTF-16
    // characters, and twenty five UTF-8 bytes, which is what makes the gap between the limits reachable.
    private const string FamilyEmoji = "\U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466";

    private static string Families(int count) => string.Concat(Enumerable.Repeat(FamilyEmoji, count));

    private static StrongReference Parent => new(
        "at://did:plc:identifier/app.bsky.feed.post/rkey",
        "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4");

    /// <summary>
    /// Asserts the text is within both the character and grapheme limits, so the only limit it can breach is the byte limit.
    /// </summary>
    private static void AssertOnlyExceedsTheByteLimit(string text, int maximumBytes, int maximumGraphemes)
    {
        Assert.True(text.Length <= maximumBytes, "text should be within the character count the limit used to be measured against");
        Assert.True(text.GetGraphemeLength() <= maximumGraphemes, "text should be within the grapheme limit");
        Assert.True(Encoding.UTF8.GetByteCount(text) > maximumBytes, "text should exceed the byte limit");
    }

    [Fact]
    public void AFamilyEmojiIsOneGraphemeElevenCharactersAndTwentyFiveUtf8Bytes()
    {
        Assert.Equal(1, FamilyEmoji.GetGraphemeLength());
        Assert.Equal(11, FamilyEmoji.Length);
        Assert.Equal(25, Encoding.UTF8.GetByteCount(FamilyEmoji));
    }

    [Fact]
    public void PostConstructorThrowsWhenTextExceedsTheMaximumNumberOfBytes()
    {
        string text = Families(272);

        AssertOnlyExceedsTheByteLimit(text, Maximum.PostLengthInBytes, Maximum.PostLengthInGraphemes);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Post(text));

        Assert.Equal("text", exception.ParamName);
    }

    [Fact]
    public void PostConstructorThrowsWhenATagExceedsTheMaximumNumberOfBytes()
    {
        string tag = Families(58);

        AssertOnlyExceedsTheByteLimit(tag, Maximum.TagLengthInBytes, Maximum.TagLengthInGraphemes);

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => new Post("text", tags: [tag]));

        Assert.Equal("tags", exception.ParamName);
    }

    [Fact]
    public void PostBuilderThrowsWhenTextExceedsTheMaximumNumberOfBytes()
    {
        string text = Families(272);

        AssertOnlyExceedsTheByteLimit(text, Maximum.PostLengthInBytes, Maximum.PostLengthInGraphemes);

        Assert.Throws<ArgumentOutOfRangeException>(() => new PostBuilder(text));
    }

    [Fact]
    public void MessageInputThrowsWhenTextExceedsTheMaximumNumberOfBytes()
    {
        string text = Families(900);

        AssertOnlyExceedsTheByteLimit(text, Maximum.MessageLengthInBytes, Maximum.MessageLengthInGraphemes);

        Assert.Throws<ArgumentOutOfRangeException>(() => new MessageInput(text));
    }

    [Fact]
    public void HashTagThrowsWhenTheTagExceedsTheMaximumNumberOfBytes()
    {
        string tag = Families(58);

        AssertOnlyExceedsTheByteLimit(tag, Maximum.TagLengthInBytes, Maximum.TagLengthInGraphemes);

        Assert.Throws<ArgumentOutOfRangeException>(() => new HashTag(tag));
    }

    [Fact]
    public void TagFacetFeatureThrowsWhenTheTagExceedsTheMaximumNumberOfBytes()
    {
        string tag = Families(58);

        AssertOnlyExceedsTheByteLimit(tag, Maximum.TagLengthInBytes, Maximum.TagLengthInGraphemes);

        Assert.Throws<ArgumentOutOfRangeException>(() => new TagFacetFeature(tag));
    }

    [Fact]
    public void DraftPostThrowsWhenTextExceedsTheMaximumNumberOfBytes()
    {
        string text = Families(900);

        AssertOnlyExceedsTheByteLimit(text, Maximum.DraftTextLengthInBytes, Maximum.DraftTextLengthInGraphemes);

        Assert.Throws<ArgumentOutOfRangeException>(() => new DraftPost(text));
    }

    [Fact]
    public void ProfileDisplayNameThrowsWhenItExceedsTheMaximumNumberOfBytes()
    {
        string displayName = Families(58);

        AssertOnlyExceedsTheByteLimit(displayName, Maximum.DisplayNameLengthInBytes, Maximum.DisplayNameLengthInGraphemes);

        Profile profile = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => profile.DisplayName = displayName);
    }

    [Fact]
    public void ProfileDescriptionThrowsWhenItExceedsTheMaximumNumberOfBytes()
    {
        string description = Families(232);

        AssertOnlyExceedsTheByteLimit(description, Maximum.DescriptionLengthInBytes, Maximum.DescriptionLengthInGraphemes);

        Profile profile = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => profile.Description = description);
    }

    [Fact]
    public void ProfilePronounsThrowWhenTheyExceedTheMaximumNumberOfBytes()
    {
        string pronouns = Families(232);

        AssertOnlyExceedsTheByteLimit(pronouns, Maximum.PronounLengthInBytes, Maximum.PronounLengthInGraphemes);

        Profile profile = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => profile.Pronouns = pronouns);
    }

    [Fact]
    public async Task ReplyToThrowsWhenTextExceedsTheMaximumNumberOfGraphemes()
    {
        // The grapheme count was compared against the byte limit, which is ten times larger, so the grapheme
        // limit could never reject anything. Plain ASCII keeps the byte count well inside its own limit.
        string text = new('a', Maximum.PostLengthInGraphemes + 1);

        Assert.True(Encoding.UTF8.GetByteCount(text) <= Maximum.PostLengthInBytes);
        Assert.True(text.GetGraphemeLength() > Maximum.PostLengthInGraphemes);

        using BlueskyAgent agent = new();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => agent.ReplyTo(Parent, text, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReplyToThrowsWhenTextExceedsTheMaximumNumberOfBytes()
    {
        string text = Families(272);

        AssertOnlyExceedsTheByteLimit(text, Maximum.PostLengthInBytes, Maximum.PostLengthInGraphemes);

        using BlueskyAgent agent = new();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => agent.ReplyTo(Parent, text, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void PublicationThrowsWhenTheNameExceedsTheMaximumNumberOfBytes()
    {
        string name = Families(116);

        AssertOnlyExceedsTheByteLimit(name, 1280, 128);

        Assert.Throws<ArgumentOutOfRangeException>(() => new Publication(new Uri("https://example.org"), name));
    }

    [Fact]
    public void PublicationThrowsWhenTheDescriptionExceedsTheMaximumNumberOfBytes()
    {
        string description = Families(272);

        AssertOnlyExceedsTheByteLimit(description, 3000, 300);

        Assert.Throws<ArgumentOutOfRangeException>(() => new Publication(new Uri("https://example.org"), "name", description: description));
    }

    [Fact]
    public void DocumentThrowsWhenTheTitleExceedsTheMaximumNumberOfBytes()
    {
        string title = Families(116);

        AssertOnlyExceedsTheByteLimit(title, 1280, 128);

        Assert.Throws<ArgumentOutOfRangeException>(() => new Document("https://example.org", title, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void DocumentThrowsWhenTheDescriptionExceedsTheMaximumNumberOfBytes()
    {
        string description = Families(272);

        AssertOnlyExceedsTheByteLimit(description, 3000, 300);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Document("https://example.org", "title", DateTimeOffset.UtcNow, description: description));
    }
}
