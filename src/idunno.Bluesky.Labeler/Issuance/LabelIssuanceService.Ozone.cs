// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Issuance;

public sealed partial class LabelIssuanceService
{
    /// <summary>Prepares an entire Ozone label batch without committing individual labels or resolving reports implicitly.</summary>
    internal async Task<LabelServiceResult<LabelOzoneEvent>> ExecuteOzoneEventAsync(
        LabelAdministrationCaller caller,
        LabelOzoneEventRequest request,
        CancellationToken token)
    {
        if (_store is not ILabelOzoneStore events || _store is not ILabelPolicyStore policies ||
            _store is not ILabelAdministrationStore administration)
        {
            return Failure<LabelOzoneEvent>(LabelServiceErrorCode.PolicyUnavailable, "Shared Ozone moderation storage is required.");
        }

        LabelServiceResult<LabelAuthorizationGrant> authority = await administration.AuthorizeAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Moderate,
            reason: request.Reason,
            cancellationToken: token).ConfigureAwait(false);
        if (!authority.Succeeded)
        {
            return LabelServiceResult<LabelOzoneEvent>.Failure(authority.Error);
        }

        LabelServiceResult<IReadOnlyList<LabelOzoneEvent>> existing = await events.ReadOzoneOperationAsync(
            caller,
            request.OperationId,
            token).ConfigureAwait(false);
        if (!existing.Succeeded)
        {
            return LabelServiceResult<LabelOzoneEvent>.Failure(existing.Error);
        }
        if (existing.Result.Count != 0)
        {
            return Failure<LabelOzoneEvent>(LabelServiceErrorCode.IdempotencyConflict, "This Ozone operation has already committed.");
        }

        LabelServiceResult<LabelPolicyState> policy = await policies.GetPolicyStateAsync(caller, token).ConfigureAwait(false);
        if (!policy.Succeeded)
        {
            return LabelServiceResult<LabelOzoneEvent>.Failure(policy.Error);
        }
        LabelTarget target = request.Subject.RecordUri is { } uri
            ? LabelTarget.Record(uri, request.Subject.Cid)
            : LabelTarget.Account(request.Subject.Repository);
        List<LabelDecisionCommit> preparations = [];
        for (int index = 0; index < request.Actions.Length; index++)
        {
            LabelCaseLabelAction action = request.Actions[index];
            LabelTarget actionTarget = target;
            long? expectedSequence = null;
            if (action.Kind == LabelOperationKind.Negate && request.Subject.RecordUri is { } record)
            {
                LabelServiceResult<LabelDecisionSnapshot> observed = await _store.ReadDecisionAsync(target, action.Value, token).ConfigureAwait(false);
                if (!observed.Succeeded)
                {
                    return LabelServiceResult<LabelOzoneEvent>.Failure(observed.Error);
                }
                if (observed.Result.CurrentLabel is { } current)
                {
                    expectedSequence = current.Sequence;
                    if (current.SignedLabel.Label.Cid is null)
                    {
                        actionTarget = LabelTarget.Record(record);
                    }
                }
            }
            LabelOperationContext context = new(caller.Actor, request.Reason);
            string operationId = request.OperationId + ":" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            LabelServiceResult<LabelAuthorizationGrant> admission = await AuthorizeAsync(
                action: action.Kind == LabelOperationKind.Issue ? LabelAuthorizationAction.Issue : LabelAuthorizationAction.Negate,
                context: context,
                operationId: operationId,
                requested: actionTarget,
                resolved: null,
                value: action.Value,
                admission: true,
                token: token).ConfigureAwait(false);
            if (!admission.Succeeded)
            {
                return LabelServiceResult<LabelOzoneEvent>.Failure(admission.Error);
            }
            LabelOperationRequest label = action.Kind == LabelOperationKind.Issue
                ? LabelOperationRequest.Issue(
                    operationId: operationId,
                    target: actionTarget,
                    value: action.Value,
                    context: context,
                    expiresAt: action.ExpiresAt)
                : LabelOperationRequest.Negate(
                    operationId: operationId,
                    target: actionTarget,
                    value: action.Value,
                    context: context);
            LabelServiceResult<LabelDecisionCommit> prepared = await PrepareDecisionAsync(label, token, expectedSequence).ConfigureAwait(false);
            if (!prepared.Succeeded)
            {
                return LabelServiceResult<LabelOzoneEvent>.Failure(prepared.Error);
            }
            preparations.Add(prepared.Result);
        }

        return await events.CommitOzoneEventAsync(
            caller: caller,
            request: request,
            policyRevision: policy.Result.Revision,
            labels: preparations,
            cancellationToken: token).ConfigureAwait(false);
    }
}
