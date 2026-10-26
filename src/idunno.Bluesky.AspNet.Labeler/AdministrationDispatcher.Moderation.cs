// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.AspNet.Labeler;

internal sealed partial class AdministrationDispatcher
{
    /// <summary>Shares live team authorization, immutable receipts and declaration verification across both adapters.</summary>
    private async Task<AdministrationResponse> DispatchModerationAsync(
        LabelAdministrationCaller caller,
        AdministrationCommand command,
        CancellationToken token)
    {
        if (policy is not LabelPolicyService verifier || store is not ILabelModerationStore cases ||
            store is not ILabelReportStore reports || store is not ILabelPolicyStore policies || store is not ILabelAppealStore appeals)
        {
            return new(false, "ModerationDisabled", "Xenon moderation is not enabled.");
        }

        ModerationCommand input = command.Moderation ?? new();
        switch (command.Method)
        {
            case "getPolicyVersion":
                {
                    LabelServiceResult<LabelPolicyDefinition> result = await policies.GetPolicyVersionAsync(
                        caller, input.PolicyVersion ?? throw new ArgumentException("A policy version is required."), token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Moderation: new(Definition: DefinitionView(result.Result))) : Failure(result.Error);
                }
            case "getPolicyVersions":
                {
                    LabelServiceResult<IReadOnlyList<LabelPolicyDefinition>> result = await policies.GetPolicyVersionsAsync(
                        caller: caller,
                        afterVersion: input.AfterVersion,
                        limit: command.Limit,
                        cancellationToken: token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Moderation: new(Definitions: [.. result.Result.Select(DefinitionView)])) : Failure(result.Error);
                }
            case "getPolicyHistory":
                {
                    LabelServiceResult<IReadOnlyList<LabelPolicyHistoryEntry>> result = await policies.GetPolicyHistoryAsync(
                        caller: caller,
                        afterSequence: command.AfterSequence,
                        limit: command.Limit,
                        cancellationToken: token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Moderation: new(PolicyHistory: [.. result.Result.Select(PolicyHistoryView)])) : Failure(result.Error);
                }
            case "getPolicyOperation":
                {
                    LabelServiceResult<LabelPolicyHistoryEntry> result = await policies.GetPolicyOperationAsync(caller, command.OperationId, token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Moderation: new(PolicyHistory: [PolicyHistoryView(result.Result)])) : Failure(result.Error);
                }
            case "getCaseQueue":
                {
                    LabelCaseQuery query = new(
                        queue: input.Queue,
                        subject: input.Subject is { } subject ? ModerationWire.ParseSubject(subject) : null,
                        reasonType: input.ReasonType,
                        assignee: input.Assignee is null ? null : new(input.Assignee));
                    LabelServiceResult<LabelCasePage> result = await cases.GetCaseQueueAsync(
                        caller: caller,
                        query: query,
                        cursor: input.Cursor,
                        limit: command.Limit,
                        cancellationToken: token).ConfigureAwait(false);
                    return result.Succeeded
                        ? new(true, Moderation: new(Cases: [.. result.Result.Cases.Select(CaseView)], Cursor: result.Result.Cursor))
                        : Failure(result.Error);
                }
            case "getCase":
                {
                    LabelServiceResult<LabelModerationCase> result = await cases.GetCaseAsync(caller, input.CaseId, token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Moderation: new(Case: CaseView(result.Result))) : Failure(result.Error);
                }
            case "getCases":
                {
                    LabelServiceResult<IReadOnlyList<LabelModerationCase>> result = await cases.GetCasesAsync(
                        caller: caller, status: input.Status, afterId: command.AfterSequence, limit: command.Limit, cancellationToken: token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Moderation: new(Cases: [.. result.Result.Select(CaseView)])) : Failure(result.Error);
                }
            case "getCaseHistory":
                {
                    LabelServiceResult<IReadOnlyList<LabelCaseReceipt>> result = await cases.GetCaseHistoryAsync(
                        caller: caller, caseId: input.CaseId, afterSequence: command.AfterSequence, limit: command.Limit, cancellationToken: token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Moderation: new(History: [.. result.Result.Select(CaseEventView)])) : Failure(result.Error);
                }
            case "getCaseOperation":
                {
                    LabelServiceResult<LabelCaseReceipt> result = await cases.GetCaseOperationAsync(caller, command.OperationId, token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Moderation: new(Event: CaseEventView(result.Result))) : Failure(result.Error);
                }
            case "getCaseDecision":
                {
                    LabelServiceResult<LabelCaseDecisionReceipt> result = await cases.GetCaseDecisionAsync(
                        caller, command.OperationId, token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Moderation: new(Decision: DecisionView(result.Result))) : Failure(result.Error);
                }
            case "getAppeal":
                {
                    LabelServiceResult<LabelAppeal> result = await appeals.GetAppealAsync(caller, input.AppealId, token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Moderation: new(Appeals: [AppealView(result.Result)])) : Failure(result.Error);
                }
            case "getAppeals":
                {
                    LabelServiceResult<IReadOnlyList<LabelAppeal>> result = await appeals.GetAppealsAsync(
                        caller: caller,
                        caseId: input.CaseId == 0 ? null : input.CaseId,
                        status: input.AppealStatus,
                        afterId: command.AfterSequence,
                        limit: command.Limit,
                        cancellationToken: token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Moderation: new(Appeals: [.. result.Result.Select(AppealView)])) : Failure(result.Error);
                }
            case "getAppealReports":
                {
                    LabelServiceResult<IReadOnlyList<long>> result = await appeals.GetAppealReportsAsync(
                        caller: caller,
                        appealId: input.AppealId,
                        afterReportId: command.AfterSequence,
                        limit: command.Limit,
                        cancellationToken: token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Moderation: new(ReportIds: [.. result.Result])) : Failure(result.Error);
                }
            case "decideCase":
                {
                    if (input.PolicyVersion is null || input.Actions is null ||
                        input.Actions.Any(action => action is null || action.Value is null))
                    {
                        return new(false, "InvalidRequest", "Supply a policy version and an explicit bounded action list.");
                    }

                    LabelCaseDecision request = new(
                        operationId: command.OperationId,
                        caseId: input.CaseId,
                        expectedRevision: input.ExpectedRevision,
                        policyVersion: input.PolicyVersion,
                        reason: command.Reason,
                        actions: input.Actions.Select(action => new LabelCaseLabelAction(
                            kind: action.Kind,
                            value: action.Value,
                            expiresAt: action.ExpiresAt,
                            expectedLabelSequence: action.ExpectedLabelSequence)).ToArray(),
                        appealIds: input.AppealIds,
                        appealOutcome: input.AppealOutcome);
                    LabelServiceResult<LabelCaseDecisionReceipt> result;
                    if (request.Actions.Count == 0)
                    {
                        LabelServiceResult<LabelPolicyState> gate = await policies.GetPolicyStateAsync(caller, token).ConfigureAwait(false);
                        if (!gate.Succeeded)
                        {
                            return Failure(gate.Error);
                        }

                        result = await cases.CommitCaseDecisionAsync(
                            new(
                                caller: caller,
                                request: request,
                                expectedPolicyRevision: gate.Result.Revision,
                                preparedActions: []),
                            token).ConfigureAwait(false);
                    }
                    else
                    {
                        LabelIssuanceService issuance = new(
                            issuer: caller.Issuer,
                            signingKey: signingKeyRing.GetSigningKey(signingKeyRing.ActiveKeyId),
                            store: store,
                            authorizer: new LabelTeamAuthorizer(administration, caller),
                            policyProvider: policy,
                            httpClient: httpClient,
                            timeProvider: clock);
                        result = await issuance.ExecuteCaseDecisionAsync(caller, request, token).ConfigureAwait(false);
                    }

                    return result.Succeeded ? new(true, Moderation: new(Decision: DecisionView(result.Result))) : Failure(result.Error);
                }
            case "changeCase":
                {
                    if (input.Subject is not { } subject || input.Action is not { } action)
                    {
                        return new(false, "InvalidRequest", "An explicit subject and case action are required.");
                    }

                    LabelServiceResult<LabelCaseReceipt> result = await cases.ChangeCaseAsync(
                        caller: caller,
                        change: new(
                            operationId: command.OperationId, subject: ModerationWire.ParseSubject(subject),
                            action: action, reason: command.Reason, caseId: input.CaseId, expectedRevision: input.ExpectedRevision,
                            assignee: input.Assignee is null ? null : new(input.Assignee)),
                        cancellationToken: token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Moderation: new(Event: CaseEventView(result.Result))) : Failure(result.Error);
                }
            case "getReport":
                {
                    LabelServiceResult<LabelModerationReport> result = await reports.GetReportAsync(caller, input.ReportId, token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Moderation: new(Reports: [ReportView(result.Result)])) : Failure(result.Error);
                }
            case "getReports":
                {
                    LabelServiceResult<IReadOnlyList<LabelModerationReport>> result = await reports.GetReportsAsync(
                        caller: caller, caseId: input.CaseId == 0 ? null : input.CaseId,
                        afterId: command.AfterSequence, limit: command.Limit, cancellationToken: token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Moderation: new(Reports: [.. result.Result.Select(ReportView)])) : Failure(result.Error);
                }
            case "getPolicy":
                {
                    LabelServiceResult<LabelPolicyState> result = await policies.GetPolicyStateAsync(caller, token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Moderation: new(Policy: PolicyView(result.Result))) : Failure(result.Error);
                }
            case "savePolicy":
                {
                    if (input.Declaration is not { } declaration || input.PolicyVersion is null || input.Rules is null)
                    {
                        return new(false, "InvalidRequest", "A complete versioned declaration and explanatory rules are required.");
                    }

                    LabelPolicyDefinition draft;
                    try
                    {
                        draft = new(
                            version: input.PolicyVersion, declaration: declaration, allowAccounts: input.AllowAccounts,
                            allowRecords: input.AllowRecords, rules: input.Rules,
                            allowedRecordCollections: input.AllowedRecordCollections?.Select(value => new Nsid(value)).ToArray());
                    }
                    catch (ArgumentException)
                    {
                        return new(false, "InvalidRequest", "The policy declaration, permissions or rules are invalid.");
                    }

                    LabelServiceResult<LabelPolicyDefinition> result = await policies.SavePolicyAsync(
                        caller: caller, operationId: command.OperationId, policy: draft, reason: command.Reason, cancellationToken: token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Moderation: new(Definition: DefinitionView(result.Result))) : Failure(result.Error);
                }
            case "beginPolicyPublication":
                {
                    if (input.PolicyVersion is null)
                    {
                        return new(false, "InvalidRequest", "A retained policy version is required.");
                    }

                    LabelServiceResult<LabelPolicyState> result = await policies.BeginPolicyPublicationAsync(
                        caller: caller, operationId: command.OperationId, version: input.PolicyVersion,
                        expectedRevision: input.ExpectedRevision, previousCid: input.PreviousCid is null ? null : new(input.PreviousCid),
                        reason: command.Reason, cancellationToken: token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Moderation: new(Policy: PolicyView(result.Result))) : Failure(result.Error);
                }
            case "verifyPolicy":
                {
                    LabelServiceResult<LabelAuthorizationGrant> admission = await administration.AuthorizeAsync(
                        caller: caller, permission: LabelAdministrationPermission.ManagePolicy, reason: command.Reason, cancellationToken: token).ConfigureAwait(false);
                    if (!admission.Succeeded)
                    {
                        return Failure(admission.Error);
                    }

                    LabelServiceResult<LabelPolicyState> observed = await verifier.GetVerifiedStateAsync(
                        forceVerification: true, cancellationToken: token).ConfigureAwait(false);
                    LabelServiceResult<LabelAuthorizationGrant> authorized = await administration.AuthorizeAsync(
                        caller: caller, permission: LabelAdministrationPermission.ManagePolicy, reason: command.Reason, cancellationToken: token).ConfigureAwait(false);
                    if (!authorized.Succeeded)
                    {
                        return Failure(authorized.Error);
                    }

                    return observed.Succeeded ? new(true, Moderation: new(Policy: PolicyView(observed.Result))) : Failure(observed.Error);
                }
            case "abandonPolicyPublication":
                {
                    LabelServiceResult<LabelPolicyState> result = await policies.AbandonPolicyPublicationAsync(
                        caller: caller,
                        operationId: command.OperationId,
                        expectedRevision: input.ExpectedRevision,
                        reason: command.Reason,
                        cancellationToken: token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Moderation: new(Policy: PolicyView(result.Result))) : Failure(result.Error);
                }
            default:
                return new(false, "InvalidRequest", "The moderation method is unknown.");
        }
    }

