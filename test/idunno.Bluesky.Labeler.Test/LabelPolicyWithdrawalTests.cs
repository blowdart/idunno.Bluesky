// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Data.Sqlite;

namespace idunno.Bluesky.Labeler.Test;

public class LabelPolicyWithdrawalTests
{
    private static readonly Cid s_cid = new(TargetResolutionHandler.RecordCid);
    private const string ReducedDeclaration = """
        {"$type":"app.bsky.labeler.service","createdAt":"2026-01-01T00:00:00Z","subjectTypes":["account","record"],"policies":{"labelValues":["scam"]}}
        """;

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task WithdrawalActivityAndProgressUseDatabaseTimeAndSurviveRestart(string engine, bool legacy)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        ILabelPolicyStore policies = (ILabelPolicyStore)fixture.Store;
        LabelPolicyState active = await LabelPolicyStoreTests.ActivateAsync(policies, caller, token);
        DateTimeOffset initialTime = fixture.DatabaseClock!.GetUtcNow();
        await fixture.Store.AppendAsync("timestamp-history", [new(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: "did:plc:timestamp-subject",
            value: "spam",
            creationTimestamp: initialTime))], cancellationToken: token);
        await fixture.Store.PublishAsync(cancellationToken: token);
        LabelPolicyState started = await BeginAsync(
            policies: policies, caller: caller, active: active, token: token);
        Assert.Null(started.Withdrawal!.LastActivityAt);
        Assert.Null(started.Withdrawal.LastProgressAt);
        if (legacy)
        {
            await using DbConnection connection = fixture.Connection();
            await connection.OpenAsync(token);
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT payload FROM label_policy_gate WHERE id=1";
            byte[] payload = Assert.IsType<byte[]>(await command.ExecuteScalarAsync(token));
            JsonNode document = JsonNode.Parse(payload)!;
            JsonObject withdrawal = document["Withdrawal"]!.AsObject();
            withdrawal.Remove("LastActivityAt");
            withdrawal.Remove("LastProgressAt");
            withdrawal.Remove("PublishedThrough");
            command.CommandText = "UPDATE label_policy_gate SET payload=@payload WHERE id=1";
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = "@payload";
            parameter.Value = Encoding.UTF8.GetBytes(document.ToJsonString());
            command.Parameters.Add(parameter);
            Assert.Equal(1, await command.ExecuteNonQueryAsync(token));
        }
        ILabelPolicyWithdrawalStore withdrawals = (ILabelPolicyWithdrawalStore)fixture.Store;
        fixture.DatabaseClock.Advance(TimeSpan.FromSeconds(1));
        LabelPolicyWithdrawalBatch scan = Success(await withdrawals.ReadPolicyWithdrawalBatchAsync(token));
        Assert.Equal(fixture.DatabaseClock.GetUtcNow(), scan.Policy!.Withdrawal!.LastActivityAt);
        Assert.Null(scan.Policy.Withdrawal.LastProgressAt);
        LabelPolicyState scanned = Success(await withdrawals.CommitPolicyWithdrawalBatchAsync(
            batch: scan, negations: [], signingPublicKey: null, cancellationToken: token));
        Assert.Equal(fixture.DatabaseClock.GetUtcNow(), scanned.Withdrawal!.LastProgressAt);
        fixture.DatabaseClock.Advance(TimeSpan.FromSeconds(1));
        LabelPolicyWithdrawalBatch negate = Success(await withdrawals.ReadPolicyWithdrawalBatchAsync(token));
        LabelPolicyState negated = Success(await withdrawals.CommitPolicyWithdrawalBatchAsync(
            batch: negate, negations: Sign(negate, key), signingPublicKey: key.ExportPublicKeyMultibase(), cancellationToken: token));
        DateTimeOffset lastProgress = negated.Withdrawal!.LastProgressAt!.Value;
        fixture.DatabaseClock.Advance(TimeSpan.FromSeconds(1));
        ILabelPolicyWithdrawalStore restarted = (ILabelPolicyWithdrawalStore)fixture.NewStore();
        LabelPolicyWithdrawalBatch waiting = Success(await restarted.ReadPolicyWithdrawalBatchAsync(token));
        LabelPolicyState unchanged = Success(await restarted.CommitPolicyWithdrawalBatchAsync(
            batch: waiting, negations: [], signingPublicKey: null, cancellationToken: token));
        Assert.Equal(LabelPolicyPhase.Withdrawing, unchanged.Phase);
        Assert.Equal(lastProgress, unchanged.Withdrawal!.LastProgressAt);
        Assert.Equal(fixture.DatabaseClock.GetUtcNow(), unchanged.Withdrawal.LastActivityAt);
        await fixture.Store.PublishAsync(cancellationToken: token);
        fixture.DatabaseClock.Advance(TimeSpan.FromSeconds(1));
        LabelPolicyWithdrawalBatch published = Success(await restarted.ReadPolicyWithdrawalBatchAsync(token));
        Assert.Equal(fixture.DatabaseClock.GetUtcNow(), published.Policy!.Withdrawal!.LastProgressAt);
        Assert.Equal((await fixture.Store.GetStateAsync(token)).PublishedThrough, published.Policy.Withdrawal.PublishedThrough);
        LabelPolicyState complete = Success(await restarted.CommitPolicyWithdrawalBatchAsync(
            batch: published, negations: [], signingPublicKey: null, cancellationToken: token));
        Assert.Equal(LabelPolicyPhase.Publishing, complete.Phase);
        LabelPolicyState retained = (await ((ILabelPolicyStore)fixture.NewStore()).ReadPolicyStateAsync(token))!;
        Assert.Equal(complete.Withdrawal!.LastActivityAt, retained.Withdrawal!.LastActivityAt);
        Assert.Equal(complete.Withdrawal.LastProgressAt, retained.Withdrawal.LastProgressAt);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task RemovedValuesAreNegatedInBoundedRestartedBatchesBeforePublication(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        ILabelPolicyStore policies = (ILabelPolicyStore)fixture.Store;
        LabelPolicyState active = await LabelPolicyStoreTests.ActivateAsync(policies, caller, token);
        DateTimeOffset now = fixture.DatabaseClock!.GetUtcNow();
        List<SignedLabelSnapshot> history = [.. Enumerable.Range(0, 501).Select(index => new SignedLabelSnapshot(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: $"did:plc:subject{index}",
            value: "spam",
            creationTimestamp: now)))];
        history.Add(new(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: "at://did:plc:author/app.bsky.feed.post/one",
            value: "spam",
            creationTimestamp: now,
            cid: s_cid)));
        history.Add(new(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: "at://did:plc:author/app.bsky.feed.post/one",
            value: "spam",
            creationTimestamp: now,
            cid: new Cid("bafyreihdwdcefgh4dqkjv67uzcmw7ojee6xedzdetojuzjevtenxquvyku"))));
        history.Add(new(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: "did:plc:retained",
            value: "scam",
            creationTimestamp: now.AddDays(1))));
        history.Add(new(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: "did:plc:expired",
            value: "spam",
            creationTimestamp: now.AddHours(-2),
            expiresAt: now.AddHours(-1))));
        history.Add(new(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: "did:plc:alreadyremoved",
            value: "spam",
            creationTimestamp: now)));
        history.Add(new(key.CreateNegation(
            source: StoreFixture.s_issuer,
            uri: "did:plc:alreadyremoved",
            value: "spam",
            creationTimestamp: now.AddTicks(1))));
        history.Add(history[0]);
        await fixture.Store.AppendAsync("initial-history", history, cancellationToken: token);
        LabelPolicyState started = await BeginAsync(
            policies: policies,
            caller: caller,
            active: active,
            token: token);
        Assert.Equal(LabelPolicyPhase.Withdrawing, started.Phase);
        Assert.Equal(["spam"], started.Withdrawal!.Values);
        using JsonDocument declaration = JsonDocument.Parse(ReducedDeclaration);
        Assert.Equal(LabelServiceErrorCode.PolicyUnavailable, (await policies.ObservePolicyAsync(
            expectedRevision: started.Revision,
            verificationStartedAt: started.DatabaseTime,
            observedCid: s_cid,
            declaration: declaration.RootElement,
            cancellationToken: token)).Error!.Code);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.AppendAsync(
            "cannot-issue-during-cleanup", [history[0]], cancellationToken: token));
        LabelStoreState stream = await fixture.Store.GetStateAsync(token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.BeginRetirementAsync(
            expectedRevision: stream.Revision,
            caller: caller,
            cancellationToken: token));

        LabelPolicyState current = started;
        int signedCount = 0;
        int batches = 0;
        while (current.Withdrawal!.NegatedThrough < current.Withdrawal.UpperBound)
        {
            ILabelPolicyWithdrawalStore restarted = (ILabelPolicyWithdrawalStore)fixture.NewStore();
            LabelPolicyWithdrawalBatch batch = Success(await restarted.ReadPolicyWithdrawalBatchAsync(token));
            Assert.InRange(batch.Labels.Count, 0, 250);
            Assert.InRange(batch.ScannedThrough - current.Withdrawal.ScannedThrough, 0, 250);
            Assert.InRange(batch.NegatedThrough - current.Withdrawal.NegatedThrough, 0, 250);
            SignedLabelSnapshot[] negations = Sign(batch, key);
            signedCount += negations.Length;
            current = Success(await restarted.CommitPolicyWithdrawalBatchAsync(
                batch: batch,
                negations: negations,
                signingPublicKey: key.ExportPublicKeyMultibase(),
                cancellationToken: token));
            Assert.Equal(LabelServiceErrorCode.StaleDecision, (await restarted.CommitPolicyWithdrawalBatchAsync(
                batch: batch,
                negations: negations,
                signingPublicKey: key.ExportPublicKeyMultibase(),
                cancellationToken: token)).Error!.Code);
            Assert.True(++batches < 10);
        }
        Assert.Equal(503, signedCount);
        Assert.Equal(LabelPolicyPhase.Withdrawing, current.Phase);
        Assert.Equal(history.Count + signedCount, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        while (stream.PublishedThrough < stream.IssuedThrough || stream.IssuedThrough == history.Count)
        {
            stream = await fixture.Store.PublishAsync(limit: 250, cancellationToken: token);
        }
        ILabelPolicyWithdrawalStore final = (ILabelPolicyWithdrawalStore)fixture.NewStore();
        current = Success(await final.CommitPolicyWithdrawalBatchAsync(
            batch: Success(await final.ReadPolicyWithdrawalBatchAsync(token)),
            negations: [],
            signingPublicKey: null,
            cancellationToken: token));
        Assert.Equal(LabelPolicyPhase.Publishing, current.Phase);
        Assert.Equal(LabelPolicyPhase.VerifiedActive, Success(await policies.ObservePolicyAsync(
            expectedRevision: current.Revision,
            verificationStartedAt: current.DatabaseTime,
            observedCid: s_cid,
            declaration: declaration.RootElement,
            cancellationToken: token)).Phase);
        List<StoredLabel> distributed = [];
        long after = 0;
        while (after < stream.PublishedThrough)
        {
            LabelDistributionPage page = await fixture.Store.ReadDistributionAsync(
                streamId: stream.StreamId,
                after: after,
                through: stream.PublishedThrough,
                uriPatterns: [],
                sources: [],
                limit: 250,
                cancellationToken: token);
            distributed.AddRange(page.Labels);
            after = page.ScannedThrough;
        }
        Assert.False(Assert.Single(distributed, item => item.SignedLabel.Label.Value == "scam").SignedLabel.Label.IsNegationLabel);
        StoredLabel[] generated = [.. distributed.Where(item => item.Sequence > history.Count)];
        Assert.Equal(503, generated.Length);
        Assert.All(generated, item =>
        {
            Assert.True(item.SignedLabel.Label.IsNegationLabel);
            Assert.True(item.SignedLabel.Label.CreationTimestamp > now.AddDays(1));
        });
        Assert.Equal(2, generated.Count(item => item.SignedLabel.Label.Cid is not null));
        LabelPolicyState activated = (await policies.ReadPolicyStateAsync(token))!;
        Success(await policies.SavePolicyAsync(
            caller: caller,
            operationId: "draft-unchanged",
            policy: new(
                version: "unchanged",
                declaration: declaration.RootElement,
                allowAccounts: true,
                allowRecords: true,
                rules: "Keep scam."),
            reason: "Keep existing values.",
            cancellationToken: token));
        LabelPolicyState unchanged = Success(await policies.BeginPolicyPublicationAsync(
            caller: caller,
            operationId: "publish-unchanged",
            version: "unchanged",
            expectedRevision: activated.Revision,
            previousCid: s_cid,
            reason: "No further withdrawals.",
            cancellationToken: token));
        Assert.Equal(LabelPolicyPhase.Publishing, unchanged.Phase);
        Assert.Null(unchanged.Withdrawal);
    }

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task WithdrawalAuthorizationIsRecheckedAtCommitAndCanBeResumedAfterRestart(string engine, bool expire)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        LabelAdministrationCaller administrator = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        Did backupDid = new("did:plc:backup");
        Success(await team.ChangeTeamAsync(administrator, new(
            operationId: "enroll-backup", action: LabelTeamAction.Enroll, target: backupDid,
            reason: "Recovery administrator.", role: LabelTeamRole.Administrator), token));
        Success(await team.ChangeTeamAsync(administrator, new(
            operationId: "bind-backup", action: LabelTeamAction.BindLocal, target: backupDid,
            reason: "Recovery administrator.", credential: "uid:1"), token));
        LabelAdministrationCaller backup = Success(await team.AuthenticateLocalAsync("uid:1", token));
        LabelAdministrationCaller browser = Success(await team.AuthenticateBrowserAsync(administrator.Actor, token));
        ILabelPolicyStore policies = (ILabelPolicyStore)fixture.Store;
        LabelPolicyState active = await LabelPolicyStoreTests.ActivateAsync(policies, administrator, token);
        await fixture.Store.AppendAsync("original", [new(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: "did:plc:subject",
            value: "spam",
            creationTimestamp: fixture.DatabaseClock!.GetUtcNow()))], cancellationToken: token);
        await BeginAsync(
            policies: policies,
            caller: browser,
            active: active,
            token: token);
        ILabelPolicyWithdrawalStore withdrawals = (ILabelPolicyWithdrawalStore)fixture.Store;
        Success(await withdrawals.CommitPolicyWithdrawalBatchAsync(
            batch: Success(await withdrawals.ReadPolicyWithdrawalBatchAsync(token)),
            negations: [],
            signingPublicKey: null,
            cancellationToken: token));
        LabelPolicyWithdrawalBatch prepared = Success(await withdrawals.ReadPolicyWithdrawalBatchAsync(token));
        SignedLabelSnapshot[] negations = Sign(prepared, key);
        if (expire)
        {
            fixture.DatabaseClock.Advance(TimeSpan.FromMinutes(10));
        }
        else
        {
            Success(await team.ChangeTeamAsync(backup, new(
                operationId: "suspend", action: LabelTeamAction.Suspend, target: administrator.Actor,
                reason: "Revoke publication caller."), token));
        }
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await withdrawals.CommitPolicyWithdrawalBatchAsync(
            batch: prepared,
            negations: negations,
            signingPublicKey: key.ExportPublicKeyMultibase(),
            cancellationToken: token)).Error!.Code);
        Assert.Equal(1, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        LabelPolicyWithdrawalProgress paused = (await policies.ReadPolicyStateAsync(token))!.Withdrawal!;
        Assert.NotNull(paused.PauseReason);
        Assert.Equal(fixture.DatabaseClock.GetUtcNow(), paused.LastActivityAt);
        Assert.Equal(prepared.Policy!.Withdrawal!.LastProgressAt, paused.LastProgressAt);
        ILabelPolicyWithdrawalStore restarted = (ILabelPolicyWithdrawalStore)fixture.NewStore();
        Success(await restarted.ResumePolicyWithdrawalAsync(
            caller: backup,
            operationId: "publish-reduced",
            reason: "Resume authorized cleanup.",
            cancellationToken: token));
        LabelPolicyWithdrawalBatch recovered = Success(await restarted.ReadPolicyWithdrawalBatchAsync(token));
        Assert.Single(recovered.Labels);
        Success(await restarted.CommitPolicyWithdrawalBatchAsync(
            batch: recovered,
            negations: Sign(recovered, key),
            signingPublicKey: key.ExportPublicKeyMultibase(),
            cancellationToken: token));
        Assert.Equal(2, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
    }

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task WithdrawalBatchAndCheckpointAreAtomicAcrossLostAcknowledgements(string engine, bool afterCommit)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        ILabelPolicyStore policies = (ILabelPolicyStore)fixture.Store;
        LabelPolicyState active = await LabelPolicyStoreTests.ActivateAsync(policies, caller, token);
        await fixture.Store.AppendAsync("original", [new(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: "did:plc:subject",
            value: "spam",
            creationTimestamp: fixture.DatabaseClock!.GetUtcNow()))], cancellationToken: token);
        await BeginAsync(
            policies: policies,
            caller: caller,
            active: active,
            token: token);
        ILabelPolicyWithdrawalStore withdrawals = (ILabelPolicyWithdrawalStore)fixture.Store;
        Success(await withdrawals.CommitPolicyWithdrawalBatchAsync(
            batch: Success(await withdrawals.ReadPolicyWithdrawalBatchAsync(token)),
            negations: [],
            signingPublicKey: null,
            cancellationToken: token));
        LabelPolicyWithdrawalBatch batch = Success(await withdrawals.ReadPolicyWithdrawalBatchAsync(token));
        if (afterCommit)
        {
            fixture.SetCommitHooks(afterCommit: (_, _) => throw new SqliteException("Lost acknowledgement.", 10));
        }
        else
        {
            fixture.SetCommitHooks(beforeCommit: (_, _, _) => throw new SqliteException("Before commit.", 10));
        }
        Assert.Equal(afterCommit ? LabelServiceErrorCode.OutcomeUnknown : LabelServiceErrorCode.StorageUnavailable,
            (await withdrawals.CommitPolicyWithdrawalBatchAsync(
                batch: batch,
                negations: Sign(batch, key),
                signingPublicKey: key.ExportPublicKeyMultibase(),
                cancellationToken: token)).Error!.Code);
        fixture.SetCommitHooks();
        ILabelPolicyWithdrawalStore restarted = (ILabelPolicyWithdrawalStore)fixture.NewStore();
        LabelPolicyWithdrawalBatch recovered = Success(await restarted.ReadPolicyWithdrawalBatchAsync(token));
        Assert.Equal(afterCommit ? 0 : 1, recovered.Labels.Count);
        Success(await restarted.CommitPolicyWithdrawalBatchAsync(
            batch: recovered,
            negations: Sign(recovered, key),
            signingPublicKey: key.ExportPublicKeyMultibase(),
            cancellationToken: token));
        Assert.Equal(2, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        LabelPolicyState pending = (await policies.ReadPolicyStateAsync(token))!;
        Success(await policies.AbandonPolicyPublicationAsync(
            caller: caller,
            operationId: "abandon",
            expectedRevision: pending.Revision,
            reason: "Publication was not completed.",
            cancellationToken: token));
        Assert.Null(Success(await restarted.ReadPolicyWithdrawalBatchAsync(token)).Policy);
        Assert.Equal(2, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        Assert.Equal(LabelServiceErrorCode.StaleDecision, (await restarted.ResumePolicyWithdrawalAsync(
            caller: caller,
            operationId: "publish-reduced",
            reason: "Cannot reopen abandoned intent.",
            cancellationToken: token)).Error!.Code);
    }

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task WithdrawalHandlesExactTimestampCapacityAndRejectsExhaustionWithoutProgress(string engine, bool exhausted)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        ILabelPolicyStore policies = (ILabelPolicyStore)fixture.Store;
        LabelPolicyState active = await LabelPolicyStoreTests.ActivateAsync(policies, caller, token);
        await fixture.Store.AppendAsync("original", [new(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: "did:plc:subject",
            value: "spam",
            creationTimestamp: DateTimeOffset.MaxValue.AddTicks(exhausted ? 0 : -2)))], cancellationToken: token);
        LabelPolicyState selected = await BeginAsync(
            policies: policies,
            caller: caller,
            active: active,
            token: token);
        ILabelPolicyWithdrawalStore withdrawals = (ILabelPolicyWithdrawalStore)fixture.Store;
        LabelServiceResult<LabelPolicyWithdrawalBatch> observed = await withdrawals.ReadPolicyWithdrawalBatchAsync(token);
        if (exhausted)
        {
            Assert.Equal(LabelServiceErrorCode.PolicyUnavailable, observed.Error!.Code);
            Assert.Equal(selected.Revision, (await policies.ReadPolicyStateAsync(token))!.Revision);
            Assert.Equal(1, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
            return;
        }
        Success(await withdrawals.CommitPolicyWithdrawalBatchAsync(
            batch: Success(observed),
            negations: [],
            signingPublicKey: null,
            cancellationToken: token));
        LabelPolicyWithdrawalBatch batch = Success(await withdrawals.ReadPolicyWithdrawalBatchAsync(token));
        Assert.Single(batch.Labels);
        Success(await withdrawals.CommitPolicyWithdrawalBatchAsync(
            batch: batch,
            negations: Sign(batch, key),
            signingPublicKey: key.ExportPublicKeyMultibase(),
            cancellationToken: token));
        await fixture.Store.PublishAsync(cancellationToken: token);
        LabelPolicyState completed = Success(await withdrawals.CommitPolicyWithdrawalBatchAsync(
            batch: Success(await withdrawals.ReadPolicyWithdrawalBatchAsync(token)),
            negations: [],
            signingPublicKey: null,
            cancellationToken: token));
        Assert.Equal(LabelPolicyPhase.Publishing, completed.Phase);
        Assert.Equal(2, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ReplacementAfterAbandonmentCompletesOnlyTheRemainingWithdrawals(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        ILabelPolicyStore policies = (ILabelPolicyStore)fixture.Store;
        LabelPolicyState active = await LabelPolicyStoreTests.ActivateAsync(policies, caller, token);
        await fixture.Store.AppendAsync("originals", [.. Enumerable.Range(0, 251).Select(index => new SignedLabelSnapshot(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: $"did:plc:subject{index}",
            value: "spam",
            creationTimestamp: fixture.DatabaseClock!.GetUtcNow())))], cancellationToken: token);
        await BeginAsync(
            policies: policies,
            caller: caller,
            active: active,
            token: token);
        ILabelPolicyWithdrawalStore withdrawals = (ILabelPolicyWithdrawalStore)fixture.Store;
        LabelPolicyState partial = active;
        for (int pass = 0; pass < 3; pass++)
        {
            LabelPolicyWithdrawalBatch batch = Success(await withdrawals.ReadPolicyWithdrawalBatchAsync(token));
            partial = Success(await withdrawals.CommitPolicyWithdrawalBatchAsync(
                batch: batch,
                negations: Sign(batch, key),
                signingPublicKey: key.ExportPublicKeyMultibase(),
                cancellationToken: token));
        }
        Assert.Equal(250, partial.Withdrawal!.NegatedThrough);
        Assert.Equal(501, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        LabelPolicyState abandoned = Success(await policies.AbandonPolicyPublicationAsync(
            caller: caller,
            operationId: "abandon-partial",
            expectedRevision: partial.Revision,
            reason: "Replace the publication intent.",
            cancellationToken: token));
        using JsonDocument declaration = JsonDocument.Parse(ReducedDeclaration);
        Success(await policies.SavePolicyAsync(
            caller: caller,
            operationId: "draft-reconciled",
            policy: new(
                version: "reconciled",
                declaration: declaration.RootElement,
                allowAccounts: true,
                allowRecords: true,
                rules: "Retire remaining spam."),
            reason: "Complete previous withdrawal.",
            cancellationToken: token));
        LabelPolicyState current = Success(await policies.BeginPolicyPublicationAsync(
            caller: caller,
            operationId: "publish-reconciled",
            version: "reconciled",
            expectedRevision: abandoned.Revision,
            previousCid: s_cid,
            reason: "Confirm remaining withdrawal.",
            cancellationToken: token));
        Assert.Equal(LabelPolicyPhase.Withdrawing, current.Phase);
        Assert.Equal(["spam"], current.Withdrawal!.Values);
        int additional = 0;
        int batches = 0;
        while (current.Phase == LabelPolicyPhase.Withdrawing)
        {
            LabelPolicyWithdrawalBatch batch = Success(await withdrawals.ReadPolicyWithdrawalBatchAsync(token));
            additional += batch.Labels.Count;
            current = Success(await withdrawals.CommitPolicyWithdrawalBatchAsync(
                batch: batch,
                negations: Sign(batch, key),
                signingPublicKey: key.ExportPublicKeyMultibase(),
                cancellationToken: token));
            await fixture.Store.PublishAsync(limit: 250, cancellationToken: token);
            Assert.True(++batches < 10);
        }
        Assert.Equal(1, additional);
        Assert.Equal(502, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        Assert.Equal(LabelPolicyPhase.Publishing, current.Phase);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task MissingDistributionMetadataStopsCleanupUntilStorageInitializationRepairsIt(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        ILabelPolicyStore policies = (ILabelPolicyStore)fixture.Store;
        LabelPolicyState active = await LabelPolicyStoreTests.ActivateAsync(policies, caller, token);
        await fixture.Store.AppendAsync("original", [new(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: "did:plc:subject",
            value: "spam",
            creationTimestamp: fixture.DatabaseClock!.GetUtcNow()))], cancellationToken: token);
        LabelPolicyState started = await BeginAsync(
            policies: policies,
            caller: caller,
            active: active,
            token: token);
        await fixture.ExecuteAsync("DELETE FROM label_distribution WHERE seq=1");
        ILabelPolicyWithdrawalStore withdrawals = (ILabelPolicyWithdrawalStore)fixture.Store;
        Assert.Equal(LabelServiceErrorCode.StorageUnavailable, (await withdrawals.ReadPolicyWithdrawalBatchAsync(token)).Error!.Code);
        LabelPolicyState blocked = (await policies.ReadPolicyStateAsync(token))!;
        Assert.Equal(LabelPolicyPhase.Withdrawing, blocked.Phase);
        Assert.Equal(started.Revision, blocked.Revision);
        Assert.Equal(0, blocked.Withdrawal!.ScannedThrough);
        Assert.Equal(1, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
        await fixture.Store.InitializeAsync(StoreFixture.s_issuer, cancellationToken: token);
        Success(await withdrawals.CommitPolicyWithdrawalBatchAsync(
            batch: Success(await withdrawals.ReadPolicyWithdrawalBatchAsync(token)),
            negations: [],
            signingPublicKey: null,
            cancellationToken: token));
        Assert.Single(Success(await withdrawals.ReadPolicyWithdrawalBatchAsync(token)).Labels);
    }

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task CleanupSkipsRetainedValuesAndHandlesRemovedValuesWithNoLiveApplications(string engine, bool remove)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        ILabelPolicyStore policies = (ILabelPolicyStore)fixture.Store;
        LabelPolicyState active = await LabelPolicyStoreTests.ActivateAsync(policies, caller, token);
        await fixture.Store.AppendAsync("retained", [new(key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: "did:plc:subject",
            value: "scam",
            creationTimestamp: fixture.DatabaseClock!.GetUtcNow()))], cancellationToken: token);
        await fixture.Store.PublishAsync(cancellationToken: token);
        using JsonDocument declaration = JsonDocument.Parse(remove ? ReducedDeclaration : active.Policy!.DeclarationJson);
        Success(await policies.SavePolicyAsync(
            caller: caller,
            operationId: "draft-next",
            policy: new(
                version: "next",
                declaration: declaration.RootElement,
                allowAccounts: true,
                allowRecords: true,
                rules: "Retain scam applications."),
            reason: "Keep applied scam.",
            cancellationToken: token));
        LabelPolicyState selected = Success(await policies.BeginPolicyPublicationAsync(
            caller: caller,
            operationId: "publish-next",
            version: "next",
            expectedRevision: active.Revision,
            previousCid: s_cid,
            reason: "Publish selected values.",
            cancellationToken: token));
        Assert.Equal(remove ? LabelPolicyPhase.Withdrawing : LabelPolicyPhase.Publishing, selected.Phase);
        if (remove)
        {
            ILabelPolicyWithdrawalStore withdrawals = (ILabelPolicyWithdrawalStore)fixture.Store;
            for (int pass = 0; pass < 2; pass++)
            {
                LabelPolicyWithdrawalBatch batch = Success(await withdrawals.ReadPolicyWithdrawalBatchAsync(token));
                Assert.Empty(batch.Labels);
                selected = Success(await withdrawals.CommitPolicyWithdrawalBatchAsync(
                    batch: batch,
                    negations: [],
                    signingPublicKey: null,
                    cancellationToken: token));
            }
        }
        Assert.Equal(LabelPolicyPhase.Publishing, selected.Phase);
        Assert.Equal(1, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
    }

    /// <summary>Saves and selects a policy that retains scam but withdraws spam without changing account/record permissions.</summary>
    private static async Task<LabelPolicyState> BeginAsync(
        ILabelPolicyStore policies,
        LabelAdministrationCaller caller,
        LabelPolicyState active,
        CancellationToken token)
    {
        using JsonDocument declaration = JsonDocument.Parse(ReducedDeclaration);
        LabelPolicyDefinition reduced = new(
            version: "v2",
            declaration: declaration.RootElement,
            allowAccounts: true,
            allowRecords: true,
            rules: "Retire spam.");
        Success(await policies.SavePolicyAsync(
            caller: caller,
            operationId: "draft-reduced",
            policy: reduced,
            reason: "Retire spam.",
            cancellationToken: token));
        return Success(await policies.BeginPolicyPublicationAsync(
            caller: caller,
            operationId: "publish-reduced",
            version: "v2",
            expectedRevision: active.Revision,
            previousCid: s_cid,
            reason: "Authorize withdrawal and publication.",
            cancellationToken: token));
    }

    /// <summary>Signs only exact candidate slots using the provider's frozen superseding timestamp.</summary>
    private static SignedLabelSnapshot[] Sign(LabelPolicyWithdrawalBatch batch, LabelSigningKey key) =>
        [.. batch.Labels.Select((entry, index) => new SignedLabelSnapshot(key.CreateNegation(
            source: StoreFixture.s_issuer,
            uri: entry.SignedLabel.Label.Uri,
            value: entry.SignedLabel.Label.Value,
            creationTimestamp: batch.CreatedAt.AddTicks(index),
            cid: entry.SignedLabel.Label.Cid)))];

    /// <summary>Requires a classified provider success before inspecting its payload.</summary>
    private static T Success<T>(LabelServiceResult<T> result) where T : class
    {
        Assert.True(result.Succeeded, result.Error?.Message);
        return result.Result!;
    }
}
