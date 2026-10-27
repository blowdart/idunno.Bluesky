// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.Bluesky.Labeler.Test;

public class LabelDeletedPostTests
{
    private static readonly Did s_owner = new(TargetResolutionHandler.SubjectDid);
    private static readonly AtUri s_uri = new($"at://{s_owner}/app.bsky.feed.post/deleted");
    private static readonly LabelReportSubject s_post = LabelReportSubject.Record(s_uri, new(TargetResolutionHandler.RecordCid));

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task AuthoritativeDeletionClosesAllReportsAndAppealsButPreservesEvidenceAndSignedLabels(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState policy = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        SignedLabelSnapshot original = new(key.CreateLabel(
            source: StoreFixture.Issuer, uri: s_uri.ToString(), value: "spam",
            creationTimestamp: policy.DatabaseTime.AddHours(-1), cid: s_post.Cid));
        await fixture.Store.AppendAsync(operationId: "manual", labels: [original], cancellationToken: token);
        List<LabelModerationReport> accepted = [];
        for (int index = 0; index < 130; index++)
        {
            accepted.Add(await SubmitAsync(fixture, policy, s_post, "ordinary-" + index));
        }

        accepted.Add(await SubmitAsync(fixture, policy, s_post, "appeal", appeal: true));
        LabelModerationReport otherCid = await SubmitAsync(
            fixture, policy, LabelReportSubject.Record(s_uri, new("bafyreif2pall7yry6b3sh5rttgeczzmhnnemy6rtbwwsvxeceebzxtpfwu")), "other-cid");
        LabelModerationReport account = await SubmitAsync(fixture, policy, LabelReportSubject.Account(s_owner), "account");
        LabelModerationReport profile = await SubmitAsync(
            fixture, policy, LabelReportSubject.Record(new($"at://{s_owner}/app.bsky.actor.profile/self"), s_post.Cid!), "profile");
        LabelStoreState before = await fixture.Store.GetStateAsync(token);
        ILabelDeletedPostStore store = (ILabelDeletedPostStore)fixture.Store;
        LabelDeletedPostBatch batch = Success(await store.ReadDeletedPostBatchAsync(token));
        Assert.Equal(2, batch.Entries.Count);
        TargetResolutionHandler handler = new() { RecordNotFound = true };
        using HttpClient http = new(handler);
        DeletedPostReviewService worker = new(
            store: fixture.Store, httpClient: http, loggerFactory: NullLoggerFactory.Instance, clock: fixture.DatabaseClock!);
        Success(await worker.RunBatchAsync(token));
        ILabelStore restarted = fixture.NewStore();
        foreach (LabelModerationReport submitted in accepted.Append(otherCid))
        {
            LabelModerationReport resolved = Success(await ((ILabelReportStore)restarted).GetReportAsync(caller, submitted.Id, token));
            Assert.Equal("Post deleted", resolved.ResolutionReason);
            Assert.NotNull(resolved.HandledAt);
            Assert.StartsWith("post-deleted/", resolved.DecisionOperationId, StringComparison.Ordinal);
            Assert.Equal(submitted.Submission.Reason, resolved.Submission.Reason);
            Assert.Equal(submitted.Submission.ReasonType, resolved.Submission.ReasonType);
            Assert.Equal(submitted.ReportedBy, resolved.ReportedBy);
            Assert.Equal(submitted.CreatedAt, resolved.CreatedAt);
        }

        LabelAppeal appeal = Assert.Single(Success(await ((ILabelAppealStore)restarted).GetAppealsAsync(caller, cancellationToken: token)));
        Assert.Equal(LabelAppealStatus.Superseded, appeal.Status);
        Assert.Equal("Post deleted", appeal.ResolutionReason);
        Assert.Equal(original.GetJsonBytes(), Assert.Single(appeal.Challenges).SignedLabel.GetJsonBytes());
        Assert.Equal(before, await restarted.GetStateAsync(token));
        foreach (LabelModerationReport untouched in new[] { account, profile })
        {
            Assert.Null(Success(await ((ILabelReportStore)restarted).GetReportAsync(caller, untouched.Id, token)).DecisionOperationId);
        }

        ILabelModerationStore cases = (ILabelModerationStore)restarted;
        foreach (LabelDeletedPostCandidate candidate in batch.Entries)
        {
            Assert.Equal(LabelCaseStatus.Resolved, Success(await cases.GetCaseAsync(caller, candidate.CaseId, token)).Status);
            LabelCaseReceipt closure = Assert.Single(
                await HistoryAsync(cases, caller, candidate.CaseId, token),
                item => item.Reason == "Post deleted");
            Assert.Equal("system:deleted-post", closure.LocalIdentity);
            Assert.Equal(StoreFixture.Issuer, closure.Actor);
            Assert.True(Success(await ((ILabelDeletedPostStore)restarted).CompleteDeletedPostCheckAsync(
                candidate, batch.ObservedAt, true, token)).Closed);
        }

        Assert.Empty(Success(await ((ILabelDeletedPostStore)restarted).ReadDeletedPostBatchAsync(token)).Entries);
        Assert.Equal(2, Success(await ((ILabelTableStore)restarted).ReadNavigationCountsAsync(caller, token)).Reports);
        Assert.Equal(133, Success(await ((ILabelSearchStore)restarted).SearchAsync(caller, "Post deleted", 1, token)).Total);
    }

