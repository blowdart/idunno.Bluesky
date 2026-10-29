// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text.Json;

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    public Task<LabelServiceResult<LabelPolicyDefinition>> GetPolicyVersionAsync(
        LabelAdministrationCaller caller,
        string version,
        CancellationToken cancellationToken = default)
    {
        LabelInput.ValidateText(version, nameof(version), 256);
        return ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect a retained policy version.",
            action: async (connection, transaction, _) =>
            {
                ModerationPolicyData? draft = await ReadPolicyDraftAsync(connection, transaction, version, cancellationToken).ConfigureAwait(false);
                return draft is null || await HasPolicyVersionActionAsync(
                    connection: connection,
                    transaction: transaction,
                    version: version,
                    action: "deleteDraft",
                    token: cancellationToken).ConfigureAwait(false)
                    ? CaseFailure<LabelPolicyDefinition>(LabelServiceErrorCode.OperationNotFound, "The retained policy version was not found.")
                    : LabelServiceResult<LabelPolicyDefinition>.Success(draft.ToPolicy());
            },
            token: cancellationToken,
            auditAction: "policy-inspection");
    }

    public Task<LabelServiceResult<IReadOnlyList<LabelPolicyDefinition>>> GetPolicyVersionsAsync(
        LabelAdministrationCaller caller,
        string? afterVersion = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ValidateCasePage(0, limit);
        if (afterVersion is not null)
        {
            LabelInput.ValidateText(afterVersion, nameof(afterVersion), 256);
        }

        return ModerationAsync<IReadOnlyList<LabelPolicyDefinition>>(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect retained policy versions.",
            action: async (connection, transaction, _) =>
            {
                DbCommand command = Command(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT v.version,v.payload FROM label_policy_versions v WHERE NOT EXISTS " +
                        $"(SELECT 1 FROM label_policy_events t WHERE {TableJsonField("Version")}=v.version AND {TableJsonField("Action")}='deleteDraft')" +
                        (afterVersion is null ? string.Empty : " AND v.version>@after") + " ORDER BY v.version LIMIT @limit",
                    ("@after", afterVersion),
                    ("@limit", limit));
                await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                List<LabelPolicyDefinition> result = [];
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    ModerationPolicyData draft = JsonSerializer.Deserialize(
                        (byte[])reader.GetValue(1), ModerationJsonContext.Default.ModerationPolicyData)
                        ?? throw new InvalidDataException("The retained policy is null.");
                    if (draft.Version != reader.GetString(0))
                    {
                        throw new InvalidDataException("The retained policy identity and index disagree.");
                    }

                    result.Add(draft.ToPolicy());
                }

                return LabelServiceResult<IReadOnlyList<LabelPolicyDefinition>>.Success(result.AsReadOnly());
            },
            token: cancellationToken,
            auditAction: "policy-inspection");
    }

    public Task<LabelServiceResult<IReadOnlyList<LabelPolicyHistoryEntry>>> GetPolicyHistoryAsync(
        LabelAdministrationCaller caller,
        long afterSequence = 0,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ValidateCasePage(afterSequence, limit);
        return ModerationAsync<IReadOnlyList<LabelPolicyHistoryEntry>>(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect retained policy history.",
            action: async (connection, transaction, _) =>
            {
                DbCommand command = Command(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT payload FROM label_policy_history WHERE seq>@after ORDER BY seq LIMIT @limit",
                    ("@after", afterSequence),
                    ("@limit", limit));
                await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                List<LabelPolicyHistoryEntry> result = [];
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    result.Add(DecodePolicyHistory((byte[])reader.GetValue(0)).ToEntry());
                }

                return LabelServiceResult<IReadOnlyList<LabelPolicyHistoryEntry>>.Success(result.AsReadOnly());
            },
            token: cancellationToken,
            auditAction: "policy-inspection");
    }

    public Task<LabelServiceResult<LabelPolicyHistoryEntry>> GetPolicyOperationAsync(
        LabelAdministrationCaller caller,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        LabelInput.ValidateText(operationId, nameof(operationId), 256);
        return ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: operationId,
            reason: "Recover a retained policy operation.",
            action: async (connection, transaction, _) =>
            {
                object? payload = await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT payload FROM label_policy_history WHERE operation_hash=@hash",
                    token: cancellationToken,
                    ("@hash", OperationHash(operationId))).ConfigureAwait(false);
                if (payload is not byte[] bytes)
                {
                    return CaseFailure<LabelPolicyHistoryEntry>(LabelServiceErrorCode.OperationNotFound, "The policy operation was not found.");
                }

                ModerationPolicyHistory history = DecodePolicyHistory(bytes);
                return history.OperationId == operationId
                    ? LabelServiceResult<LabelPolicyHistoryEntry>.Success(history.ToEntry())
                    : CaseFailure<LabelPolicyHistoryEntry>(LabelServiceErrorCode.IdempotencyConflict, "The indexed policy operation has a different identity.");
            },
            token: cancellationToken,
            auditAction: "policy-inspection");
    }

    /// <summary>Appends immutable administration and authoritative observations with their original database time.</summary>
    private async Task AppendPolicyHistoryAsync(
        DbConnection connection,
        DbTransaction transaction,
        string? operationId,
        LabelPolicyHistoryAction action,
        string? actor,
        string reason,
        ModerationPolicyData? draft,
        ModerationPolicyGate? gate,
        string? observedCid,
        string? observedDeclaration,
        CancellationToken token)
    {
        long sequence = checked(Integer(await ScalarAsync(
            connection: connection,
            transaction: transaction,
            sql: "SELECT COALESCE(MAX(seq),0) FROM label_policy_history",
            token: token).ConfigureAwait(false)) + 1);
        ModerationPolicyHistory entry = new(
            Sequence: sequence,
            OperationId: operationId,
            Action: action,
            Actor: actor,
            Reason: reason,
            CreatedAt: await DatabaseTimeAsync(connection, transaction, token).ConfigureAwait(false),
            Draft: draft,
            Gate: gate,
            ObservedCid: observedCid,
            ObservedDeclaration: observedDeclaration);
        await ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "INSERT INTO label_policy_history(seq,operation_hash,payload) VALUES(@seq,@hash,@payload)",
            token: token,
            ("@seq", sequence),
            ("@hash", operationId is null ? null : OperationHash(operationId)),
            ("@payload", JsonSerializer.SerializeToUtf8Bytes(entry, ModerationJsonContext.Default.ModerationPolicyHistory))).ConfigureAwait(false);
    }

    /// <summary>Rejects missing retained history rather than returning a success-shaped fabricated result.</summary>
    private static ModerationPolicyHistory DecodePolicyHistory(byte[] bytes) =>
        JsonSerializer.Deserialize(bytes, ModerationJsonContext.Default.ModerationPolicyHistory)
            ?? throw new InvalidDataException("The policy history is null.");
}
