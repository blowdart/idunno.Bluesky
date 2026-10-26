// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    public Task<LabelServiceResult<LabelCaseReceipt>> ChangeCaseAsync(
        LabelAdministrationCaller caller,
        LabelCaseChange change,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(change);
        byte[] intent = JsonSerializer.SerializeToUtf8Bytes(
            new ModerationCaseIntent(
                Actor: caller.Actor.Value,
                Subject: change.Subject.Subject,
                Cid: change.Subject.Cid?.ToString(),
                Action: change.Action,
                Reason: change.Reason,
                CaseId: change.CaseId,
                ExpectedRevision: change.ExpectedRevision,
                Assignee: change.Assignee?.Value),
            ModerationJsonContext.Default.ModerationCaseIntent);

        return ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Moderate,
            operationId: change.OperationId,
            reason: change.Reason,
            action: async (connection, transaction, administration) =>
            {
                DbCommand receiptCommand = Command(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT operation_id,intent,payload FROM label_case_events WHERE operation_hash=@hash",
                    ("@hash", CaseOperationHash(change.OperationId)));
                await using (receiptCommand.ConfigureAwait(false))
                {
                    DbDataReader receiptReader = await receiptCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    await using ConfiguredAsyncDisposable receiptReaderDisposal = receiptReader.ConfigureAwait(false);
                    if (await receiptReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (receiptReader.GetString(0) != change.OperationId ||
                            !((byte[])receiptReader.GetValue(1)).AsSpan().SequenceEqual(intent))
                        {
                            return CaseFailure<LabelCaseReceipt>(
                                LabelServiceErrorCode.IdempotencyConflict, "The case operation ID identifies different intent.");
                        }

                        return LabelServiceResult<LabelCaseReceipt>.Success(
                            DecodeCaseEvent((byte[])receiptReader.GetValue(2)).ToReceipt());
                    }
                }

                long now = Integer(await ScalarAsync(
                    connection: connection, transaction: transaction, sql: ClockSql, token: cancellationToken).ConfigureAwait(false));
                DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeMilliseconds(now);
                bool opening = change.Action is LabelCaseAction.Open or LabelCaseAction.OpenAppeal;
                ModerationCaseData? current = opening
                    ? await OpenCaseAsync(connection, transaction, change.Subject, cancellationToken).ConfigureAwait(false)
                    : await ReadCaseAsync(connection, transaction, change.CaseId, cancellationToken).ConfigureAwait(false);
                if (!opening &&
                    (current is null || current.Subject != change.Subject.Subject || current.Cid != change.Subject.Cid?.ToString()))
                {
                    return CaseFailure<LabelCaseReceipt>(LabelServiceErrorCode.CaseNotFound, "The exact case subject was not found.");
                }

                if (!opening && current!.Revision != change.ExpectedRevision)
                {
                    return CaseFailure<LabelCaseReceipt>(LabelServiceErrorCode.CaseConflict, "Reload the case before changing its revision.");
                }

                if (change.Action == LabelCaseAction.Resolve && Integer(await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT COUNT(*) FROM label_appeals WHERE case_id=@case AND status=@pending",
                    token: cancellationToken,
                    ("@case", current!.Id),
                    ("@pending", (int)LabelAppealStatus.Pending)).ConfigureAwait(false)) != 0)
                {
                    return CaseFailure<LabelCaseReceipt>(LabelServiceErrorCode.CaseConflict, "Pending appeals require explicit review before closing this case.");
                }

                if (change.Action == LabelCaseAction.Reopen &&
                    await OpenCaseAsync(connection, transaction, change.Subject, cancellationToken).ConfigureAwait(false) is not null)
                {
                    return CaseFailure<LabelCaseReceipt>(LabelServiceErrorCode.CaseConflict, "The subject already has an unresolved case.");
                }

                if (change.Assignee is { } assignee &&
                    (!administration.Members.TryGetValue(assignee.Value, out AdministrationMember? member) ||
                        !member.Active || member.Removed || member.Role < LabelTeamRole.Moderator))
                {
                    return CaseFailure<LabelCaseReceipt>(LabelServiceErrorCode.Unauthorized, "Assignment requires an active moderator.");
                }

                LabelCaseStatus? status = change.Action switch
                {
                    LabelCaseAction.Open or LabelCaseAction.OpenAppeal => current?.Status ?? LabelCaseStatus.Open,
                    LabelCaseAction.Note => current!.Status,
                    LabelCaseAction.StartReview when current!.Status == LabelCaseStatus.Open => LabelCaseStatus.InReview,
                    LabelCaseAction.Assign when current!.Status != LabelCaseStatus.Resolved => current.Status,
                    LabelCaseAction.Resolve when current!.Status != LabelCaseStatus.Resolved => LabelCaseStatus.Resolved,
                    LabelCaseAction.Reopen when current!.Status == LabelCaseStatus.Resolved => LabelCaseStatus.Open,
                    _ => null
                };
                if (status is null)
                {
                    return CaseFailure<LabelCaseReceipt>(LabelServiceErrorCode.CaseConflict, "The case status does not permit this transition.");
                }

                StoredLabel[] challenges = [];
                if (change.Action == LabelCaseAction.OpenAppeal)
                {
                    LabelServiceResult<StoredLabel[]> captured = await ReadAppealChallengesAsync(
                        connection: connection,
                        transaction: transaction,
                        state: await StateAsync(connection, transaction, cancellationToken).ConfigureAwait(false),
                        subject: change.Subject,
                        now: now,
                        token: cancellationToken).ConfigureAwait(false);
                    if (!captured.Succeeded)
                    {
                        return LabelServiceResult<LabelCaseReceipt>.Failure(captured.Error);
                    }

                    challenges = captured.Result;
                }

                long id = current?.Id ?? checked(Integer(await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT COALESCE(MAX(case_id),0) FROM label_cases",
                    token: cancellationToken).ConfigureAwait(false)) + 1);
                ModerationCaseData next = new(
                    Id: id,
                    Revision: checked((current?.Revision ?? 0) + 1),
                    Subject: change.Subject.Subject,
                    Cid: change.Subject.Cid?.ToString(),
                    Status: status.Value,
                    Assignee: change.Action == LabelCaseAction.Assign ? change.Assignee?.Value : current?.Assignee,
                    CreatedAt: current?.CreatedAt ?? timestamp,
                    UpdatedAt: timestamp);
                await SaveCaseAsync(
                    connection: connection, transaction: transaction, next: next, subject: change.Subject,
                    insert: current is null, token: cancellationToken).ConfigureAwait(false);
                if (change.Action == LabelCaseAction.OpenAppeal)
                {
                    await CaptureAppealAsync(
                        connection: connection,
                        transaction: transaction,
                        caseId: next.Id,
                        initiator: caller.Actor.Value,
                        subject: change.Subject,
                        createdAt: timestamp,
                        challenges: challenges,
                        token: cancellationToken).ConfigureAwait(false);
                }

                ModerationCaseEvent result = await AppendCaseEventAsync(
                    connection: connection, transaction: transaction, next: next, operationId: change.OperationId,
                    action: change.Action, actor: caller.Actor, localIdentity: caller.LocalIdentity, reason: change.Reason,
                    intent: intent, operationNamespace: "case", token: cancellationToken).ConfigureAwait(false);
                return LabelServiceResult<LabelCaseReceipt>.Success(result.ToReceipt());
            },
            token: cancellationToken);
    }

    public Task<LabelServiceResult<LabelModerationCase>> GetCaseAsync(
        LabelAdministrationCaller caller,
        long caseId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);
        return ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect a moderation case.",
            action: async (connection, transaction, _) =>
            {
                ModerationCaseData? result = await ReadCaseAsync(connection, transaction, caseId, cancellationToken).ConfigureAwait(false);
                return result is null ? CaseFailure<LabelModerationCase>(LabelServiceErrorCode.CaseNotFound, "The case was not found.")
                    : LabelServiceResult<LabelModerationCase>.Success(result.ToCase());
            },
            token: cancellationToken);
    }

    public Task<LabelServiceResult<IReadOnlyList<LabelModerationCase>>> GetCasesAsync(
        LabelAdministrationCaller caller,
        LabelCaseStatus? status = null,
        long afterId = 0,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ValidateCasePage(afterId, limit);
        if (status is { } value && !Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        return ModerationAsync<IReadOnlyList<LabelModerationCase>>(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect moderation cases.",
            action: async (connection, transaction, _) =>
            {
                DbCommand command = Command(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT payload FROM label_cases WHERE case_id>@after" +
                        (status is null ? string.Empty : " AND status=@status") + " ORDER BY case_id LIMIT @limit",
                    ("@after", afterId), ("@status", (int?)status), ("@limit", limit));
                await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                List<LabelModerationCase> cases = [];
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    cases.Add(DecodeCase((byte[])reader.GetValue(0)).ToCase());
                }

                return LabelServiceResult<IReadOnlyList<LabelModerationCase>>.Success(cases.AsReadOnly());
            },
            token: cancellationToken);
    }

    public Task<LabelServiceResult<IReadOnlyList<LabelCaseReceipt>>> GetCaseHistoryAsync(
        LabelAdministrationCaller caller,
        long caseId,
        long afterSequence = 0,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);
        ValidateCasePage(afterSequence, limit);
        return ModerationAsync<IReadOnlyList<LabelCaseReceipt>>(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect moderation case history.",
            action: async (connection, transaction, _) =>
            {
                if (await ReadCaseAsync(connection, transaction, caseId, cancellationToken).ConfigureAwait(false) is null)
                {
                    return CaseFailure<IReadOnlyList<LabelCaseReceipt>>(LabelServiceErrorCode.CaseNotFound, "The case was not found.");
                }

                DbCommand command = Command(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT payload FROM label_case_events WHERE case_id=@id AND seq>@after ORDER BY seq LIMIT @limit",
                    ("@id", caseId), ("@after", afterSequence), ("@limit", limit));
                await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                List<LabelCaseReceipt> history = [];
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    history.Add(DecodeCaseEvent((byte[])reader.GetValue(0)).ToReceipt());
                }

                return LabelServiceResult<IReadOnlyList<LabelCaseReceipt>>.Success(history.AsReadOnly());
            },
            token: cancellationToken);
    }

    public Task<LabelServiceResult<LabelCaseReceipt>> GetCaseOperationAsync(
        LabelAdministrationCaller caller,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        LabelInput.ValidateText(operationId, nameof(operationId), 256);
        return ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: operationId,
            reason: "Recover a moderation case operation.",
            action: async (connection, transaction, _) =>
            {
                DbCommand command = Command(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT operation_id,payload FROM label_case_events WHERE operation_hash=@hash",
                    ("@hash", CaseOperationHash(operationId)));
                await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return CaseFailure<LabelCaseReceipt>(LabelServiceErrorCode.OperationNotFound, "The case operation was not found.");
                }

                if (reader.GetString(0) != operationId)
                {
                    return CaseFailure<LabelCaseReceipt>(LabelServiceErrorCode.IdempotencyConflict, "The operation hash identifies different intent.");
                }

                return LabelServiceResult<LabelCaseReceipt>.Success(DecodeCaseEvent((byte[])reader.GetValue(1)).ToReceipt());
            },
            token: cancellationToken);
    }

    /// <summary>Checks live authority under the issuer lock and couples denials and mutations to administrative audit.</summary>
    private Task<LabelServiceResult<T>> ModerationAsync<T>(
        LabelAdministrationCaller caller,
        LabelAdministrationPermission permission,
        string operationId,
        string reason,
        Func<DbConnection, DbTransaction, AdministrationState, Task<LabelServiceResult<T>>> action,
        CancellationToken token,
        string auditAction = "moderation-case") where T : class
    {
        ArgumentNullException.ThrowIfNull(caller);
        return AdministrationAsync(async (connection, transaction, state) =>
        {
            AdministrationState team = await ReadAdministrationAsync(connection, transaction, token).ConfigureAwait(false);
            bool allowed = await HasPermissionAsync(
                connection: connection, transaction: transaction, state: state, administration: team,
                caller: caller, permission: permission, token: token).ConfigureAwait(false);
            LabelServiceResult<T> result = allowed ? await action(connection, transaction, team).ConfigureAwait(false)
                : CaseFailure<T>(LabelServiceErrorCode.Unauthorized, "Current team authority is required.");
            if (!result.Succeeded || permission != LabelAdministrationPermission.Inspect)
            {
                await AuditAdministrationAsync(
                    connection: connection, transaction: transaction, administration: team,
                    actor: caller.Actor, localIdentity: caller.LocalIdentity, operationId: operationId,
                    action: auditAction, reason: reason, succeeded: result.Succeeded, token: token).ConfigureAwait(false);
            }

            return result;
        }, token);
    }

    /// <summary>Reads an exact unresolved subject while retaining collision checks independently of its hash.</summary>
    private static async Task<ModerationCaseData?> OpenCaseAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelReportSubject subject,
        CancellationToken token)
    {
        byte[] key = CaseSubjectKey(subject);
        DbCommand command = Command(
            connection: connection,
            transaction: transaction,
            sql: "SELECT subject_key,payload FROM label_cases WHERE subject_hash=@hash AND status<>@resolved",
            ("@hash", SHA256.HashData(key)), ("@resolved", (int)LabelCaseStatus.Resolved));
        await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
        DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
        ModerationCaseData? result = null;
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            if (((byte[])reader.GetValue(0)).AsSpan().SequenceEqual(key))
            {
                if (result is not null)
                {
                    throw new InvalidDataException("The subject has multiple unresolved cases.");
                }

                result = DecodeCase((byte[])reader.GetValue(1));
            }
        }

        return result;
    }

    /// <summary>Reads one immutable case payload under the provider's issuer lock.</summary>
    private static async Task<ModerationCaseData?> ReadCaseAsync(
        DbConnection connection,
        DbTransaction transaction,
        long caseId,
        CancellationToken token)
    {
        object? payload = await ScalarAsync(
            connection: connection, transaction: transaction,
            sql: "SELECT payload FROM label_cases WHERE case_id=@id", token: token, ("@id", caseId)).ConfigureAwait(false);
        return payload is byte[] bytes ? DecodeCase(bytes) : null;
    }

    /// <summary>Encodes canonical exact-subject identity with a delimiter absent from validated identifiers.</summary>
    private static byte[] CaseSubjectKey(LabelReportSubject subject) =>
        Encoding.UTF8.GetBytes(subject.Subject + "\n" + subject.Cid?.ToString());

    /// <summary>Separates manual workflow identities from automatic report-link events.</summary>
    private static byte[] CaseOperationHash(string operationId, string operationNamespace = "case") =>
        OperationHash(operationNamespace + "\0" + operationId);

    /// <summary>Writes case state for manual workflow edits and automatic report linkage under the same issuer lock.</summary>
    private static Task SaveCaseAsync(
        DbConnection connection,
        DbTransaction transaction,
        ModerationCaseData next,
        LabelReportSubject subject,
        bool insert,
        CancellationToken token)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(next, ModerationJsonContext.Default.ModerationCaseData);
        byte[] key = CaseSubjectKey(subject);
        byte[]? assignee = next.Assignee is null ? null : s_strictUtf8.GetBytes(next.Assignee);
        byte[]? assigneeHash = assignee is null ? null : SHA256.HashData(assignee);
        return insert
            ? ExecuteAsync(
                connection: connection, transaction: transaction,
                sql: "INSERT INTO label_cases(case_id,subject_hash,subject_key,status,assignee_hash,assignee_key,payload) " +
                    "VALUES(@id,@hash,@key,@status,@assigneeHash,@assignee,@payload)",
                token: token, ("@id", next.Id), ("@hash", SHA256.HashData(key)), ("@key", key),
                ("@status", (int)next.Status), ("@assigneeHash", assigneeHash), ("@assignee", assignee), ("@payload", payload))
            : ExecuteAsync(
                connection: connection, transaction: transaction,
                sql: "UPDATE label_cases SET status=@status,assignee_hash=@assigneeHash,assignee_key=@assignee,payload=@payload WHERE case_id=@id",
                token: token, ("@id", next.Id), ("@status", (int)next.Status),
                ("@assigneeHash", assigneeHash), ("@assignee", assignee), ("@payload", payload));
    }

    /// <summary>Appends exact historical case state for both moderator edits and report linkage.</summary>
    private static async Task<ModerationCaseEvent> AppendCaseEventAsync(
        DbConnection connection,
        DbTransaction transaction,
        ModerationCaseData next,
        string operationId,
        LabelCaseAction action,
        Did actor,
        string? localIdentity,
        string reason,
        byte[] intent,
        string operationNamespace,
        CancellationToken token)
    {
        long sequence = checked(Integer(await ScalarAsync(
            connection: connection, transaction: transaction,
            sql: "SELECT COALESCE(MAX(seq),0) FROM label_case_events", token: token).ConfigureAwait(false)) + 1);
        ModerationCaseEvent result = new(
            Sequence: sequence,
            OperationId: operationId,
            Action: action,
            Actor: actor.Value,
            LocalIdentity: localIdentity,
            Reason: reason,
            CreatedAt: next.UpdatedAt,
            Case: next);
        await ExecuteAsync(
            connection: connection, transaction: transaction,
            sql: "INSERT INTO label_case_events(seq,case_id,operation_hash,operation_id,intent,payload) VALUES(@seq,@id,@hash,@operation,@intent,@payload)",
            token: token,
            ("@seq", sequence), ("@id", next.Id), ("@hash", CaseOperationHash(operationId, operationNamespace)),
            ("@operation", operationId), ("@intent", intent),
            ("@payload", JsonSerializer.SerializeToUtf8Bytes(result, ModerationJsonContext.Default.ModerationCaseEvent))).ConfigureAwait(false);
        return result;
    }

    /// <summary>Decodes required case metadata without reflection or success-shaped null recovery.</summary>
    private static ModerationCaseData DecodeCase(byte[] bytes) =>
        JsonSerializer.Deserialize(bytes, ModerationJsonContext.Default.ModerationCaseData)
            ?? throw new InvalidDataException("The case payload is null.");

    /// <summary>Decodes a required immutable case event.</summary>
    private static ModerationCaseEvent DecodeCaseEvent(byte[] bytes) =>
        JsonSerializer.Deserialize(bytes, ModerationJsonContext.Default.ModerationCaseEvent)
            ?? throw new InvalidDataException("The case event is null.");

    /// <summary>Rejects unbounded or backwards keyset pagination before opening storage.</summary>
    private static void ValidateCasePage(long after, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(after);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 100);
    }

    /// <summary>Returns a classified moderation outcome without suppressing provider failures.</summary>
    private static LabelServiceResult<T> CaseFailure<T>(LabelServiceErrorCode code, string message) where T : class =>
        LabelServiceResult<T>.Failure(new(code, message));
}