    [Theory]
    [InlineData(false, null, null, false)]
    [InlineData(false, HttpStatusCode.NotFound, "NotFound", false)]
    [InlineData(false, HttpStatusCode.ServiceUnavailable, "RecordNotFound", false)]
    [InlineData(false, HttpStatusCode.BadRequest, "RecordNotFound", true)]
    [InlineData(false, HttpStatusCode.NotFound, "RecordNotFound", true)]
    [InlineData(true, null, null, false)]
    public async Task OnlyAnExplicitAuthoritativeAbsenceClosesWorkAndInconclusiveChecksAreThrottled(
        bool offline,
        HttpStatusCode? status,
        string? error,
        bool closes)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync("sqlite");
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState policy = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        LabelModerationReport submitted = await SubmitAsync(fixture, policy, s_post, "exists");
        TargetResolutionHandler handler = new()
        {
            Offline = offline,
            RecordErrorStatus = status,
            RecordError = error ?? "NotFound",
            Cid = "bafyreif2pall7yry6b3sh5rttgeczzmhnnemy6rtbwwsvxeceebzxtpfwu"
        };
        using HttpClient http = new(handler);
        DeletedPostReviewService worker = new(
            store: fixture.Store, httpClient: http, loggerFactory: NullLoggerFactory.Instance, clock: fixture.DatabaseClock!);
        Success(await worker.RunBatchAsync(token));
        Assert.Equal(closes ? "Post deleted" : null,
            Success(await ((ILabelReportStore)fixture.NewStore()).GetReportAsync(caller, submitted.Id, token)).ResolutionReason);
        int requests = handler.Requests;
        Success(await worker.RunBatchAsync(token));
        Assert.Equal(requests, handler.Requests);
        if (!closes)
        {
            fixture.DatabaseClock!.Advance(TimeSpan.FromMinutes(5));
            handler.Offline = false;
            handler.RecordErrorStatus = HttpStatusCode.NotFound;
            handler.RecordError = "RecordNotFound";
            Success(await worker.RunBatchAsync(token));
            Assert.Equal("Post deleted",
                Success(await ((ILabelReportStore)fixture.Store).GetReportAsync(caller, submitted.Id, token)).ResolutionReason);
        }
    }

    [Theory]
    [InlineData("sqlite", false)]
    [InlineData("sqlite", true)]
    [InlineData("postgresql", false)]
    [InlineData("postgresql", true)]
    [InlineData("mysql", false)]
    [InlineData("mysql", true)]
    public async Task NewReportsAndExpiredObservationsCannotBeClosedByAnOlderCheck(string engine, bool expire)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState policy = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        LabelModerationReport original = await SubmitAsync(fixture, policy, s_post, "first");
        ILabelDeletedPostStore store = (ILabelDeletedPostStore)fixture.Store;
        LabelDeletedPostBatch batch = Success(await store.ReadDeletedPostBatchAsync(token));
        if (expire)
        {
            fixture.DatabaseClock!.Advance(TimeSpan.FromMinutes(6));
        }
        else
        {
            await SubmitAsync(fixture, policy, s_post, "concurrent");
        }

        Assert.Equal(LabelServiceErrorCode.CaseConflict,
            (await store.CompleteDeletedPostCheckAsync(Assert.Single(batch.Entries), batch.ObservedAt, true, token)).Error!.Code);
        Assert.Null(Success(await ((ILabelReportStore)fixture.NewStore()).GetReportAsync(caller, original.Id, token)).HandledAt);
        Assert.Equal(LabelCaseStatus.Open, Success(await ((ILabelModerationStore)fixture.Store).GetCaseAsync(caller, original.CaseId, token)).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosureRollsBackBeforeCommitAndRecoversAfterLostAcknowledgement(bool loseAcknowledgement)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync("sqlite");
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState policy = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        LabelModerationReport report = await SubmitAsync(fixture, policy, s_post, "atomic");
        await SubmitAsync(fixture, policy, s_post, "appeal", appeal: true);
        ILabelDeletedPostStore store = (ILabelDeletedPostStore)fixture.Store;
        LabelDeletedPostBatch batch = Success(await store.ReadDeletedPostBatchAsync(token));
        fixture.SetCommitHooks(
            beforeCommit: loseAcknowledgement ? null : (_, _, _) => throw new SqliteException("Closure rollback.", 1),
            afterCommit: loseAcknowledgement ? (_, _) => throw new SqliteException("Closure acknowledgement lost.", 1) : null);
        LabelServiceResult<LabelDeletedPostCheck> failed = await store.CompleteDeletedPostCheckAsync(
            Assert.Single(batch.Entries), batch.ObservedAt, true, token);
        fixture.SetCommitHooks();
        Assert.Equal(loseAcknowledgement ? LabelServiceErrorCode.OutcomeUnknown : LabelServiceErrorCode.StorageUnavailable, failed.Error!.Code);
        LabelModerationReport reread = Success(await ((ILabelReportStore)fixture.NewStore()).GetReportAsync(caller, report.Id, token));
        Assert.Equal(loseAcknowledgement ? "Post deleted" : null, reread.ResolutionReason);
        Assert.True(Success(await ((ILabelDeletedPostStore)fixture.NewStore()).CompleteDeletedPostCheckAsync(
            Assert.Single(batch.Entries), batch.ObservedAt, true, token)).Closed);
        Assert.Single(
            Success(await ((ILabelModerationStore)fixture.Store).GetCaseHistoryAsync(caller, report.CaseId, cancellationToken: token)),
            item => item.Reason == "Post deleted");
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ResolvedCasesWithUnhandledReportsRemainEligibleAlongsideNewerCases(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState policy = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        LabelModerationReport old = await SubmitAsync(fixture, policy, s_post, "old");
        ILabelModerationStore cases = (ILabelModerationStore)fixture.Store;
        LabelModerationCase current = Success(await cases.GetCaseAsync(caller, old.CaseId, token));
        Success(await cases.ChangeCaseAsync(
            caller,
            new(
                operationId: "old-workflow-close",
                subject: s_post,
                action: LabelCaseAction.Resolve,
                reason: "Legacy workflow closure without report handling.",
                caseId: current.Id,
                expectedRevision: current.Revision),
            token));
        LabelModerationReport newer = await SubmitAsync(fixture, policy, s_post, "new");
        Assert.NotEqual(old.CaseId, newer.CaseId);
        using HttpClient http = new(new TargetResolutionHandler { RecordNotFound = true });
        DeletedPostReviewService worker = new(
            store: fixture.Store, httpClient: http, loggerFactory: NullLoggerFactory.Instance, clock: fixture.DatabaseClock!);
        Assert.Equal(2, Success(await worker.RunBatchAsync(token)).Entries.Count);
        foreach (LabelModerationReport report in new[] { old, newer })
        {
            Assert.Equal("Post deleted",
                Success(await ((ILabelReportStore)fixture.NewStore()).GetReportAsync(caller, report.Id, token)).ResolutionReason);
        }
    }

    [Fact]
    public async Task PdsDeadlineLeavesTheReportOpenAndPersistsTheCheckForRetry()
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync("sqlite");
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState policy = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        LabelModerationReport report = await SubmitAsync(fixture, policy, s_post, "timeout");
        using SlowPdsHandler handler = new();
        using HttpClient http = new(handler);
        DeletedPostReviewService worker = new(
            store: fixture.Store, httpClient: http, loggerFactory: NullLoggerFactory.Instance, clock: fixture.DatabaseClock!);
        Task<LabelServiceResult<LabelDeletedPostBatch>> checking = worker.RunBatchAsync(token);
        await handler.Entered.Task.WaitAsync(token);
        fixture.DatabaseClock!.Advance(TimeSpan.FromSeconds(20));
        Success(await checking);
        Assert.Null(Success(await ((ILabelReportStore)fixture.NewStore()).GetReportAsync(caller, report.Id, token)).HandledAt);
        Assert.Empty(Success(await ((ILabelDeletedPostStore)fixture.NewStore()).ReadDeletedPostBatchAsync(token)).Entries);
    }

    /// <summary>Signals the exact network phase after the production deadline is registered, then waits for cancellation.</summary>
    private sealed class SlowPdsHandler() : DelegatingHandler(new TargetResolutionHandler())
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Leaves identity resolution deterministic while holding only the authoritative record request.</summary>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.Host == "pds.example")
            {
                Entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }

    /// <summary>Submits independently authenticated report evidence, including genuine subject-authored appeal text.</summary>
    private static async Task<LabelModerationReport> SubmitAsync(
        StoreFixture fixture,
        LabelPolicyState policy,
        LabelReportSubject subject,
        string identity,
        bool appeal = false)
    {
        ModerationReportAuthenticationResult authentication = await ReportTestAuthentication.CreateAsync(s_owner, policy.DatabaseTime, identity);
        return Success(await ((ILabelReportStore)fixture.Store).SubmitReportAsync(
            authentication: authentication,
            submission: new(
                subject: subject,
                reasonType: appeal ? "com.atproto.moderation.defs#reasonAppeal" : "tools.ozone.report.defs#reasonMisleadingSpam",
                reason: "Original evidence: " + identity),
            expectedPolicyRevision: policy.Revision,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Reads through the final bounded history page so large report groups cannot hide the closure event.</summary>
    private static async Task<IReadOnlyList<LabelCaseReceipt>> HistoryAsync(
        ILabelModerationStore store,
        LabelAdministrationCaller caller,
        long caseId,
        CancellationToken token)
    {
        List<LabelCaseReceipt> history = [];
        while (true)
        {
            IReadOnlyList<LabelCaseReceipt> page = Success(await store.GetCaseHistoryAsync(
                caller: caller, caseId: caseId, afterSequence: history.Count == 0 ? 0 : history[^1].Sequence, cancellationToken: token));
            history.AddRange(page);
            if (page.Count < 100)
            {
                return history;
            }
        }
    }

    /// <summary>Includes provider diagnostics when a required operation does not succeed.</summary>
    private static T Success<T>(LabelServiceResult<T> result) where T : class
    {
        Assert.True(result.Succeeded, result.Error?.Message);
        return result.Result;
    }
}
