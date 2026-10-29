// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    public Task<LabelPolicyState> InitializePolicyAsync(
        TimeSpan? verificationFreshness = null,
        CancellationToken cancellationToken = default)
    {
        TimeSpan freshness = verificationFreshness ?? TimeSpan.FromMinutes(30);
        ArgumentOutOfRangeException.ThrowIfLessThan(freshness, TimeSpan.FromMilliseconds(1));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(freshness, TimeSpan.FromDays(1));
        long milliseconds = checked((long)freshness.TotalMilliseconds);
        return RunAsync(
            operation: "policy-initialize",
            action: async (connection, transaction, _) =>
            {
                LabelStoreState state = await StateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                ModerationPolicyGate? gate = await ReadPolicyGateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (gate is not null && gate.Issuer != state.Issuer)
                {
                    throw new InvalidDataException("The policy gate does not match the bound labeler issuer.");
                }

                if (gate is not null && gate.FreshnessMilliseconds != milliseconds)
                {
                    throw new InvalidOperationException("All hosts must use the persisted policy verification freshness.");
                }

                if (gate is null)
                {
                    gate = new(state.Issuer, 1, LabelPolicyPhase.Unverified, null, null, null, null, milliseconds);
                    await SavePolicyGateAsync(connection, transaction, gate, cancellationToken).ConfigureAwait(false);
                }

                return gate.ToState(await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false));
            },
            token: cancellationToken);
    }

    public Task<LabelServiceResult<LabelPolicyDefinition>> SavePolicyAsync(
        LabelAdministrationCaller caller,
        string operationId,
        LabelPolicyDefinition policy,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(policy);
        LabelInput.ValidateText(operationId, nameof(operationId), 256);
        LabelInput.ValidateText(reason, nameof(reason), 4096);
        ModerationPolicyData draft = ModerationPolicyData.FromPolicy(policy);
        byte[] intent = EncodePolicyOperation(new(
            Action: "draft",
            Actor: caller.Actor.Value,
            Version: draft.Version,
            Reason: reason,
            ExpectedRevision: 0,
            PreviousCid: null,
            Draft: draft,
            Gate: null));
        return ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.ManagePolicy,
            operationId: operationId,
            reason: reason,
            action: async (connection, transaction, _) =>
            {
                LabelServiceResult<ModerationPolicyOperation>? recovered = await ReadPolicyOperationAsync(
                    connection: connection, transaction: transaction, operationId: operationId, intent: intent,
                    token: cancellationToken).ConfigureAwait(false);
                if (recovered is not null)
                {
                    return recovered.Succeeded
                        ? LabelServiceResult<LabelPolicyDefinition>.Success(recovered.Result.Draft!.ToPolicy())
                        : LabelServiceResult<LabelPolicyDefinition>.Failure(recovered.Error);
                }

                if ((await StateAsync(connection, transaction, cancellationToken).ConfigureAwait(false)).Phase != LabelerLifecyclePhase.Active)
                {
                    return CaseFailure<LabelPolicyDefinition>(
                        LabelServiceErrorCode.IssuanceClosed,
                        "New policy workflows are closed because the labeler is retiring.");
                }

                if (await ReadPolicyGateAsync(connection, transaction, cancellationToken).ConfigureAwait(false) is null)
                {
                    return CaseFailure<LabelPolicyDefinition>(LabelServiceErrorCode.PolicyUnavailable, "Moderation is not enabled.");
                }

                ModerationPolicyData? existing = await ReadPolicyDraftAsync(connection, transaction, draft.Version, cancellationToken).ConfigureAwait(false);
                if (await HasPolicyVersionActionAsync(
                    connection: connection,
                    transaction: transaction,
                    version: draft.Version,
                    action: "deleteDraft",
                    token: cancellationToken).ConfigureAwait(false))
                {
                    return CaseFailure<LabelPolicyDefinition>(LabelServiceErrorCode.IdempotencyConflict, "Deleted policy version identifiers cannot be reused.");
                }
                byte[] payload = JsonSerializer.SerializeToUtf8Bytes(draft, ModerationJsonContext.Default.ModerationPolicyData);
                if (existing is not null && !JsonSerializer.SerializeToUtf8Bytes(
                    existing, ModerationJsonContext.Default.ModerationPolicyData).AsSpan().SequenceEqual(payload))
                {
                    return CaseFailure<LabelPolicyDefinition>(LabelServiceErrorCode.IdempotencyConflict, "Policy versions are immutable.");
                }

                if (existing is null)
                {
                    await ExecuteAsync(
                        connection: connection, transaction: transaction,
                        sql: "INSERT INTO label_policy_versions(version_hash,version,payload) VALUES(@hash,@version,@payload)",
                        token: cancellationToken,
                        ("@hash", OperationHash(draft.Version)), ("@version", draft.Version), ("@payload", payload)).ConfigureAwait(false);
                }

                await SavePolicyOperationAsync(
                    connection: connection, transaction: transaction, operationId: operationId, intent: intent, result: intent,
                    token: cancellationToken).ConfigureAwait(false);
                return LabelServiceResult<LabelPolicyDefinition>.Success(policy);
            },
            token: cancellationToken,
            auditAction: "policy-draft");
    }

    public Task<LabelServiceResult<LabelPolicyState>> BeginPolicyPublicationAsync(
        LabelAdministrationCaller caller,
        string operationId,
        string version,
        long expectedRevision,
        Cid? previousCid,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        LabelInput.ValidateText(operationId, nameof(operationId), 256);
        LabelInput.ValidateText(version, nameof(version), 256);
        LabelInput.ValidateText(reason, nameof(reason), 4096);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedRevision);
        ModerationPolicyOperation operation = new(
            Action: "publication",
            Actor: caller.Actor.Value,
            Version: version,
            Reason: reason,
            ExpectedRevision: expectedRevision,
            PreviousCid: previousCid?.ToString(),
            Draft: null,
            Gate: null);
        byte[] intent = EncodePolicyOperation(operation);
        return ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.ManagePolicy,
            operationId: operationId,
            reason: reason,
            action: async (connection, transaction, _) =>
            {
                long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                LabelServiceResult<ModerationPolicyOperation>? recovered = await ReadPolicyOperationAsync(
                    connection: connection, transaction: transaction, operationId: operationId, intent: intent,
                    token: cancellationToken).ConfigureAwait(false);
                if (recovered is not null)
                {
                    return recovered.Succeeded
                        ? LabelServiceResult<LabelPolicyState>.Success(recovered.Result.Gate!.ToState(now))
                        : LabelServiceResult<LabelPolicyState>.Failure(recovered.Error);
                }

                ModerationPolicyGate? gate = await ReadPolicyGateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (gate is null || gate.Revision != expectedRevision || gate.Phase is LabelPolicyPhase.Publishing or LabelPolicyPhase.Withdrawing)
                {
                    return CaseFailure<LabelPolicyState>(LabelServiceErrorCode.StaleDecision, "Reload or recover the pending policy publication.");
                }

                LabelStoreState state = await StateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (state.Phase != LabelerLifecyclePhase.Active ||
                    (await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false)).IsActive)
                {
                    return CaseFailure<LabelPolicyState>(LabelServiceErrorCode.IssuanceClosed, "Publication conflicts with lifecycle or maintenance.");
                }

                ModerationPolicyData? draft = await ReadPolicyDraftAsync(connection, transaction, version, cancellationToken).ConfigureAwait(false);
                if (draft is null || await HasPolicyVersionActionAsync(
                    connection: connection,
                    transaction: transaction,
                    version: version,
                    action: "deleteDraft",
                    token: cancellationToken).ConfigureAwait(false))
                {
                    return CaseFailure<LabelPolicyState>(LabelServiceErrorCode.PolicyUnavailable, "The policy draft was not found.");
                }

                string[] unfinished = gate.Withdrawal is { } previousWithdrawal &&
                    (previousWithdrawal.ScannedThrough < previousWithdrawal.UpperBound ||
                    previousWithdrawal.NegatedThrough < previousWithdrawal.UpperBound ||
                    state.PublishedThrough < previousWithdrawal.NegationSequence)
                    ? previousWithdrawal.Values : [];
                string[] removed = [.. (gate.Policy?.ToPolicy().Issuance.DeclaredValues ?? [])
                    .Concat(unfinished)
                    .Except(draft.ToPolicy().Issuance.DeclaredValues, StringComparer.Ordinal)];
                ModerationPolicyWithdrawal? withdrawal = removed.Length == 0 || state.IssuedThrough == 0 ? null : new(
                    Values: removed,
                    UpperBound: state.IssuedThrough,
                    ScannedThrough: 0,
                    NegatedThrough: 0,
                    NegationSequence: state.IssuedThrough,
                    CreatedAt: DateTimeOffset.FromUnixTimeMilliseconds(now),
                    Actor: caller.Actor.Value,
                    Generation: caller.Generation,
                    Credential: caller.Credential,
                    LocalIdentity: caller.LocalIdentity,
                    ExpiresAt: caller.ExpiresAt,
                    Reason: reason,
                    PublishedThrough: state.PublishedThrough);
                ModerationPolicyGate next = gate with
                {
                    Revision = checked(gate.Revision + 1),
                    Phase = withdrawal is null ? LabelPolicyPhase.Publishing : LabelPolicyPhase.Withdrawing,
                    Withdrawal = withdrawal,
                    Policy = draft,
                    PublicationOperationId = operationId,
                    PreviousCid = previousCid?.ToString(),
                    VerifiedCid = null,
                    VerifiedUntil = null
                };
                await SavePolicyGateAsync(connection, transaction, next, cancellationToken).ConfigureAwait(false);
                await SavePolicyOperationAsync(
                    connection: connection, transaction: transaction, operationId: operationId, intent: intent,
                    result: EncodePolicyOperation(operation with { Gate = next }), token: cancellationToken).ConfigureAwait(false);
                return LabelServiceResult<LabelPolicyState>.Success(next.ToState(now));
            },
            token: cancellationToken,
            auditAction: "policy-publication");
    }

    public Task<LabelServiceResult<LabelPolicyState>> AbandonPolicyPublicationAsync(
        LabelAdministrationCaller caller,
        string operationId,
        long expectedRevision,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        LabelInput.ValidateText(operationId, nameof(operationId), 256);
        LabelInput.ValidateText(reason, nameof(reason), 4096);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedRevision);
        ModerationPolicyOperation operation = new(
            Action: "abandon",
            Actor: caller.Actor.Value,
            Version: string.Empty,
            Reason: reason,
            ExpectedRevision: expectedRevision,
            PreviousCid: null,
            Draft: null,
            Gate: null);
        byte[] intent = EncodePolicyOperation(operation);
        return ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.ManagePolicy,
            operationId: operationId,
            reason: reason,
            action: async (connection, transaction, _) =>
            {
                long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                LabelServiceResult<ModerationPolicyOperation>? recovered = await ReadPolicyOperationAsync(
                    connection: connection,
                    transaction: transaction,
                    operationId: operationId,
                    intent: intent,
                    token: cancellationToken).ConfigureAwait(false);
                if (recovered is not null)
                {
                    return recovered.Succeeded
                        ? LabelServiceResult<LabelPolicyState>.Success(recovered.Result.Gate!.ToState(now))
                        : LabelServiceResult<LabelPolicyState>.Failure(recovered.Error);
                }

                ModerationPolicyGate? gate = await ReadPolicyGateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (gate is null || gate.Revision != expectedRevision ||
                    gate.Phase is not (LabelPolicyPhase.Publishing or LabelPolicyPhase.Withdrawing))
                {
                    return CaseFailure<LabelPolicyState>(LabelServiceErrorCode.StaleDecision, "Only the observed pending publication may be abandoned.");
                }

                ModerationPolicyGate next = gate with
                {
                    Revision = checked(gate.Revision + 1),
                    Phase = LabelPolicyPhase.Conflict,
                    VerifiedCid = null,
                    VerifiedUntil = null
                };
                await SavePolicyGateAsync(connection, transaction, next, cancellationToken).ConfigureAwait(false);
                await SavePolicyOperationAsync(
                    connection: connection,
                    transaction: transaction,
                    operationId: operationId,
                    intent: intent,
                    result: EncodePolicyOperation(operation with { Gate = next }),
                    token: cancellationToken).ConfigureAwait(false);
                return LabelServiceResult<LabelPolicyState>.Success(next.ToState(now));
            },
            token: cancellationToken,
            auditAction: "policy-abandon");
    }

    public Task<LabelServiceResult<LabelPolicyState>> GetPolicyStateAsync(
        LabelAdministrationCaller caller,
        CancellationToken cancellationToken = default) =>
        ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect the policy gate.",
            action: async (connection, transaction, _) =>
            {
                ModerationPolicyGate? gate = await ReadPolicyGateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                return gate is null
                    ? CaseFailure<LabelPolicyState>(LabelServiceErrorCode.PolicyUnavailable, "Moderation is not enabled.")
                    : LabelServiceResult<LabelPolicyState>.Success(gate.ToState(
                        await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false)));
            },
            token: cancellationToken);

    public Task<LabelPolicyState?> ReadPolicyStateAsync(CancellationToken cancellationToken = default) =>
        RunAsync<LabelPolicyState?>(
            operation: "policy-state",
            action: async (connection, transaction, _) =>
            {
                ModerationPolicyGate? gate = await ReadPolicyGateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                return gate?.ToState(await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false));
            },
            token: cancellationToken);

    public Task<LabelServiceResult<LabelPolicyState>> ObservePolicyAsync(
        long expectedRevision,
        DateTimeOffset verificationStartedAt,
        Cid? observedCid,
        JsonElement declaration,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedRevision);
        if (observedCid is null && declaration.ValueKind != JsonValueKind.Null)
        {
            throw new ArgumentException("Authoritative absence requires a null declaration.", nameof(declaration));
        }

        string observed;
        bool invalidDeclaration = false;
        try
        {
            observed = observedCid is null ? string.Empty : LabelPolicyDefinition.CanonicalDeclaration(declaration);
        }
        catch (ArgumentException)
        {
            // Retain a bounded invalid-observation marker, never the oversized or malformed payload.
            observed = "null";
            invalidDeclaration = true;
        }
        return AdministrationAsync(async (connection, transaction, _) =>
        {
            ModerationPolicyGate? gate = await ReadPolicyGateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            long started = verificationStartedAt.ToUnixTimeMilliseconds();
            if (gate is null || gate.Revision != expectedRevision || gate.Policy is null)
            {
                return CaseFailure<LabelPolicyState>(LabelServiceErrorCode.StaleDecision, "The policy changed during lookup.");
            }

            if (gate.Phase == LabelPolicyPhase.Withdrawing)
            {
                return CaseFailure<LabelPolicyState>(LabelServiceErrorCode.PolicyUnavailable,
                    "Removed-label withdrawal must finish and its negations must be published before declaration verification.");
            }

            if (started > now)
            {
                return CaseFailure<LabelPolicyState>(
                    LabelServiceErrorCode.StaleDecision,
                    $"The verification start is {started - now} milliseconds ahead of the database clock. Restart independent verification.");
            }

            if (started <= now - gate.FreshnessMilliseconds)
            {
                return CaseFailure<LabelPolicyState>(LabelServiceErrorCode.StaleDecision, "The verification window expired during lookup.");
            }

            bool matching = gate.Policy.Declaration == observed;
            LabelPolicyPhase phase = (matching, gate.Phase) switch
            {
                (false, LabelPolicyPhase.Publishing) when !invalidDeclaration => LabelPolicyPhase.Publishing,
                (true, not LabelPolicyPhase.Conflict) => LabelPolicyPhase.VerifiedActive,
                _ => LabelPolicyPhase.Conflict
            };
            ModerationPolicyGate next = gate with
            {
                Revision = checked(gate.Revision + 1),
                Phase = phase,
                VerifiedCid = matching ? observedCid!.ToString() : null,
                VerifiedUntil = phase == LabelPolicyPhase.VerifiedActive ? checked(started + gate.FreshnessMilliseconds) : null
            };
            await SavePolicyGateAsync(connection, transaction, next, cancellationToken).ConfigureAwait(false);
            if (gate.Phase != phase || gate.VerifiedCid != next.VerifiedCid || !matching)
            {
                await AppendPolicyHistoryAsync(
                    connection: connection,
                    transaction: transaction,
                    operationId: null,
                    action: LabelPolicyHistoryAction.Observation,
                    actor: null,
                    reason: matching ? "Declaration verified." : "Declaration drift detected.",
                    draft: null,
                    gate: next,
                    observedCid: observedCid?.ToString(),
                    observedDeclaration: observedCid is null ? "null" : observed,
                    token: cancellationToken).ConfigureAwait(false);
                AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                await AuditAdministrationAsync(
                    connection: connection, transaction: transaction, administration: team, actor: null, localIdentity: null,
                    operationId: string.Empty, action: "policy-observation", reason: matching ? "Declaration verified." : "Declaration drift detected.",
                    succeeded: phase == LabelPolicyPhase.VerifiedActive, token: cancellationToken).ConfigureAwait(false);
            }

            return LabelServiceResult<LabelPolicyState>.Success(next.ToState(now));
        }, cancellationToken);
    }

    /// <summary>Checks the durable policy gate and exact prepared version under the label commit lock.</summary>
    private async Task<bool> PolicyAllowsIssuanceAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelDecisionCommit decision,
        CancellationToken token)
    {
        ModerationPolicyGate? gate = await ReadPolicyGateAsync(connection, transaction, token).ConfigureAwait(false);
        if (gate is null)
        {
            return true;
        }

        long now = await DatabaseTimeAsync(connection, transaction, token).ConfigureAwait(false);
        if (gate.Phase != LabelPolicyPhase.VerifiedActive || gate.VerifiedUntil is null || gate.VerifiedUntil <= now ||
            now < gate.VerifiedUntil - gate.FreshnessMilliseconds ||
            gate.Policy is null || gate.Policy.Version != decision.Audit.PolicyVersion)
        {
            return false;
        }

        LabelIssuancePolicy policy = gate.Policy.ToPolicy().Issuance;
        LabelTarget target = decision.ResolvedTarget;
        return policy.DeclaredValues.Contains(decision.Request.Value, StringComparer.Ordinal) &&
            (target.Kind == LabelTargetKind.Account ? policy.AllowAccounts :
                policy.AllowRecords && (policy.AllowedRecordCollections is null ||
                    policy.AllowedRecordCollections.Contains(target.RecordUri!.Collection!)));
    }

    /// <summary>Samples the database clock, or its internal test replacement, for every persisted time boundary.</summary>
    private async Task<long> DatabaseTimeAsync(DbConnection connection, DbTransaction transaction, CancellationToken token) =>
        DatabaseClock is { } clock
            ? clock.GetUtcNow().ToUnixTimeMilliseconds()
            : Integer(await ScalarAsync(connection: connection, transaction: transaction, sql: ClockSql, token: token).ConfigureAwait(false));

    /// <summary>Reads the optional moderation gate while holding the issuer serialization lock.</summary>
    private static async Task<ModerationPolicyGate?> ReadPolicyGateAsync(DbConnection connection, DbTransaction transaction, CancellationToken token)
    {
        object? payload = await ScalarAsync(
            connection: connection, transaction: transaction, sql: "SELECT payload FROM label_policy_gate WHERE id=1", token: token).ConfigureAwait(false);
        return payload is byte[] bytes
            ? JsonSerializer.Deserialize(bytes, ModerationJsonContext.Default.ModerationPolicyGate)
                ?? throw new InvalidDataException("The policy gate is null.")
            : null;
    }

    /// <summary>Writes a complete gate atomically with the mutation or observation audit.</summary>
    private static async Task SavePolicyGateAsync(
        DbConnection connection, DbTransaction transaction, ModerationPolicyGate gate, CancellationToken token)
    {
        await ExecuteAsync(
            connection: connection, transaction: transaction, sql: "DELETE FROM label_policy_gate WHERE id=1", token: token).ConfigureAwait(false);
        await ExecuteAsync(
            connection: connection, transaction: transaction, sql: "INSERT INTO label_policy_gate(id,payload) VALUES(1,@payload)", token: token,
            ("@payload", JsonSerializer.SerializeToUtf8Bytes(gate, ModerationJsonContext.Default.ModerationPolicyGate))).ConfigureAwait(false);
    }

    /// <summary>Reads a versioned draft and checks its full identity independently of its index hash.</summary>
    private static async Task<ModerationPolicyData?> ReadPolicyDraftAsync(
        DbConnection connection, DbTransaction transaction, string version, CancellationToken token)
    {
        DbCommand command = Command(
            connection: connection, transaction: transaction,
            sql: "SELECT version,payload FROM label_policy_versions WHERE version_hash=@hash", ("@hash", OperationHash(version)));
        await using ConfiguredAsyncDisposable disposal = command.ConfigureAwait(false);
        DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false))
        {
            return null;
        }

        if (reader.GetString(0) != version)
        {
            throw new InvalidDataException("The policy version index has a conflicting full identity.");
        }

        return JsonSerializer.Deserialize((byte[])reader.GetValue(1), ModerationJsonContext.Default.ModerationPolicyData)
            ?? throw new InvalidDataException("The policy draft is null.");
    }

    /// <summary>Encodes complete same-intent policy operations using source-generated metadata.</summary>
    private static byte[] EncodePolicyOperation(ModerationPolicyOperation operation) =>
        JsonSerializer.SerializeToUtf8Bytes(operation, ModerationJsonContext.Default.ModerationPolicyOperation);

    /// <summary>Recovers a prior policy receipt without silently reusing an identity for different intent.</summary>
    private static async Task<LabelServiceResult<ModerationPolicyOperation>?> ReadPolicyOperationAsync(
        DbConnection connection, DbTransaction transaction, string operationId, byte[] intent, CancellationToken token)
    {
        DbCommand command = Command(
            connection: connection, transaction: transaction,
            sql: "SELECT operation_id,intent,payload FROM label_policy_events WHERE operation_hash=@hash", ("@hash", OperationHash(operationId)));
        await using ConfiguredAsyncDisposable disposal = command.ConfigureAwait(false);
        DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false))
        {
            return null;
        }

        if (reader.GetString(0) != operationId || !((byte[])reader.GetValue(1)).AsSpan().SequenceEqual(intent))
        {
            return CaseFailure<ModerationPolicyOperation>(LabelServiceErrorCode.IdempotencyConflict, "The policy operation identifies different intent.");
        }

        return LabelServiceResult<ModerationPolicyOperation>.Success(
            JsonSerializer.Deserialize((byte[])reader.GetValue(2), ModerationJsonContext.Default.ModerationPolicyOperation)
                ?? throw new InvalidDataException("The policy receipt is null."));
    }

    /// <summary>Appends an immutable policy result in the same transaction as its gate or draft.</summary>
    private async Task SavePolicyOperationAsync(
        DbConnection connection, DbTransaction transaction, string operationId, byte[] intent, byte[] result, CancellationToken token)
    {
        await ExecuteAsync(
            connection: connection, transaction: transaction,
            sql: "INSERT INTO label_policy_events(operation_hash,operation_id,intent,payload) VALUES(@hash,@operation,@intent,@payload)",
            token: token,
            ("@hash", OperationHash(operationId)), ("@operation", operationId), ("@intent", intent), ("@payload", result)).ConfigureAwait(false);
        ModerationPolicyOperation operation = JsonSerializer.Deserialize(result, ModerationJsonContext.Default.ModerationPolicyOperation)
            ?? throw new InvalidDataException("The retained policy operation is null.");
        await AppendPolicyHistoryAsync(
            connection: connection,
            transaction: transaction,
            operationId: operationId,
            action: operation.Action switch
            {
                "draft" => LabelPolicyHistoryAction.Draft,
                "publication" => LabelPolicyHistoryAction.Publication,
                "abandon" => LabelPolicyHistoryAction.Abandonment,
                "deleteDraft" => LabelPolicyHistoryAction.DraftDeletion,
                _ => throw new InvalidDataException("The retained policy action is invalid.")
            },
            actor: operation.Actor,
            reason: operation.Reason,
            draft: operation.Draft,
            gate: operation.Gate,
            observedCid: null,
            observedDeclaration: null,
            token: token).ConfigureAwait(false);
    }

    /// <summary>Checks retained policy actions using the shared provider-specific JSON expression.</summary>
    private async Task<bool> HasPolicyVersionActionAsync(
        DbConnection connection,
        DbTransaction transaction,
        string version,
        string action,
        CancellationToken token) =>
        Integer(await ScalarAsync(
            connection: connection,
            transaction: transaction,
            sql: $"SELECT COUNT(*) FROM label_policy_events t WHERE {TableJsonField("Version")}=@version AND {TableJsonField("Action")}=@action",
            token: token,
            ("@version", version),
            ("@action", action)).ConfigureAwait(false)) > 0;

    /// <summary>Atomically fences deletion against publication and retains a durable same-intent deletion receipt.</summary>
    Task<LabelServiceResult<LabelPolicyDefinition>> ILabelTableStore.DeletePolicyDraftAsync(
        LabelAdministrationCaller caller,
        string operationId,
        string version,
        string reason,
        CancellationToken cancellationToken)
    {
        LabelInput.ValidateText(operationId, nameof(operationId), 256);
        LabelInput.ValidateText(version, nameof(version), 256);
        LabelInput.ValidateText(reason, nameof(reason), 4096);
        ModerationPolicyOperation operation = new(
            Action: "deleteDraft",
            Actor: caller.Actor.Value,
            Version: version,
            Reason: reason,
            ExpectedRevision: 0,
            PreviousCid: null,
            Draft: null,
            Gate: null);
        byte[] intent = EncodePolicyOperation(operation);
        return ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.ManagePolicy,
            operationId: operationId,
            reason: reason,
            action: async (connection, transaction, _) =>
            {
                LabelServiceResult<ModerationPolicyOperation>? recovered = await ReadPolicyOperationAsync(
                    connection: connection,
                    transaction: transaction,
                    operationId: operationId,
                    intent: intent,
                    token: cancellationToken).ConfigureAwait(false);
                if (recovered is not null)
                {
                    return recovered.Succeeded
                        ? LabelServiceResult<LabelPolicyDefinition>.Success(recovered.Result.Draft!.ToPolicy())
                        : LabelServiceResult<LabelPolicyDefinition>.Failure(recovered.Error);
                }

                ModerationPolicyGate? gate = await ReadPolicyGateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (gate?.Policy?.Version == version ||
                    await HasPolicyVersionActionAsync(
                        connection: connection,
                        transaction: transaction,
                        version: version,
                        action: "publication",
                        token: cancellationToken).ConfigureAwait(false))
                {
                    return CaseFailure<LabelPolicyDefinition>(LabelServiceErrorCode.StaleDecision, "Selected or previously published policies cannot be deleted.");
                }

                ModerationPolicyData? draft = await ReadPolicyDraftAsync(
                    connection: connection,
                    transaction: transaction,
                    version: version,
                    token: cancellationToken).ConfigureAwait(false);
                if (draft is null || await HasPolicyVersionActionAsync(
                    connection: connection,
                    transaction: transaction,
                    version: version,
                    action: "deleteDraft",
                    token: cancellationToken).ConfigureAwait(false))
                {
                    return CaseFailure<LabelPolicyDefinition>(LabelServiceErrorCode.OperationNotFound, "The unpublished draft was not found.");
                }

                await SavePolicyOperationAsync(
                    connection: connection,
                    transaction: transaction,
                    operationId: operationId,
                    intent: intent,
                    result: EncodePolicyOperation(operation with { Draft = draft }),
                    token: cancellationToken).ConfigureAwait(false);
                return LabelServiceResult<LabelPolicyDefinition>.Success(draft.ToPolicy());
            },
            token: cancellationToken,
            auditAction: "policy-draft-delete");
    }
}
