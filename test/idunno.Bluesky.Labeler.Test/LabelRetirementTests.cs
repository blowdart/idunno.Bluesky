// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Signing;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Data.Sqlite;

namespace idunno.Bluesky.Labeler.Test;

/// <summary>
/// Shared helpers for the <see cref="LabelRetirement"/> test classes.
/// </summary>
public abstract class LabelRetirementTestBase
{

    /// <summary>Exposes one fixture-owned key without secret-store I/O during failure-injection tests.</summary>
    private protected sealed class RetirementKeyRing(LabelSigningKey key) : ILabelSigningKeyRing
    {
        public string ActiveKeyId => "active";
        public LabelSigningKey GetSigningKey(string keyId) => keyId == ActiveKeyId ? key : throw new KeyNotFoundException();
        public string GetPublicKeyMultibase(string keyId) => GetSigningKey(keyId).ExportPublicKeyMultibase();
        public Task ActivateAsync(string keyId, CancellationToken cancellationToken = default)
        {
            GetSigningKey(keyId);
            return Task.CompletedTask;
        }
    }


    /// <summary>Obtains and explicitly consumes a server-issued challenge for one immutable intent.</summary>
    private protected static async Task<LabelRetirementView> ConfirmAsync(
        ILabelRetirementStore store,
        LabelAdministrationCaller caller,
        LabelRetirementCommand command,
        CancellationToken token)
    {
        LabelRetirementView preview = Success(await store.InspectRetirementAsync(caller, command, token));
        return Success(await store.ExecuteRetirementAsync(caller, command with { Confirmation = preview.Confirmation }, token));
    }


    /// <summary>Retains the provider diagnostic when an expected successful result fails.</summary>
    private protected static T Success<T>(LabelServiceResult<T> result) where T : class
    {
        Assert.True(result.Succeeded, result.Error?.Message + result.Error?.Exception);
        return result.Result;
    }
}

/// <summary>
/// Engine-parameterised tests; one derived class per engine lets xUnit run the engines in parallel.
/// </summary>
public abstract class LabelRetirementTests : LabelRetirementTestBase
{
    /// <summary>
    /// Gets the storage engine exercised by the inherited tests.
    /// </summary>
    protected abstract string Engine { get; }

    [Fact]
    public async Task PublicationOnlyWaitingDoesNotManufactureRetirementProgressOrAudit()
    {
        string engine = Engine;
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot[] labels = [.. Enumerable.Range(0, 4).Select(index => new SignedLabelSnapshot(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: $"did:plc:subject{index}",
            value: "spam",
            creationTimestamp: fixture.DatabaseClock!.GetUtcNow())))];
        await fixture.Store.AppendAsync("initial-history", labels, cancellationToken: token);
        await fixture.PublishThroughAsync();
        ILabelRetirementStore retirement = (ILabelRetirementStore)fixture.Store;
        ILabelAdministrationStore administration = (ILabelAdministrationStore)fixture.Store;
        await ConfirmAsync(retirement, caller, new(
            Action: "beginRetirement",
            OperationId: "start",
            Reason: "Withdraw labels.",
            VerifiedSigningPublicKey: key.ExportPublicKeyMultibase()), token);
        LabelRetirementService service = new(fixture.Store, retirement, new RetirementKeyRing(key));
        Assert.Null(await service.AdvanceAsync(token));
        Assert.Null(await service.AdvanceAsync(token));
        LabelRetirementView before = Success(await retirement.InspectRetirementAsync(caller, null, token));
        Assert.Equal("Distributing", before.Phase);
        Assert.Equal(8, before.IssuedThrough);
        Assert.Equal(5, before.PublishedThrough);
        Assert.True(before.Running);
        LabelStoreState stream = await fixture.Store.GetStateAsync(token);
        int auditCount = Success(await administration.GetAdministrationAuditAsync(caller, cancellationToken: token))
            .Count(receipt => receipt.Action != "retirement-inspection");
        for (int attempt = 0; attempt < 3; attempt++)
        {
            fixture.DatabaseClock!.Advance(TimeSpan.FromMilliseconds(1));
            Assert.Null(await service.AdvanceAsync(token));
            // Direct provider callers must also leave publication-only waiting unchanged.
            LabelRetirementBatch batch = Success(await retirement.ReadRetirementBatchAsync(token));
            Assert.True(batch.Publishing);
            Success(await retirement.CommitRetirementBatchAsync(
                batch: batch,
                negations: [],
                signingPublicKey: null,
                cancellationToken: token));
            LabelRetirementView waiting = Success(await retirement.InspectRetirementAsync(caller, null, token));
            Assert.Equal(before with { Hosts = null }, waiting with { Hosts = null });
            Assert.Equal(stream, await fixture.Store.GetStateAsync(token));
            Assert.Equal(auditCount, Success(await administration.GetAdministrationAuditAsync(caller, cancellationToken: token))
                .Count(receipt => receipt.Action != "retirement-inspection"));
        }

