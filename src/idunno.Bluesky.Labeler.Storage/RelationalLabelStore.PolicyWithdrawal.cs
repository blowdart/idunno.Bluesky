// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Runtime.CompilerServices;

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore : ILabelPolicyWithdrawalStore
{
    /// <summary>Reads one bounded page under the same state lock used for label and policy mutations.</summary>
    Task<LabelServiceResult<LabelPolicyWithdrawalBatch>> ILabelPolicyWithdrawalStore.ReadPolicyWithdrawalBatchAsync(
        CancellationToken cancellationToken) =>
        AdministrationAsync(async (connection, transaction, state) =>
        {
            ModerationPolicyGate? gate = await ReadPolicyGateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            if (gate is { Phase: LabelPolicyPhase.Withdrawing, Withdrawal: { } withdrawal })
            {
                DateTimeOffset now = DateTimeOffset.FromUnixTimeMilliseconds(
                    await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false));
                // Telemetry alone must not invalidate another worker's prepared, revision-fenced batch.
                gate = gate with
                {
                    Withdrawal = withdrawal with
                    {
                        LastActivityAt = now,
                        LastProgressAt = withdrawal.PublishedThrough is { } published && state.PublishedThrough > published
                            ? now : withdrawal.LastProgressAt,
                        PublishedThrough = state.PublishedThrough
                    }
                };
                await SavePolicyGateAsync(connection, transaction, gate, cancellationToken).ConfigureAwait(false);
            }
            return await PolicyWithdrawalBatchAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                gate: gate,
                token: cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    /// <summary>Checks revocable saved evidence before scanning; expiration pauses rather than silently granting background authority.</summary>
    private async Task<LabelServiceResult<LabelPolicyWithdrawalBatch>> PolicyWithdrawalBatchAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelStoreState state,
        ModerationPolicyGate? gate,
        CancellationToken token)
    {
        if (gate?.Phase != LabelPolicyPhase.Withdrawing || gate.Withdrawal is not { } withdrawal)
        {
            return LabelServiceResult<LabelPolicyWithdrawalBatch>.Success(new(
                Policy: null,
                StoreRevision: state.Revision,
                ScannedThrough: 0,
                NegatedThrough: 0,
                CreatedAt: default,
                Labels: []));
        }

        LabelAdministrationCaller caller = WithdrawalCaller(gate, withdrawal);
        AdministrationState team = await ReadAdministrationAsync(connection, transaction, token).ConfigureAwait(false);
        if (!await HasPermissionAsync(
            connection: connection,
            transaction: transaction,
            state: state,
            administration: team,
            caller: caller,
            permission: LabelAdministrationPermission.ManagePolicy,
            token: token).ConfigureAwait(false))
        {
            const string message = "Publication cleanup is paused because its administrator authorization expired or was revoked. An active administrator must resume it.";
            if (withdrawal.PauseReason != message)
            {
                await SavePolicyGateAsync(connection, transaction, gate with
                {
                    Revision = checked(gate.Revision + 1),
                    Withdrawal = withdrawal with
                    {
                        PauseReason = message,
                        LastActivityAt = DateTimeOffset.FromUnixTimeMilliseconds(
                            await DatabaseTimeAsync(connection, transaction, token).ConfigureAwait(false))
                    }
                }, token).ConfigureAwait(false);
                await AuditAdministrationAsync(
                    connection: connection,
                    transaction: transaction,
                    administration: team,
                    actor: caller.Actor,
                    localIdentity: caller.LocalIdentity,
                    operationId: gate.PublicationOperationId!,
                    action: "policy-withdrawal-paused",
                    reason: message,
                    succeeded: false,
                    token: token).ConfigureAwait(false);
            }
            return CaseFailure<LabelPolicyWithdrawalBatch>(LabelServiceErrorCode.Unauthorized, message);
        }

        if (state.Phase != LabelerLifecyclePhase.Active ||
            (await ReadMaintenanceAsync(connection, transaction, token).ConfigureAwait(false)).IsActive)
        {
            const string maintenancePause = "Publication cleanup is paused during lifecycle shutdown or coordinated maintenance.";
            await SavePolicyGateAsync(connection, transaction, gate with
            {
                Revision = checked(gate.Revision + 1),
                Withdrawal = withdrawal with
                {
                    PauseReason = maintenancePause,
                    LastActivityAt = DateTimeOffset.FromUnixTimeMilliseconds(
                        await DatabaseTimeAsync(connection, transaction, token).ConfigureAwait(false))
                }
            }, token).ConfigureAwait(false);
            return CaseFailure<LabelPolicyWithdrawalBatch>(LabelServiceErrorCode.IssuanceClosed,
                maintenancePause);
        }

        if (withdrawal.PauseReason is not null)
        {
            withdrawal = withdrawal with { PauseReason = null };
            gate = gate with { Revision = checked(gate.Revision + 1), Withdrawal = withdrawal };
            await SavePolicyGateAsync(connection, transaction, gate, token).ConfigureAwait(false);
        }
        bool scanning = withdrawal.ScannedThrough < withdrawal.UpperBound;
        long after = scanning ? withdrawal.ScannedThrough : withdrawal.NegatedThrough;
        long through = after;
        DateTimeOffset created = withdrawal.CreatedAt;
        List<StoredLabel> candidates = [];
        DbCommand command = Command(
            connection: connection,
            transaction: transaction,
            sql: """
                SELECT e.seq,e.signed_json,e.payload,e.format_version,
                CASE WHEN d.seq IS NULL THEN -1 WHEN NOT EXISTS (
                    SELECT 1 FROM label_distribution newer
                    WHERE newer.slot_hash=d.slot_hash AND newer.slot_key=d.slot_key AND newer.seq>d.seq
                ) THEN 1 ELSE 0 END
                FROM label_entries e LEFT JOIN label_distribution d ON d.seq=e.seq
                WHERE e.seq>@after AND e.seq<=@upper ORDER BY e.seq LIMIT 250
                """,
            ("@after", after),
            ("@upper", withdrawal.UpperBound));
        long now = await DatabaseTimeAsync(connection, transaction, token).ConfigureAwait(false);
        int inspected = 0;
        await using (command.ConfigureAwait(false))
        {
            DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            await using ConfiguredAsyncDisposable disposal = reader.ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                if (reader.GetInt32(4) < 0)
                {
                    return CaseFailure<LabelPolicyWithdrawalBatch>(LabelServiceErrorCode.StorageUnavailable,
                        "A stored label has no distribution metadata; publication cleanup cannot advance.");
                }
                inspected++;
                through = reader.GetInt64(0);
                SignedLabelSnapshot signed = Restore(reader);
                Label label = signed.Label;
                if (scanning)
                {
                    if (label.CreationTimestamp > created)
                    {
                        created = label.CreationTimestamp.ToUniversalTime();
                    }
                }
                else if (reader.GetInt32(4) == 1 && !label.IsNegationLabel &&
                    (label.ExpiresAt is null || label.ExpiresAt > DateTimeOffset.FromUnixTimeMilliseconds(now)) &&
                    withdrawal.Values.Contains(label.Value, StringComparer.Ordinal))
                {
                    candidates.Add(new(through, signed));
                }
            }
        }
        if (inspected < 250)
        {
            through = withdrawal.UpperBound;
        }
        int timestampAdvance = scanning && through == withdrawal.UpperBound ? 1 : candidates.Count;
        if (created.Ticks > DateTimeOffset.MaxValue.Ticks - timestampAdvance)
        {
            return CaseFailure<LabelPolicyWithdrawalBatch>(LabelServiceErrorCode.PolicyUnavailable,
                "Retained label timestamps leave no representable superseding withdrawal timestamp.");
        }
        if (scanning && through == withdrawal.UpperBound)
        {
            created = created.AddTicks(1);
        }

        return LabelServiceResult<LabelPolicyWithdrawalBatch>.Success(new(
            Policy: gate.ToState(now),
            StoreRevision: state.Revision,
            ScannedThrough: scanning ? through : withdrawal.ScannedThrough,
            NegatedThrough: scanning ? withdrawal.NegatedThrough : through,
            CreatedAt: created,
            Labels: candidates.AsReadOnly()));
    }

    /// <summary>Revalidates the signed page and atomically checkpoints its labels, audit and policy gate.</summary>
    Task<LabelServiceResult<LabelPolicyState>> ILabelPolicyWithdrawalStore.CommitPolicyWithdrawalBatchAsync(
        LabelPolicyWithdrawalBatch batch,
        IReadOnlyList<SignedLabelSnapshot> negations,
        string? signingPublicKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(negations);
        SignedLabelSnapshot[] signed = [.. negations];
        return AdministrationAsync(async (connection, transaction, state) =>
        {
            ModerationPolicyGate? gate = await ReadPolicyGateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            if (batch.Policy is null || gate?.Phase != LabelPolicyPhase.Withdrawing ||
                gate.Withdrawal is not { } withdrawal || gate.Revision != batch.Policy.Revision ||
                gate.PublicationOperationId != batch.Policy.PublicationOperationId || state.Revision != batch.StoreRevision)
            {
                return CaseFailure<LabelPolicyState>(LabelServiceErrorCode.StaleDecision,
                    "Publication cleanup changed while signing; reload its next batch.");
            }
            LabelServiceResult<LabelPolicyWithdrawalBatch> observed = await PolicyWithdrawalBatchAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                gate: gate,
                token: cancellationToken).ConfigureAwait(false);
            if (!observed.Succeeded)
            {
                return LabelServiceResult<LabelPolicyState>.Failure(observed.Error);
            }
            LabelPolicyWithdrawalBatch expected = observed.Result;
            LabelMaintenanceState maintenance = await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            if (signed.Length != 0 && maintenance.ReplacementPublicKey is { } replacement &&
                maintenance.Phase == LabelMaintenancePhase.Completed && signingPublicKey != replacement)
            {
                return CaseFailure<LabelPolicyState>(LabelServiceErrorCode.StaleDecision,
                    "The withdrawal signing key was replaced; reload the active key before signing.");
            }
            if (expected.ScannedThrough != batch.ScannedThrough || expected.NegatedThrough != batch.NegatedThrough ||
                expected.CreatedAt != batch.CreatedAt || expected.Labels.Count != signed.Length ||
                !expected.Labels.Select(label => label.Sequence).SequenceEqual(batch.Labels.Select(label => label.Sequence)))
            {
                return CaseFailure<LabelPolicyState>(LabelServiceErrorCode.StaleDecision,
                    "Publication cleanup candidates changed while signing; reload its next batch.");
            }
            for (int i = 0; i < signed.Length; i++)
            {
                Label original = expected.Labels[i].SignedLabel.Label;
                Label negation = signed[i].Label;
                if (!negation.IsNegationLabel || negation.Source.Value != gate.Issuer ||
                    negation.Uri != original.Uri || negation.Cid != original.Cid || negation.Value != original.Value ||
                    negation.CreationTimestamp != expected.CreatedAt.AddTicks(i) || negation.ExpiresAt is not null)
                {
                    return CaseFailure<LabelPolicyState>(LabelServiceErrorCode.StaleDecision,
                        "A withdrawal batch must negate exactly its retained label slots.");
                }
            }
            long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            long last = checked(state.IssuedThrough + signed.Length);
            if (last > MaximumSequence)
            {
                throw new OverflowException("The label stream sequence space is exhausted.");
            }
            await AppendSignedBatchAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                labels: signed,
                queuedAt: now,
                token: cancellationToken).ConfigureAwait(false);
            long negationSequence = signed.Length == 0 ? withdrawal.NegationSequence : last;
            bool advanced = expected.ScannedThrough > withdrawal.ScannedThrough ||
                expected.NegatedThrough > withdrawal.NegatedThrough ||
                (withdrawal.PublishedThrough is { } published && state.PublishedThrough > published);
            DateTimeOffset observedAt = DateTimeOffset.FromUnixTimeMilliseconds(now);
            ModerationPolicyWithdrawal nextWithdrawal = withdrawal with
            {
                ScannedThrough = expected.ScannedThrough,
                NegatedThrough = expected.NegatedThrough,
                NegationSequence = negationSequence,
                CreatedAt = expected.CreatedAt.AddTicks(signed.Length),
                PauseReason = null,
                LastActivityAt = observedAt,
                LastProgressAt = advanced ? observedAt : withdrawal.LastProgressAt,
                PublishedThrough = state.PublishedThrough
            };
            bool complete = nextWithdrawal.ScannedThrough == withdrawal.UpperBound &&
                nextWithdrawal.NegatedThrough == withdrawal.UpperBound && state.PublishedThrough >= negationSequence;
            if (complete)
            {
                nextWithdrawal = nextWithdrawal with { LastProgressAt = observedAt };
            }
            ModerationPolicyGate next = gate with
            {
                Revision = checked(gate.Revision + 1),
                Phase = complete ? LabelPolicyPhase.Publishing : LabelPolicyPhase.Withdrawing,
                Withdrawal = nextWithdrawal
            };
            if (signed.Length != 0)
            {
                await SaveAsync(connection, transaction, state with
                {
                    Revision = checked(state.Revision + 1),
                    IssuedThrough = last
                }, cancellationToken).ConfigureAwait(false);
            }
            await SavePolicyGateAsync(connection, transaction, next, cancellationToken).ConfigureAwait(false);
            AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            await AuditAdministrationAsync(
                connection: connection,
                transaction: transaction,
                administration: team,
                actor: new(withdrawal.Actor),
                localIdentity: withdrawal.LocalIdentity,
                operationId: gate.PublicationOperationId!,
                action: complete ? "policy-withdrawal-complete" : "policy-withdrawal-batch",
                reason: withdrawal.Reason,
                succeeded: true,
                token: cancellationToken).ConfigureAwait(false);
            return LabelServiceResult<LabelPolicyState>.Success(next.ToState(now));
        }, cancellationToken);
    }

    /// <summary>Restores only a still-selected cleanup operation's revocable authority, preserving all committed negations.</summary>
    Task<LabelServiceResult<LabelPolicyState>> ILabelPolicyWithdrawalStore.ResumePolicyWithdrawalAsync(
        LabelAdministrationCaller caller,
        string operationId,
        string reason,
        CancellationToken cancellationToken)
    {
        LabelInput.ValidateText(operationId, nameof(operationId), 256);
        LabelInput.ValidateText(reason, nameof(reason), 4096);
        return ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.ManagePolicy,
            operationId: operationId,
            reason: reason,
            action: async (connection, transaction, _) =>
            {
                ModerationPolicyGate? gate = await ReadPolicyGateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (gate?.Phase != LabelPolicyPhase.Withdrawing || gate.PublicationOperationId != operationId ||
                    gate.Withdrawal is not { } withdrawal)
                {
                    return CaseFailure<LabelPolicyState>(LabelServiceErrorCode.StaleDecision, "Only the selected pending cleanup may be resumed.");
                }
                ModerationPolicyGate next = gate with
                {
                    Revision = checked(gate.Revision + 1),
                    Withdrawal = withdrawal with
                    {
                        Actor = caller.Actor.Value,
                        Generation = caller.Generation,
                        Credential = caller.Credential,
                        LocalIdentity = caller.LocalIdentity,
                        ExpiresAt = caller.ExpiresAt,
                        Reason = reason,
                        PauseReason = null
                    }
                };
                await SavePolicyGateAsync(connection, transaction, next, cancellationToken).ConfigureAwait(false);
                return LabelServiceResult<LabelPolicyState>.Success(next.ToState(
                    await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false)));
            },
            token: cancellationToken,
            auditAction: "policy-withdrawal-resume");
    }

    /// <summary>Reconstitutes provider evidence solely from the previously authorized durable publication intent.</summary>
    private static LabelAdministrationCaller WithdrawalCaller(ModerationPolicyGate gate, ModerationPolicyWithdrawal withdrawal) => new(
        issuer: new(gate.Issuer),
        actor: new(withdrawal.Actor),
        generation: withdrawal.Generation,
        credential: withdrawal.Credential,
        localIdentity: withdrawal.LocalIdentity,
        expiresAt: withdrawal.ExpiresAt);
}
