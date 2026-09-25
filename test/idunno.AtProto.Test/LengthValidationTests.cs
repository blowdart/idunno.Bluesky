// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using idunno.AtProto.Admin;
using idunno.AtProto.Labels;

namespace idunno.AtProto.Test;

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

    /// <summary>
    /// Asserts the text is within the character count the limit used to be measured against, so the only limit it can breach is the byte limit.
    /// </summary>
    private static void AssertOnlyExceedsTheByteLimit(string text, int maximumBytes)
    {
        Assert.True(text.Length <= maximumBytes, "text should be within the character count the limit used to be measured against");
        Assert.True(Encoding.UTF8.GetByteCount(text) > maximumBytes, "text should exceed the byte limit");
    }

    [Fact]
    public void LabelConstructorThrowsWhenValueExceedsTheMaximumNumberOfBytes()
    {
        string value = Families(11);

        AssertOnlyExceedsTheByteLimit(value, 128);

        ArgumentOutOfRangeException caughtException = Assert.Throws<ArgumentOutOfRangeException>(() => new Label(
            version: 1,
            source: new Did("did:plc:identifier"),
            uri: "at://did:plc:identifier/app.bsky.feed.post/rkey",
            cid: null,
            value: value,
            isNegationLabel: false,
            creationTimestamp: DateTimeOffset.UtcNow,
            signature: []));

        Assert.Equal("value.GetUtf8Length()", caughtException.ParamName);
    }

    [Fact]
    public void LabelConstructorDoesNotThrowWhenValueIsWithinTheMaximumNumberOfBytes()
    {
        Label label = new(
            version: 1,
            source: new Did("did:plc:identifier"),
            uri: "at://did:plc:identifier/app.bsky.feed.post/rkey",
            cid: null,
            value: Families(5),
            isNegationLabel: false,
            creationTimestamp: DateTimeOffset.UtcNow,
            signature: []);

        Assert.Equal(Families(5), label.Value);
    }

    [Fact]
    public void SelfLabelConstructorThrowsWhenValueExceedsTheMaximumNumberOfBytes()
    {
        string value = Families(11);

        AssertOnlyExceedsTheByteLimit(value, 128);

        ArgumentOutOfRangeException caughtException = Assert.Throws<ArgumentOutOfRangeException>(() => new SelfLabel(value));

        Assert.Equal("value.GetUtf8Length()", caughtException.ParamName);
    }

    [Fact]
    public void SelfLabelConstructorDoesNotThrowWhenValueIsWithinTheMaximumNumberOfBytes()
    {
        SelfLabel selfLabel = new(Families(5));

        Assert.Equal(Families(5), selfLabel.Value);
    }

    [Fact]
    public async Task CreateModerationReportThrowsWhenReasonExceedsTheMaximumNumberOfBytes()
    {
        string reason = Families(801);

        AssertOnlyExceedsTheByteLimit(reason, 20000);
        Assert.True(reason.GetGraphemeLength() <= 2000, "reason should be within the grapheme limit");

        using AtProtoAgent agent = new(new Uri("https://example.org"));

        ArgumentOutOfRangeException caughtException = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => agent.CreateModerationReport(
                labelerDid: new Did("did:plc:identifier"),
                reportSubject: new RepoReference { Did = new Did("did:plc:subject") },
                reasonType: "com.atproto.moderation.defs#reasonSpam",
                reason: reason,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("reason.GetUtf8Length()", caughtException.ParamName);
    }

    [Fact]
    public async Task CreateModerationReportThrowsWhenReasonExceedsTheMaximumNumberOfGraphemes()
    {
        // Comfortably inside the 20,000 byte limit, but over the 2,000 grapheme limit the library never checked.
        string reason = new('a', 2001);

        Assert.True(reason.GetUtf8Length() <= 20000, "reason should be within the byte limit");
        Assert.True(reason.GetGraphemeLength() > 2000, "reason should exceed the grapheme limit");

        using AtProtoAgent agent = new(new Uri("https://example.org"));

        ArgumentOutOfRangeException caughtException = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => agent.CreateModerationReport(
                labelerDid: new Did("did:plc:identifier"),
                reportSubject: new RepoReference { Did = new Did("did:plc:subject") },
                reasonType: "com.atproto.moderation.defs#reasonSpam",
                reason: reason,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("reason.GetGraphemeLength()", caughtException.ParamName);
    }
}