    /// <summary>Projects exact case identity and immutable workflow provenance.</summary>
    private static ModerationCaseView CaseView(LabelModerationCase value) => new(
        value.Id, value.Revision, ModerationWire.SubjectView(value.Subject), value.Status.ToString(),
        value.Assignee?.Value, value.CreatedAt, value.UpdatedAt);

    /// <summary>Projects immutable case operation outcomes independently of current mutable workflow state.</summary>
    private static ModerationCaseEventView CaseEventView(LabelCaseReceipt value) => new(
        value.Sequence, value.OperationId, value.Action.ToString(), value.Actor.Value,
        value.LocalIdentity, value.Reason, value.CreatedAt, CaseView(value.Case));

    /// <summary>Projects complete atomic decision provenance and original signed outcomes for protected recovery.</summary>
    private static ModerationDecisionView DecisionView(LabelCaseDecisionReceipt value) => new(
        OperationId: value.Request.OperationId,
        Actor: value.Actor.Value,
        Reason: value.Request.Reason,
        PolicyVersion: value.Request.PolicyVersion,
        Event: CaseEventView(value.CaseEvent),
        Actions: value.Labels.Select(label =>
        {
            JsonElement? signed = null;
            if (label.Label is { } outcome)
            {
                using JsonDocument document = JsonDocument.Parse(outcome.SignedLabel.GetJsonBytes());
                signed = document.RootElement.Clone();
            }

            return new ModerationLabelOutcomeView(
                label.Request.OperationId, label.Request.Kind.ToString(), label.Request.Value,
                label.Changed, label.Label?.Sequence, signed);
        }).ToArray(),
        State: PublicationView(value.StateAtCommit),
        AppealIds: [.. value.Request.AppealIds],
        AppealOutcome: value.Request.AppealOutcome?.ToString());

