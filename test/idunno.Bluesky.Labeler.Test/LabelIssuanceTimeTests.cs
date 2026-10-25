// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Issuance;

using Microsoft.Extensions.Time.Testing;

namespace idunno.Bluesky.Labeler.Test;

/// <summary>Checks decision timestamps and expiry against a fixed clock shared with the request inputs.</summary>
public class LabelIssuanceTimeTests
{
    private static readonly DateTimeOffset s_start = new(
        year: 2026,
        month: 1,
        day: 1,
        hour: 0,
        minute: 0,
        second: 0,
        offset: TimeSpan.Zero);
    private static readonly LabelTarget s_target = LabelTarget.Account(TargetResolutionHandler.SubjectDid);
    private static readonly LabelOperationContext s_context = new(StoreFixture.Issuer, "Clock boundary");

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task AdvancesCreationWithStalledOrRegressedClockAndRejectsEarlierExpiry(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient client = new(new TargetResolutionHandler());
        FakeTimeProvider clock = new(s_start);
        LabelIssuanceService service = Service(
            fixture: fixture,
            key: key,
            client: client,
            clock: clock);
        LabelServiceResult<LabelOperationReceipt> first = await service.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "first",
                target: s_target,
                value: "spam",
                context: s_context),
            TestContext.Current.CancellationToken);
        Assert.True(first.Succeeded);
        Assert.Equal(s_start, first.Result.Label!.SignedLabel.Label.CreationTimestamp);

        LabelServiceResult<LabelOperationReceipt> negated = await service.ExecuteAsync(
            LabelOperationRequest.Negate(
                operationId: "negate",
                target: s_target,
                value: "spam",
                context: s_context),
            TestContext.Current.CancellationToken);
        Assert.True(negated.Succeeded);
        Assert.Equal(s_start.AddTicks(1), negated.Result.Label!.SignedLabel.Label.CreationTimestamp);
        Assert.Equal(s_start, negated.Result.Audit.EvaluatedAt);

        // A new process can start with an earlier wall clock. Its service must still supersede
        // persisted history; replacing the fake models restart without making fake time run backwards.
        FakeTimeProvider regressedClock = new(s_start.AddDays(-1));
        LabelIssuanceService restarted = Service(
            fixture: fixture,
            key: key,
            client: client,
            clock: regressedClock);
        LabelServiceResult<LabelOperationReceipt> invalid = await restarted.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "too-early",
                target: s_target,
                value: "spam",
                context: s_context,
                expiresAt: s_start.AddTicks(2)),
            TestContext.Current.CancellationToken);
        Assert.False(invalid.Succeeded);
        Assert.Equal(LabelServiceErrorCode.InvalidExpiry, invalid.Error.Code);
        LabelServiceResult<LabelOperationReceipt> issued = await restarted.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "reissue",
                target: s_target,
                value: "spam",
                context: s_context,
                expiresAt: s_start.AddMinutes(1)),
            TestContext.Current.CancellationToken);
        Assert.True(issued.Succeeded);
        Assert.Equal(s_start.AddTicks(2), issued.Result.Label!.SignedLabel.Label.CreationTimestamp);
        Assert.Equal(regressedClock.GetUtcNow(), issued.Result.Audit.EvaluatedAt);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("mysql")]
    public async Task TreatsExpiryBoundaryAsInactiveAndRetainsOriginalRetryAfterExpiry(string engine)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using HttpClient client = new(new TargetResolutionHandler());
        FakeTimeProvider clock = new(s_start);
        LabelIssuanceService service = Service(
            fixture: fixture,
            key: key,
            client: client,
            clock: clock);
        DateTimeOffset expiry = clock.GetUtcNow().AddMinutes(1);
        LabelOperationRequest original = LabelOperationRequest.Issue(
            operationId: "original",
            target: s_target,
            value: "spam",
            context: s_context,
            expiresAt: expiry);
        LabelServiceResult<LabelOperationReceipt> first = await service.ExecuteAsync(original, TestContext.Current.CancellationToken);
        Assert.True(first.Succeeded);
        clock.Advance(TimeSpan.FromMinutes(1) - TimeSpan.FromTicks(1));
        LabelServiceResult<LabelOperationReceipt> before = await service.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "before-expiry",
                target: s_target,
                value: "spam",
                context: s_context,
                expiresAt: expiry),
            TestContext.Current.CancellationToken);
        Assert.True(before.Succeeded);
        Assert.False(before.Result.Changed);
        clock.Advance(TimeSpan.FromTicks(1));
        LabelServiceResult<LabelOperationReceipt> at = await service.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "at-expiry",
                target: s_target,
                value: "spam",
                context: s_context,
                expiresAt: expiry),
            TestContext.Current.CancellationToken);
        Assert.False(at.Succeeded);
        Assert.Equal(LabelServiceErrorCode.InvalidExpiry, at.Error.Code);

        LabelServiceResult<LabelOperationReceipt> retry = await service.ExecuteAsync(original, TestContext.Current.CancellationToken);
        Assert.True(retry.Succeeded);
        Assert.Equal(first.Result.Label!.SignedLabel.GetJsonBytes(), retry.Result.Label!.SignedLabel.GetJsonBytes());
        Assert.Equal(first.Result.Audit.EvaluatedAt, retry.Result.Audit.EvaluatedAt);
        LabelServiceResult<LabelOperationReceipt> renewed = await service.ExecuteAsync(
            LabelOperationRequest.Issue(
                operationId: "renewed",
                target: s_target,
                value: "spam",
                context: s_context,
                expiresAt: clock.GetUtcNow().AddMinutes(1)),
            TestContext.Current.CancellationToken);
        Assert.True(renewed.Succeeded);
        Assert.True(renewed.Result.Changed);
        Assert.Equal(clock.GetUtcNow(), renewed.Result.Label!.SignedLabel.Label.CreationTimestamp);
    }

    /// <summary>Builds an independent service over the same persistent stream and explicit test clock.</summary>
    private static LabelIssuanceService Service(
        StoreFixture fixture,
        LabelSigningKey key,
        HttpClient client,
        FakeTimeProvider clock) =>
        new(
            issuer: StoreFixture.Issuer,
            signingKey: key,
            store: fixture.NewStore(),
            authorizer: new TestLabelIssuanceAuthorizer(),
            policyProvider: new TestLabelIssuancePolicyProvider(
                new(
                    version: "policy",
                    declaredValues: ["spam"],
                    allowAccounts: true,
                    allowRecords: true)),
            httpClient: client,
            timeProvider: clock);
}
