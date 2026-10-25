// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;

using idunno.AtProto;
using idunno.AtProto.Labels;

namespace idunno.Bluesky.Labeler.Test;

/// <summary>Verifies actionable service failures leave the committed decision unchanged.</summary>
public class LabelIssuanceFailureTests
{
    [Theory]
    [InlineData("invalid-target", LabelServiceErrorCode.InvalidTarget)]
    [InlineData("resolution", LabelServiceErrorCode.TargetResolutionFailed)]
    [InlineData("policy", LabelServiceErrorCode.PolicyUnavailable)]
    [InlineData("value", LabelServiceErrorCode.ValueNotDeclared)]
    [InlineData("accounts", LabelServiceErrorCode.TargetNotAllowed)]
    [InlineData("records", LabelServiceErrorCode.TargetNotAllowed)]
    [InlineData("collection", LabelServiceErrorCode.TargetNotAllowed)]
    [InlineData("expiry", LabelServiceErrorCode.InvalidExpiry)]
    [InlineData("closed", LabelServiceErrorCode.IssuanceClosed)]
    [InlineData("timestamp", LabelServiceErrorCode.TimestampExhausted)]
    public async Task ReportsCauseWithoutCommitting(string scenario, LabelServiceErrorCode expected)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync("sqlite");
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        TargetResolutionHandler handler = new() { Offline = scenario == "resolution" };
        using HttpClient client = new(handler);
        TestLabelIssuancePolicyProvider policy = new(
            new(
                version: "policy",
                declaredValues: ["spam"],
                allowAccounts: scenario != "accounts",
                allowRecords: scenario != "records",
                allowedRecordCollections: scenario == "collection" ? [] : null))
        {
            Unavailable = scenario == "policy"
        };
        LabelTarget target = scenario is "records" or "collection"
            ? LabelTarget.Record(new AtUri($"at://{TargetResolutionHandler.SubjectDid}/example.test.record/key"))
            : LabelTarget.Account(TargetResolutionHandler.SubjectDid);
        LabelOperationContext context = new(StoreFixture.Issuer, "Failure boundary");
        if (scenario == "timestamp")
        {
            await fixture.Store.AppendAsync(
                operationId: "future",
                labels: [new(
                    key.CreateLabel(
                        source: StoreFixture.Issuer,
                        uri: target.Subject,
                        value: "spam",
                        creationTimestamp: DateTimeOffset.MaxValue))],
                cancellationToken: TestContext.Current.CancellationToken);
        }

        if (scenario == "closed")
        {
            await fixture.Store.BeginRetirementAsync(0, cancellationToken: TestContext.Current.CancellationToken);
        }

        LabelStoreState before = await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken);
        LabelOperationRequest request = scenario is "invalid-target" or "timestamp"
            ? LabelOperationRequest.Negate(
                operationId: "test",
                target: scenario == "invalid-target" ? LabelTarget.Account(new Handle("example.test")) : target,
                value: "spam",
                context: context)
            : LabelOperationRequest.Issue(
                operationId: "test",
                target: target,
                value: scenario == "value" ? "beans" : "spam",
                context: context,
                expiresAt: scenario == "expiry" ? DateTimeOffset.MinValue : null);
        LabelIssuanceService service = new(
            issuer: StoreFixture.Issuer,
            signingKey: key,
            store: fixture.Store,
            authorizer: new TestLabelIssuanceAuthorizer(),
            policyProvider: policy,
            httpClient: client);
        LabelServiceResult<LabelOperationReceipt> result = await service.ExecuteAsync(request, TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded);
        Assert.Equal(expected, result.Error.Code);
        Assert.Equal(before, await fixture.Store.GetStateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PreservesUnknownOutcomeWhenCancellationFollowsCommitSubmission()
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync("sqlite");
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient client = new(new TargetResolutionHandler());
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        LabelIssuanceService service = new(
            issuer: StoreFixture.Issuer,
            signingKey: key,
            store: fixture.Store,
            authorizer: new TestLabelIssuanceAuthorizer(),
            policyProvider: new TestLabelIssuancePolicyProvider(
                new(
                    version: "policy",
                    declaredValues: ["spam"],
                    allowAccounts: true,
                    allowRecords: true)),
            httpClient: client);
        fixture.SetCommitHooks(
            afterCommit: async (connection, token) =>
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM label_decisions";
            if (Convert.ToInt64(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture) == 1)
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            }
        });
        LabelOperationRequest request = LabelOperationRequest.Issue(
            operationId: "cancelled",
            target: LabelTarget.Account(TargetResolutionHandler.SubjectDid),
            value: "spam",
            context: new(StoreFixture.Issuer, "Commit cancellation"));
        LabelServiceResult<LabelOperationReceipt> result = await service.ExecuteAsync(request, cancellation.Token);
        Assert.False(result.Succeeded);
        Assert.Equal(LabelServiceErrorCode.OutcomeUnknown, result.Error.Code);
        fixture.SetCommitHooks();
        LabelServiceResult<LabelOperationReceipt> recovered = await service.GetOperationAsync(request.OperationId, request.Context, TestContext.Current.CancellationToken);
        Assert.True(recovered.Succeeded);
        Assert.Equal(1, recovered.Result.Label!.Sequence);
    }
}
