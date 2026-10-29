// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.Labeler.Test;

/// <summary>Exercises transaction, migration and lifecycle boundaries independently of an application clock.</summary>
public class LabelDecisionBoundaryTests
{
    private static readonly DateTimeOffset s_instant = new(
        year: 2026,
        month: 1,
        day: 1,
        hour: 0,
        minute: 0,
        second: 0,
        offset: TimeSpan.Zero);
    private static readonly LabelTarget s_target = LabelTarget.Account(TargetResolutionHandler.SubjectDid);
    private static readonly LabelOperationContext s_context = new(StoreFixture.s_issuer, "Boundary decision");

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task UpgradesPopulatedVersionOneWithoutChangingHistoryOrProgress(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot signed = new(
            key.CreateLabel(
                source: StoreFixture.s_issuer,
                uri: s_target.Subject,
                value: "spam",
                creationTimestamp: s_instant));
        await fixture.Store.AppendAsync(
            operationId: "legacy",
            labels: [signed],
            cancellationToken: TestContext.Current.CancellationToken);
        LabelStoreState original = await fixture.Store.PublishAsync(cancellationToken: TestContext.Current.CancellationToken);
        await fixture.ExecuteAsync("DROP TABLE label_decisions; DROP TABLE label_decision_index; UPDATE label_schema SET version=1 WHERE id=1");

        ILabelStore upgraded = fixture.NewStore();
        await upgraded.InitializeAsync(StoreFixture.s_issuer, TestContext.Current.CancellationToken);
        await upgraded.InitializeAsync(StoreFixture.s_issuer, TestContext.Current.CancellationToken);
        Assert.Equal(original, await upgraded.GetStateAsync(TestContext.Current.CancellationToken));
        LabelServiceResult<LabelDecisionSnapshot> snapshot = await upgraded.ReadDecisionAsync(s_target, "spam", TestContext.Current.CancellationToken);
        Assert.True(snapshot.Succeeded);
        Assert.Equal(signed.GetJsonBytes(), snapshot.Result.CurrentLabel!.SignedLabel.GetJsonBytes());
        Assert.NotNull(await upgraded.GetReceiptAsync("legacy", TestContext.Current.CancellationToken));

        LabelOperationRequest request = LabelOperationRequest.Issue(
            operationId: "service",
            target: s_target,
            value: "spam",
            context: s_context);
        LabelServiceResult<LabelOperationReceipt> receipt = await upgraded.CommitDecisionAsync(
            new(
                request: request,
                resolvedTarget: s_target,
                expectedRevision: original.Revision,
                signedLabel: null,
                audit: new(
                    evaluatedAt: s_instant,
                    authorizationVersion: "grant",
                    policyVersion: "policy",
                    signingKeyMultibase: null)),
            TestContext.Current.CancellationToken);
        Assert.True(receipt.Succeeded);
        Assert.False(receipt.Result.Changed);
        Assert.Equal(original.IssuedThrough, receipt.Result.StateAtCommit.IssuedThrough);
        Assert.Same(fixture.Store, Assert.Single(fixture.Services.GetServices<ILabelStore>()));
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task SerializesConcurrentDecisionsAndPreservesWinningReceipt(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        ILabelStore first = fixture.Store;
        ILabelStore second = fixture.NewStore();
        LabelDecisionCommit Command(string id) => new(
            request: LabelOperationRequest.Issue(
                operationId: id,
                target: s_target,
                value: "spam",
                context: s_context),
            resolvedTarget: s_target,
            expectedRevision: 0,
            signedLabel: new(
                key.CreateLabel(
                    source: StoreFixture.s_issuer,
                    uri: s_target.Subject,
                    value: "spam",
                    creationTimestamp: s_instant)),
            audit: new(
                evaluatedAt: s_instant,
                authorizationVersion: "grant",
                policyVersion: "policy",
                signingKeyMultibase: key.ExportPublicKeyMultibase()));

        // Both commands are based on the same snapshot. The gate controls admission, not elapsed time;
        // the provider lock must arbitrate independently created connections.
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<LabelServiceResult<LabelOperationReceipt>> Execute(ILabelStore store, LabelDecisionCommit command)
        {
            await start.Task;
            return await store.CommitDecisionAsync(command, TestContext.Current.CancellationToken);
        }

        Task<LabelServiceResult<LabelOperationReceipt>> a = Execute(first, Command("first"));
        Task<LabelServiceResult<LabelOperationReceipt>> b = Execute(second, Command("second"));
        start.SetResult();
        LabelServiceResult<LabelOperationReceipt>[] results = await Task.WhenAll(a, b);
        LabelOperationReceipt winner = Assert.Single(results, result => result.Succeeded).Result!;
        Assert.Equal(LabelServiceErrorCode.StaleDecision, Assert.Single(results, result => !result.Succeeded).Error!.Code);
        Assert.Equal(1, winner.StateAtCommit.IssuedThrough);
        LabelServiceResult<LabelOperationReceipt> retry = await second.CommitDecisionAsync(Command(winner.Request.OperationId), TestContext.Current.CancellationToken);
        Assert.True(retry.Succeeded);
        Assert.Equal(winner.Label!.SignedLabel.GetJsonBytes(), retry.Result.Label!.SignedLabel.GetJsonBytes());
        Assert.Equal(1, (await first.GetStateAsync(TestContext.Current.CancellationToken)).IssuedThrough);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task RejectsAmbiguousLatestHistoryButAcceptsStrictlyLaterDecision(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        await fixture.Store.AppendAsync(
            operationId: "conflict",
            labels: [new(
                key.CreateLabel(
                    source: StoreFixture.s_issuer,
                    uri: s_target.Subject,
                    value: "spam",
                    creationTimestamp: s_instant)),
             new(
                 key.CreateNegation(
                     source: StoreFixture.s_issuer,
                     uri: s_target.Subject,
                     value: "spam",
                     creationTimestamp: s_instant))],
            cancellationToken: TestContext.Current.CancellationToken);
        ILabelStore store = fixture.Store;
        LabelServiceResult<LabelDecisionSnapshot> ambiguous = await store.ReadDecisionAsync(s_target, "spam", TestContext.Current.CancellationToken);
        Assert.False(ambiguous.Succeeded);
        Assert.Equal(LabelServiceErrorCode.AmbiguousHistory, ambiguous.Error.Code);
        IReadOnlyList<LabelServiceResult<LabelDecisionSnapshot>> batch = await store.ReadDecisionBatchAsync(
            [(s_target, "spam"), (s_target, "missing"), (s_target, "spam")],
            TestContext.Current.CancellationToken);
        Assert.Equal(3, batch.Count);
        Assert.False(batch[0].Succeeded);
        Assert.Equal(LabelServiceErrorCode.AmbiguousHistory, batch[0].Error!.Code);
        Assert.True(batch[1].Succeeded);
        Assert.Null(batch[1].Result!.CurrentLabel);
        Assert.False(batch[2].Succeeded);
        Assert.Equal(LabelServiceErrorCode.AmbiguousHistory, batch[2].Error!.Code);
        await fixture.Store.AppendAsync(
            operationId: "later",
            labels: [new(
                key.CreateNegation(
                    source: StoreFixture.s_issuer,
                    uri: s_target.Subject,
                    value: "spam",
                    creationTimestamp: s_instant.AddTicks(1)))],
            cancellationToken: TestContext.Current.CancellationToken);
        LabelServiceResult<LabelDecisionSnapshot> resolved = await store.ReadDecisionAsync(s_target, "spam", TestContext.Current.CancellationToken);
        Assert.True(resolved.Succeeded);
        Assert.Equal(3, resolved.Result.CurrentLabel!.Sequence);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task PurgesAuditedNoOpsIndependentlyOfEventSequences(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        ILabelStore store = fixture.Store;
        for (int index = 0; index < 3; index++)
        {
            LabelOperationRequest request = LabelOperationRequest.Negate(
                operationId: $"noop-{index}",
                target: s_target,
                value: "spam",
                context: s_context);
            LabelServiceResult<LabelOperationReceipt> committed = await store.CommitDecisionAsync(
                new(
                    request: request,
                    resolvedTarget: s_target,
                    expectedRevision: index,
                    signedLabel: null,
                    audit: new(
                        evaluatedAt: s_instant,
                        authorizationVersion: "grant",
                        policyVersion: null,
                        signingKeyMultibase: null)),
                TestContext.Current.CancellationToken);
            Assert.True(committed.Succeeded);
            Assert.False(committed.Result.Changed);
            Assert.Null(committed.Result.Label);
        }

        LabelStoreState state = await store.BeginRetirementAsync(3, cancellationToken: TestContext.Current.CancellationToken);
        state = await store.BeginGracePeriodAsync(state.Revision, TimeSpan.FromDays(1), cancellationToken: TestContext.Current.CancellationToken);
        // The deadline is controlled in persisted database time, not by sleeping or comparing host clocks.
        await fixture.ExecuteAsync("UPDATE label_state SET grace_deadline=0 WHERE id=1");
        state = await store.CompleteRetirementAsync(state.Revision, cancellationToken: TestContext.Current.CancellationToken);
        for (int index = 0; index < 3; index++)
        {
            state = await fixture.NewStore().PurgeAsync(
                expectedRevision: state.Revision,
                confirmed: true,
                limit: 1,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(index == 2 ? LabelerLifecyclePhase.Purged : LabelerLifecyclePhase.Purging, state.Phase);
            LabelServiceResult<LabelOperationReceipt> removed = await store.GetOperationAsync($"noop-{index}", TestContext.Current.CancellationToken);
            Assert.False(removed.Succeeded);
            Assert.Equal(LabelServiceErrorCode.OperationNotFound, removed.Error.Code);
        }

        Assert.Equal(0, state.IssuedThrough);
    }
}
