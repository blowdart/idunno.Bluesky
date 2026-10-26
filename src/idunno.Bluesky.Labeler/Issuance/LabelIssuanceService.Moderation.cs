// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Issuance;

public sealed partial class LabelIssuanceService
{
    /// <summary>Prepares and atomically commits a reasoned case decision through the shared issuance rules.</summary>
    /// <param name="caller">The provider-issued moderator.</param>
    /// <param name="request">The complete immutable case decision.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The original or newly committed atomic receipt, or a classified failure.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The configured service issuer differs from the bound store.</exception>
    /// <remarks><para>Same-intent recovery precedes resolution, policy lookup and signing. No individual
    /// action is committed during preparation. Hosts must include label-bearing calls in their existing
    /// coordinated maintenance admission and drain accounting.</para></remarks>
    public async Task<LabelServiceResult<LabelCaseDecisionReceipt>> ExecuteCaseDecisionAsync(
        LabelAdministrationCaller caller,
        LabelCaseDecision request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        LabelServiceResult<LabelCaseDecisionReceipt> result = await ExecuteCaseDecisionCoreAsync(
            caller, request, cancellationToken).ConfigureAwait(false);
        return Report(result, "case-decision");
    }

    /// <summary>Recovers original intent or prepares every action before submitting one provider transaction.</summary>
    private async Task<LabelServiceResult<LabelCaseDecisionReceipt>> ExecuteCaseDecisionCoreAsync(
        LabelAdministrationCaller caller,
        LabelCaseDecision request,
        CancellationToken token)
    {
        if (_store is not ILabelModerationStore cases || _store is not ILabelPolicyStore policies ||
            _store is not ILabelAdministrationStore administration)
        {
            return Failure<LabelCaseDecisionReceipt>(LabelServiceErrorCode.PolicyUnavailable, "Shared moderation storage is required.");
        }

        LabelServiceResult<LabelAuthorizationGrant> authority = await administration.AuthorizeAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Moderate,
            reason: request.Reason,
            cancellationToken: token).ConfigureAwait(false);
        if (!authority.Succeeded)
        {
            return LabelServiceResult<LabelCaseDecisionReceipt>.Failure(authority.Error);
        }

        LabelServiceResult<LabelCaseDecisionReceipt> recovered = await cases.GetCaseDecisionAsync(
            caller, request.OperationId, token).ConfigureAwait(false);
        if (recovered.Succeeded)
        {
            CheckIssuer(recovered.Result.StateAtCommit);
            return SameCaseIntent(caller, request, recovered);
        }

        if (recovered.Error.Code != LabelServiceErrorCode.OperationNotFound)
        {
            return recovered;
        }

        LabelServiceResult<LabelModerationCase> current = await cases.GetCaseAsync(caller, request.CaseId, token).ConfigureAwait(false);
        if (!current.Succeeded)
        {
            return LabelServiceResult<LabelCaseDecisionReceipt>.Failure(current.Error);
        }

        if (current.Result.Revision != request.ExpectedRevision || current.Result.Status == LabelCaseStatus.Resolved)
        {
            return Failure<LabelCaseDecisionReceipt>(LabelServiceErrorCode.CaseConflict, "Reload the unresolved case before deciding it.");
        }

        if (request.Actions.Any(action => action.Kind == LabelOperationKind.Issue))
        {
            LabelServiceResult<LabelIssuancePolicy> verified = await _policyProvider.GetPolicyAsync(token).ConfigureAwait(false);
            if (!verified.Succeeded)
            {
                return LabelServiceResult<LabelCaseDecisionReceipt>.Failure(verified.Error);
            }
        }

        LabelServiceResult<LabelPolicyState> policy = await policies.GetPolicyStateAsync(caller, token).ConfigureAwait(false);
        if (!policy.Succeeded)
        {
            return LabelServiceResult<LabelCaseDecisionReceipt>.Failure(policy.Error);
        }

        if (policy.Result.Issuer != _issuer)
        {
            throw new InvalidOperationException("The configured service issuer differs from the bound policy store.");
        }

        if (policy.Result.Policy?.Issuance.Version != request.PolicyVersion)
        {
            return Failure<LabelCaseDecisionReceipt>(LabelServiceErrorCode.PolicyUnavailable, "The selected policy version differs from the requested decision.");
        }

        LabelReportSubject subject = current.Result.Subject;
        LabelTarget target = subject.RecordUri is { } record
            ? LabelTarget.Record(record, subject.Cid)
            : LabelTarget.Account(subject.Repository);
        LabelOperationContext context = new(caller.Actor, request.Reason);
        List<LabelDecisionCommit> preparations = [];
        for (int index = 0; index < request.Actions.Count; index++)
        {
            LabelCaseLabelAction action = request.Actions[index];
            string operationId = request.ActionOperationId(index);
            LabelServiceResult<LabelAuthorizationGrant> admission = await AuthorizeAsync(
                action: action.Kind == LabelOperationKind.Issue ? LabelAuthorizationAction.Issue : LabelAuthorizationAction.Negate,
                context: context,
                operationId: operationId,
                requested: target,
                resolved: null,
                value: action.Value,
                admission: true,
                token: token).ConfigureAwait(false);
            if (!admission.Succeeded)
            {
                return LabelServiceResult<LabelCaseDecisionReceipt>.Failure(admission.Error);
            }

            LabelOperationRequest label = action.Kind == LabelOperationKind.Issue
                ? LabelOperationRequest.Issue(
                    operationId: operationId, target: target, value: action.Value, context: context, expiresAt: action.ExpiresAt)
                : LabelOperationRequest.Negate(operationId: operationId, target: target, value: action.Value, context: context);
            LabelServiceResult<LabelDecisionCommit> prepared = await PrepareDecisionAsync(label, token).ConfigureAwait(false);
            if (!prepared.Succeeded)
            {
                return LabelServiceResult<LabelCaseDecisionReceipt>.Failure(prepared.Error);
            }

            preparations.Add(prepared.Result);
        }

        LabelServiceResult<LabelCaseDecisionReceipt> committed = await cases.CommitCaseDecisionAsync(
            new(
                caller: caller,
                request: request,
                expectedPolicyRevision: policy.Result.Revision,
                preparedActions: preparations),
            token).ConfigureAwait(false);
        if (committed.Succeeded)
        {
            CheckIssuer(committed.Result.StateAtCommit);
            return SameCaseIntent(caller, request, committed);
        }

        if (committed.Error.Code is LabelServiceErrorCode.OutcomeUnknown or LabelServiceErrorCode.StaleDecision &&
            !token.IsCancellationRequested)
        {
            try
            {
                recovered = await cases.GetCaseDecisionAsync(caller, request.OperationId, token).ConfigureAwait(false);
                if (recovered.Succeeded)
                {
                    CheckIssuer(recovered.Result.StateAtCommit);
                    return SameCaseIntent(caller, request, recovered);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return committed;
            }
        }

        return committed;
    }

    /// <summary>Rejects changed decision intent without regenerating any historical signature or target.</summary>
    private static LabelServiceResult<LabelCaseDecisionReceipt> SameCaseIntent(
        LabelAdministrationCaller caller,
        LabelCaseDecision request,
        LabelServiceResult<LabelCaseDecisionReceipt> receipt) =>
        caller.Actor == receipt.Result!.Actor && request.SemanticallyEquals(receipt.Result.Request)
            ? receipt
            : Failure<LabelCaseDecisionReceipt>(LabelServiceErrorCode.IdempotencyConflict, "The case decision ID identifies different intent.");
}
