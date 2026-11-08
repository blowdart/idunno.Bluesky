// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Storage;

/// <summary>Reconstructs validated stored subjects using their identity rather than treating an absent CID as an account.</summary>
internal static class ModerationSubjectEncoding
{
    /// <summary>Preserves the distinction between an account, a URI-only record and a versioned record.</summary>
    internal static LabelReportSubject Decode(string subject, string? cid)
    {
        if (subject.StartsWith("at://", StringComparison.Ordinal))
        {
            return LabelReportSubject.Record(new(subject), cid is null ? null : new(cid));
        }

        if (cid is not null)
        {
            throw new InvalidDataException("A stored account subject cannot have a record CID.");
        }

        return LabelReportSubject.Account(new(subject));
    }
}

/// <summary>Retains primitive case state without reflection-based protocol serialization.</summary>
internal sealed record ModerationCaseData(
    long Id,
    long Revision,
    string Subject,
    string? Cid,
    LabelCaseStatus Status,
    string? Assignee,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    LabelRecordResolution? RecordResolution = null)
{
    /// <summary>Validates persisted subject and workflow metadata when constructing a public snapshot.</summary>
    internal LabelModerationCase ToCase()
    {
        if (Id <= 0 || Revision <= 0 || !Enum.IsDefined(Status))
        {
            throw new InvalidDataException("The persisted case metadata is invalid.");
        }

        LabelReportSubject subject = ModerationSubjectEncoding.Decode(Subject, Cid);
        return new(
            id: Id,
            revision: Revision,
            subject: subject,
            status: Status,
            assignee: Assignee is null ? null : new(Assignee),
            createdAt: CreatedAt,
            updatedAt: UpdatedAt)
        {
            RecordResolution = RecordResolution
        };
    }
}

/// <summary>Retains complete retry intent, excluding transport-specific credential and diagnostic details.</summary>
internal sealed record ModerationCaseIntent(
    string Actor,
    string Subject,
    string? Cid,
    LabelCaseAction Action,
    string Reason,
    long CaseId,
    long ExpectedRevision,
    string? Assignee);

/// <summary>Retains the immutable event and resulting historical case snapshot together.</summary>
internal sealed record ModerationCaseEvent(
    long Sequence,
    string OperationId,
    LabelCaseAction Action,
    string Actor,
    string? LocalIdentity,
    string Reason,
    DateTimeOffset CreatedAt,
    ModerationCaseData Case)
{
    /// <summary>Projects primitive event fields without exposing authentication evidence.</summary>
    internal LabelCaseReceipt ToReceipt(bool isOzoneEvent = false) => new(
        sequence: Sequence,
        operationId: OperationId,
        action: Action,
        actor: new Did(Actor),
        localIdentity: LocalIdentity,
        reason: Reason,
        createdAt: CreatedAt,
        moderationCase: Case.ToCase())
    {
        IsOzoneEvent = isOzoneEvent
    };
}

/// <summary>Retains a complete immutable policy independently of JSON implementation types.</summary>
internal sealed record ModerationPolicyData(
    string Version,
    string Declaration,
    bool AllowAccounts,
    bool AllowRecords,
    string Rules,
    string[]? Collections)
{
    /// <summary>Copies the immutable public policy into its storage representation.</summary>
    internal static ModerationPolicyData FromPolicy(LabelPolicyDefinition policy) => new(
        Version: policy.Issuance.Version,
        Declaration: policy.DeclarationJson,
        AllowAccounts: policy.Issuance.AllowAccounts,
        AllowRecords: policy.Issuance.AllowRecords,
        Rules: policy.Rules,
        Collections: policy.Issuance.AllowedRecordCollections?.Select(collection => collection.ToString()).ToArray());

    /// <summary>Revalidates persisted permissions and declaration before use.</summary>
    internal LabelPolicyDefinition ToPolicy()
    {
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(Declaration);
        return new(
            version: Version,
            declaration: document.RootElement,
            allowAccounts: AllowAccounts,
            allowRecords: AllowRecords,
            rules: Rules,
            allowedRecordCollections: Collections?.Select(value => new Nsid(value)).ToArray());
    }
}

