// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Data.Sqlite;

namespace idunno.Bluesky.Labeler.Test;

public class LabelRetirementTests
{
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task VersionElevenMigrationPreservesLegacyRetirementAndRequiresExplicitAdoption(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelStoreState original = await fixture.Store.GetStateAsync(token);
        LabelStoreState legacy = await fixture.Store.BeginRetirementAsync(
            expectedRevision: original.Revision,
            caller: caller,
            cancellationToken: token);
        await fixture.ExecuteAsync("DROP TABLE label_retirement_slots");
        await fixture.ExecuteAsync("DROP TABLE label_retirement_confirmations");
        await fixture.ExecuteAsync("DROP TABLE label_retirement_receipts");
        await fixture.ExecuteAsync("DROP TABLE label_retirement");
        await fixture.ExecuteAsync("UPDATE label_schema SET version=11");
        ILabelStore restartedStore = fixture.NewStore();
        await restartedStore.InitializeAsync(StoreFixture.s_issuer, token);
        Assert.Equal(legacy, await restartedStore.GetStateAsync(token));
        ILabelRetirementStore retirement = (ILabelRetirementStore)restartedStore;
        LabelRetirementView before = Success(await retirement.InspectRetirementAsync(caller, null, token));
        Assert.False(before.Running);
        Assert.Null(before.OperationId);
        LabelRetirementView resumed = Success(await retirement.ExecuteRetirementAsync(
            caller, new("resumeRetirement", "adopt-legacy", "Adopt existing legacy retirement."), token));
        Assert.True(resumed.Running);
        Assert.Equal(legacy.RetirementUpperBound, resumed.UpperBound);
        Assert.Equal("adopt-legacy", resumed.OperationId);
    }

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task TransitionRecoveryDistinguishesRollbackFromLostAcknowledgement(string engine, bool afterCommit)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        ILabelRetirementStore retirement = (ILabelRetirementStore)fixture.Store;
        LabelRetirementCommand begin = new("beginRetirement", "recover-begin", "Retire empty labeler.");
        LabelRetirementView preview = Success(await retirement.InspectRetirementAsync(caller, begin, token));
        fixture.SetCommitHooks(
            beforeCommit: afterCommit ? null : (_, _, _) => throw new SqliteException("Injected rollback.", 10),
            afterCommit: afterCommit ? (_, _) => throw new SqliteException("Injected lost acknowledgement.", 10) : null);
        LabelServiceResult<LabelRetirementView> failed = await retirement.ExecuteRetirementAsync(
            caller, begin with { Confirmation = preview.Confirmation }, token);
        fixture.SetCommitHooks();
        Assert.Equal(afterCommit ? LabelServiceErrorCode.OutcomeUnknown : LabelServiceErrorCode.StorageUnavailable, failed.Error!.Code);
        ILabelRetirementStore restarted = (ILabelRetirementStore)fixture.NewStore();
        Assert.Equal(afterCommit ? "Distributing" : "Active", Success(await restarted.InspectRetirementAsync(caller, null, token)).Phase);
        LabelRetirementView recovered = Success(await restarted.ExecuteRetirementAsync(
            caller, begin with { Confirmation = preview.Confirmation }, token));
        Assert.Equal("Distributing", recovered.Phase);
        Assert.Single(Success(await ((ILabelAdministrationStore)fixture.Store).GetAdministrationAuditAsync(
            caller, cancellationToken: token)), receipt => receipt.Action == "beginRetirement");
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ExpiredWorkerAuthorityPausesDurablyUntilExplicitResume(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        LabelAdministrationCaller administrator = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        LabelAdministrationCaller browser = Success(await team.AuthenticateBrowserAsync(administrator.Actor, token));
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        await fixture.Store.AppendAsync("initial-label", [new(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: "did:plc:subject",
            value: "spam",
            creationTimestamp: fixture.DatabaseClock!.GetUtcNow()))], cancellationToken: token);
        await fixture.Store.PublishAsync(cancellationToken: token);
        ILabelRetirementStore retirement = (ILabelRetirementStore)fixture.Store;
        await ConfirmAsync(retirement, browser, new(
            Action: "beginRetirement",
            OperationId: "browser-retirement",
            Reason: "Withdraw labels.",
            VerifiedSigningPublicKey: key.ExportPublicKeyMultibase()), token);
        LabelRetirementBatch batch = Success(await retirement.ReadRetirementBatchAsync(token));
        fixture.DatabaseClock.Advance(TimeSpan.FromHours(2));
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await retirement.CommitRetirementBatchAsync(
            batch: batch,
            negations: [],
            signingPublicKey: null,
            cancellationToken: token)).Error!.Code);
        ILabelRetirementStore restarted = (ILabelRetirementStore)fixture.NewStore();
        LabelRetirementView paused = Success(await restarted.InspectRetirementAsync(administrator, null, token));
        Assert.False(paused.Running);
        Assert.StartsWith("Unauthorized:", paused.PauseReason);
        Assert.Null(Success(await restarted.ReadRetirementBatchAsync(token)).OperationId);
        LabelRetirementView resumed = Success(await restarted.ExecuteRetirementAsync(
            administrator, new("resumeRetirement", "resume-authority", "Adopt work with current authority."), token));
        Assert.True(resumed.Running);
        Assert.Null(resumed.PauseReason);
        await restarted.PauseRetirementAsync(batch.Generation, "An old worker failed.", token);
        Assert.True(Success(await restarted.InspectRetirementAsync(administrator, null, token)).Running);
        Assert.NotNull(Success(await restarted.ReadRetirementBatchAsync(token)).OperationId);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ConfirmationIsActionBoundExpiresAndRecoversCommittedIntent(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        ILabelRetirementStore retirement = (ILabelRetirementStore)fixture.Store;
        LabelRetirementCommand begin = new("beginRetirement", "retirement", "Retire this labeler.");
        Assert.Equal(LabelServiceErrorCode.StaleDecision, (await retirement.ExecuteRetirementAsync(caller, begin, token)).Error!.Code);
        LabelRetirementView preview = Success(await retirement.InspectRetirementAsync(caller, begin, token));
        Assert.NotNull(preview.Confirmation);
        Assert.Equal(LabelServiceErrorCode.StaleDecision, (await retirement.ExecuteRetirementAsync(
            caller, begin with { Reason = "Changed intent.", Confirmation = preview.Confirmation }, token)).Error!.Code);
        fixture.DatabaseClock!.Advance(TimeSpan.FromMinutes(11));
        Assert.Equal(LabelServiceErrorCode.StaleDecision, (await retirement.ExecuteRetirementAsync(
            caller, begin with { Confirmation = preview.Confirmation }, token)).Error!.Code);
        LabelRetirementView started = await ConfirmAsync(retirement, caller, begin, token);
        Assert.Equal("Distributing", started.Phase);
        ILabelRetirementStore restarted = (ILabelRetirementStore)fixture.NewStore();
        Assert.Equal(started, Success(await restarted.ExecuteRetirementAsync(caller, begin, token)));
        Assert.Equal(LabelServiceErrorCode.IdempotencyConflict, (await restarted.ExecuteRetirementAsync(
            caller, begin with { Reason = "Different intent." }, token)).Error!.Code);
        LabelRetirementView inspected = Success(await restarted.InspectRetirementAsync(caller, null, token));
        Assert.Equal(started.Generation, inspected.Generation);
        Assert.Null(inspected.Confirmation);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task HistoryScanningIsBoundedRestartedTimestampOrderedAndExactlyOnce(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DateTimeOffset now = fixture.DatabaseClock!.GetUtcNow();
        List<SignedLabelSnapshot> history = [.. Enumerable.Range(0, 501).Select(index => new SignedLabelSnapshot(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: $"did:plc:retirement{index}",
            value: "spam",
            creationTimestamp: now.AddDays(1))))];
        history.Add(new(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: "did:plc:expired",
            value: "spam",
            creationTimestamp: now.AddDays(-2),
            expiresAt: now.AddDays(-1))));
        history.Add(new(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: "did:plc:negated",
            value: "spam",
            creationTimestamp: now)));
        history.Add(new(key.CreateNegation(
            source: StoreFixture.s_issuer,
            uri: "did:plc:negated",
            value: "spam",
            creationTimestamp: now.AddTicks(1))));
        // A later sequence must not replace a timestamp-newer observation.
        history.Add(new(key.CreateNegation(
            source: StoreFixture.s_issuer,
            uri: "did:plc:retirement0",
            value: "spam",
            creationTimestamp: now)));
        await fixture.Store.AppendAsync("initial-history", history, cancellationToken: token);
        await fixture.Store.PublishAsync(limit: 1000, cancellationToken: token);
        LabelRetirementCommand begin = new(
            Action: "beginRetirement",
            OperationId: "retire-history",
            Reason: "Withdraw applicable labels.",
            VerifiedSigningPublicKey: key.ExportPublicKeyMultibase());
        await ConfirmAsync((ILabelRetirementStore)fixture.Store, caller, begin, token);
        int pages = 0;
        LabelRetirementView state;
        do
        {
            ILabelRetirementStore retirement = (ILabelRetirementStore)fixture.NewStore();
            LabelRetirementBatch batch = Success(await retirement.ReadRetirementBatchAsync(token));
            Assert.InRange(batch.Labels.Count, 0, 250);
            List<SignedLabelSnapshot> negations = batch.Scanning ? [] : [.. batch.Labels.Select((label, index) => new SignedLabelSnapshot(key.CreateNegation(
                source: label.SignedLabel.Label.Source,
                uri: label.SignedLabel.Label.Uri,
                value: label.SignedLabel.Label.Value,
                creationTimestamp: batch.CreatedAt.AddTicks(index),
                cid: label.SignedLabel.Label.Cid)))];
            state = Success(await retirement.CommitRetirementBatchAsync(
                batch: batch,
                negations: negations,
                signingPublicKey: negations.Count == 0 ? null : key.ExportPublicKeyMultibase(),
                cancellationToken: token));
            Assert.Equal(LabelServiceErrorCode.StaleDecision, (await retirement.CommitRetirementBatchAsync(
                batch: batch,
                negations: negations,
                signingPublicKey: negations.Count == 0 ? null : key.ExportPublicKeyMultibase(),
                cancellationToken: token)).Error!.Code);
            await fixture.Store.PublishAsync(limit: 1000, cancellationToken: token);
            Assert.True(++pages < 12, "Retirement must make bounded progress.");
        }
        while (state.Phase == "Retiring");
        Assert.Equal("Distributing", state.Phase);
        Assert.Equal(history.Count + 501, state.IssuedThrough);
        Assert.Equal(history.Count, state.NegatedThrough);
        LabelStoreState stream = await fixture.NewStore().GetStateAsync(token);
        Assert.Equal(stream.IssuedThrough, stream.PublishedThrough);
        LabelDistributionPage distribution = await fixture.Store.ReadDistributionAsync(
            streamId: stream.StreamId,
            after: history.Count,
            through: stream.PublishedThrough,
            uriPatterns: [],
            sources: [],
            limit: 250,
            cancellationToken: token);
        Assert.Equal(250, distribution.Labels.Count);
        Assert.All(distribution.Labels, label =>
        {
            Assert.True(label.SignedLabel.Label.IsNegationLabel);
            Assert.True(label.SignedLabel.Label.CreationTimestamp > now.AddDays(1));
        });
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task GraceUsesDatabaseTimeAndDestructiveActionsHaveSeparateConfirmations(string engine)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        ILabelRetirementStore retirement = (ILabelRetirementStore)fixture.Store;
        ILabelMaintenanceStore maintenance = (ILabelMaintenanceStore)fixture.Store;
        Guid signingHost = Guid.NewGuid();
        await maintenance.RegisterHostAsync(signingHost, token);
        await ConfirmAsync(retirement, caller, new("beginRetirement", "begin", "Retire empty labeler."), token);
        LabelRetirementCommand grace = new("beginGracePeriod", "grace", "Distribute withdrawals.", GracePeriodDays: 1);
        LabelRetirementView state = await ConfirmAsync(retirement, caller, grace, token);
        Assert.Equal(fixture.DatabaseClock!.GetUtcNow().AddDays(1), state.GraceDeadline);
        LabelRetirementCommand complete = new(
            Action: "completeRetirement",
            OperationId: "complete",
            Reason: "Grace has elapsed.",
            Attestation: "Public distribution and DID verification remained available throughout grace.");
        LabelRetirementView preview = Success(await retirement.InspectRetirementAsync(caller, complete, token));
        Assert.Equal(LabelServiceErrorCode.StaleDecision, (await retirement.ExecuteRetirementAsync(
            caller, complete with { Confirmation = preview.Confirmation }, token)).Error!.Code);
        fixture.DatabaseClock.Advance(TimeSpan.FromDays(1));
        state = await ConfirmAsync(retirement, caller, complete, token);
        Assert.Equal("Retired", state.Phase);
        Assert.Equal("None", state.KeyRemoval);
        LabelRetirementCommand request = new(
            Action: "requestKeyRemoval",
            OperationId: "inventory",
            Reason: "Remove keys separately.",
            Inventory: "Primary PEM, environment copies, deployment secrets and encrypted backup.");
        state = await ConfirmAsync(retirement, caller, request, token);
        Assert.Equal("Requested", state.KeyRemoval);
        LabelRetirementCommand attest = new(
            Action: "attestKeyRemoval",
            OperationId: "attestation",
            Reason: "External removal completed.",
            Inventory: request.Inventory,
            Attestation: "All signing processes stopped and inventoried copies removed.");
        Assert.Equal(LabelServiceErrorCode.StaleDecision, (await retirement.ExecuteRetirementAsync(caller, attest, token)).Error!.Code);
        LabelRetirementView blocked = Success(await retirement.InspectRetirementAsync(caller, attest, token));
        Assert.Equal(LabelServiceErrorCode.StaleDecision, (await retirement.ExecuteRetirementAsync(
            caller, attest with { Confirmation = blocked.Confirmation }, token)).Error!.Code);
        await Assert.ThrowsAsync<InvalidOperationException>(() => maintenance.RegisterHostAsync(Guid.NewGuid(), token));
        await maintenance.RegisterHostWithSigningRoleAsync(Guid.NewGuid(), false, token);
        LabelRetirementCommand stop = new(
            Action: "confirmRetirementHostStopped",
            OperationId: "confirm-crashed-host-stopped",
            Reason: "Recover stale registration without starting rotation.",
            Attestation: "Independently verified that this exact signing process is stopped.",
            HostInstanceId: signingHost);
        LabelRetirementView stopPreview = Success(await retirement.InspectRetirementAsync(caller, stop, token));
        Assert.Equal(LabelServiceErrorCode.StaleDecision, (await retirement.ExecuteRetirementAsync(
            caller, stop with { HostInstanceId = Guid.NewGuid(), Confirmation = stopPreview.Confirmation }, token)).Error!.Code);
        await ConfirmAsync(retirement, caller, stop, token);
        Assert.Equal(LabelServiceErrorCode.StaleDecision, (await retirement.ExecuteRetirementAsync(
            caller, attest with { Confirmation = blocked.Confirmation }, token)).Error!.Code);
        state = await ConfirmAsync(retirement, caller, attest, token);
        Assert.Equal("Attested", state.KeyRemoval);
        LabelRetirementCommand purge = new("purgeRetiredData", "purge", "Delete only retained local labels.");
        Assert.Equal(LabelServiceErrorCode.StaleDecision, (await retirement.ExecuteRetirementAsync(caller, purge, token)).Error!.Code);
        state = await ConfirmAsync(retirement, caller, purge, token);
        Assert.Equal("Purging", state.Phase);
        await retirement.AdvanceRetirementPurgeAsync(token);
        state = Success(await ((ILabelRetirementStore)fixture.NewStore()).InspectRetirementAsync(caller, null, token));
        Assert.Equal("Purged", state.Phase);
        Assert.NotEmpty(Success(await ((ILabelAdministrationStore)fixture.Store).GetAdministrationAuditAsync(caller, cancellationToken: token)));
    }

    /// <summary>Obtains and explicitly consumes a server-issued challenge for one immutable intent.</summary>
    private static async Task<LabelRetirementView> ConfirmAsync(
        ILabelRetirementStore store,
        LabelAdministrationCaller caller,
        LabelRetirementCommand command,
        CancellationToken token)
    {
        LabelRetirementView preview = Success(await store.InspectRetirementAsync(caller, command, token));
        return Success(await store.ExecuteRetirementAsync(caller, command with { Confirmation = preview.Confirmation }, token));
    }

    /// <summary>Retains the provider diagnostic when an expected successful result fails.</summary>
    private static T Success<T>(LabelServiceResult<T> result) where T : class
    {
        Assert.True(result.Succeeded, result.Error?.Message + result.Error?.Exception);
        return result.Result;
    }
}
