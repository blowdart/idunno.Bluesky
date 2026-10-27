// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;
using idunno.Bluesky.Labeler.Storage;

namespace idunno.Bluesky.Labeler.Test;

public class LabelSearchTests
{
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task SearchCombinesPartialHandlesOriginalAppealMessagesAndGloballyPagedLiteralText(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState policy = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        LabelReportSubject first = LabelReportSubject.Account(new("did:plc:first-subject"));
        LabelReportSubject second = LabelReportSubject.Account(new("did:plc:second-subject"));
        await SubmitAsync(fixture, policy, first, "first", "CAF\u00c9 100%_! evidence");
        await SubmitAsync(fixture, policy, second, "second", "Different original evidence.");
        await SubmitAsync(fixture, policy, first, "appeal", "My caf\u00e9-message contests this label.", appeal: true);
        for (int index = 0; index < 18; index++)
        {
            await SubmitAsync(fixture, policy, LabelReportSubject.Account(new("did:plc:paged")), "paged-" + index, "Page-match");
        }

        Success(await ((ILabelModerationStore)fixture.Store).ChangeCaseAsync(
            caller,
            new(
                operationId: "internal-appeal",
                subject: LabelReportSubject.Account(new("did:plc:only-internal")),
                action: LabelCaseAction.OpenAppeal,
                reason: "Independent review without an external report."),
            token));
        ILabelSearchStore store = (ILabelSearchStore)fixture.Store;
        LabelHandleIndexBatch batch = Success(await store.ReadHandleIndexBatchAsync(token));
        Success(await store.SaveHandleIndexBatchAsync(
            batch with
            {
                Entries = batch.Entries.Select(entry => entry with
                {
                    Handle = entry.Did == first.Repository.Value ? "blowdart.me" :
                        entry.Did == second.Repository.Value ? "ablow.com" : "other.example"
                }).ToArray()
            }, token));
        LabelSearchPage handles = Success(await store.SearchAsync(caller, "bLoW", 1, token));
        Assert.Equal(4, handles.Total);
        Assert.Equal(0, handles.PendingHandleCases);
        Assert.Equal(0, handles.UnavailableHandleCases);
        Assert.Equal(4, Success(await store.SearchAsync(caller, "@blow", 1, token)).Total);
        Assert.Empty(Success(await store.SearchAsync(caller, "@", 1, token)).Hits);
        Assert.Equal(3, Success(await store.SearchAsync(caller, "caf\u00e9", 1, token)).Total);
        foreach (string literal in new[] { "%", "_", "!" })
        {
            Assert.Single(Success(await store.SearchAsync(caller, literal, 1, token)).Hits);
        }

        LabelSearchPage messages = Success(await store.SearchAsync(caller, "CAF\u00c9-MESSAGE", 1, token));
        Assert.Equal(2, messages.Total);
        LabelSearchHit appeal = Assert.Single(messages.Hits, hit => hit.Appeal is not null);
        Assert.Equal("My caf\u00e9-message contests this label.", appeal.MatchText);
        Assert.NotNull(appeal.RelatedReportId);
        Assert.NotNull(Assert.Single(Success(await store.SearchAsync(caller, "only-internal", 1, token)).Hits).Appeal);
        LabelSearchPage pageOne = Success(await store.SearchAsync(caller, "page-match", 1, token));
        LabelSearchPage pageTwo = Success(await store.SearchAsync(caller, "page-match", long.MaxValue, token));
        Assert.Equal(18, pageOne.Total);
        Assert.Equal(16, pageOne.Hits.Count);
        Assert.Equal(2, pageTwo.Hits.Count);
        Assert.Equal(2, pageTwo.Page);
        long[] ids = pageOne.Hits.Concat(pageTwo.Hits).Select(hit => hit.Report!.Id).ToArray();
        Assert.Equal(ids.OrderDescending(), ids);
        Assert.Equal(18, ids.Distinct().Count());
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task HandleObservationsDoNotRegressAndExpiryFailureAndLiveRevocationRemainVisible(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState policy = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        LabelReportSubject subject = LabelReportSubject.Account(new("did:plc:renamed"));
        await SubmitAsync(fixture, policy, subject, "report", "Confidential text.");
        ILabelSearchStore store = (ILabelSearchStore)fixture.Store;
        LabelHandleIndexBatch batch = Success(await store.ReadHandleIndexBatchAsync(token));
        LabelHandleIndexBatch old = batch with { Entries = [Assert.Single(batch.Entries) with { Handle = "blowdart.me" }] };
        Success(await store.SaveHandleIndexBatchAsync(old, token));
        Assert.Equal(1, Success(await store.SearchAsync(caller, "blow", 1, token)).Total);
        fixture.DatabaseClock!.Advance(TimeSpan.FromMinutes(1));
        LabelHandleIndexBatch renamed = old with
        {
            ObservedAt = fixture.DatabaseClock.GetUtcNow().ToUnixTimeMilliseconds(),
            Entries = [old.Entries[0] with { Handle = "renamed.example" }]
        };
        Success(await store.SaveHandleIndexBatchAsync(renamed, token));
        Success(await store.SaveHandleIndexBatchAsync(old, token));
        ILabelSearchStore restarted = (ILabelSearchStore)fixture.NewStore();
        Assert.Equal(0, Success(await restarted.SearchAsync(caller, "blow", 1, token)).Total);
        Assert.Equal(1, Success(await restarted.SearchAsync(caller, "renamed.example", 1, token)).Total);
        fixture.DatabaseClock.Advance(TimeSpan.FromHours(1));
        LabelSearchPage expired = Success(await restarted.SearchAsync(caller, "renamed.example", 1, token));
        Assert.Equal(0, expired.Total);
        Assert.Equal(1, expired.PendingHandleCases);
        LabelHandleIndexBatch unavailable = Success(await restarted.ReadHandleIndexBatchAsync(token));
        Success(await restarted.SaveHandleIndexBatchAsync(unavailable, token));
        LabelSearchPage failed = Success(await restarted.SearchAsync(caller, "renamed.example", 1, token));
        Assert.Equal(0, failed.Total);
        Assert.Equal(0, failed.PendingHandleCases);
        Assert.Equal(1, failed.UnavailableHandleCases);
        Assert.Equal(1, Success(await restarted.SearchAsync(caller, "confidential", 1, token)).Total);
        ILabelAdministrationStore team = (ILabelAdministrationStore)fixture.Store;
        Did viewerDid = new("did:plc:viewer");
        Success(await team.ChangeTeamAsync(
            caller, new(operationId: "enroll-viewer", action: LabelTeamAction.Enroll, target: viewerDid, reason: "Inspect search.", role: LabelTeamRole.Viewer), token));
        LabelAdministrationCaller viewer = Success(await team.AuthenticateBrowserAsync(viewerDid, token));
        Assert.Equal(1, Success(await restarted.SearchAsync(viewer, "confidential", 1, token)).Total);
        Success(await team.ChangeTeamAsync(
            caller, new(operationId: "revoke-viewer", action: LabelTeamAction.Suspend, target: viewerDid, reason: "Revoke access."), token));
        Assert.Equal(LabelServiceErrorCode.Unauthorized, (await restarted.SearchAsync(viewer, "confidential", 1, token)).Error!.Code);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task SchemaNineUpgradeBackfillsRetainedTextAndResumesWithoutDuplicatingResults(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateModerationAsync(engine);
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelAdministrationCaller caller = await LabelPolicyStoreTests.BootstrapAsync(fixture);
        LabelPolicyState policy = await LabelPolicyStoreTests.ActivateAsync((ILabelPolicyStore)fixture.Store, caller, token);
        await SubmitAsync(fixture, policy, LabelReportSubject.Account(new("did:plc:migration")), "appeal", "Preserved message.", appeal: true);
        await fixture.ExecuteAsync("DROP TABLE label_search_text; DROP TABLE label_search_handles; DROP TABLE label_post_review_checks; UPDATE label_schema SET version=9 WHERE id=1");
        for (int restart = 0; restart < 2; restart++)
        {
            ILabelStore reopened = fixture.NewStore();
            await reopened.InitializeAsync(StoreFixture.Issuer, token);
            Assert.Equal(2, Success(await ((ILabelSearchStore)reopened).SearchAsync(caller, "preserved message", 1, token)).Total);
        }
    }

    /// <summary>Preserves real independent report authentication and the external appeal-message linkage.</summary>
    private static async Task SubmitAsync(
        StoreFixture fixture,
        LabelPolicyState policy,
        LabelReportSubject subject,
        string identity,
        string text,
        bool appeal = false)
    {
        ModerationReportAuthenticationResult authentication = await ReportTestAuthentication.CreateAsync(subject.Repository, policy.DatabaseTime, identity);
        Success(await ((ILabelReportStore)fixture.Store).SubmitReportAsync(
            authentication: authentication,
            submission: new(
                subject: subject,
                reasonType: appeal ? "com.atproto.moderation.defs#reasonAppeal" : "tools.ozone.report.defs#reasonMisleadingSpam",
                reason: text),
            expectedPolicyRevision: policy.Revision,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Retains provider diagnostics on test failures.</summary>
    private static T Success<T>(LabelServiceResult<T> result) where T : class
    {
        Assert.True(result.Succeeded, $"{result.Error?.Message}{Environment.NewLine}{result.Error?.Exception}");
        return result.Result;
    }
}
