// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text.Json;

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    public Task<LabelServiceResult<LabelCaseDecisionReceipt>> CommitCaseDecisionAsync(
        LabelCaseDecisionCommit decision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        LabelCaseDecision request = decision.Request;
        ModerationDecisionIntent intent = ModerationDecisionIntent.FromDecision(decision.Caller.Actor, request);
        byte[] intentBytes = JsonSerializer.SerializeToUtf8Bytes(intent, ModerationJsonContext.Default.ModerationDecisionIntent);
        return ModerationAsync(
            caller: decision.Caller,
            permission: LabelAdministrationPermission.Moderate,
            operationId: request.OperationId,
            reason: request.Reason,
            action: async (connection, transaction, _) =>
            {
                ModerationDecisionData? previous = await ReadCaseDecisionAsync(
                    connection, transaction, request.OperationId, cancellationToken).ConfigureAwait(false);
                if (previous is not null)
                {
                    byte[] previousIntent = JsonSerializer.SerializeToUtf8Bytes(previous.Intent, ModerationJsonContext.Default.ModerationDecisionIntent);
                    return previous.OperationId == request.OperationId && previousIntent.AsSpan().SequenceEqual(intentBytes)
                        ? LabelServiceResult<LabelCaseDecisionReceipt>.Success(previous.ToReceipt())
                        : CaseFailure<LabelCaseDecisionReceipt>(LabelServiceErrorCode.IdempotencyConflict, "The case decision ID identifies different intent.");
                }

                ModerationCaseData? current = await ReadCaseAsync(connection, transaction, request.CaseId, cancellationToken).ConfigureAwait(false);
                if (current is null)
                {
                    return CaseFailure<LabelCaseDecisionReceipt>(LabelServiceErrorCode.CaseNotFound, "The case was not found.");
                }

                if (current.Revision != request.ExpectedRevision || current.Status == LabelCaseStatus.Resolved)
                {
                    return CaseFailure<LabelCaseDecisionReceipt>(LabelServiceErrorCode.CaseConflict, "Reload the unresolved case before deciding it.");
                }

                ModerationPolicyGate? gate = await ReadPolicyGateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (gate?.Policy is null || gate.Revision != decision.ExpectedPolicyRevision || gate.Policy.Version != request.PolicyVersion)
                {
                    return CaseFailure<LabelCaseDecisionReceipt>(LabelServiceErrorCode.PolicyUnavailable, "The selected policy changed during decision preparation.");
                }

                LabelStoreState state = await StateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                LabelServiceResult<IReadOnlyList<ModerationAppealData>> appeals = await ValidateAppealDecisionAsync(
                    connection: connection,
                    transaction: transaction,
                    state: state,
                    request: request,
                    token: cancellationToken).ConfigureAwait(false);
                if (!appeals.Succeeded)
                {
                    return LabelServiceResult<LabelCaseDecisionReceipt>.Failure(appeals.Error);
                }

                StoredLabel?[] observations = new StoredLabel?[decision.PreparedActions.Count];
                for (int index = 0; index < decision.PreparedActions.Count; index++)
                {
                    LabelDecisionCommit prepared = decision.PreparedActions[index];
                    LabelCaseLabelAction action = request.Actions[index];
                    ValidateCaseActionIntent(current, decision, index);
                    if (await DecisionReceiptAsync(
                        connection: connection,
                        transaction: transaction,
                        operationId: prepared.Request.OperationId,
                        token: cancellationToken).ConfigureAwait(false) is not null)
                    {
                        return CaseFailure<LabelCaseDecisionReceipt>(
                            LabelServiceErrorCode.IdempotencyConflict, "A case action identity already exists outside this atomic decision.");
                    }

                    LabelServiceResult<LabelDecisionSnapshot> validated = await ValidatePreparedDecisionAsync(
                        connection: connection,
                        transaction: transaction,
                        state: state,
                        decision: prepared,
                        token: cancellationToken).ConfigureAwait(false);
                    if (!validated.Succeeded)
                    {
                        return LabelServiceResult<LabelCaseDecisionReceipt>.Failure(validated.Error);
                    }

                    observations[index] = validated.Result.CurrentLabel;
                    if (action.ExpectedLabelSequence is { } sequence && observations[index]?.Sequence != sequence)
                    {
                        return CaseFailure<LabelCaseDecisionReceipt>(
                            LabelServiceErrorCode.StaleDecision, "The challenged label is no longer the current decision.");
                    }
                }

                List<byte[]> receipts = [];
                for (int index = 0; index < decision.PreparedActions.Count; index++)
                {
                    LabelOperationReceipt receipt = await ApplyPreparedDecisionAsync(
                        connection: connection,
                        transaction: transaction,
                        state: state,
                        decision: decision.PreparedActions[index],
                        current: observations[index],
                        administration: null,
                        token: cancellationToken).ConfigureAwait(false);
                    receipts.Add(DecisionReceiptEncoding.Encode(receipt));
                    state = receipt.StateAtCommit;
                }

                DateTimeOffset completedAt = DateTimeOffset.FromUnixTimeMilliseconds(
                    await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false));
                foreach (ModerationAppealData appeal in appeals.Result)
                {
                    await SaveAppealOutcomeAsync(
                        connection: connection,
                        transaction: transaction,
                        appeal: appeal with
                        {
                            Status = request.AppealOutcome!.Value,
                            UpdatedAt = completedAt,
                            DecisionOperationId = request.OperationId
                        },
                        token: cancellationToken).ConfigureAwait(false);
                }

                long pending = Integer(await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT COUNT(*) FROM label_appeals WHERE case_id=@case AND status=@pending",
                    token: cancellationToken,
                    ("@case", request.CaseId),
                    ("@pending", (int)LabelAppealStatus.Pending)).ConfigureAwait(false));
                ModerationCaseData next = current with
                {
                    Revision = checked(current.Revision + 1),
                    Status = pending == 0 ? LabelCaseStatus.Resolved : LabelCaseStatus.InReview,
                    UpdatedAt = completedAt
                };
                await SaveCaseAsync(
                    connection: connection,
                    transaction: transaction,
                    next: next,
                    subject: current.ToCase().Subject,
                    insert: false,
                    token: cancellationToken).ConfigureAwait(false);
                ModerationCaseEvent caseEvent = await AppendCaseEventAsync(
                    connection: connection,
                    transaction: transaction,
                    next: next,
                    operationId: request.OperationId,
                    action: LabelCaseAction.Decide,
                    actor: decision.Caller.Actor,
                    localIdentity: decision.Caller.LocalIdentity,
                    reason: request.Reason,
                    intent: intentBytes,
                    operationNamespace: "decision",
                    token: cancellationToken).ConfigureAwait(false);
                ModerationDecisionData result = new(request.OperationId, intent, caseEvent, [.. receipts], state);
                await ExecuteAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "INSERT INTO label_case_decisions(operation_hash,operation_id,case_id,intent,payload) VALUES(@hash,@operation,@case,@intent,@payload)",
                    token: cancellationToken,
                    ("@hash", OperationHash(request.OperationId)),
                    ("@operation", request.OperationId),
                    ("@case", request.CaseId),
                    ("@intent", intentBytes),
                    ("@payload", JsonSerializer.SerializeToUtf8Bytes(result, ModerationJsonContext.Default.ModerationDecisionData))).ConfigureAwait(false);
                return LabelServiceResult<LabelCaseDecisionReceipt>.Success(result.ToReceipt());
            },
            token: cancellationToken,
            auditAction: "case-decision");
    }

    public Task<LabelServiceResult<LabelCaseDecisionReceipt>> GetCaseDecisionAsync(
        LabelAdministrationCaller caller,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        LabelInput.ValidateText(operationId, nameof(operationId), 256);
        return ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: operationId,
            reason: "Inspect an atomic case decision.",
            action: async (connection, transaction, _) =>
            {
                ModerationDecisionData? result = await ReadCaseDecisionAsync(connection, transaction, operationId, cancellationToken).ConfigureAwait(false);
                return result is not null && result.OperationId == operationId
                    ? LabelServiceResult<LabelCaseDecisionReceipt>.Success(result.ToReceipt())
                    : CaseFailure<LabelCaseDecisionReceipt>(LabelServiceErrorCode.OperationNotFound, "The case decision was not found.");
            },
            token: cancellationToken,
            auditAction: "case-decision");
    }

    /// <summary>Rejects any preparation that changes exact case identity, action ordering or authenticated intent.</summary>
    private static void ValidateCaseActionIntent(ModerationCaseData current, LabelCaseDecisionCommit decision, int index)
    {
        LabelCaseLabelAction action = decision.Request.Actions[index];
        LabelDecisionCommit prepared = decision.PreparedActions[index];
        LabelOperationRequest request = prepared.Request;
        if (request.OperationId != decision.Request.ActionOperationId(index) ||
            request.Kind != action.Kind || request.Value != action.Value || request.ExpiresAt != action.ExpiresAt ||
            request.Context.Actor != decision.Caller.Actor || request.Context.Reason != decision.Request.Reason ||
            request.Target.Subject != current.Subject || request.Target.Cid?.ToString() != current.Cid ||
            prepared.ResolvedTarget.Subject != current.Subject || prepared.ResolvedTarget.Cid?.ToString() != current.Cid ||
            (request.Kind == LabelOperationKind.Issue && prepared.Audit.PolicyVersion != decision.Request.PolicyVersion))
        {
            throw new ArgumentException("A prepared action differs from the exact case decision intent.", nameof(decision));
        }
    }

    /// <summary>Reads historical decision data under the shared issuer lock, retaining full operation identity.</summary>
    private static async Task<ModerationDecisionData?> ReadCaseDecisionAsync(
        DbConnection connection,
        DbTransaction transaction,
        string operationId,
        CancellationToken token)
    {
        DbCommand command = Command(
            connection: connection,
            transaction: transaction,
            sql: "SELECT operation_id,payload FROM label_case_decisions WHERE operation_hash=@hash",
            ("@hash", OperationHash(operationId)));
        await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
        DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false))
        {
            return null;
        }

        ModerationDecisionData result = JsonSerializer.Deserialize(
            (byte[])reader.GetValue(1), ModerationJsonContext.Default.ModerationDecisionData)
            ?? throw new InvalidDataException("The case decision receipt is null.");
        if (result.OperationId != reader.GetString(0))
        {
            throw new InvalidDataException("The case decision index and payload disagree.");
        }

        return result;
    }
}
