// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Moderation;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class LabelerAndModerationTests
{
    private static readonly Did s_labelerDid = new("did:plc:ar7c4by46qjdydhdevvrndac");

    private static StrongReference CreateSubject() => new(
        new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3kk2abcd2yk2a"),
        new Cid("bafyreihbjgqbtbcw2acjnbhtdgapbnbsvnzqsrz4pxuncfmcbxcnrbg4rq"));

    [Fact]
    public void ReportOptionsCannotBeChangedThroughTheDictionaryItIsExposedAs()
    {
        IReadOnlyList<ReportOption> options = WellKnown.ReportOptions["post"];

        Assert.NotEmpty(options);
        Assert.Throws<NotSupportedException>(() => ((IList<ReportOption>)options).Clear());
    }

    [Fact]
    public void ReportOptionsAreStillIntactAfterAnAttemptToChangeThem()
    {
        int count = WellKnown.ReportOptions["post"].Count;

        try
        {
            ((IList<ReportOption>)WellKnown.ReportOptions["post"]).Clear();
        }
        catch (NotSupportedException)
        {
            // Expected, the assertion below is what this test is about.
        }

        Assert.Equal(count, WellKnown.ReportOptions["post"].Count);
    }

    [Fact]
    public async Task CreateModerationReportThrowsWhenTheReportTypeIsNotDefined()
    {
        using BlueskyAgent agent = new();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => agent.CreateModerationReport(s_labelerDid, CreateSubject(), (ReportType)9999, null, TestContext.Current.CancellationToken));
    }

    [Theory]
    // The reason is limited to 2000 graphemes and 20000 UTF-8 bytes. Two thousand and one accented characters
    // exceed only the grapheme limit, which the previous character count check did not catch.
    [InlineData(2001, false)]
    // Two thousand family emoji are within the grapheme limit but are twenty five bytes each, so they exceed
    // the byte limit.
    [InlineData(2000, true)]
    public async Task CreateModerationReportThrowsWhenTheReasonIsTooLong(int length, bool useFamilyEmoji)
    {
        string reason = useFamilyEmoji
            ? string.Concat(Enumerable.Repeat("\U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466", length))
            : new string('\u00e9', length);

        using BlueskyAgent agent = new();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => agent.CreateModerationReport(s_labelerDid, CreateSubject(), ReportType.Spam, reason, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetLabelerServicesThrowsWhenNoDidsAreSupplied()
    {
        using BlueskyAgent agent = new();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => agent.GetLabelerServices([], cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetLabelerServicesThrowsWhenTooManyDidsAreSupplied()
    {
        List<Did> dids = [.. Enumerable.Range(0, Maximum.LabelerServices + 1).Select(i => new Did($"did:plc:ar7c4by46qjdydhdevvrndac{i}"))];

        using BlueskyAgent agent = new();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => agent.GetLabelerServices(dids, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void LabelersPreferenceThrowsWhenItIsGivenNoLabelers()
    {
        Assert.Throws<ArgumentNullException>(() => new LabelersPreference(null!));
    }

    [Fact]
    public void LabelersPreferenceIsNotChangedByLaterChangesToTheCollectionItWasGiven()
    {
        List<LabelerPreference> labelers = [new(s_labelerDid)];

        LabelersPreference preference = new(labelers);

        labelers.Clear();

        Assert.Single(preference.Labelers);
    }
}
