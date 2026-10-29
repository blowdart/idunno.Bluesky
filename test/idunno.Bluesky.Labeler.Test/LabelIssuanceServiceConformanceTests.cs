// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Storage;

namespace idunno.Bluesky.Labeler.Test;

public class LabelIssuanceServiceConformanceTests
{
    private static readonly Did s_actor = new("did:plc:label-test-moderator");
    private static readonly DateTimeOffset s_instant = new(
        year: 2026,
        month: 1,
        day: 2,
        hour: 3,
        minute: 4,
        second: 5,
        offset: TimeSpan.Zero);

    [Fact]
    public void RecordTargetsRejectIncompleteUrisAndInvalidCids()
    {
        Assert.Throws<ArgumentException>(
            () => LabelTarget.Record(
                new AtUri("at://did:plc:label-test-subject/app.bsky.feed.post")));
        Assert.ThrowsAny<ArgumentException>(() => new Cid("not-a-cid"));
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task IssuesAccountAndArbitraryRecordTargetsWithBothSigningCurves(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);

        (string? Uri, bool HasCid)[] targets =
        [
            (null, false),
            ("at://did:plc:label-test-subject/app.bsky.feed.post/post-key", true),
            ("at://did:plc:label-test-subject/app.bsky.actor.profile/self", false),
            ("at://did:plc:label-test-subject/app.bsky.graph.list/list-key", false),
            ("at://did:plc:label-test-subject/app.bsky.feed.generator/feed-key", false),
            ("at://did:plc:label-test-subject/com.example.record/custom-key", false)
        ];

        foreach (LabelSigningCurve curve in new[] { LabelSigningCurve.P256, LabelSigningCurve.Secp256k1 })
        {
            using LabelSigningKey key = LabelSigningKey.Generate(curve);
            TargetResolutionHandler handler = new();
            using HttpClient httpClient = new(handler);
            LabelIssuanceService service = CreateService(
                store: fixture.Store,
                key: key,
                httpClient: httpClient,
                authorizer: new TestLabelIssuanceAuthorizer(),
                policy: new TestLabelIssuancePolicyProvider(Policy()));

            for (int index = 0; index < targets.Length; index++)
            {
                (string? uriText, bool hasCid) = targets[index];
                LabelTarget target = uriText is null
                    ? LabelTarget.Account(new Did(TargetResolutionHandler.SubjectDid))
                    : LabelTarget.Record(new AtUri(uriText), hasCid ? new Cid(TargetResolutionHandler.RecordCid) : null);
                LabelOperationRequest request = LabelOperationRequest.Issue(
                    operationId: $"arbitrary-{curve}-{index}",
                    target: target,
                    value: "spam",
                    context: Context($"target-{index}"));

                LabelServiceResult<LabelOperationReceipt> result = await service.ExecuteAsync(
                    request,
                    TestContext.Current.CancellationToken);

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Target {target.Subject} on {curve} failed with {result.Error?.Code}: {result.Error?.Message} " +
                        $"HTTP {result.Error?.StatusCode} {result.Error?.AtErrorDetail?.Error}; handler requests {handler.Requests}.");
                }

                LabelOperationReceipt receipt = result.Result!;
                Assert.True(receipt.Changed);
                Assert.NotNull(receipt.Label);
                Assert.Equal(target.Kind, receipt.ResolvedTarget.Kind);
                Assert.Equal(
                    hasCid ? new Cid(TargetResolutionHandler.RecordCid) : null,
                    receipt.Label!.SignedLabel.Label.Cid);
                string expectedSubject = uriText ?? TargetResolutionHandler.SubjectDid;
                Assert.Equal(expectedSubject, receipt.Label!.SignedLabel.Label.Uri.ToString());
                receipt.Label!.SignedLabel.Label.Verify(SigningDocument(key));

                LabelServiceResult<LabelOperationReceipt> negated = await service.ExecuteAsync(
                    LabelOperationRequest.Negate(
                        operationId: $"arbitrary-negate-{curve}-{index}",
                        target: receipt.ResolvedTarget,
                        value: "spam",
                        context: Context($"withdraw-target-{index}")),
                    TestContext.Current.CancellationToken);
                Assert.True(negated.Succeeded, negated.Error?.Message);
                Assert.True(negated.Result!.Changed);
                StoredLabel negation = Assert.IsType<StoredLabel>(negated.Result.Label);
                Assert.True(negation.SignedLabel.Label.IsNegationLabel);
                Assert.Equal(expectedSubject, negation.SignedLabel.Label.Uri.ToString());
                Assert.Equal(receipt.Label.SignedLabel.Label.Cid, negation.SignedLabel.Label.Cid);
                negation.SignedLabel.Label.Verify(SigningDocument(key));
            }
        }
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task RecoversSemanticRetriesAndAuditsDuplicateDecisionsWithoutAppending(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        TargetResolutionHandler handler = new();
        using HttpClient httpClient = new(handler);
        TestLabelIssuancePolicyProvider policy = new(Policy());
        LabelIssuanceService service = CreateService(
            store: fixture.Store,
            key: key,
            httpClient: httpClient,
            authorizer: new TestLabelIssuanceAuthorizer(),
            policy: policy);
        LabelTarget target = LabelTarget.Account(new Did(TargetResolutionHandler.SubjectDid));
        LabelOperationRequest original = LabelOperationRequest.Issue(
            operationId: "semantic-retry",
            target: target,
            value: "spam",
            context: Context("reviewed evidence", "correlation-first"));

        LabelOperationReceipt first = (await service.ExecuteAsync(original, TestContext.Current.CancellationToken)).Result!;
        policy.Unavailable = true;

        LabelOperationReceipt recovered = (await service.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "semantic-retry",
                target: target,
                value: "spam",
                context: Context("reviewed evidence", "correlation-second")),
            TestContext.Current.CancellationToken)).Result!;

        Assert.Equal(first.Request.OperationId, recovered.Request.OperationId);
        Assert.Equal(first.StateAtCommit, recovered.StateAtCommit);
        Assert.Equal(first.Label!.Sequence, recovered.Label!.Sequence);
        Assert.Equal(first.Label.SignedLabel.GetJsonBytes(), recovered.Label.SignedLabel.GetJsonBytes());
        Assert.Equal("correlation-first", recovered.Request.Context.CorrelationId);

        LabelServiceResult<LabelOperationReceipt> changedReason = await service.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "semantic-retry",
                target: target,
                value: "spam",
                context: Context("different reason")),
            TestContext.Current.CancellationToken);
        Assert.False(changedReason.Succeeded);
        Assert.Equal(LabelServiceErrorCode.IdempotencyConflict, changedReason.Error!.Code);

        LabelServiceResult<LabelOperationReceipt> newOperationWithUnavailablePolicy = await service.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "new-operation",
                target: target,
                value: "spam",
                context: Context("new-operation")),
            TestContext.Current.CancellationToken);
        Assert.False(newOperationWithUnavailablePolicy.Succeeded);
        Assert.Equal(LabelServiceErrorCode.PolicyUnavailable, newOperationWithUnavailablePolicy.Error!.Code);

        policy.Unavailable = false;
        LabelOperationReceipt noOp = (await service.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "same-decision",
                target: target,
                value: "spam",
                context: Context("same-decision")),
            TestContext.Current.CancellationToken)).Result!;
        Assert.False(noOp.Changed);
        Assert.Equal(first.Label.Sequence, noOp.Label!.Sequence);
        Assert.Equal(first.Label.SignedLabel.GetJsonBytes(), noOp.Label.SignedLabel.GetJsonBytes());
        Assert.Equal(first.StateAtCommit.IssuedThrough, noOp.StateAtCommit.IssuedThrough);
        Assert.Equal(first.StateAtCommit.Revision + 1, noOp.StateAtCommit.Revision);
        Assert.Equal(3, handler.Requests);
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task WithdrawsStoredRecordWhenResolverIsOfflineAndPolicyNoLongerAllowsIssuance(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        TargetResolutionHandler handler = new();
        using HttpClient httpClient = new(handler);
        TestLabelIssuancePolicyProvider policy = new(Policy());
        LabelIssuanceService service = CreateService(
            store: fixture.Store,
            key: key,
            httpClient: httpClient,
            authorizer: new TestLabelIssuanceAuthorizer(),
            policy: policy);
        LabelTarget record = LabelTarget.Record(
            new AtUri("at://did:plc:label-test-subject/app.bsky.feed.post/post-key"),
            new Cid(TargetResolutionHandler.RecordCid));
        LabelOperationReceipt issued = (await service.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "record-issue",
                target: record,
                value: "spam",
                context: Context("issue")),
            TestContext.Current.CancellationToken)).Result!;
        StoredLabel issuedLabel = Assert.IsType<StoredLabel>(issued.Label!);

        policy.Policy = new LabelIssuancePolicy(
            version: "policy-v2",
            declaredValues: ["other-value"],
            allowAccounts: false,
            allowRecords: false,
            allowedRecordCollections: []);
        LabelServiceResult<LabelOperationReceipt> stale = await service.ExecuteAsync(
            LabelOperationRequest.Negate(
                operationId: "stale-withdrawal",
                target: LabelTarget.Record(
                    record.RecordUri!,
                    new Cid("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4")),
                value: "spam",
                context: Context("stale-withdrawal")),
            TestContext.Current.CancellationToken);
        Assert.False(stale.Succeeded);
        Assert.Equal(LabelServiceErrorCode.StaleDecision, stale.Error!.Code);

        handler.Offline = true;
        LabelOperationReceipt withdrawn = (await service.ExecuteAsync(
            LabelOperationRequest.Negate(
                operationId: "record-negation",
                target: LabelTarget.Record(record.RecordUri!),
                value: "spam",
                context: Context("withdrawal")),
            TestContext.Current.CancellationToken)).Result!;

        Assert.True(withdrawn.Changed);
        StoredLabel withdrawnLabel = Assert.IsType<StoredLabel>(withdrawn.Label);
        Assert.True(withdrawnLabel.SignedLabel.Label.IsNegationLabel);
        Assert.Equal(issuedLabel.SignedLabel.Label.Cid, withdrawnLabel.SignedLabel.Label.Cid);
        Assert.Equal(issuedLabel.SignedLabel.Label.Uri, withdrawnLabel.SignedLabel.Label.Uri);
        Assert.Equal(issuedLabel.Sequence + 1, withdrawnLabel.Sequence);
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task AuthorizesCopiedWithdrawalCidBeforeCommittingAndAuditsFinalGrant(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient httpClient = new(new TargetResolutionHandler());
        TestLabelIssuanceAuthorizer authorizer = new()
        {
            GrantVersion = request => request.ResolvedTarget?.Cid is not null ? "cid-grant" : "unpinned-grant"
        };
        LabelIssuanceService service = CreateService(
            store: fixture.Store,
            key: key,
            httpClient: httpClient,
            authorizer: authorizer,
            policy: new TestLabelIssuancePolicyProvider(Policy()));
        LabelTarget record = LabelTarget.Record(
            new AtUri("at://did:plc:label-test-subject/app.bsky.feed.post/post-key"),
            new Cid(TargetResolutionHandler.RecordCid));
        LabelServiceResult<LabelOperationReceipt> issued = await service.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "pinned-issue",
                target: record,
                value: "spam",
                context: Context("issue")),
            TestContext.Current.CancellationToken);
        Assert.True(issued.Succeeded);

        for (int attempt = 0; attempt < 2; attempt++)
        {
            LabelOperationRequest withdrawal = LabelOperationRequest.Negate(
                operationId: $"unpinned-withdrawal-{attempt}",
                target: LabelTarget.Record(record.RecordUri!),
                value: "spam",
                context: Context("withdrawal"));
            LabelStoreState before = await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken);
            authorizer.Deny = request => request.Action == LabelAuthorizationAction.Negate &&
                request.ResolvedTarget?.Cid is not null;

            LabelServiceResult<LabelOperationReceipt> denied = await service.ExecuteAsync(
                withdrawal,
                TestContext.Current.CancellationToken);

            Assert.False(denied.Succeeded);
            Assert.Equal(LabelServiceErrorCode.Unauthorized, denied.Error.Code);
            Assert.Equal(before, await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken));
            LabelServiceResult<LabelOperationReceipt> absent = await fixture.Store.GetOperationAsync(
                withdrawal.OperationId,
                TestContext.Current.CancellationToken);
            Assert.False(absent.Succeeded);
            Assert.Equal(LabelServiceErrorCode.OperationNotFound, absent.Error.Code);

            authorizer.Deny = null;
            LabelServiceResult<LabelOperationReceipt> allowed = await service.ExecuteAsync(
                withdrawal,
                TestContext.Current.CancellationToken);

            Assert.True(allowed.Succeeded);
            Assert.Equal(attempt == 0, allowed.Result.Changed);
            Assert.Equal("cid-grant", allowed.Result.Audit.AuthorizationVersion);
            Assert.Equal(record.Cid, allowed.Result.ResolvedTarget.Cid);
            Assert.NotNull(allowed.Result.Label);
            Assert.True(allowed.Result.Label.SignedLabel.Label.IsNegationLabel);
            allowed.Result.Label.SignedLabel.Label.Verify(SigningDocument(key));
        }
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task AuthorizationAndPolicyDenialsDoNotCommitOrResolveBeforeAdmission(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        TargetResolutionHandler handler = new();
        using HttpClient httpClient = new(handler);
        TestLabelIssuanceAuthorizer authorizer = new();
        TestLabelIssuancePolicyProvider policy = new(Policy());
        LabelIssuanceService service = CreateService(
            store: fixture.Store,
            key: key,
            httpClient: httpClient,
            authorizer: authorizer,
            policy: policy);
        LabelOperationRequest request = LabelOperationRequest.Issue(
            operationId: "denied-admission",
            target: LabelTarget.Account(new Did(TargetResolutionHandler.SubjectDid)),
            value: "spam",
            context: Context("denial"));

        authorizer.Deny = check => check.IsAdmissionCheck;
        LabelServiceResult<LabelOperationReceipt> admission = await service.ExecuteAsync(
            request,
            TestContext.Current.CancellationToken);
        Assert.False(admission.Succeeded);
        Assert.Equal(LabelServiceErrorCode.Unauthorized, admission.Error!.Code);
        Assert.Equal(0, handler.Requests);
        Assert.Equal(0, (await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken)).Revision);

        authorizer.Deny = check => !check.IsAdmissionCheck;
        LabelServiceResult<LabelOperationReceipt> finalAuthorization = await service.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "denied-final",
                target: request.Target,
                value: request.Value,
                context: request.Context),
            TestContext.Current.CancellationToken);
        Assert.False(finalAuthorization.Succeeded);
        Assert.Equal(LabelServiceErrorCode.Unauthorized, finalAuthorization.Error!.Code);
        Assert.Equal(1, handler.Requests);
        Assert.Equal(0, (await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken)).Revision);

        authorizer.Deny = null;
        policy.Policy = new LabelIssuancePolicy(
            version: "policy-deny",
            declaredValues: ["spam"],
            allowAccounts: false,
            allowRecords: false);
        LabelServiceResult<LabelOperationReceipt> deniedByPolicy = await service.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "denied-policy",
                target: request.Target,
                value: request.Value,
                context: request.Context),
            TestContext.Current.CancellationToken);
        Assert.False(deniedByPolicy.Succeeded);
        Assert.Equal(LabelServiceErrorCode.TargetNotAllowed, deniedByPolicy.Error!.Code);
        Assert.Equal(0, (await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken)).Revision);
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task ClassifiesMissingAndMismatchedRecordVersionsWithoutCommitting(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        TargetResolutionHandler handler = new() { RecordNotFound = true };
        using HttpClient httpClient = new(handler);
        LabelIssuanceService service = CreateService(
            store: fixture.Store,
            key: key,
            httpClient: httpClient,
            authorizer: new TestLabelIssuanceAuthorizer(),
            policy: new TestLabelIssuancePolicyProvider(Policy()));
        LabelTarget target = LabelTarget.Record(
            new AtUri("at://did:plc:label-test-subject/app.bsky.feed.post/post-key"),
            new Cid(TargetResolutionHandler.RecordCid));

        LabelServiceResult<LabelOperationReceipt> missing = await service.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "missing-record",
                target: target,
                value: "spam",
                context: Context("missing")),
            TestContext.Current.CancellationToken);
        Assert.False(missing.Succeeded);
        Assert.Equal(LabelServiceErrorCode.TargetNotFound, missing.Error!.Code);

        handler.RecordNotFound = false;
        handler.Cid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";
        LabelServiceResult<LabelOperationReceipt> mismatched = await service.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "wrong-record-version",
                target: target,
                value: "spam",
                context: Context("wrong-version")),
            TestContext.Current.CancellationToken);
        Assert.False(mismatched.Succeeded);
        Assert.Equal(LabelServiceErrorCode.CidMismatch, mismatched.Error!.Code);
        Assert.Equal(0, (await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken)).Revision);
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task AuthorizesRecoveryAgainstStoredTargetAndDoesNotResolveItAgain(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        TargetResolutionHandler handler = new();
        using HttpClient httpClient = new(handler);
        TestLabelIssuanceAuthorizer authorizer = new();
        TestLabelIssuancePolicyProvider policy = new(Policy());
        LabelIssuanceService service = CreateService(
            store: fixture.Store,
            key: key,
            httpClient: httpClient,
            authorizer: authorizer,
            policy: policy);
        LabelOperationRequest request = LabelOperationRequest.Issue(
            operationId: "authorized-recovery",
            target: LabelTarget.Account(new Did(TargetResolutionHandler.SubjectDid)),
            value: "spam",
            context: Context("original moderation reason"));
        LabelServiceResult<LabelOperationReceipt> issued = await service.ExecuteAsync(
            request,
            TestContext.Current.CancellationToken);
        Assert.True(issued.Succeeded, issued.Error?.Message);
        handler.Offline = true;
        policy.Unavailable = true;
        LabelOperationContext inspectingCaller = new(new Did("did:plc:another-moderator"), "inspect prior action");
        authorizer.Deny = check => !check.IsAdmissionCheck;

        LabelServiceResult<LabelOperationReceipt> denied = await service.GetOperationAsync(
            request.OperationId,
            inspectingCaller,
            TestContext.Current.CancellationToken);
        Assert.False(denied.Succeeded);
        Assert.Equal(LabelServiceErrorCode.Unauthorized, denied.Error!.Code);
        Assert.Contains(
            authorizer.Requests,
            check =>
            check.Action == LabelAuthorizationAction.InspectOperation &&
            !check.IsAdmissionCheck && check.Context.Actor == inspectingCaller.Actor &&
            check.ResolvedTarget?.Repository.Value == TargetResolutionHandler.SubjectDid);

        authorizer.Deny = null;
        LabelServiceResult<LabelOperationReceipt> recovered = await service.GetOperationAsync(
            request.OperationId,
            inspectingCaller,
            TestContext.Current.CancellationToken);
        Assert.True(recovered.Succeeded, recovered.Error?.Message);
        Assert.Equal(issued.Result!.Request.Context.Actor, recovered.Result!.Request.Context.Actor);
        Assert.Equal(issued.Result.Request.Context.Reason, recovered.Result.Request.Context.Reason);
        Assert.Equal(
            issued.Result.Label!.SignedLabel.GetJsonBytes(),
            recovered.Result.Label!.SignedLabel.GetJsonBytes());
        Assert.Equal(1, handler.Requests);
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task LeavesNewDecisionsPendingUntilPublishedAndRecoversAfterProviderRestart(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient httpClient = new(new TargetResolutionHandler());
        LabelOperationContext context = Context("publication");
        LabelIssuanceService service = CreateService(
            store: fixture.Store,
            key: key,
            httpClient: httpClient,
            authorizer: new TestLabelIssuanceAuthorizer(),
            policy: new TestLabelIssuancePolicyProvider(Policy()));
        LabelOperationReceipt committed = (await service.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "pending-publication",
                target: LabelTarget.Account(
                    new Did(TargetResolutionHandler.SubjectDid)),
                value: "spam",
                context: context),
            TestContext.Current.CancellationToken)).Result!;

        Assert.Equal(1, committed.StateAtCommit.IssuedThrough);
        Assert.Equal(0, committed.StateAtCommit.PublishedThrough);
        Assert.Empty(
            (await fixture.Store.QueryAsync(
                uriPatterns: [],
                cancellationToken: TestContext.Current.CancellationToken)).Labels);

        ILabelStore publisher = fixture.NewStore();
        LabelStoreState published = await publisher.PublishAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, published.PublishedThrough);

        ILabelStore restartedStore = fixture.NewStore();
        LabelIssuanceService restartedService = CreateService(
            store: restartedStore,
            key: key,
            httpClient: httpClient,
            authorizer: new TestLabelIssuanceAuthorizer(),
            policy: new TestLabelIssuancePolicyProvider(Policy()));
        LabelOperationReceipt restored = (await restartedService.GetOperationAsync(
            "pending-publication",
            context,
            TestContext.Current.CancellationToken)).Result!;
        LabelServiceResult<LabelStoreState> publication = await restartedService.GetPublicationStateAsync(
            context,
            TestContext.Current.CancellationToken);

        Assert.Equal(committed.Request.OperationId, restored.Request.OperationId);
        Assert.Equal(committed.StateAtCommit, restored.StateAtCommit);
        Assert.Equal(committed.Label!.Sequence, restored.Label!.Sequence);
        Assert.Equal(committed.Label.SignedLabel.GetJsonBytes(), restored.Label.SignedLabel.GetJsonBytes());
        Assert.Equal(committed.Audit.EvaluatedAt, restored.Audit.EvaluatedAt);
        Assert.Equal(committed.Audit.SigningKeyMultibase, restored.Audit.SigningKeyMultibase);
        Assert.True(publication.Succeeded);
        Assert.Equal(1, publication.Result!.PublishedThrough);
    }

    [Theory(Timeout = 120_000)]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task RecognizesLegacyAppendsAsTheCurrentDecision(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        LabelTarget target = LabelTarget.Account(new Did(TargetResolutionHandler.SubjectDid));
        Label signed = key.CreateLabel(
            source: StoreFixture.s_issuer,
            uri: TargetResolutionHandler.SubjectDid,
            value: "spam",
            creationTimestamp: s_instant);
        SignedLabelSnapshot snapshot = new(key.Sign(signed));
        LabelAppendReceipt legacyReceipt = await fixture.Store.AppendAsync(
            operationId: "legacy-append",
            labels: [snapshot],
            cancellationToken: TestContext.Current.CancellationToken);
        using HttpClient httpClient = new(new TargetResolutionHandler());
        LabelIssuanceService service = CreateService(
            store: fixture.Store,
            key: key,
            httpClient: httpClient,
            authorizer: new TestLabelIssuanceAuthorizer(),
            policy: new TestLabelIssuancePolicyProvider(Policy()));

        LabelOperationReceipt result = (await service.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "service-no-op",
                target: target,
                value: "spam",
                context: Context("legacy-reconciliation")),
            TestContext.Current.CancellationToken)).Result!;

        Assert.False(result.Changed);
        Assert.NotNull(result.Label);
        Assert.Equal(legacyReceipt.FirstSequence, result.Label!.Sequence);
        Assert.Equal(snapshot.GetJsonBytes(), result.Label.SignedLabel.GetJsonBytes());
        Assert.Equal(1, result.StateAtCommit.IssuedThrough);
    }

    /// <summary>Builds the production service over the chosen provider and controllable host-boundary test helpers.</summary>
    private static LabelIssuanceService CreateService(
        ILabelStore store,
        LabelSigningKey key,
        HttpClient httpClient,
        TestLabelIssuanceAuthorizer authorizer,
        TestLabelIssuancePolicyProvider policy) =>
        new(
            issuer: StoreFixture.s_issuer,
            signingKey: key,
            store: store,
            authorizer: authorizer,
            policyProvider: policy,
            httpClient: httpClient);

    /// <summary>Declares the test label for accounts and arbitrary record collections without a collection allowlist.</summary>
    private static LabelIssuancePolicy Policy() => new(
        version: "policy-v1",
        declaredValues: ["spam"],
        allowAccounts: true,
        allowRecords: true);

    /// <summary>Keeps the trusted actor fixed while tests vary audited reason and diagnostic correlation.</summary>
    private static LabelOperationContext Context(string reason, string? correlationId = null) =>
        new(s_actor, reason, correlationId);

    /// <summary>Publishes the test key under the issuer's label-verification method for cryptographic assertions.</summary>
    private static DidDocument SigningDocument(LabelSigningKey key) =>
        JsonSerializer.Deserialize<DidDocument>(
            $$"""
            {"id":"{{StoreFixture.s_issuer}}","verificationMethod":[{"id":"{{StoreFixture.s_issuer}}#atproto_label",
            "type":"Multikey","controller":"{{StoreFixture.s_issuer}}","publicKeyMultibase":"{{key.ExportPublicKeyMultibase()}}"}]}
            """,
            AtProtoJsonSerializerOptions.Default)!;
}