    /// <summary>Projects original signed challenges without replacing them with current label history.</summary>
    private static ModerationAppealView AppealView(LabelAppeal value) => new(
        Id: value.Id,
        CaseId: value.CaseId,
        Initiator: value.Initiator.Value,
        Subject: ModerationWire.SubjectView(value.Subject),
        Challenges: value.Challenges.Select(challenge =>
        {
            using JsonDocument document = JsonDocument.Parse(challenge.SignedLabel.GetJsonBytes());
            return new ModerationChallengeView(challenge.Sequence, document.RootElement.Clone());
        }).ToArray(),
        Status: value.Status.ToString(),
        CreatedAt: value.CreatedAt,
        UpdatedAt: value.UpdatedAt,
        DecisionOperationId: value.DecisionOperationId);

    /// <summary>Projects confidential report metadata only after live protected inspection.</summary>
    private static ModerationReportView ReportView(LabelModerationReport value) => new(
        value.CaseId, ModerationWire.ReportOutput(value), value.PolicyVersion, value.Audience,
        value.Submission.ModToolName, value.Submission.ModToolMetadata);

    /// <summary>Projects complete versioned policy intent without repository credentials.</summary>
    private static ModerationPolicyDefinitionView DefinitionView(LabelPolicyDefinition value)
    {
        using JsonDocument declaration = JsonDocument.Parse(value.DeclarationJson);
        return new(
            Version: value.Issuance.Version, Declaration: declaration.RootElement.Clone(),
            AllowAccounts: value.Issuance.AllowAccounts, AllowRecords: value.Issuance.AllowRecords,
            AllowedRecordCollections: value.Issuance.AllowedRecordCollections?.Select(collection => collection.ToString()).ToArray(), Rules: value.Rules);
    }

