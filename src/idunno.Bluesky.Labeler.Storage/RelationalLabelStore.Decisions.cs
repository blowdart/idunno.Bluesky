// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Maintenance;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    /// <summary>Validates a prepared action without changing labels, receipts or stream state.</summary>
    /// <remarks><para>Callers must establish live administrative authority and recover same-intent
    /// operations first. Compound decisions validate every distinct label slot against the same initial
    /// state before applying any action under the existing issuer transaction.</para></remarks>
    private async Task<LabelServiceResult<LabelDecisionSnapshot>> ValidatePreparedDecisionAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelStoreState state,
        LabelDecisionCommit decision,
        CancellationToken token)
    {
        LabelOperationRequest request = decision.Request;
        if (state.Revision != decision.ExpectedRevision)
        {
            return CaseFailure<LabelDecisionSnapshot>(
                LabelServiceErrorCode.StaleDecision,
                "The store revision changed; reload and reauthorize the original decision.");
        }

        if (request.Kind == LabelOperationKind.Issue &&
            !await PolicyAllowsIssuanceAsync(connection, transaction, decision, token).ConfigureAwait(false))
        {
            return CaseFailure<LabelDecisionSnapshot>(
                LabelServiceErrorCode.PolicyUnavailable,
                "The independently verified moderation policy no longer permits this issuance.");
        }

        if (state.Phase != LabelerLifecyclePhase.Active)
        {
            return CaseFailure<LabelDecisionSnapshot>(
                LabelServiceErrorCode.IssuanceClosed,
                "Ordinary issuance and negation are closed by the labeler lifecycle.");
        }

        LabelMaintenanceState maintenance = await ReadMaintenanceAsync(connection, transaction, token).ConfigureAwait(false);
        if (maintenance.IsActive)
        {
            return CaseFailure<LabelDecisionSnapshot>(
                LabelServiceErrorCode.IssuanceClosed,
                "Ordinary issuance and negation are closed during coordinated maintenance.");
        }

        if (decision.SignedLabel is not null &&
            maintenance.Phase == LabelMaintenancePhase.Completed &&
            !string.Equals(decision.Audit.SigningKeyMultibase, maintenance.ReplacementPublicKey, StringComparison.Ordinal))
        {
            return CaseFailure<LabelDecisionSnapshot>(
                LabelServiceErrorCode.StaleDecision,
                "The selected signing key was replaced; recreate the issuance service with the active key.");
        }

        IReadOnlyList<LabelServiceResult<LabelDecisionSnapshot>> snapshots = await DecisionSnapshotsAsync(
            connection: connection,
            transaction: transaction,
            state: state,
            queries: [(decision.ResolvedTarget, request.Value)],
            token: token).ConfigureAwait(false);
        LabelServiceResult<LabelDecisionSnapshot> snapshot = snapshots[0];
        if (!snapshot.Succeeded)
        {
            return snapshot;
        }

        StoredLabel? current = snapshot.Result.CurrentLabel;
        if (request.Kind == LabelOperationKind.Negate && request.Target.Cid is not null &&
            (current is null || current.SignedLabel.Label.Cid != request.Target.Cid))
        {
            return CaseFailure<LabelDecisionSnapshot>(
                LabelServiceErrorCode.StaleDecision,
                "The requested withdrawal CID is not the current stored decision.");
        }

        if (request.Kind == LabelOperationKind.Negate && decision.ResolvedTarget.Cid != current?.SignedLabel.Label.Cid)
        {
            throw new ArgumentException("The resolved withdrawal scope must copy the current decision CID.");
        }

        if ((decision.SignedLabel is not null) != LabelDecisionRules.NeedsLabel(request, current, decision.Audit.EvaluatedAt))
        {
            throw new ArgumentException("The signed decision does not match the current state or no-op intent.");
        }

        if (decision.SignedLabel is { } signed)
        {
            Label label = signed.Label;
            Cid? expectedCid = request.Kind == LabelOperationKind.Issue ? request.Target.Cid : current!.SignedLabel.Label.Cid;
            if (label.Source.Value != state.Issuer || label.Uri != decision.ResolvedTarget.Subject ||
                label.Value != request.Value || label.Cid != expectedCid ||
                label.IsNegationLabel != (request.Kind == LabelOperationKind.Negate) ||
                !LabelDecisionRules.HasExpiry(signed, request.ExpiresAt) ||
                label.CreationTimestamp < decision.Audit.EvaluatedAt ||
                (label.ExpiresAt is { } expiry && expiry <= label.CreationTimestamp) ||
                (current is not null && !LabelDecisionRules.IsLater(signed, current.SignedLabel)))
            {
                throw new ArgumentException("The signed label does not represent the authorized decision.");
            }
        }

        return snapshot;
    }

    /// <summary>Appends one previously validated action and its immutable receipt inside the caller's transaction.</summary>
    /// <remarks><para>All actions in a compound decision must be validated before calling this method.
    /// Any failure must roll back the owning transaction, including earlier action receipts and audit.</para></remarks>
    private async Task<LabelOperationReceipt> ApplyPreparedDecisionAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelStoreState state,
        LabelDecisionCommit decision,
        StoredLabel? current,
        AdministrationState? administration,
        CancellationToken token)
    {
        LabelOperationRequest request = decision.Request;
        bool changed = decision.SignedLabel is not null;
        StoredLabel? outcome = current;
        if (decision.SignedLabel is { } signed)
        {
            long sequence = checked(state.IssuedThrough + 1);
            if (sequence > MaximumSequence)
            {
                throw new OverflowException("The label stream sequence space is exhausted.");
            }

            long now = await DatabaseTimeAsync(connection, transaction, token).ConfigureAwait(false);
            await ExecuteAsync(
                connection: connection,
                transaction: transaction,
                sql: "INSERT INTO label_entries(seq, signed_json, payload, format_version, queued_at) VALUES(@seq,@json,@payload,1,@now)",
                token: token,
                ("@seq", sequence),
                ("@json", signed.GetJsonBytes()),
                ("@payload", signed.GetCanonicalUnsignedBytes()),
                ("@now", now)).ConfigureAwait(false);
            await IndexDistributionAsync(
                connection: connection,
                transaction: transaction,
                sequence: sequence,
                signed: signed,
                token: token).ConfigureAwait(false);
            await IndexDecisionAsync(
                connection: connection,
                transaction: transaction,
                sequence: sequence,
                signed: signed,
                token: token).ConfigureAwait(false);
            await IndexSubjectAsync(
                connection: connection,
                transaction: transaction,
                sequence: sequence,
                signed: signed,
                token: token).ConfigureAwait(false);
            outcome = new(sequence, signed);
        }

        LabelStoreState next = state with
        {
            Revision = checked(state.Revision + 1),
            IssuedThrough = changed ? checked(state.IssuedThrough + 1) : state.IssuedThrough
        };
        LabelOperationReceipt receipt = new(
            request: request,
            resolvedTarget: decision.ResolvedTarget,
            changed: changed,
            label: outcome,
            stateAtCommit: next,
            audit: decision.Audit);
        await SaveAsync(connection: connection, transaction: transaction, state: next, token: token).ConfigureAwait(false);
        await ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "INSERT INTO label_decisions(operation_hash,operation_id,revision,receipt) VALUES(@hash,@id,@revision,@receipt)",
            token: token,
            ("@hash", OperationHash(request.OperationId)),
            ("@id", request.OperationId),
            ("@revision", next.Revision),
            ("@receipt", DecisionReceiptEncoding.Encode(receipt))).ConfigureAwait(false);
        if (administration is not null && decision.AdministrationCaller is { } caller)
        {
            await AuditAdministrationAsync(
                connection: connection,
                transaction: transaction,
                administration: administration,
                actor: caller.Actor,
                localIdentity: caller.LocalIdentity,
                operationId: request.OperationId,
                action: request.Kind.ToString(),
                reason: request.Context.Reason,
                succeeded: true,
                token: token).ConfigureAwait(false);
        }

        return receipt;
    }
}