/// <summary>Retains the durable selected policy and database-clock verification deadline.</summary>
internal sealed record ModerationPolicyGate(
    string Issuer,
    long Revision,
    LabelPolicyPhase Phase,
    ModerationPolicyData? Policy,
    string? PreviousCid,
    string? VerifiedCid,
    long? VerifiedUntil,
    long FreshnessMilliseconds,
    string? PublicationOperationId = null,
    ModerationPolicyWithdrawal? Withdrawal = null)
{
    /// <summary>Projects a gate using the current database clock rather than an application clock.</summary>
    internal LabelPolicyState ToState(long now) => new(
        issuer: new(Issuer),
        revision: Revision,
        phase: Phase,
        policy: Policy?.ToPolicy(),
        previousCid: PreviousCid is null ? null : new(PreviousCid),
        verifiedCid: VerifiedCid is null ? null : new(VerifiedCid),
        verifiedUntil: VerifiedUntil is { } deadline ? DateTimeOffset.FromUnixTimeMilliseconds(deadline) : null,
        verificationFreshness: TimeSpan.FromMilliseconds(FreshnessMilliseconds),
        databaseTime: DateTimeOffset.FromUnixTimeMilliseconds(now),
        publicationOperationId: PublicationOperationId,
        withdrawal: Withdrawal is null ? null : new(
            Values: Withdrawal.Values,
            UpperBound: Withdrawal.UpperBound,
            ScannedThrough: Withdrawal.ScannedThrough,
            NegatedThrough: Withdrawal.NegatedThrough,
            NegationSequence: Withdrawal.NegationSequence,
            PauseReason: Withdrawal.PauseReason,
            LastActivityAt: Withdrawal.LastActivityAt,
            LastProgressAt: Withdrawal.LastProgressAt,
            PublishedThrough: Withdrawal.PublishedThrough));
}

/// <summary>Retains a publication's frozen scan, superseding timestamp and revocable caller evidence across host restarts.</summary>
internal sealed record ModerationPolicyWithdrawal(
    string[] Values,
    long UpperBound,
    long ScannedThrough,
    long NegatedThrough,
    long NegationSequence,
    DateTimeOffset CreatedAt,
    string Actor,
    long Generation,
    string Credential,
    string? LocalIdentity,
    DateTimeOffset ExpiresAt,
    string Reason,
    string? PauseReason = null,
    DateTimeOffset? LastActivityAt = null,
    DateTimeOffset? LastProgressAt = null,
    long? PublishedThrough = null);

/// <summary>Retains immutable publication intent and the historical result for uncertain-commit recovery.</summary>
internal sealed record ModerationPolicyOperation(
    string Action,
    string Actor,
    string Version,
    string Reason,
    long ExpectedRevision,
    string? PreviousCid,
    ModerationPolicyData? Draft,
    ModerationPolicyGate? Gate);

/// <summary>Retains administration and authoritative observations independently of the mutable current gate.</summary>
internal sealed record ModerationPolicyHistory(
    long Sequence,
    string? OperationId,
    LabelPolicyHistoryAction Action,
    string? Actor,
    string Reason,
    long CreatedAt,
    ModerationPolicyData? Draft,
    ModerationPolicyGate? Gate,
    string? ObservedCid,
    string? ObservedDeclaration)
{
    /// <summary>Projects historical permission at its original append time, never as current authorization.</summary>
    internal LabelPolicyHistoryEntry ToEntry() => new(
        sequence: Sequence,
        operationId: OperationId,
        action: Action,
        actor: Actor is null ? null : new(Actor),
        reason: Reason,
        createdAt: DateTimeOffset.FromUnixTimeMilliseconds(CreatedAt),
        policy: (Draft ?? Gate?.Policy)?.ToPolicy(),
        state: Gate?.ToState(CreatedAt),
        observedCid: ObservedCid is null ? null : new(ObservedCid),
        observedDeclarationJson: ObservedDeclaration);
}

/// <summary>Retains ordered action intent without serializing protocol label implementation types.</summary>
internal sealed record ModerationDecisionAction(LabelOperationKind Kind, string Value, DateTimeOffset? ExpiresAt, long? ExpectedLabelSequence);