    /// <summary>Projects the durable publication gate and independently verified freshness.</summary>
    private static ModerationPolicyView PolicyView(LabelPolicyState value) => new(
        value.Revision, value.Phase.ToString(), value.Policy is null ? null : DefinitionView(value.Policy),
        value.PreviousCid?.ToString(), value.VerifiedCid?.ToString(), value.VerifiedUntil,
        checked((long)value.VerificationFreshness.TotalMilliseconds), value.DatabaseTime,
        value.Issuer.Value, value.PublicationOperationId);

    /// <summary>Projects historical policy intent and observations without claiming that old freshness grants current permission.</summary>
    private static ModerationPolicyHistoryView PolicyHistoryView(LabelPolicyHistoryEntry value)
    {
        JsonElement? observed = null;
        if (value.ObservedDeclarationJson is { } json)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            observed = document.RootElement.Clone();
        }

        return new(
            Sequence: value.Sequence,
            OperationId: value.OperationId,
            Action: value.Action.ToString(),
            Actor: value.Actor?.Value,
            Reason: value.Reason,
            CreatedAt: value.CreatedAt,
            Definition: value.Policy is null ? null : DefinitionView(value.Policy),
            State: value.State is null ? null : PolicyView(value.State),
            ObservedCid: value.ObservedCid?.ToString(),
            ObservedDeclaration: observed);
    }
}

