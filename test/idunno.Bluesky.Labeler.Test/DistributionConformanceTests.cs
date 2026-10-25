// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.Labeler.Test;

/// <summary>Compares durable projection results with Ozone's CID-scoped upsert model on every real provider.</summary>
public class DistributionConformanceTests
{
    private static readonly DateTimeOffset s_instant = new(
        year: 2026,
        month: 1,
        day: 2,
        hour: 3,
        minute: 4,
        second: 5,
        offset: TimeSpan.Zero);
    private const string Subject = "at://did:plc:subject/app.bsky.actor.profile/self";

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite", 0)]
    [InlineData("sqlite", 1)]
    [InlineData("sqlite", 2)]
    [InlineData("postgresql", 0)]
    [InlineData("postgresql", 1)]
    [InlineData("postgresql", 2)]
    [InlineData("mysql", 0)]
    [InlineData("mysql", 1)]
    [InlineData("mysql", 2)]
    public async Task RetainsRawMixedCaseCidSlotsDuringAppendAndBackfill(string engine, int priorVersion)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot scoped = new(key.CreateLabel(
            source: StoreFixture.Issuer,
            uri: Subject,
            value: "spam",
            creationTimestamp: s_instant,
            cid: new Cid("bafyreigh5fjr6an3qujvfbm6xmso6jyhido2dguixaeqp3jtcqft2gofba")));
        string json = Encoding.UTF8.GetString(scoped.GetJsonBytes());
        SignedLabelSnapshot raw = new(Encoding.UTF8.GetBytes(json.Replace("\"cid\":", "\"CID\":", StringComparison.Ordinal)));
        SignedLabelSnapshot wide = new(key.CreateLabel(
            source: StoreFixture.Issuer,
            uri: Subject,
            value: "spam",
            creationTimestamp: s_instant));
        await fixture.Store.AppendAsync("raw-slots", [raw, wide], cancellationToken: token);
        await fixture.Store.PublishAsync(cancellationToken: token);
        if (priorVersion != 0)
        {
            await fixture.ExecuteAsync($"UPDATE label_schema SET version={priorVersion}; DELETE FROM label_distribution");
        }

        ILabelStore restarted = fixture.NewStore();
        await restarted.InitializeAsync(StoreFixture.Issuer, token);
        LabelStoreState state = await restarted.GetStateAsync(token);
        List<StoredLabel> actual = await ReadAllAsync(restarted, state);
        Assert.Equal([1L, 2L], actual.Select(label => label.Sequence));
        Assert.Equal(raw.GetJsonBytes(), actual[0].SignedLabel.GetJsonBytes());
        Assert.Equal(scoped.GetCanonicalUnsignedBytes(), actual[0].SignedLabel.GetCanonicalUnsignedBytes());

        await restarted.AppendAsync("canonical-replacement", [scoped], cancellationToken: token);
        state = await restarted.PublishAsync(cancellationToken: token);
        Assert.Equal([2L, 3L], (await ReadAllAsync(restarted, state)).Select(label => label.Sequence));
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task MatchesReferenceSlotReplacementAndPreservesSnapshotAndRawHistory(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        Cid cid = new("bafyreigh5fjr6an3qujvfbm6xmso6jyhido2dguixaeqp3jtcqft2gofba");
        SignedLabelSnapshot[] input =
        [
            new(key.CreateLabel(
                source: StoreFixture.Issuer,
                uri: Subject,
                value: "spam",
                creationTimestamp: s_instant)),
            new(key.CreateLabel(
                source: StoreFixture.Issuer,
                uri: Subject,
                value: "spam",
                creationTimestamp: s_instant,
                cid: cid)),
            new(key.CreateNegation(
                source: StoreFixture.Issuer,
                uri: Subject,
                value: "spam",
                creationTimestamp: s_instant.AddSeconds(1))),
            new(key.CreateLabel(
                source: StoreFixture.Issuer,
                uri: Subject,
                value: "expired",
                creationTimestamp: s_instant,
                cid: null,
                expiresAt: s_instant.AddSeconds(1))),
            new(key.CreateLabel(
                source: StoreFixture.Issuer,
                uri: Subject,
                value: "spam",
                creationTimestamp: s_instant.AddSeconds(-1))),
            new(key.CreateNegation(
                source: StoreFixture.Issuer,
                uri: Subject,
                value: "spam",
                creationTimestamp: s_instant.AddSeconds(2),
                cid: cid)),
            new(key.CreateLabel(
                source: StoreFixture.Issuer,
                uri: Subject,
                value: "spam",
                creationTimestamp: s_instant,
                cid: new Cid("bafyreib2rxk3rh6kzwq6y7ug4eqhfhpqaqzqvuflstfpvgkzjt7b5yfkzy")))
        ];
        await fixture.Store.AppendAsync("initial", input[..4], cancellationToken: token);
        LabelStoreState initial = await fixture.Store.PublishAsync(cancellationToken: token);
        await fixture.Store.AppendAsync("replacements", input[4..], cancellationToken: token);
        // A committed but unpublished replacement cannot hide a public slot.
        Assert.Equal([2L, 3L, 4L], (await ReadAllAsync(fixture.Store, initial)).Select(label => label.Sequence));
        LabelStoreState published = await fixture.Store.PublishAsync(cancellationToken: token);
        ILabelStore restarted = fixture.NewStore();
        await restarted.InitializeAsync(StoreFixture.Issuer, token);
        List<StoredLabel> actual = await ReadAllAsync(restarted, published);

        // Independent oracle mirrors INSERT ... ON CONFLICT (src, uri, cid, val) UPDATE id.
        Dictionary<(string Source, string Uri, string Cid, string Value), int> reference = [];
        for (int i = 0; i < input.Length; i++)
        {
            Label label = input[i].Label;
            reference[(label.Source.Value, label.Uri, label.Cid?.ToString() ?? string.Empty, label.Value)] = i;
        }

        int[] expected = [.. reference.Values.Order()];
        Assert.Equal(expected.Select(index => index + 1L), actual.Select(label => label.Sequence));
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(input[expected[i]].GetJsonBytes(), actual[i].SignedLabel.GetJsonBytes());
        }

