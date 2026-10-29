// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Storage;

namespace idunno.Bluesky.Labeler.Test;

public class LabelDecisionPrecisionTests
{
    [Theory(Timeout = 120_000)]
    [InlineData("sqlite", "123456789", true)]
    [InlineData("postgresql", "123456789", true)]
    [InlineData("mysql", "123456789", true)]
    [InlineData("sqlite", "123456700", false)]
    [InlineData("postgresql", "123456700", false)]
    [InlineData("mysql", "123456700", false)]
    public async Task ComparesLegacyExpiryAtFullSignedPrecision(string engine, string fraction, bool changed)
    {
        await using StoreFixture fixture = await StoreFixture.CreateAsync(engine);
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        Label legacyLabel = JsonSerializer.Deserialize<Label>(
            $$"""
            {"ver":1,"src":"did:plc:label-issuer","uri":"did:plc:label-test-subject","val":"spam",
            "cts":"2026-01-01T00:00:00.000000001Z","exp":"2027-01-01T00:00:00.{{fraction}}Z"}
            """,
            AtProtoJsonSerializerOptions.Default)!;
        SignedLabelSnapshot legacy = new(key.Sign(legacyLabel));
        await fixture.Store.AppendAsync(
            operationId: "legacy-precise-expiry",
            labels: [legacy],
            cancellationToken: TestContext.Current.CancellationToken);
        ILabelStore store = fixture.NewStore();
        LabelTarget target = LabelTarget.Account(new Did(TargetResolutionHandler.SubjectDid));
        LabelServiceResult<LabelDecisionSnapshot> observed = await store.ReadDecisionAsync(
            target,
            "spam",
            TestContext.Current.CancellationToken);
        Assert.True(observed.Succeeded, observed.Error?.Message);
        Assert.NotNull(observed.Result!.CurrentLabel);

        DateTimeOffset evaluatedAt = new(
            year: 2026,
            month: 1,
            day: 2,
            hour: 3,
            minute: 4,
            second: 5,
            offset: TimeSpan.Zero);
        DateTimeOffset requestedExpiry = new DateTimeOffset(
            year: 2027,
            month: 1,
            day: 1,
            hour: 0,
            minute: 0,
            second: 0,
            offset: TimeSpan.Zero).AddTicks(1_234_567);
        LabelOperationRequest request = LabelOperationRequest.Issue(
            operationId: "tick-precision-request",
            target: target,
            value: "spam",
            context: new LabelOperationContext(new Did("did:plc:label-test-moderator"), "compare exact expiry"),
            expiresAt: requestedExpiry);
        SignedLabelSnapshot? replacement = changed
            ? new(
                key.CreateLabel(
                    source: StoreFixture.s_issuer,
                    uri: TargetResolutionHandler.SubjectDid,
                    value: "spam",
                    creationTimestamp: evaluatedAt,
                    expiresAt: requestedExpiry))
            : null;
        LabelDecisionCommit decision = new(
            request: request,
            resolvedTarget: target,
            expectedRevision: observed.Result.State.Revision,
            signedLabel: replacement,
            audit: new LabelAuditRecord(
                evaluatedAt: evaluatedAt,
                authorizationVersion: "grant-v1",
                policyVersion: "policy-v1",
                signingKeyMultibase: changed ? key.ExportPublicKeyMultibase() : null));

        LabelServiceResult<LabelOperationReceipt> committed = await store.CommitDecisionAsync(
            decision,
            TestContext.Current.CancellationToken);

        Assert.True(committed.Succeeded, committed.Error?.Message);
        Assert.Equal(changed, committed.Result!.Changed);
        Assert.Equal(changed ? 2 : 1, committed.Result.StateAtCommit.IssuedThrough);
        Assert.Equal(observed.Result.State.Revision + 1, committed.Result.StateAtCommit.Revision);
        Assert.Equal(
            (replacement ?? legacy).GetJsonBytes(),
            committed.Result.Label!.SignedLabel.GetJsonBytes());
    }
}