/// <summary>Retains complete same-intent case decision inputs independently of prepared signatures.</summary>
internal sealed record ModerationDecisionIntent(
    string Actor,
    long CaseId,
    long ExpectedRevision,
    string PolicyVersion,
    string Reason,
    ModerationDecisionAction[] Actions,
    long[] AppealIds,
    LabelAppealStatus? AppealOutcome,
    long? ReportId = null,
    bool HandleOtherReports = false,
    bool HandleReportsAcrossRecordVersions = false)
{
    /// <summary>Copies immutable request intent before entering the provider transaction.</summary>
    internal static ModerationDecisionIntent FromDecision(Did actor, LabelCaseDecision decision) => new(
        actor.Value,
        decision.CaseId,
        decision.ExpectedRevision,
        decision.PolicyVersion,
        decision.Reason,
        [.. decision.Actions.Select(action => new ModerationDecisionAction(
            action.Kind, action.Value, action.ExpiresAt, action.ExpectedLabelSequence))],
        [.. decision.AppealIds],
        decision.AppealOutcome,
        decision.ReportId,
        decision.HandleOtherReports,
        decision.HandleReportsAcrossRecordVersions);

    /// <summary>Revalidates retained semantic intent when recovering a historical receipt.</summary>
    internal LabelCaseDecision ToDecision(string operationId) => new(
        operationId: operationId,
        caseId: CaseId,
        expectedRevision: ExpectedRevision,
        policyVersion: PolicyVersion,
        reason: Reason,
        actions: [.. Actions.Select(action => new LabelCaseLabelAction(
            kind: action.Kind,
            value: action.Value,
            expiresAt: action.ExpiresAt,
            expectedLabelSequence: action.ExpectedLabelSequence))],
        appealIds: AppealIds,
        appealOutcome: AppealOutcome,
        reportId: ReportId,
        handleOtherReports: HandleOtherReports,
        handleReportsAcrossRecordVersions: HandleReportsAcrossRecordVersions);
}

/// <summary>Retains the atomic case event and original signed receipt encodings without reconstructing signatures.</summary>
internal sealed record ModerationDecisionData(
    string OperationId,
    ModerationDecisionIntent Intent,
    ModerationCaseEvent CaseEvent,
    byte[][] LabelReceipts,
    LabelStoreState StateAtCommit)
{
    /// <summary>Restores the exact historical outcome for authorized recovery.</summary>
    internal LabelCaseDecisionReceipt ToReceipt() => new(
        request: Intent.ToDecision(OperationId),
        actor: new(Intent.Actor),
        caseEvent: CaseEvent.ToReceipt(),
        labels: [.. LabelReceipts.Select(DecisionReceiptEncoding.Decode)],
        stateAtCommit: StateAtCommit);
}

/// <summary>Retains the exact signed stream outcome challenged at the intake boundary.</summary>
internal sealed record ModerationAppealChallenge(long Sequence, byte[] SignedJson);

/// <summary>Retains original appeal capture independently of subsequent label decisions or report retries.</summary>
internal sealed record ModerationAppealData(
    long Id,
    long CaseId,
    string Initiator,
    string Subject,
    string? Cid,
    ModerationAppealChallenge[] Challenges,
    LabelAppealStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? DecisionOperationId,
    string? ResolutionReason = null)
{
    /// <summary>Restores immutable challenge signatures without consulting current label state.</summary>
    internal LabelAppeal ToAppeal() => new(
        id: Id,
        caseId: CaseId,
        initiator: new(Initiator),
        subject: ModerationSubjectEncoding.Decode(Subject, Cid),
        challenges: [.. Challenges.Select(challenge => new StoredLabel(challenge.Sequence, new(challenge.SignedJson)))],
        status: Status,
        createdAt: CreatedAt,
        updatedAt: UpdatedAt,
        decisionOperationId: DecisionOperationId,
        resolutionReason: ResolutionReason);
}