/// <summary>Defines bounded moderation commands without any asserted caller identity.</summary>
internal sealed record ModerationCommand(
    long CaseId = 0,
    long ReportId = 0,
    long ExpectedRevision = 0,
    LabelCaseAction? Action = null,
    JsonElement? Subject = null,
    string? Assignee = null,
    LabelCaseStatus? Status = null,
    string? PolicyVersion = null,
    JsonElement? Declaration = null,
    bool AllowAccounts = false,
    bool AllowRecords = false,
    string[]? AllowedRecordCollections = null,
    string? Rules = null,
    string? PreviousCid = null,
    ModerationLabelActionInput[]? Actions = null,
    long AppealId = 0,
    LabelAppealStatus? AppealStatus = null,
    long[]? AppealIds = null,
    LabelAppealStatus? AppealOutcome = null,
    LabelCaseWorkflow Queue = LabelCaseWorkflow.Unresolved,
    string? ReasonType = null,
    string? Cursor = null,
    string? AfterVersion = null);

/// <summary>Defines one bounded label-slot action without accepting target or actor overrides.</summary>
internal sealed record ModerationLabelActionInput(LabelOperationKind Kind, string Value, DateTimeOffset? ExpiresAt = null, long? ExpectedLabelSequence = null);

/// <summary>Defines protected moderation responses separately from public report protocol output.</summary>
internal sealed record ModerationResponse(
    ModerationCaseView? Case = null,
    ModerationCaseView[]? Cases = null,
    ModerationCaseEventView? Event = null,
    ModerationCaseEventView[]? History = null,
    ModerationReportView[]? Reports = null,
    ModerationPolicyView? Policy = null,
    ModerationPolicyDefinitionView? Definition = null,
    ModerationDecisionView? Decision = null,
    ModerationAppealView[]? Appeals = null,
    long[]? ReportIds = null,
    string? Cursor = null,
    ModerationPolicyDefinitionView[]? Definitions = null,
    ModerationPolicyHistoryView[]? PolicyHistory = null);

/// <summary>Contains immutable policy publication and observation history for protected recovery.</summary>
internal sealed record ModerationPolicyHistoryView(
    long Sequence,
    string? OperationId,
    string Action,
    string? Actor,
    string Reason,
    DateTimeOffset CreatedAt,
    ModerationPolicyDefinitionView? Definition,
    ModerationPolicyView? State,
    string? ObservedCid,
    JsonElement? ObservedDeclaration);

/// <summary>Contains an atomic case decision and its exact action outcomes for protected recovery.</summary>
internal sealed record ModerationDecisionView(
    string OperationId,
    string Actor,
    string Reason,
    string PolicyVersion,
    ModerationCaseEventView Event,
    ModerationLabelOutcomeView[] Actions,
    AdministrationPublicationView State,
    long[] AppealIds,
    string? AppealOutcome);
/// <summary>Contains protected immutable challenge provenance and the latest explicit review outcome.</summary>
internal sealed record ModerationAppealView(
    long Id,
    long CaseId,
    string Initiator,
    ReportSubjectView Subject,
    ModerationChallengeView[] Challenges,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? DecisionOperationId);
/// <summary>Contains the original signed stream outcome captured when an appeal was accepted.</summary>
internal sealed record ModerationChallengeView(long Sequence, JsonElement Label);
/// <summary>Contains original signed or no-op label provenance without internal diagnostics.</summary>
internal sealed record ModerationLabelOutcomeView(string OperationId, string Kind, string Value, bool Changed, long? Sequence, JsonElement? Label);

/// <summary>Contains a protected exact-subject workflow snapshot.</summary>
internal sealed record ModerationCaseView(long Id, long Revision, ReportSubjectView Subject, string Status, string? Assignee, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
/// <summary>Contains a protected immutable case event and its historical outcome.</summary>
internal sealed record ModerationCaseEventView(long Sequence, string OperationId, string Action, string Actor, string? LocalIdentity, string Reason, DateTimeOffset CreatedAt, ModerationCaseView Case);
/// <summary>Contains confidential report linkage and acceptance provenance.</summary>
internal sealed record ModerationReportView(long CaseId, ReportOutput Report, string PolicyVersion, string Audience, string? ModToolName, JsonElement? ModToolMetadata);
/// <summary>Contains a complete selected policy definition for explicit publication or inspection.</summary>
internal sealed record ModerationPolicyDefinitionView(string Version, JsonElement Declaration, bool AllowAccounts, bool AllowRecords, string[]? AllowedRecordCollections, string Rules);
/// <summary>Contains the shared durable gate, observation deadline and conditional-update CID.</summary>
internal sealed record ModerationPolicyView(long Revision, string Phase, ModerationPolicyDefinitionView? Definition, string? PreviousCid, string? VerifiedCid, DateTimeOffset? VerifiedUntil, long VerificationFreshnessMilliseconds, DateTimeOffset DatabaseTime, string Issuer, string? PublicationOperationId);
