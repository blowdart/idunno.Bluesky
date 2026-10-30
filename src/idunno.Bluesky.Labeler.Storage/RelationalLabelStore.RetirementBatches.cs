// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Maintenance;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    /// <summary>Captures the command epoch before any fallible batch preparation, signature or purge is admitted.</summary>
    Task<LabelServiceResult<LabelRetirementAdmission>> ILabelRetirementStore.ReadRetirementCheckpointAsync(CancellationToken cancellationToken) =>
        AdministrationAsync(async (connection, transaction, state) =>
        {
            RetirementData? data = await ReadRetirementDataAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            return LabelServiceResult<LabelRetirementAdmission>.Success(new(
                data is { Running: true } && state.Phase is
                    LabelerLifecyclePhase.Retiring or LabelerLifecyclePhase.Distributing or LabelerLifecyclePhase.Purging
                    ? new(data.Generation, data.ControlId) : null));
        }, cancellationToken);

    /// <summary>Pauses an interrupted epoch even if its batch committed without acknowledgement; a later command invalidates it.</summary>
    Task ILabelRetirementStore.PauseRetirementCheckpointAsync(
        LabelRetirementCheckpoint checkpoint,
        string error,
        CancellationToken cancellationToken) =>
        RunAsync(
            operation: "retirement-recover-pause",
            action: async (connection, transaction, state) =>
            {
                RetirementData? data = await ReadRetirementDataAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (data is not null && data.ControlId == checkpoint.ControlId && data.Generation >= checkpoint.Generation &&
                    (checkpoint.ControlId != Guid.Empty || data.Generation == checkpoint.Generation) &&
                    data.PauseReason is null && state.Phase is
                        LabelerLifecyclePhase.Retiring or LabelerLifecyclePhase.Distributing or LabelerLifecyclePhase.Purging)
                {
                    await PauseRetirementCoreAsync(
                        connection: connection,
                        transaction: transaction,
                        state: state,
                        data: data,
                        error: error,
                        token: cancellationToken).ConfigureAwait(false);
                }

                return state;
            },
            token: cancellationToken);

    /// <summary>Reconstitutes only the revocable authority saved by a previously authorized command.</summary>
    private static LabelAdministrationCaller RetirementCaller(LabelStoreState state, RetirementData data) => new(
        issuer: new(state.Issuer),
        actor: new(data.Actor),
        generation: data.AuthorityGeneration,
        credential: data.Credential,
        localIdentity: data.LocalIdentity,
        expiresAt: data.ExpiresAt);

    /// <summary>Pauses once and audits a failure without treating repeated attempts as progress.</summary>
    private async Task PauseRetirementCoreAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelStoreState state,
        RetirementData data,
        string error,
        CancellationToken token)
    {
        DateTimeOffset now = DateTimeOffset.FromUnixTimeMilliseconds(
            await DatabaseTimeAsync(connection, transaction, token).ConfigureAwait(false));
        await SaveRetirementDataAsync(
            connection: connection,
            transaction: transaction,
            data: data with
            {
                Running = false,
                Generation = checked(data.Generation + 1),
                PauseReason = error,
                LastActivityAt = now
            },
            token: token).ConfigureAwait(false);
        await AuditRetirementAsync(
            connection: connection,
            transaction: transaction,
            team: await ReadAdministrationAsync(connection, transaction, token).ConfigureAwait(false),
            caller: RetirementCaller(state, data),
            operationId: data.OperationId,
            action: "retirement-paused",
            reason: error,
            succeeded: false,
            token: token).ConfigureAwait(false);
    }

    /// <summary>Checks authority before reading work, and pauses rather than silently renewing expired evidence.</summary>
    private async Task<bool> RetirementWorkerAllowedAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelStoreState state,
        RetirementData data,
        CancellationToken token)
    {
        bool allowed = await HasPermissionAsync(
            connection: connection,
            transaction: transaction,
            state: state,
            administration: await ReadAdministrationAsync(connection, transaction, token).ConfigureAwait(false),
            caller: RetirementCaller(state, data),
            permission: LabelAdministrationPermission.Maintain,
            token: token).ConfigureAwait(false);
        if (!allowed)
        {
            await PauseRetirementCoreAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                data: data,
                error: "Unauthorized: saved authority expired or was revoked. An active administrator must explicitly resume.",
                token: token).ConfigureAwait(false);
        }

        return allowed;
    }

    /// <summary>Reads at most 250 immutable rows, including pages with no eligible labels.</summary>
    Task<LabelServiceResult<LabelRetirementBatch>> ILabelRetirementStore.ReadRetirementBatchAsync(
        CancellationToken cancellationToken,
        LabelRetirementCheckpoint? checkpoint) =>
        AdministrationAsync(async (connection, transaction, state) =>
        {
            RetirementData? data = await ReadRetirementDataAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            if (checkpoint is not null && (data is null || data.ControlId != checkpoint.ControlId || data.Generation != checkpoint.Generation))
            {
                return CaseFailure<LabelRetirementBatch>(LabelServiceErrorCode.StaleDecision, "Retirement changed after worker admission; reload its recovery checkpoint.");
            }

            if (data is null || !data.Running ||
                state.Phase is not (LabelerLifecyclePhase.Retiring or LabelerLifecyclePhase.Distributing))
            {
                return LabelServiceResult<LabelRetirementBatch>.Success(new(null, data?.Generation ?? 0, 0, 0, 0, default, []));
            }

            if (!await RetirementWorkerAllowedAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                data: data,
                token: cancellationToken).ConfigureAwait(false))
            {
                return AdminFailure<LabelRetirementBatch>("Retirement paused because saved authority is no longer valid.");
            }

            if ((await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false)).IsActive)
            {
                return LabelServiceResult<LabelRetirementBatch>.Success(new(null, 0, 0, 0, 0, default, []));
            }

            if (state.PublishedThrough < state.RetirementUpperBound)
            {
                // The publisher owns visibility; this worker never enumerates unpublished history.
                return LabelServiceResult<LabelRetirementBatch>.Success(new(
                    data.OperationId, data.Generation, state.Revision, data.ScannedThrough, state.RetirementThrough, data.CreatedAt, []));
            }

            return await PrepareRetirementBatchAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                data: data,
                token: cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    /// <summary>Builds a fixed bounded observation; timestamp scanning also materializes timestamp-ordered CID winners.</summary>
    private async Task<LabelServiceResult<LabelRetirementBatch>> PrepareRetirementBatchAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelStoreState state,
        RetirementData data,
        CancellationToken token)
    {
        bool scanning = data.ScannedThrough < state.RetirementUpperBound;
        long after = scanning ? data.ScannedThrough : state.RetirementThrough;
        string sql = scanning
            ? "SELECT seq,signed_json,payload,format_version FROM label_entries WHERE seq>@after AND seq<=@upper ORDER BY seq LIMIT 250"
            : "SELECT e.seq,e.signed_json,e.payload,e.format_version FROM label_retirement_slots s JOIN label_entries e ON e.seq=s.seq WHERE s.seq>@after AND s.seq<=@upper ORDER BY s.seq LIMIT 250";
        DbCommand command = Command(
            connection: connection,
            transaction: transaction,
            sql: sql,
            ("@after", after),
            ("@upper", state.RetirementUpperBound));
        List<StoredLabel> rows = [];
        await using (command.ConfigureAwait(false))
        {
            DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            await using ConfiguredAsyncDisposable disposal = reader.ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                rows.Add(new(reader.GetInt64(0), Restore(reader)));
            }
        }

        long through = rows.Count < 250 ? state.RetirementUpperBound : rows[^1].Sequence;
        DateTimeOffset now = DateTimeOffset.FromUnixTimeMilliseconds(
            await DatabaseTimeAsync(connection, transaction, token).ConfigureAwait(false));
        DateTimeOffset created = data.CreatedAt > now ? data.CreatedAt : now;
        List<StoredLabel> candidates = [];
        foreach (StoredLabel row in rows)
        {
            if (scanning)
            {
                if (row.SignedLabel.Label.CreationTimestamp >= created)
                {
                    if (row.SignedLabel.Label.CreationTimestamp == DateTimeOffset.MaxValue)
                    {
                        await PauseRetirementCoreAsync(
                            connection: connection,
                            transaction: transaction,
                            state: state,
                            data: data,
                            error: "TimestampExhausted: no timestamp can supersede retained history.",
                            token: token).ConfigureAwait(false);
                        return CaseFailure<LabelRetirementBatch>(LabelServiceErrorCode.TimestampExhausted, "No timestamp can supersede retained history.");
                    }

                    created = row.SignedLabel.Label.CreationTimestamp.ToUniversalTime().AddTicks(1);
                }

                candidates.Add(row);
            }
            else if (LabelDecisionRules.IsActive(row.SignedLabel, now))
            {
                candidates.Add(row);
            }
        }

        if (created.Ticks > DateTimeOffset.MaxValue.Ticks - candidates.Count)
        {
            await PauseRetirementCoreAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                data: data,
                error: "TimestampExhausted: no timestamp range remains for a retirement batch.",
                token: token).ConfigureAwait(false);
            return CaseFailure<LabelRetirementBatch>(LabelServiceErrorCode.TimestampExhausted, "No timestamp range remains for a retirement batch.");
        }

        return LabelServiceResult<LabelRetirementBatch>.Success(new(
            data.OperationId, data.Generation, state.Revision,
            scanning ? through : data.ScannedThrough,
            scanning ? state.RetirementThrough : through,
            created,
            candidates.AsReadOnly(),
            scanning));
    }

    /// <summary>Materializes a CID slot's timestamp winner without scanning that subject's history.</summary>
    private static async Task IndexRetirementSlotAsync(
        DbConnection connection,
        DbTransaction transaction,
        StoredLabel candidate,
        CancellationToken token)
    {
        byte[] key = DistributionKey(candidate.SignedLabel);
        byte[] hash = SHA256.HashData(key);
        DbCommand command = Command(
            connection: connection,
            transaction: transaction,
            sql: "SELECT e.seq,e.signed_json,e.payload,e.format_version,s.slot_key FROM label_retirement_slots s JOIN label_entries e ON e.seq=s.seq WHERE s.slot_hash=@hash",
            ("@hash", hash));
        StoredLabel? previous = null;
        await using (command.ConfigureAwait(false))
        {
            DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            await using ConfiguredAsyncDisposable disposal = reader.ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                if (!key.AsSpan().SequenceEqual((byte[])reader.GetValue(4)))
                {
                    throw new InvalidDataException("A retirement slot hash collision requires operator recovery.");
                }

                previous = new(reader.GetInt64(0), Restore(reader));
            }
        }

        int order = previous is null ? 1 : LabelDecisionRules.CompareCreation(candidate.SignedLabel, previous.SignedLabel);
        if (order < 0 || order == 0 && (candidate.SignedLabel.Label.IsNegationLabel ||
            previous is not null && !previous.SignedLabel.Label.IsNegationLabel &&
            LabelDecisionRules.CompareExpiry(candidate.SignedLabel, previous.SignedLabel) <= 0))
        {
            return;
        }

        if (previous is not null)
        {
            await ExecuteAsync(
                connection: connection,
                transaction: transaction,
                sql: "DELETE FROM label_retirement_slots WHERE seq=@seq",
                token: token,
                ("@seq", previous.Sequence)).ConfigureAwait(false);
        }

        await ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "INSERT INTO label_retirement_slots(seq,slot_hash,slot_key) VALUES(@seq,@hash,@key)",
            token: token,
            ("@seq", candidate.Sequence),
            ("@hash", hash),
            ("@key", key)).ConfigureAwait(false);
    }

    /// <summary>Revalidates a prepared page and atomically saves signatures, checkpoints and audit.</summary>
    Task<LabelServiceResult<LabelRetirementView>> ILabelRetirementStore.CommitRetirementBatchAsync(
        LabelRetirementBatch batch,
        IReadOnlyList<SignedLabelSnapshot> negations,
        string? signingPublicKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(negations);
        SignedLabelSnapshot[] signed = [.. negations];
        return AdministrationAsync(async (connection, transaction, state) =>
        {
            RetirementData? data = await ReadRetirementDataAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            if (data is null || !data.Running || data.OperationId != batch.OperationId || data.Generation != batch.Generation ||
                state.Revision != batch.StoreRevision || state.Phase is not (LabelerLifecyclePhase.Retiring or LabelerLifecyclePhase.Distributing))
            {
                return CaseFailure<LabelRetirementView>(LabelServiceErrorCode.StaleDecision, "Retirement progress changed; reload the durable checkpoint.");
            }

            if (!await RetirementWorkerAllowedAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                data: data,
                token: cancellationToken).ConfigureAwait(false))
            {
                return AdminFailure<LabelRetirementView>("Retirement authority expired or was revoked.");
            }

            if ((await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false)).IsActive)
            {
                return CaseFailure<LabelRetirementView>(LabelServiceErrorCode.StaleDecision, "Retirement signing is blocked during recovery rotation.");
            }

            if (state.PublishedThrough < state.RetirementUpperBound)
            {
                return LabelServiceResult<LabelRetirementView>.Success(RetirementView(state, data));
            }

            LabelServiceResult<LabelRetirementBatch> prepared = await PrepareRetirementBatchAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                data: data,
                token: cancellationToken).ConfigureAwait(false);
            if (!prepared.Succeeded)
            {
                return LabelServiceResult<LabelRetirementView>.Failure(prepared.Error);
            }

            LabelRetirementBatch expected = prepared.Result;
            bool scanning = data.ScannedThrough < state.RetirementUpperBound;
            // Creation time comes from the fixed observation, not a second wall-clock sample during commit.
            if (expected.ScannedThrough != batch.ScannedThrough || expected.NegatedThrough != batch.NegatedThrough ||
                !expected.Labels.Select(label => label.Sequence).SequenceEqual(batch.Labels.Select(label => label.Sequence)) ||
                batch.Scanning != scanning || batch.CreatedAt < data.CreatedAt ||
                scanning && expected.Labels.Any(label => label.SignedLabel.Label.CreationTimestamp >= batch.CreatedAt) ||
                signed.Length != (scanning ? 0 : expected.Labels.Count))
            {
                return CaseFailure<LabelRetirementView>(LabelServiceErrorCode.StaleDecision, "Retirement candidates changed; reload the durable checkpoint.");
            }

            Signing.LabelSigningKeySelection? selection = await ReadSigningSelectionAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                token: cancellationToken).ConfigureAwait(false);
            LabelMaintenanceState maintenance = await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            string? requiredKey = selection?.PublicKey ?? maintenance.ReplacementPublicKey ?? data.SigningPublicKey;
            if (signed.Length != 0 && (signingPublicKey is null || requiredKey is not null && signingPublicKey != requiredKey))
            {
                await PauseRetirementCoreAsync(
                    connection: connection,
                    transaction: transaction,
                    state: state,
                    data: data,
                    error: "SigningFailed: restore the verified selected signing key and explicitly resume.",
                    token: cancellationToken).ConfigureAwait(false);
                return CaseFailure<LabelRetirementView>(LabelServiceErrorCode.SigningFailed, "The selected signing key changed; restore it and explicitly resume.");
            }

            for (int i = 0; i < signed.Length; i++)
            {
                Label prior = expected.Labels[i].SignedLabel.Label;
                Label label = signed[i].Label;
                if (!label.IsNegationLabel || label.Source.Value != state.Issuer || label.Uri != prior.Uri ||
                    label.Value != prior.Value || label.Cid != prior.Cid || label.ExpiresAt is not null ||
                    label.CreationTimestamp != batch.CreatedAt.AddTicks(i) ||
                    !LabelDecisionRules.IsLater(signed[i], expected.Labels[i].SignedLabel))
                {
                    return CaseFailure<LabelRetirementView>(LabelServiceErrorCode.StaleDecision, "Retirement must negate exactly its retained CID slots with later timestamps.");
                }
            }

            if (scanning)
            {
                foreach (StoredLabel row in expected.Labels)
                {
                    await IndexRetirementSlotAsync(
                        connection: connection,
                        transaction: transaction,
                        candidate: row,
                        token: cancellationToken).ConfigureAwait(false);
                }
            }

            long last = checked(state.IssuedThrough + signed.Length);
            if (last > MaximumSequence)
            {
                throw new OverflowException("The label stream sequence space is exhausted.");
            }

            long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            await AppendSignedBatchAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                labels: signed,
                queuedAt: now,
                token: cancellationToken).ConfigureAwait(false);
            LabelStoreState nextState = state with
            {
                Revision = checked(state.Revision + 1),
                IssuedThrough = last,
                RetirementThrough = batch.NegatedThrough,
                Phase = batch.NegatedThrough == state.RetirementUpperBound ? LabelerLifecyclePhase.Distributing : state.Phase
            };
            DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeMilliseconds(now);
            string stage = nextState.Phase == LabelerLifecyclePhase.Distributing ? "Publication" : "Negation";
            RetirementData next = data with
            {
                Generation = checked(data.Generation + 1),
                ScannedThrough = batch.ScannedThrough,
                CreatedAt = batch.CreatedAt.AddTicks(signed.Length),
                Stage = scanning ? "TimestampScan" : stage,
                Running = nextState.Phase != LabelerLifecyclePhase.Distributing || nextState.PublishedThrough < last,
                LastActivityAt = timestamp,
                LastProgressAt = timestamp
            };
            await SaveAsync(
                connection: connection,
                transaction: transaction,
                state: nextState,
                token: cancellationToken).ConfigureAwait(false);
            await SaveRetirementDataAsync(
                connection: connection,
                transaction: transaction,
                data: next,
                token: cancellationToken).ConfigureAwait(false);
            await AuditRetirementAsync(
                connection: connection,
                transaction: transaction,
                team: await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false),
                caller: RetirementCaller(state, data),
                operationId: data.OperationId,
                action: scanning ? "retirement-scan" : "retirement-batch",
                reason: data.Reason,
                succeeded: true,
                token: cancellationToken).ConfigureAwait(false);
            return LabelServiceResult<LabelRetirementView>.Success(RetirementView(nextState, next));
        }, cancellationToken);
    }

    /// <summary>Persists a fault only if the failed observation still identifies the current worker generation.</summary>
    Task ILabelRetirementStore.PauseRetirementAsync(long generation, string error, CancellationToken cancellationToken) =>
        RunAsync(
            operation: "retirement-pause",
            action: async (connection, transaction, state) =>
            {
                RetirementData? data = await ReadRetirementDataAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (data is { Running: true } && data.Generation == generation)
                {
                    await PauseRetirementCoreAsync(
                        connection: connection,
                        transaction: transaction,
                        state: state,
                        data: data,
                        error: error,
                        token: cancellationToken).ConfigureAwait(false);
                }

                return state;
            },
            token: cancellationToken);

    /// <summary>Continues optional confirmed deletion only while its saved administrative authority remains valid.</summary>
    async Task ILabelRetirementStore.AdvanceRetirementPurgeAsync(CancellationToken cancellationToken, LabelRetirementCheckpoint? checkpoint)
    {
        await RunAsync(
            operation: "retirement-purge",
            action: async (connection, transaction, state) =>
            {
                RetirementData? data = await ReadRetirementDataAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (data is not { Running: true } || state.Phase != LabelerLifecyclePhase.Purging ||
                    checkpoint is not null && (data.ControlId != checkpoint.ControlId || data.Generation != checkpoint.Generation) ||
                    (await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false)).IsActive)
                {
                    return state;
                }

                if (!await RetirementWorkerAllowedAsync(
                    connection: connection,
                    transaction: transaction,
                    state: state,
                    data: data,
                    token: cancellationToken).ConfigureAwait(false))
                {
                    return state;
                }

                LabelStoreState next = await PurgeHistoryAsync(
                    connection: connection,
                    transaction: transaction,
                    state: state,
                    limit: 250,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                DateTimeOffset now = DateTimeOffset.FromUnixTimeMilliseconds(
                    await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false));
                await SaveRetirementDataAsync(
                    connection: connection,
                    transaction: transaction,
                    data: data with
                    {
                        Generation = checked(data.Generation + 1),
                        Running = next.Phase != LabelerLifecyclePhase.Purged,
                        Stage = next.Phase.ToString(),
                        LastActivityAt = now,
                        LastProgressAt = now
                    },
                    token: cancellationToken).ConfigureAwait(false);
                await AuditRetirementAsync(
                    connection: connection,
                    transaction: transaction,
                    team: await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false),
                    caller: RetirementCaller(state, data),
                    operationId: data.OperationId,
                    action: "retirement-purge-batch",
                    reason: data.Reason,
                    succeeded: true,
                    token: cancellationToken).ConfigureAwait(false);
                return next;
            },
            token: cancellationToken).ConfigureAwait(false);
    }
}