        fixture.DatabaseClock!.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(await service.AdvanceAsync(token));
        LabelRetirementView completed = Success(await retirement.InspectRetirementAsync(caller, null, token));
        Assert.False(completed.Running);
        Assert.Equal(8, completed.PublishedThrough);
        Assert.Equal(before.Generation + 1, completed.Generation);
        Assert.True(completed.LastProgressAt > before.LastProgressAt);
        Assert.Equal(auditCount + 1, Success(await administration.GetAdministrationAuditAsync(caller, cancellationToken: token))
            .Count(receipt => receipt.Action != "retirement-inspection"));
        Assert.Null(await service.AdvanceAsync(token));
        Assert.Equal(completed with { Hosts = null }, Success(await retirement.InspectRetirementAsync(caller, null, token)) with { Hosts = null });
    }


    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task TiedPositiveObservationsRetainLongestLifetimeRegardlessOfInsertionOrder(bool reverse, bool unlimited)
    {
        string engine = Engine;
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DateTimeOffset now = fixture.DatabaseClock!.GetUtcNow();
        DateTimeOffset? expiry = unlimited ? null : now.AddDays(1);
        SignedLabelSnapshot expired = new(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: "did:plc:subject",
            value: "spam",
            creationTimestamp: now.AddDays(-2),
            expiresAt: now.AddDays(-1)));
        SignedLabelSnapshot active = new(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: "did:plc:subject",
            value: "spam",
            creationTimestamp: now.AddDays(-2),
            expiresAt: expiry));
        await fixture.Store.AppendAsync("tied-history", reverse ? [active, expired] : [expired, active], cancellationToken: token);
        await fixture.PublishThroughAsync();
        ILabelRetirementStore retirement = (ILabelRetirementStore)fixture.Store;
        await ConfirmAsync(retirement, caller, new(
            Action: "beginRetirement",
            OperationId: "retire-tied-history",
            Reason: "Withdraw every applicable observation.",
            VerifiedSigningPublicKey: key.ExportPublicKeyMultibase()), token);
        LabelRetirementService service = new(fixture.Store, retirement, new RetirementKeyRing(key));
        Assert.Null(await service.AdvanceAsync(token));
        LabelRetirementBatch batch = Success(await retirement.ReadRetirementBatchAsync(token));
        Assert.Equal(expiry, Assert.Single(batch.Labels).SignedLabel.Label.ExpiresAt);
        LabelRetirementView view;
        int steps = 0;
        do
        {
            Assert.Null(await service.AdvanceAsync(token));
            await fixture.PublishThroughAsync();
            view = Success(await retirement.InspectRetirementAsync(caller, null, token));
            Assert.True(++steps < 5);
        }
        while (view.Phase == "Retiring");
        Assert.Equal(3, view.IssuedThrough);
        Assert.Equal(view.IssuedThrough, view.PublishedThrough);
    }


    [Theory]
    [InlineData("read", false)]
    [InlineData("commit", false)]
    [InlineData("publication", false)]
    [InlineData("read", true)]
    [InlineData("commit", true)]
    [InlineData("publication", true)]
    public async Task FailedWorkerEpochRecoversPauseAcrossUncertainProgressButNotAdministratorResume(string failure, bool signing)
    {
        string engine = Engine;
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        await fixture.Store.AppendAsync("initial-label", [new(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: "did:plc:subject",
            value: "spam",
            creationTimestamp: fixture.DatabaseClock!.GetUtcNow()))], cancellationToken: token);
        await fixture.Store.PublishAsync(cancellationToken: token);
        ILabelRetirementStore retirement = (ILabelRetirementStore)fixture.Store;
        await ConfirmAsync(retirement, caller, new(
            Action: "beginRetirement",
            OperationId: "start",
            Reason: "Withdraw labels.",
            VerifiedSigningPublicKey: key.ExportPublicKeyMultibase()), token);
        LabelRetirementService service = new(fixture.Store, retirement, new RetirementKeyRing(key));
        if (signing)
        {
            Assert.Null(await service.AdvanceAsync(token));
        }

        int transaction = 0;
        fixture.SetCommitHooks(
            beforeCommit: (_, _, _) =>
            {
                transaction++;
                if (failure == "read" && transaction == 2 || failure == "publication" && transaction == 4)
                {
                    throw new SqliteException("Injected storage failure.", 10);
                }

                return Task.CompletedTask;
            },
            afterCommit: (_, _) => failure == "commit" && transaction == 3
                ? throw new SqliteException("Injected lost batch acknowledgement.", 10)
                : Task.CompletedTask);
        if (failure == "publication")
        {
            await Assert.ThrowsAsync<SqliteException>(() => service.AdvanceAsync(token));
        }
        else
        {
            LabelServiceError? error = await service.AdvanceAsync(token);
            Assert.Equal(failure == "read" ? LabelServiceErrorCode.StorageUnavailable : LabelServiceErrorCode.OutcomeUnknown, error?.Code);
        }

        fixture.SetCommitHooks();
        LabelRetirementCheckpoint checkpoint = Assert.IsType<LabelRetirementCheckpoint>(service.RecoveryCheckpoint);
        LabelRetirementView before = Success(await retirement.InspectRetirementAsync(caller, null, token));
        Assert.Equal(failure == "read" ? checkpoint.Generation : checkpoint.Generation + 1, before.Generation);
        Assert.Equal(signing && failure != "read" ? 2 : 1, before.IssuedThrough);
        if (failure == "publication")
        {
            Assert.Equal(before.Generation, service.ObservedGeneration);
        }

        ILabelRetirementStore restarted = (ILabelRetirementStore)fixture.NewStore();
        await restarted.PauseRetirementCheckpointAsync(checkpoint, "StorageUnavailable: explicit recovery required.", token);
        LabelRetirementView paused = Success(await restarted.InspectRetirementAsync(caller, null, token));
        Assert.False(paused.Running);
        Assert.StartsWith("StorageUnavailable:", paused.PauseReason);
        Assert.Null(Success(await restarted.ReadRetirementBatchAsync(token)).OperationId);
        Success(await restarted.ExecuteRetirementAsync(caller, new("resumeRetirement", "resume", "Recover explicitly."), token));
        await restarted.PauseRetirementCheckpointAsync(checkpoint, "A stale worker must not override recovery.", token);
        Assert.True(Success(await restarted.InspectRetirementAsync(caller, null, token)).Running);
        Assert.Equal(LabelServiceErrorCode.StaleDecision, (await restarted.ReadRetirementBatchAsync(token, checkpoint)).Error!.Code);
    }


    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ExhaustedTimestampPreparationPausesBeforeReturningFailure(int ticksBeforeMaximum)
    {
        string engine = Engine;
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        await fixture.Store.AppendAsync("maximum-label", [new(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: "did:plc:subject",
            value: "spam",
            creationTimestamp: DateTimeOffset.MaxValue.AddTicks(-ticksBeforeMaximum)))], cancellationToken: token);
        await fixture.Store.PublishAsync(cancellationToken: token);
        ILabelRetirementStore retirement = (ILabelRetirementStore)fixture.Store;
        await ConfirmAsync(retirement, caller, new(
            Action: "beginRetirement",
            OperationId: "start",
            Reason: "Withdraw labels.",
            VerifiedSigningPublicKey: key.ExportPublicKeyMultibase()), token);
        Assert.Equal(LabelServiceErrorCode.TimestampExhausted, (await retirement.ReadRetirementBatchAsync(token)).Error!.Code);
        ILabelRetirementStore restarted = (ILabelRetirementStore)fixture.NewStore();
        LabelRetirementView paused = Success(await restarted.InspectRetirementAsync(caller, null, token));
        Assert.False(paused.Running);
        Assert.StartsWith("TimestampExhausted:", paused.PauseReason);
        Assert.Null(Success(await restarted.ReadRetirementBatchAsync(token)).OperationId);
    }


    [Fact]
    public async Task VersionElevenMigrationPreservesLegacyRetirementAndRequiresExplicitAdoption()
    {
        string engine = Engine;
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransitionRecoveryDistinguishesRollbackFromLostAcknowledgement(bool afterCommit)
    {
        string engine = Engine;
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


    [Fact]
    public async Task ExpiredWorkerAuthorityPausesDurablyUntilExplicitResume()
    {
        string engine = Engine;
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


    [Fact]
    public async Task ConfirmationIsActionBoundExpiresAndRecoversCommittedIntent()
    {
        string engine = Engine;
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


    [Fact]
    public async Task HistoryScanningIsBoundedRestartedTimestampOrderedAndExactlyOnce()
    {
        string engine = Engine;
        const int bulk = 25;
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        fixture.UsePageSizes(history: 10);
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DateTimeOffset now = fixture.DatabaseClock!.GetUtcNow();
        List<SignedLabelSnapshot> history = [.. Enumerable.Range(0, bulk).Select(index => new SignedLabelSnapshot(key.CreateLabel(
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
        await fixture.PublishThroughAsync();
        LabelRetirementCommand begin = new(
            Action: "beginRetirement",
            OperationId: "retire-history",
            Reason: "Withdraw applicable labels.",
            VerifiedSigningPublicKey: key.ExportPublicKeyMultibase());
        await ConfirmAsync((ILabelRetirementStore)fixture.Store, caller, begin, token);
        LabelRetirementCommand resume = new(
            Action: "resumeRetirement",
            OperationId: "resume-before-distribution",
            Reason: "Recover with verified signing evidence.",
            VerifiedSigningPublicKey: key.ExportPublicKeyMultibase());
        LabelRetirementView resumed = Success(await ((ILabelRetirementStore)fixture.Store).ExecuteRetirementAsync(caller, resume, token));
        int pages = 0;
        LabelRetirementView state;
        do
        {
            ILabelRetirementStore retirement = (ILabelRetirementStore)fixture.NewStore();
            LabelRetirementBatch batch = Success(await retirement.ReadRetirementBatchAsync(token));
            Assert.InRange(batch.Labels.Count, 0, 10);
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
            await fixture.PublishThroughAsync();
            Assert.True(++pages < 12, "Retirement must make bounded progress.");
        }
        while (state.Phase == "Retiring");
        Assert.Equal("Distributing", state.Phase);
        Assert.Equal(history.Count + bulk, state.IssuedThrough);
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
        Assert.Equal(bulk, distribution.Labels.Count);
        ILabelRetirementStore completed = (ILabelRetirementStore)fixture.NewStore();
        LabelRetirementCommand retry = resume with { VerifiedSigningPublicKey = null };
        Assert.Equal(resumed, Success(await completed.RecoverRetirementAsync(caller, retry, token)).Receipt);
        Assert.Equal(resumed, Success(await completed.ExecuteRetirementAsync(caller, retry, token)));
        Assert.Equal(LabelServiceErrorCode.IdempotencyConflict, (await completed.RecoverRetirementAsync(
            caller, retry with { Reason = "Different intent." }, token)).Error!.Code);
        Assert.All(distribution.Labels, label =>
        {
            Assert.True(label.SignedLabel.Label.IsNegationLabel);
            Assert.True(label.SignedLabel.Label.CreationTimestamp > now.AddDays(1));
        });
    }


    [Fact]
    public async Task GraceUsesDatabaseTimeAndDestructiveActionsHaveSeparateConfirmations()
    {
        string engine = Engine;
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
}

/// <summary>
/// Runs <see cref="LabelRetirementTests"/> against sqlite.
/// </summary>
public sealed class LabelRetirementSqliteTests : LabelRetirementTests
{
    /// <inheritdoc />
    protected override string Engine => "sqlite";
}

/// <summary>
/// Runs <see cref="LabelRetirementTests"/> against postgresql.
/// </summary>
public sealed class LabelRetirementPostgreSqlTests : LabelRetirementTests
{
    /// <inheritdoc />
    protected override string Engine => "postgresql";
}

/// <summary>
/// Runs <see cref="LabelRetirementTests"/> against mysql.
/// </summary>
public sealed class LabelRetirementMySqlTests : LabelRetirementTests
{
    /// <inheritdoc />
    protected override string Engine => "mysql";
}