/// <summary>Retains submitted report content and authentication provenance without retaining bearer tokens.</summary>
internal sealed record ModerationReportData(
    long Id,
    long CaseId,
    string Subject,
    string? Cid,
    string ReasonType,
    string? Reason,
    string? ToolName,
    string? ToolMetadata,
    string Reporter,
    DateTimeOffset CreatedAt,
    string PolicyVersion,
    string Audience,
    string TokenHash,
    string? DecisionOperationId = null,
    DateTimeOffset? HandledAt = null,
    string? ResolutionReason = null,
    string? AppealLabelValue = null,
    LabelRecordResolution? RecordResolution = null)
{
    /// <summary>Revalidates a retained report and defensively clones arbitrary tool metadata.</summary>
    internal LabelModerationReport ToReport()
    {
        System.Text.Json.JsonElement? metadata = null;
        if (ToolMetadata is not null)
        {
            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(ToolMetadata);
            metadata = document.RootElement.Clone();
        }

        return new(
            id: Id,
            caseId: CaseId,
            submission: new(
                subject: ModerationSubjectEncoding.Decode(Subject, Cid),
                reasonType: ReasonType,
                reason: Reason,
                modToolName: ToolName,
                modToolMetadata: metadata,
                appealLabelValue: AppealLabelValue)
            {
                RecordResolution = RecordResolution
            },
            reportedBy: new(Reporter),
            createdAt: CreatedAt,
            policyVersion: PolicyVersion,
            audience: Audience,
            decisionOperationId: DecisionOperationId,
            handledAt: HandledAt,
            resolutionReason: ResolutionReason);
    }
}

/// <summary>Retains an authenticated report before policy scope can be established.</summary>
internal sealed record PendingModerationReportData(
    long Id,
    string Subject,
    string? Cid,
    string ReasonType,
    string? Reason,
    string? ToolName,
    string? ToolMetadata,
    string Reporter,
    DateTimeOffset CreatedAt,
    string Audience,
    string TokenHash,
    DateTimeOffset ExpiresAt,
    ModerationAppealChallenge[]? AppealChallenges = null,
    string? AppealLabelValue = null,
    bool PendingVerification = true,
    string? PolicyVersion = null,
    DateTimeOffset? ProcessedAt = null,
    string? ResolutionReason = null,
    bool ReporterMuted = false,
    LabelRecordResolution? RecordResolution = null)
{
    /// <summary>Revalidates the bounded report intent before exposing it to an authorized moderator.</summary>
    internal LabelPendingModerationReport ToPendingReport()
    {
        System.Text.Json.JsonElement? metadata = null;
        if (ToolMetadata is not null)
        {
            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(ToolMetadata);
            metadata = document.RootElement.Clone();
        }

        return new(
            Id: Id,
            Submission: new(
                subject: ModerationSubjectEncoding.Decode(Subject, Cid),
                reasonType: ReasonType,
                reason: Reason,
                modToolName: ToolName,
                modToolMetadata: metadata,
                appealLabelValue: AppealLabelValue)
            {
                RecordResolution = RecordResolution
            },
            ReportedBy: new(Reporter),
            CreatedAt: CreatedAt,
            PendingVerification: PendingVerification,
            PolicyVersion: PolicyVersion,
            ProcessedAt: ProcessedAt,
            ResolutionReason: ResolutionReason,
            ReporterMuted: ReporterMuted);
    }

    /// <summary>Copies the pending submission into the ordinary report representation for verified promotion.</summary>
    internal ModerationReportData ToReport(long caseId, string policyVersion) => new(
        Id: Id,
        CaseId: caseId,
        Subject: Subject,
        Cid: Cid,
        ReasonType: ReasonType,
        Reason: Reason,
        ToolName: ToolName,
        ToolMetadata: ToolMetadata,
        Reporter: Reporter,
        CreatedAt: CreatedAt,
        PolicyVersion: policyVersion,
        Audience: Audience,
        TokenHash: TokenHash,
        AppealLabelValue: AppealLabelValue,
        RecordResolution: RecordResolution);
}

/// <summary>Supplies trimming-safe metadata for moderation state, retry intent and history.</summary>
[JsonSerializable(typeof(ModerationCaseData))]
[JsonSerializable(typeof(ModerationCaseIntent))]
[JsonSerializable(typeof(ModerationCaseEvent))]
[JsonSerializable(typeof(ModerationPolicyData))]
[JsonSerializable(typeof(ModerationPolicyGate))]
[JsonSerializable(typeof(ModerationPolicyWithdrawal))]
[JsonSerializable(typeof(ModerationPolicyOperation))]
[JsonSerializable(typeof(ModerationPolicyHistory))]
[JsonSerializable(typeof(ModerationReportData))]
[JsonSerializable(typeof(PendingModerationReportData))]
[JsonSerializable(typeof(ModerationDecisionIntent))]
[JsonSerializable(typeof(ModerationDecisionData))]
[JsonSerializable(typeof(ModerationAppealData))]
internal sealed partial class ModerationJsonContext : JsonSerializerContext;