        Assert.Equal([2L, 3L, 4L], (await ReadAllAsync(restarted, initial)).Select(label => label.Sequence));
        Assert.Equal(input.Length, (await restarted.ReplayAsync(
            streamId: published.StreamId,
            after: 0,
            through: published.PublishedThrough,
            cancellationToken: token)).Labels.Count);
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task BoundsSparseFilterWorkAndResumesEmptyPages(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.Secp256k1);
        await fixture.Store.AppendAsync("subjects",
        [
            new(key.CreateLabel(
                source: StoreFixture.Issuer,
                uri: "did:plc:first",
                value: "spam",
                creationTimestamp: s_instant)),
            new(key.CreateLabel(
                source: StoreFixture.Issuer,
                uri: "did:plc:last",
                value: "spam",
                creationTimestamp: s_instant))
        ], cancellationToken: token);
        LabelStoreState state = await fixture.Store.PublishAsync(cancellationToken: token);
        LabelDistributionPage first = await fixture.Store.ReadDistributionAsync(
            streamId: state.StreamId,
            after: 0,
            through: 2,
            uriPatterns: ["did:plc:last"],
            sources: [],
            limit: 1,
            scanLimit: 1,
            cancellationToken: token);
        Assert.Empty(first.Labels);
        Assert.Equal(1, first.ScannedThrough);
        LabelDistributionPage second = await fixture.Store.ReadDistributionAsync(
            streamId: state.StreamId,
            after: first.ScannedThrough,
            through: 2,
            uriPatterns: ["did:plc:last", "did:plc:*"],
            sources: [StoreFixture.Issuer],
            limit: 1,
            scanLimit: 1,
            cancellationToken: token);
        Assert.Equal(2, Assert.Single(second.Labels).Sequence);
        Assert.Equal(2, second.ScannedThrough);
        Assert.Empty((await fixture.Store.ReadDistributionAsync(
            streamId: state.StreamId,
            after: 0,
            through: 2,
            uriPatterns: ["*"],
            sources: [new Did("did:plc:other")],
            cancellationToken: token)).Labels);
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite", 1)]
    [InlineData("sqlite", 2)]
    [InlineData("postgresql", 1)]
    [InlineData("postgresql", 2)]
    [InlineData("mysql", 1)]
    [InlineData("mysql", 2)]
    public async Task ResumesDerivedMetadataUpgradeWithoutChangingSignedBytes(string engine, int version)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot signed = new(key.CreateLabel(
            source: StoreFixture.Issuer,
            uri: Subject,
            value: "spam",
            creationTimestamp: s_instant));
        await fixture.Store.AppendAsync("prior", [signed], cancellationToken: token);
        await fixture.Store.PublishAsync(cancellationToken: token);
        await fixture.ExecuteAsync($"UPDATE label_schema SET version={version}; DELETE FROM label_distribution");
        ILabelStore restarted = fixture.NewStore();
        await restarted.InitializeAsync(StoreFixture.Issuer, token);
        await restarted.InitializeAsync(StoreFixture.Issuer, token);
        LabelStoreState state = await restarted.GetStateAsync(token);
        Assert.Equal(signed.GetJsonBytes(), Assert.Single(await ReadAllAsync(restarted, state)).SignedLabel.GetJsonBytes());
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task RejectsSequenceExhaustionWithoutPartialAppend(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        await fixture.ExecuteAsync("UPDATE label_state SET issued=9007199254740991,published=9007199254740991,replay_floor=9007199254740991");
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        await Assert.ThrowsAsync<OverflowException>(() => fixture.Store.AppendAsync(
            "exhausted",
            [new(key.CreateLabel(
                source: StoreFixture.Issuer,
                uri: Subject,
                value: "spam",
                creationTimestamp: s_instant))],
            cancellationToken: token));
        Assert.Null(await fixture.Store.GetReceiptAsync("exhausted", token));
        Assert.Equal(9_007_199_254_740_991, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task SupersededRowsConsumeTheExactScanBudget(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        Assert.Same(fixture.Store, fixture.Services.GetRequiredService<ILabelDistributionStore>());
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot signed = new(key.CreateLabel(
            source: StoreFixture.Issuer,
            uri: Subject,
            value: "spam",
            creationTimestamp: s_instant));
        await fixture.Store.AppendAsync("batch", [.. Enumerable.Repeat(signed, 1000)], cancellationToken: token);
        await fixture.Store.AppendAsync("replacement", [signed], cancellationToken: token);
        await fixture.Store.PublishAsync(1000, token);
        LabelStoreState state = await fixture.Store.PublishAsync(cancellationToken: token);
        LabelDistributionPage first = await fixture.Store.ReadDistributionAsync(
            streamId: state.StreamId,
            after: 0,
            through: 1001,
            uriPatterns: ["*"],
            sources: [],
            limit: 1,
            scanLimit: 1000,
            cancellationToken: token);
        Assert.Empty(first.Labels);
        Assert.Equal(1000, first.ScannedThrough);
        LabelDistributionPage last = await fixture.Store.ReadDistributionAsync(
            streamId: state.StreamId,
            after: first.ScannedThrough,
            through: 1001,
            uriPatterns: ["*"],
            sources: [],
            limit: 1,
            scanLimit: 1000,
            cancellationToken: token);
        Assert.Equal(1001, Assert.Single(last.Labels).Sequence);
        await fixture.ExecuteAsync("DELETE FROM label_distribution WHERE seq=1001");
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Store.ReadDistributionAsync(
            streamId: state.StreamId,
            after: 1000,
            through: 1001,
            uriPatterns: ["*"],
            sources: [],
            cancellationToken: token));
        await fixture.Store.InitializeAsync(StoreFixture.Issuer, token);
        LabelDistributionPage repaired = await fixture.Store.ReadDistributionAsync(
            streamId: state.StreamId,
            after: 1000,
            through: 1001,
            uriPatterns: ["*"],
            sources: [],
            cancellationToken: token);
        Assert.Equal(1001, Assert.Single(repaired.Labels).Sequence);
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task DecisionCommitsIndexAtomicallyAndRejectExhaustionWithoutReceipts(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        LabelTarget target = LabelTarget.Account(new Did("did:plc:subject"));
        SignedLabelSnapshot signed = new(key.CreateLabel(
            source: StoreFixture.Issuer,
            uri: target.Subject,
            value: "spam",
            creationTimestamp: s_instant));
        LabelDecisionCommit Command(string id, long revision) => new(
            request: LabelOperationRequest.Issue(
                operationId: id,
                target: target,
                value: "spam",
                context: new LabelOperationContext(StoreFixture.Issuer, "Distribution boundary")),
            resolvedTarget: target,
            expectedRevision: revision,
            signedLabel: signed,
            audit: new LabelAuditRecord(
                evaluatedAt: s_instant,
                authorizationVersion: "grant",
                policyVersion: "policy",
                signingKeyMultibase: key.ExportPublicKeyMultibase()));
        LabelServiceResult<LabelOperationReceipt> committed = await fixture.Store.CommitDecisionAsync(Command("decision", 0), token);
        Assert.True(committed.Succeeded, committed.Error?.Message);
        LabelStoreState state = await fixture.Store.PublishAsync(cancellationToken: token);
        Assert.Equal(signed.GetJsonBytes(), Assert.Single(await ReadAllAsync(fixture.Store, state)).SignedLabel.GetJsonBytes());
        await fixture.ExecuteAsync("UPDATE label_state SET issued=9007199254740991,published=9007199254740991,replay_floor=9007199254740991");
        target = LabelTarget.Account(new Did("did:plc:second"));
        signed = new(key.CreateLabel(
            source: StoreFixture.Issuer,
            uri: target.Subject,
            value: "spam",
            creationTimestamp: s_instant));
        await Assert.ThrowsAsync<OverflowException>(() => fixture.Store.CommitDecisionAsync(Command("exhausted-decision", state.Revision), token));
        LabelServiceResult<LabelOperationReceipt> absent = await fixture.Store.GetOperationAsync("exhausted-decision", token);
        Assert.False(absent.Succeeded);
        Assert.Equal(LabelServiceErrorCode.OperationNotFound, absent.Error.Code);
    }

    /// <summary>Drains one fixed projection snapshot in small pages, including empty-progress pages.</summary>
    private static async Task<List<StoredLabel>> ReadAllAsync(ILabelStore store, LabelStoreState state)
    {
        List<StoredLabel> labels = [];
        long after = state.ReplayFloor;
        while (after < state.PublishedThrough)
        {
            LabelDistributionPage page = await store.ReadDistributionAsync(
                streamId: state.StreamId,
                after: after,
                through: state.PublishedThrough,
                uriPatterns: ["*"],
                sources: [],
                limit: 1,
                scanLimit: 1,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.InRange(page.ScannedThrough, after + 1, state.PublishedThrough);
            labels.AddRange(page.Labels);
            after = page.ScannedThrough;
        }

        return labels;
    }
}
