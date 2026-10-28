// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Storage;
namespace idunno.Bluesky.Labeler.Test;

/// <summary>Checks that immutable service contracts validate their construction inputs before exposing state.</summary>
public class LabelServiceContractTests
{
    [Theory]
    [InlineData("authorizationVersion")]
    [InlineData("policyVersion")]
    [InlineData("signingKeyMultibase")]
    public void RejectsBlankAuditMetadata(string parameterName)
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => new LabelAuditRecord(
            evaluatedAt: DateTimeOffset.UnixEpoch,
            authorizationVersion: parameterName == "authorizationVersion" ? " " : "grant",
            policyVersion: parameterName == "policyVersion" ? " " : null,
            signingKeyMultibase: parameterName == "signingKeyMultibase" ? " " : null));

        Assert.Equal(parameterName, error.ParamName);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("state")]
    public void RejectsNullRequiredContextAndSnapshotInputs(string parameterName)
    {
        ArgumentNullException error = Assert.Throws<ArgumentNullException>(() =>
        {
            if (parameterName == "actor")
            {
                _ = new LabelOperationContext(null!, "reason");
            }
            else
            {
                _ = new LabelDecisionSnapshot(null!, null);
            }
        });

        Assert.Equal(parameterName, error.ParamName);
    }

    [Theory]
    [InlineData("request")]
    [InlineData("resolvedTarget")]
    [InlineData("stateAtCommit")]
    [InlineData("audit")]
    public void RejectsNullRequiredReceiptInputs(string parameterName)
    {
        LabelTarget target = LabelTarget.Account(StoreFixture.s_issuer);
        LabelOperationRequest request = LabelOperationRequest.Issue(
            operationId: "contract",
            target: target,
            value: "spam",
            context: new(StoreFixture.s_issuer, "contract validation"));
        LabelStoreState state = new(
            StreamId: Guid.Empty,
            Issuer: StoreFixture.s_issuer.ToString(),
            Revision: 0,
            IssuedThrough: 0,
            PublishedThrough: 0,
            ReplayFloor: 0,
            Phase: LabelerLifecyclePhase.Active,
            RetirementUpperBound: 0,
            RetirementThrough: 0,
            GraceDeadline: null);
        LabelAuditRecord audit = new(
            evaluatedAt: DateTimeOffset.UnixEpoch,
            authorizationVersion: "grant",
            policyVersion: null,
            signingKeyMultibase: null);

        ArgumentNullException error = Assert.Throws<ArgumentNullException>(() => new LabelOperationReceipt(
            request: parameterName == "request" ? null! : request,
            resolvedTarget: parameterName == "resolvedTarget" ? null! : target,
            changed: false,
            label: null,
            stateAtCommit: parameterName == "stateAtCommit" ? null! : state,
            audit: parameterName == "audit" ? null! : audit));

        Assert.Equal(parameterName, error.ParamName);
    }

    [Fact]
    public void RejectsUndefinedErrorCodeBeforeValidatingMessage()
    {
        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(() => new LabelServiceError(
            code: (LabelServiceErrorCode)(-1),
            message: " "));

        Assert.Equal("code", error.ParamName);
    }

    [Theory]
    [InlineData(-7)]
    [InlineData(0)]
    [InlineData(9)]
    public void NormalizesAuditTimestampWithoutChangingMetadata(int offsetHours)
    {
        DateTimeOffset instant = DateTimeOffset.UnixEpoch.ToOffset(TimeSpan.FromHours(offsetHours));
        LabelAuditRecord audit = new(
            evaluatedAt: instant,
            authorizationVersion: "grant",
            policyVersion: "policy",
            signingKeyMultibase: "public-key");

        Assert.Equal(DateTimeOffset.UnixEpoch, audit.EvaluatedAt);
        Assert.Equal(TimeSpan.Zero, audit.EvaluatedAt.Offset);
        Assert.Equal("grant", audit.AuthorizationVersion);
        Assert.Equal("policy", audit.PolicyVersion);
        Assert.Equal("public-key", audit.SigningKeyMultibase);
    }
}
