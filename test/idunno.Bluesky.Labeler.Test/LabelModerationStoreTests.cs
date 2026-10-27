// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;
using idunno.Bluesky.Labeler.Storage;

namespace idunno.Bluesky.Labeler.Test;

public class LabelModerationStoreTests
{
    private static readonly Did s_administrator = new("did:plc:administrator");
    private static readonly Did s_member = new("did:plc:moderator");
    private static readonly LabelReportSubject s_subject = LabelReportSubject.Account(new("did:plc:subject"));

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task RegistrationUsesTheSameProviderAndCasesSurviveRecreation(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        ILabelModerationStore store = fixture.Services.GetRequiredService<ILabelModerationStore>();
        Assert.Same(fixture.Store, store);
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        LabelCaseReceipt created = Success(await store.ChangeCaseAsync(administrator, Open("open"), token));
        ILabelModerationStore restarted = (ILabelModerationStore)fixture.NewStore();
        LabelModerationCase snapshot = Success(await restarted.GetCaseAsync(administrator, created.Case.Id, token));
        Assert.Equal(1, snapshot.Revision);
        Assert.Equal(s_subject.Subject, snapshot.Subject.Subject);
        Assert.Equal(LabelCaseStatus.Open, snapshot.Status);
        Assert.Equal(s_administrator, snapshot.Assignee);
        Assert.Equal(created.CreatedAt, snapshot.CreatedAt);
        LabelCaseReceipt recovered = Success(await restarted.GetCaseOperationAsync(administrator, "open", token));
        Assert.Equal(created.Sequence, recovered.Sequence);
        Assert.Equal(created.Case.Id, recovered.Case.Id);
        Assert.Equal(s_administrator, recovered.Actor);
        Assert.Equal("uid:0", recovered.LocalIdentity);
        Assert.Equal("Review this subject.", recovered.Reason);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ConcurrentOpenOperationsShareOneUnresolvedExactSubject(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        ILabelModerationStore first = (ILabelModerationStore)fixture.Store;
        ILabelModerationStore second = (ILabelModerationStore)fixture.NewStore();
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<LabelCaseReceipt> OpenAsync(ILabelModerationStore store, string operationId)
        {
            await start.Task.WaitAsync(token);
            return Success(await store.ChangeCaseAsync(administrator, Open(operationId), token));
        }

        Task<LabelCaseReceipt> one = OpenAsync(first, "one");
        Task<LabelCaseReceipt> two = OpenAsync(second, "two");
        start.SetResult();
        LabelCaseReceipt[] results = await Task.WhenAll(one, two);
        Assert.Equal(results[0].Case.Id, results[1].Case.Id);
        Assert.Equal([1L, 2L], results.Select(result => result.Case.Revision).Order().ToArray());
        Assert.Single(Success(await first.GetCasesAsync(administrator, cancellationToken: token)));
        Assert.Equal(2, Success(await first.GetCaseHistoryAsync(administrator, results[0].Case.Id, cancellationToken: token)).Count);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task RecordVersionsAccountsAndProfileRecordsDoNotShareCases(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        ILabelModerationStore store = (ILabelModerationStore)fixture.Store;
        Cid firstCid = new("bafyreihdwdcefgh4dqkjv67uzcmw7ojee6xedzdetojuzjevtenxquvyku");
        Cid secondCid = new("bafyreif2pall7yry6b3sh5rttgeczzmhnnemy6rtbwwsvxeceebzxtpfwu");
        AtUri uri = new($"at://{s_subject.Repository}/app.bsky.actor.profile/self");
        LabelReportSubject[] subjects =
        [
            s_subject,
            LabelReportSubject.Record(uri, firstCid),
            LabelReportSubject.Record(uri, secondCid)
        ];
        List<long> ids = [];
        for (int index = 0; index < subjects.Length; index++)
        {
            ids.Add(Success(await store.ChangeCaseAsync(
                administrator,
                new(
                    operationId: "subject-" + index,
                    subject: subjects[index],
                    action: LabelCaseAction.Open,
                    reason: "Distinct subject."),
                token)).Case.Id);
        }

        Assert.Equal(3, ids.Distinct().Count());
        IReadOnlyList<LabelModerationCase> cases = Success(await store.GetCasesAsync(administrator, cancellationToken: token));
        Assert.Equal(3, cases.Count);
        Assert.Null(cases[0].Subject.Cid);
        Assert.Equal(firstCid, cases[1].Subject.Cid);
        Assert.Equal(secondCid, cases[2].Subject.Cid);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task RevisionsTransitionsAndReopeningProtectConcurrentReview(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        ILabelModerationStore store = (ILabelModerationStore)fixture.Store;
        LabelModerationCase current = Success(await store.ChangeCaseAsync(administrator, Open("open"), token)).Case;
        LabelModerationCase review = Success(await store.ChangeCaseAsync(
            administrator, Change(current, "review", LabelCaseAction.StartReview), token)).Case;
        Assert.Equal(LabelCaseStatus.InReview, review.Status);
        Assert.Equal(LabelServiceErrorCode.CaseConflict, (await store.ChangeCaseAsync(
            administrator, Change(current, "stale", LabelCaseAction.Resolve), token)).Error!.Code);
        Assert.Equal(LabelServiceErrorCode.CaseConflict, (await store.ChangeCaseAsync(
            administrator, Change(review, "review-again", LabelCaseAction.StartReview), token)).Error!.Code);
        LabelModerationCase resolved = Success(await store.ChangeCaseAsync(
            administrator, Change(review, "resolve", LabelCaseAction.Resolve), token)).Case;
        Assert.Equal(LabelCaseStatus.Resolved, resolved.Status);
        LabelModerationCase reopened = Success(await store.ChangeCaseAsync(
            administrator, Change(resolved, "reopen", LabelCaseAction.Reopen), token)).Case;
        Assert.Equal(LabelCaseStatus.Open, reopened.Status);
        LabelModerationCase closedAgain = Success(await store.ChangeCaseAsync(
            administrator, Change(reopened, "close-again", LabelCaseAction.Resolve), token)).Case;
        LabelModerationCase next = Success(await store.ChangeCaseAsync(administrator, Open("new-review"), token)).Case;
        Assert.NotEqual(closedAgain.Id, next.Id);
        Assert.Equal(LabelServiceErrorCode.CaseConflict, (await store.ChangeCaseAsync(
            administrator, Change(closedAgain, "conflicting-reopen", LabelCaseAction.Reopen), token)).Error!.Code);
        Assert.Equal(0, (await fixture.Store.GetStateAsync(token)).IssuedThrough);
    }

    [Theory]
    [InlineData("sqlite", LabelTeamRole.Viewer, false)]
    [InlineData("sqlite", LabelTeamRole.Moderator, true)]
    [InlineData("postgresql", LabelTeamRole.Viewer, false)]
    [InlineData("postgresql", LabelTeamRole.Moderator, true)]
    [InlineData("mysql", LabelTeamRole.Viewer, false)]
    [InlineData("mysql", LabelTeamRole.Administrator, true)]
    public async Task ReadsMutationsAndRecoveryEnforceLiveRoles(string engine, LabelTeamRole role, bool mayModerate)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        ILabelModerationStore store = (ILabelModerationStore)fixture.Store;
        LabelModerationCase created = Success(await store.ChangeCaseAsync(administrator, Open("open"), token)).Case;
        LabelAdministrationCaller member = await EnrollAsync(team, administrator, role, token);
        Assert.True((await store.GetCaseAsync(member, created.Id, token)).Succeeded);
        Assert.True((await store.GetCaseOperationAsync(member, "open", token)).Succeeded);
        Assert.Equal(mayModerate, (await store.ChangeCaseAsync(
            member, Change(created, "member-note", LabelCaseAction.Note), token)).Succeeded);
        Success(await team.ChangeTeamAsync(
            administrator,
            new(
                operationId: "suspend", action: LabelTeamAction.Suspend, target: s_member,
                reason: "Revoke moderation authority."),
            token));
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await store.GetCaseAsync(member, created.Id, token)).Error!.Code);
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await store.GetCaseOperationAsync(member, "open", token)).Error!.Code);
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await store.GetCasesAsync(member, cancellationToken: token)).Error!.Code);
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await store.GetCaseHistoryAsync(member, created.Id, cancellationToken: token)).Error!.Code);
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await store.ChangeCaseAsync(member, Open("revoked"), token)).Error!.Code);
        Assert.Contains(
            Success(await team.GetAdministrationAuditAsync(administrator, cancellationToken: token)),
            receipt => receipt.Action == "moderation-case" && receipt.Actor == s_member && !receipt.Succeeded);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task AssignmentRequiresLiveEligibleMembersButRetainsHistoricalProvenance(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        ILabelModerationStore store = (ILabelModerationStore)fixture.Store;
        await EnrollAsync(team, administrator, LabelTeamRole.Viewer, token);
        LabelModerationCase created = Success(await store.ChangeCaseAsync(administrator, Open("open"), token)).Case;
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await store.ChangeCaseAsync(
            administrator, Change(created, "assign-viewer", LabelCaseAction.Assign, s_member), token)).Error!.Code);
        Success(await team.ChangeTeamAsync(
            administrator,
            new(
                operationId: "promote", action: LabelTeamAction.SetRole, target: s_member,
                reason: "Allow review.", role: LabelTeamRole.Moderator),
            token));
        LabelModerationCase assigned = Success(await store.ChangeCaseAsync(
            administrator, Change(created, "assign", LabelCaseAction.Assign, s_member), token)).Case;
        Assert.Equal(s_member, assigned.Assignee);
        Success(await team.ChangeTeamAsync(
            administrator,
            new(
                operationId: "suspend", action: LabelTeamAction.Suspend, target: s_member,
                reason: "Revoke assignment eligibility."),
            token));
        LabelModerationCase automatic = Success(await store.GetCaseAsync(administrator, assigned.Id, token));
        Assert.Equal(s_administrator, automatic.Assignee);
        Assert.Equal(s_member, Success(await store.GetCaseOperationAsync(administrator, "assign", token)).Case.Assignee);
        Assert.Equal(LabelServiceErrorCode.CaseConflict, (await store.ChangeCaseAsync(
            administrator, Change(assigned, "stale-assignment", LabelCaseAction.Assign), token)).Error!.Code);
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await store.ChangeCaseAsync(
            administrator, Change(automatic, "assign-revoked", LabelCaseAction.Assign, s_member), token)).Error!.Code);
        LabelModerationCase released = Success(await store.ChangeCaseAsync(
            administrator, Change(automatic, "release", LabelCaseAction.Assign), token)).Case;
        Assert.Equal(s_administrator, released.Assignee);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task SoleModeratorAssignmentReconcilesBatchesAtomicallyAndPreservesResolvedCasesAndReceipts(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        ILabelModerationStore store = (ILabelModerationStore)fixture.Store;
        await EnrollAsync(team, administrator, LabelTeamRole.Moderator, token);
        List<LabelCaseReceipt> opened = [];
        for (int index = 0; index < 130; index++)
        {
            string suffix = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            opened.Add(Success(await store.ChangeCaseAsync(
                administrator,
                new(
                    operationId: "open-" + suffix,
                    subject: LabelReportSubject.Account(new("did:plc:subject" + suffix)),
                    action: LabelCaseAction.Open,
                    reason: "Exercise automatic assignment batches."),
                token)));
        }

        Assert.All(opened, receipt => Assert.Null(receipt.Case.Assignee));
        LabelModerationCase resolved = Success(await store.ChangeCaseAsync(
            administrator, Change(opened[0].Case, "resolve-one", LabelCaseAction.Resolve), token)).Case;
        LabelTeamChange suspend = new(
            operationId: "suspend-auto",
            action: LabelTeamAction.Suspend,
            target: s_member,
            reason: "Leave one active moderator.");
        fixture.SetCommitHooks(beforeCommit: (_, _, _) => throw new SqliteException("Injected rollback.", 10));
        Assert.Equal(LabelServiceErrorCode.StorageUnavailable, (await team.ChangeTeamAsync(administrator, suspend, token)).Error!.Code);
        fixture.SetCommitHooks();
        Assert.All(Success(await store.GetCasesAsync(administrator, cancellationToken: token)), item => Assert.Null(item.Assignee));
        Success(await team.ChangeTeamAsync(administrator, suspend, token));
        ILabelModerationStore restarted = (ILabelModerationStore)fixture.NewStore();
        LabelModerationCase first = Success(await restarted.GetCaseAsync(administrator, opened[1].Case.Id, token));
        LabelModerationCase last = Success(await restarted.GetCaseAsync(administrator, opened[^1].Case.Id, token));
        Assert.Equal(s_administrator, first.Assignee);
        Assert.Equal(s_administrator, last.Assignee);
        Assert.Equal(2, first.Revision);
        Assert.Equal(resolved.Revision, Success(await restarted.GetCaseAsync(administrator, resolved.Id, token)).Revision);
        Assert.Null(Success(await restarted.GetCaseAsync(administrator, resolved.Id, token)).Assignee);
        Assert.Null(Success(await restarted.GetCaseOperationAsync(administrator, "open-1", token)).Case.Assignee);
        LabelCaseReceipt automatic = Success(await restarted.GetCaseHistoryAsync(administrator, first.Id, cancellationToken: token))[^1];
        Assert.Equal(LabelCaseAction.Assign, automatic.Action);
        Assert.Contains("Automatically assigned", automatic.Reason, StringComparison.Ordinal);
        Success(await team.ChangeTeamAsync(administrator, suspend, token));
        Assert.Equal(2, Success(await restarted.GetCaseHistoryAsync(administrator, first.Id, cancellationToken: token)).Count);

        Success(await team.ChangeTeamAsync(
            administrator,
            new(operationId: "reinstate", action: LabelTeamAction.Reinstate, target: s_member, reason: "Restore manual routing."),
            token));
        LabelModerationCase manual = Success(await restarted.ChangeCaseAsync(
            administrator, Change(first, "unassign", LabelCaseAction.Assign), token)).Case;
        Assert.Null(manual.Assignee);
        Assert.Equal(s_administrator, Success(await restarted.GetCaseAsync(administrator, last.Id, token)).Assignee);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task RetryReturnsOriginalSnapshotAndChangedIntentConflicts(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        ILabelModerationStore store = (ILabelModerationStore)fixture.Store;
        LabelCaseReceipt original = Success(await store.ChangeCaseAsync(administrator, Open("open"), token));
        Success(await store.ChangeCaseAsync(administrator, Change(original.Case, "note", LabelCaseAction.Note), token));
        LabelCaseReceipt retry = Success(await store.ChangeCaseAsync(administrator, Open("open"), token));
        Assert.Equal(original.Sequence, retry.Sequence);
        Assert.Equal(1, retry.Case.Revision);
        Assert.Equal(2, Success(await store.GetCaseAsync(administrator, original.Case.Id, token)).Revision);
        LabelCaseChange different = new(
            operationId: "open", subject: s_subject, action: LabelCaseAction.Open, reason: "Different intent.");
        Assert.Equal(LabelServiceErrorCode.IdempotencyConflict, (await store.ChangeCaseAsync(administrator, different, token)).Error!.Code);
        Assert.Equal(2, Success(await store.GetCaseHistoryAsync(administrator, original.Case.Id, cancellationToken: token)).Count);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task CaseAuditAndReceiptsRollbackTogetherAndUnknownAcknowledgementsRecover(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        ILabelModerationStore store = (ILabelModerationStore)fixture.Store;
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        int auditCount = Success(await team.GetAdministrationAuditAsync(administrator, cancellationToken: token)).Count;
        fixture.SetCommitHooks(beforeCommit: (_, _, _) => throw new SqliteException("Injected precommit failure.", 10));
        LabelServiceResult<LabelCaseReceipt> failed = await store.ChangeCaseAsync(administrator, Open("failed"), token);
        fixture.SetCommitHooks();
        Assert.Equal(LabelServiceErrorCode.StorageUnavailable, failed.Error!.Code);
        Assert.Empty(Success(await store.GetCasesAsync(administrator, cancellationToken: token)));
        Assert.Equal(auditCount, Success(await team.GetAdministrationAuditAsync(administrator, cancellationToken: token)).Count);
        fixture.SetCommitHooks(afterCommit: (_, _) => throw new SqliteException("Injected lost acknowledgement.", 10));
        LabelServiceResult<LabelCaseReceipt> uncertain = await store.ChangeCaseAsync(administrator, Open("uncertain"), token);
        fixture.SetCommitHooks();
        Assert.Equal(LabelServiceErrorCode.OutcomeUnknown, uncertain.Error!.Code);
        LabelCaseReceipt recovered = Success(await store.GetCaseOperationAsync(administrator, "uncertain", token));
        Assert.Equal(recovered.Sequence, Success(await store.ChangeCaseAsync(administrator, Open("uncertain"), token)).Sequence);
        Assert.Single(Success(await store.GetCasesAsync(administrator, cancellationToken: token)));
        Assert.Single(Success(await store.GetCaseHistoryAsync(administrator, recovered.Case.Id, cancellationToken: token)));
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task CaseAndHistoryPagesAreBoundedAndSuccessfulReadsDoNotExtendAudit(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        ILabelModerationStore store = (ILabelModerationStore)fixture.Store;
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        LabelModerationCase created = Success(await store.ChangeCaseAsync(administrator, Open("open"), token)).Case;
        LabelCaseReceipt note = Success(await store.ChangeCaseAsync(administrator, Change(created, "note", LabelCaseAction.Note), token));
        int count = Success(await team.GetAdministrationAuditAsync(administrator, cancellationToken: token)).Count;
        Assert.Single(Success(await store.GetCasesAsync(
            caller: administrator, status: LabelCaseStatus.Open, afterId: 0, limit: 1, cancellationToken: token)));
        Assert.Empty(Success(await store.GetCasesAsync(
            caller: administrator, status: LabelCaseStatus.Resolved, afterId: 0, limit: 1, cancellationToken: token)));
        Assert.Empty(Success(await store.GetCasesAsync(
            caller: administrator, status: null, afterId: created.Id, limit: 1, cancellationToken: token)));
        LabelCaseReceipt first = Assert.Single(Success(await store.GetCaseHistoryAsync(
            caller: administrator, caseId: created.Id, afterSequence: 0, limit: 1, cancellationToken: token)));
        LabelCaseReceipt second = Assert.Single(Success(await store.GetCaseHistoryAsync(
            caller: administrator, caseId: created.Id, afterSequence: first.Sequence, limit: 1, cancellationToken: token)));
        Assert.Equal(note.Sequence, second.Sequence);
        Assert.Empty(Success(await store.GetCaseHistoryAsync(
            caller: administrator, caseId: created.Id, afterSequence: second.Sequence, limit: 1, cancellationToken: token)));
        Assert.Equal(count, Success(await team.GetAdministrationAuditAsync(administrator, cancellationToken: token)).Count);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task SchemaEightUpgradePreservesIssuerTeamAndStreamState(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller administrator = await BootstrapAsync(fixture);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot original = new(key.CreateLabel(
            source: StoreFixture.Issuer,
            uri: s_subject.Subject,
            value: "spam",
            creationTimestamp: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        await fixture.Store.AppendAsync("before-upgrade", [original], cancellationToken: token);
        LabelStoreState before = await fixture.Store.GetStateAsync(token);
        await fixture.ExecuteAsync("""
            DROP TABLE label_post_review_checks;
            DROP TABLE label_search_handles;
            DROP TABLE label_search_text;
            DROP TABLE label_report_tokens;
            DROP TABLE label_appeal_reports;
            DROP TABLE label_appeals;
            DROP TABLE label_reports;
            DROP TABLE label_case_decisions;
            DROP TABLE label_subject_index;
            DROP TABLE label_case_events;
            DROP TABLE label_cases;
            DROP TABLE label_policy_events;
            DROP TABLE label_policy_history;
            DROP TABLE label_policy_versions;
            DROP TABLE label_policy_gate;
            UPDATE label_schema SET version=8 WHERE id=1
            """);
        ILabelStore restarted = fixture.NewStore();
        await restarted.InitializeAsync(StoreFixture.Issuer, token);
        Assert.Equal(before, await restarted.GetStateAsync(token));
        ILabelModerationStore store = (ILabelModerationStore)restarted;
        LabelCaseReceipt created = Success(await store.ChangeCaseAsync(administrator, Open("after-upgrade"), token));
        Assert.Equal(1, created.Case.Id);
        Assert.Equal(s_administrator, created.Actor);
        LabelPolicyState policy = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)restarted, administrator, token);
        ModerationReportAuthenticationResult owner = await ReportTestAuthentication.CreateAsync(s_subject.Repository, policy.DatabaseTime, "appeal");
        Success(await ((ILabelReportStore)restarted).SubmitReportAsync(
            owner, new(s_subject, "com.atproto.moderation.defs#reasonAppeal"), policy.Revision, token));
        LabelAppeal appeal = Assert.Single(Success(await ((ILabelAppealStore)restarted).GetAppealsAsync(administrator, cancellationToken: token)));
        Assert.Equal(original.GetJsonBytes(), Assert.Single(appeal.Challenges).SignedLabel.GetJsonBytes());
        Assert.Equal(before, await restarted.GetStateAsync(token));
    }

    /// <summary>Requires an explicit successful result so failures retain their diagnostic message.</summary>
    private static T Success<T>(LabelServiceResult<T> result) where T : class
    {
        Assert.True(result.Succeeded, result.Error?.Message);
        return result.Result!;
    }

    /// <summary>Bootstraps the initial OS binding through the shared provider boundary.</summary>
    private static async Task<LabelAdministrationCaller> BootstrapAsync(StoreFixture fixture)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        Success(await team.BootstrapAsync(s_administrator, "uid:0", "bootstrap", token));
        return Success(await team.AuthenticateLocalAsync("uid:0", token));
    }

    /// <summary>Creates independently authenticated member evidence for role and revocation coverage.</summary>
    private static async Task<LabelAdministrationCaller> EnrollAsync(
        ILabelAdministrationStore team,
        LabelAdministrationCaller administrator,
        LabelTeamRole role,
        CancellationToken token)
    {
        Success(await team.ChangeTeamAsync(
            administrator,
            new(
                operationId: "enroll", action: LabelTeamAction.Enroll, target: s_member,
                reason: "Enroll test member.", role: role),
            token));
        Success(await team.ChangeTeamAsync(
            administrator,
            new(
                operationId: "bind", action: LabelTeamAction.BindLocal, target: s_member,
                reason: "Bind test identity.", credential: "uid:1"),
            token));
        return Success(await team.AuthenticateLocalAsync("uid:1", token));
    }

    /// <summary>Creates stable exact-subject open intent.</summary>
    private static LabelCaseChange Open(string operationId) => new(
        operationId: operationId, subject: s_subject, action: LabelCaseAction.Open, reason: "Review this subject.");

    /// <summary>Uses a returned snapshot's exact subject and revision for optimistic edits.</summary>
    private static LabelCaseChange Change(
        LabelModerationCase snapshot,
        string operationId,
        LabelCaseAction action,
        Did? assignee = null) => new(
            operationId: operationId,
            subject: snapshot.Subject,
            action: action,
            reason: "Review this subject.",
            caseId: snapshot.Id,
            expectedRevision: snapshot.Revision,
            assignee: assignee);
}
