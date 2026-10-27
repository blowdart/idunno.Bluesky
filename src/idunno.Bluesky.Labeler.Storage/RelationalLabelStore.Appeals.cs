// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    public Task<LabelServiceResult<LabelAppeal>> GetAppealAsync(
        LabelAdministrationCaller caller,
        long appealId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(appealId);
        return ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect a confidential appeal.",
            action: async (connection, transaction, _) =>
            {
                ModerationAppealData? result = await ReadAppealAsync(connection, transaction, appealId, cancellationToken).ConfigureAwait(false);
                return result is null
                    ? CaseFailure<LabelAppeal>(LabelServiceErrorCode.OperationNotFound, "The appeal was not found.")
                    : LabelServiceResult<LabelAppeal>.Success(result.ToAppeal());
            },
            token: cancellationToken,
            auditAction: "appeal-inspection");
    }

    public Task<LabelServiceResult<IReadOnlyList<LabelAppeal>>> GetAppealsAsync(
        LabelAdministrationCaller caller,
        long? caseId = null,
        LabelAppealStatus? status = null,
        long afterId = 0,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ValidateCasePage(afterId, limit);
        if (caseId is <= 0 || status is { } filter && !Enum.IsDefined(filter))
        {
            throw new ArgumentOutOfRangeException(nameof(caseId), "The case or status filter is invalid.");
        }

        return ModerationAsync<IReadOnlyList<LabelAppeal>>(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect confidential appeal pages.",
            action: async (connection, transaction, _) =>
            {
                DbCommand command = Command(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT payload FROM label_appeals WHERE appeal_id>@after" +
                        (caseId is null ? string.Empty : " AND case_id=@case") +
                        (status is null ? string.Empty : " AND status=@status") + " ORDER BY appeal_id LIMIT @limit",
                    ("@after", afterId),
                    ("@case", caseId),
                    ("@status", status is null ? null : (int)status.Value),
                    ("@limit", limit));
                await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                List<LabelAppeal> appeals = [];
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    appeals.Add(DecodeAppeal((byte[])reader.GetValue(0)).ToAppeal());
                }

                return LabelServiceResult<IReadOnlyList<LabelAppeal>>.Success(appeals.AsReadOnly());
            },
            token: cancellationToken,
            auditAction: "appeal-inspection");
    }

    public Task<LabelServiceResult<IReadOnlyList<long>>> GetAppealReportsAsync(
        LabelAdministrationCaller caller,
        long appealId,
        long afterReportId = 0,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(appealId);
        ValidateCasePage(afterReportId, limit);
        return ModerationAsync<IReadOnlyList<long>>(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect confidential appeal submissions.",
            action: async (connection, transaction, _) =>
            {
                DbCommand command = Command(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT report_id FROM label_appeal_reports WHERE appeal_id=@appeal AND report_id>@after ORDER BY report_id LIMIT @limit",
                    ("@appeal", appealId),
                    ("@after", afterReportId),
                    ("@limit", limit));
                await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                List<long> reports = [];
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    reports.Add(reader.GetInt64(0));
                }

                return LabelServiceResult<IReadOnlyList<long>>.Success(reports.AsReadOnly());
            },
            token: cancellationToken,
            auditAction: "appeal-inspection");
    }

    /// <summary>Captures every currently active exact-version label before any report or workflow mutation.</summary>
    private static async Task<LabelServiceResult<StoredLabel[]>> ReadAppealChallengesAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelStoreState state,
        LabelReportSubject subject,
        long now,
        CancellationToken token) =>
        await ReadTargetLabelsAsync(
            connection: connection,
            transaction: transaction,
            state: state,
            target: subject.RecordUri is { } record ? LabelTarget.Record(record, subject.Cid) : LabelTarget.Account(subject.Repository),
            exactVersion: true,
            now: now,
            token: token).ConfigureAwait(false);

    /// <summary>Captures bounded active label values, optionally retaining other record versions for manual review.</summary>
    private static async Task<LabelServiceResult<StoredLabel[]>> ReadTargetLabelsAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelStoreState state,
        LabelTarget target,
        bool exactVersion,
        long now,
        CancellationToken token)
    {
        byte[] key = s_strictUtf8.GetBytes(target.Subject);
        List<string> values = [];
        DbCommand command = Command(
            connection: connection,
            transaction: transaction,
            sql: "SELECT DISTINCT value FROM label_subject_index WHERE subject_hash=@hash AND subject_key=@key LIMIT 1001",
            ("@hash", SHA256.HashData(key)),
            ("@key", key));
        await using (command.ConfigureAwait(false))
        {
            DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    values.Add(reader.GetString(0));
                }
            }
        }

        if (values.Count > 1000)
        {
            return CaseFailure<StoredLabel[]>(LabelServiceErrorCode.PolicyUnavailable, "The label target requires explicit review beyond the bounded capture limit.");
        }

        if (values.Count == 0)
        {
            return LabelServiceResult<StoredLabel[]>.Success([]);
        }

        IReadOnlyList<LabelServiceResult<LabelDecisionSnapshot>> snapshots = await DecisionSnapshotsAsync(
            connection: connection,
            transaction: transaction,
            state: state,
            queries: values.Select(value => (target, value)).ToArray(),
            token: token).ConfigureAwait(false);
        List<StoredLabel> challenges = [];
        foreach (LabelServiceResult<LabelDecisionSnapshot> snapshot in snapshots)
        {
            if (!snapshot.Succeeded)
            {
                return LabelServiceResult<StoredLabel[]>.Failure(snapshot.Error);
            }

            if (snapshot.Result.CurrentLabel is { } current && (!exactVersion || current.SignedLabel.Label.Cid == target.Cid) &&
                LabelDecisionRules.IsActive(current.SignedLabel, DateTimeOffset.FromUnixTimeMilliseconds(now)))
            {
                challenges.Add(current);
            }
        }

        return LabelServiceResult<StoredLabel[]>.Success([.. challenges.OrderBy(challenge => challenge.Sequence)]);
    }

    /// <summary>Coalesces pending identical captured outcomes while preserving every separate reporter submission.</summary>
    private static async Task CaptureReportAppealAsync(
        DbConnection connection,
        DbTransaction transaction,
        ModerationReportData report,
        IReadOnlyList<StoredLabel> challenges,
        CancellationToken token)
    {
        long appealId = await CaptureAppealAsync(
            connection: connection,
            transaction: transaction,
            caseId: report.CaseId,
            initiator: report.Reporter,
            subject: report.ToReport().Submission.Subject,
            createdAt: report.CreatedAt,
            challenges: challenges,
            token: token).ConfigureAwait(false);
        await ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "INSERT INTO label_appeal_reports(report_id,appeal_id) VALUES(@report,@appeal)",
            token: token,
            ("@report", report.Id),
            ("@appeal", appealId)).ConfigureAwait(false);
    }

    /// <summary>Shares immutable challenge capture between authenticated reports and internal moderator reviews.</summary>
    private static async Task<long> CaptureAppealAsync(
        DbConnection connection,
        DbTransaction transaction,
        long caseId,
        string initiator,
        LabelReportSubject subject,
        DateTimeOffset createdAt,
        IReadOnlyList<StoredLabel> challenges,
        CancellationToken token)
    {
        using MemoryStream buffer = new();
        using (BinaryWriter writer = new(buffer, s_strictUtf8, leaveOpen: true))
        {
            writer.Write(caseId);
            foreach (StoredLabel challenge in challenges)
            {
                writer.Write(challenge.Sequence);
            }
        }

        byte[] key = buffer.ToArray();
        long? appealId = null;
        DbCommand command = Command(
            connection: connection,
            transaction: transaction,
            sql: "SELECT appeal_id,pending_key FROM label_appeals WHERE pending_hash=@hash AND status=@pending",
            ("@hash", SHA256.HashData(key)),
            ("@pending", (int)LabelAppealStatus.Pending));
        await using (command.ConfigureAwait(false))
        {
            DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    if (((byte[])reader.GetValue(1)).AsSpan().SequenceEqual(key))
                    {
                        if (appealId is not null)
                        {
                            throw new InvalidDataException("The captured outcome has multiple pending appeals.");
                        }

                        appealId = reader.GetInt64(0);
                    }
                }
            }
        }

        if (appealId is null)
        {
            appealId = checked(Integer(await ScalarAsync(
                connection: connection,
                transaction: transaction,
                sql: "SELECT COALESCE(MAX(appeal_id),0) FROM label_appeals",
                token: token).ConfigureAwait(false)) + 1);
            ModerationAppealData appeal = new(
                Id: appealId.Value,
                CaseId: caseId,
                Initiator: initiator,
                Subject: subject.Subject,
                Cid: subject.Cid?.ToString(),
                Challenges: [.. challenges.Select(challenge => new ModerationAppealChallenge(
                    challenge.Sequence, challenge.SignedLabel.GetJsonBytes()))],
                Status: LabelAppealStatus.Pending,
                CreatedAt: createdAt,
                UpdatedAt: createdAt,
                DecisionOperationId: null);
            await ExecuteAsync(
                connection: connection,
                transaction: transaction,
                sql: "INSERT INTO label_appeals(appeal_id,case_id,status,pending_hash,pending_key,payload) VALUES(@id,@case,@status,@hash,@key,@payload)",
                token: token,
                ("@id", appealId),
                ("@case", caseId),
                ("@status", (int)LabelAppealStatus.Pending),
                ("@hash", SHA256.HashData(key)),
                ("@key", key),
                ("@payload", JsonSerializer.SerializeToUtf8Bytes(appeal, ModerationJsonContext.Default.ModerationAppealData))).ConfigureAwait(false);
        }

        return appealId.Value;
    }

    /// <summary>Reads required original appeal provenance under the issuer lock.</summary>
    private static async Task<ModerationAppealData?> ReadAppealAsync(
        DbConnection connection,
        DbTransaction transaction,
        long appealId,
        CancellationToken token)
    {
        object? payload = await ScalarAsync(
            connection: connection,
            transaction: transaction,
            sql: "SELECT payload FROM label_appeals WHERE appeal_id=@id",
            token: token,
            ("@id", appealId)).ConfigureAwait(false);
        return payload is byte[] bytes ? DecodeAppeal(bytes) : null;
    }

    /// <summary>Fences every captured label and requires explicit appeal outcomes before changing labels or case status.</summary>
    private async Task<LabelServiceResult<IReadOnlyList<ModerationAppealData>>> ValidateAppealDecisionAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelStoreState state,
        LabelCaseDecision request,
        CancellationToken token)
    {
        if (request.AppealIds.Count == 0 && Integer(await ScalarAsync(
            connection: connection,
            transaction: transaction,
            sql: "SELECT COUNT(*) FROM label_appeals WHERE case_id=@case AND status=@pending",
            token: token,
            ("@case", request.CaseId),
            ("@pending", (int)LabelAppealStatus.Pending)).ConfigureAwait(false)) != 0)
        {
            return CaseFailure<IReadOnlyList<ModerationAppealData>>(
                LabelServiceErrorCode.CaseConflict, "Pending appeals require an explicit review outcome.");
        }

        List<ModerationAppealData> appeals = [];
        HashSet<string> challengedValues = new(StringComparer.Ordinal);
        DateTimeOffset now = DateTimeOffset.FromUnixTimeMilliseconds(
            await DatabaseTimeAsync(connection, transaction, token).ConfigureAwait(false));
        foreach (long id in request.AppealIds)
        {
            ModerationAppealData? appeal = await ReadAppealAsync(connection, transaction, id, token).ConfigureAwait(false);
            if (appeal is null || appeal.CaseId != request.CaseId || appeal.Status != LabelAppealStatus.Pending)
            {
                return CaseFailure<IReadOnlyList<ModerationAppealData>>(
                    LabelServiceErrorCode.CaseConflict, "The appeal is not pending on this exact case.");
            }

            bool changed = false;
            if (appeal.Challenges.Length > 0)
            {
                StoredLabel[] captured = appeal.ToAppeal().Challenges.ToArray();
                IReadOnlyList<LabelServiceResult<LabelDecisionSnapshot>> current = await DecisionSnapshotsAsync(
                    connection: connection,
                    transaction: transaction,
                    state: state,
                    queries: captured.Select(challenge =>
                        (appeal.Cid is null ? LabelTarget.Account(new Did(appeal.Subject)) :
                            LabelTarget.Record(new(appeal.Subject), new(appeal.Cid)), challenge.SignedLabel.Label.Value)).ToArray(),
                    token: token).ConfigureAwait(false);
                for (int index = 0; index < captured.Length; index++)
                {
                    LabelServiceResult<LabelDecisionSnapshot> snapshot = current[index];
                    if (!snapshot.Succeeded)
                    {
                        return LabelServiceResult<IReadOnlyList<ModerationAppealData>>.Failure(snapshot.Error);
                    }

                    challengedValues.Add(captured[index].SignedLabel.Label.Value);
                    changed |= snapshot.Result.CurrentLabel is not { } label ||
                        label.Sequence != captured[index].Sequence || !LabelDecisionRules.IsActive(label.SignedLabel, now);
                }
            }

            if (changed != (request.AppealOutcome == LabelAppealStatus.Superseded))
            {
                return CaseFailure<IReadOnlyList<ModerationAppealData>>(
                    LabelServiceErrorCode.StaleDecision,
                    changed ? "Newer or expired label outcomes require explicit superseded review." : "The captured label outcomes have not been superseded.");
            }

            appeals.Add(appeal);
        }

        if (request.AppealIds.Count > 0 && request.Actions.Any(action =>
            action.Kind == LabelOperationKind.Negate && !challengedValues.Contains(action.Value)))
        {
            return CaseFailure<IReadOnlyList<ModerationAppealData>>(
                LabelServiceErrorCode.TargetNotAllowed, "Appeal withdrawals must identify explicitly captured challenged labels.");
        }

        return LabelServiceResult<IReadOnlyList<ModerationAppealData>>.Success(appeals.AsReadOnly());
    }

    /// <summary>Closes only fully obsolete exact-target appeals under the decision transaction, retaining original evidence.</summary>
    private static async Task<IReadOnlyList<ModerationAppealData>> SupersedeObsoleteAppealsAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelStoreState state,
        LabelReportSubject subject,
        LabelCaseDecision request,
        DateTimeOffset completedAt,
        CancellationToken token)
    {
        LabelServiceResult<StoredLabel[]> observed = await ReadAppealChallengesAsync(
            connection: connection,
            transaction: transaction,
            state: state,
            subject: subject,
            now: completedAt.ToUnixTimeMilliseconds(),
            token: token).ConfigureAwait(false);
        if (!observed.Succeeded)
        {
            throw new InvalidDataException("Current appeal outcomes could not be inspected: " + observed.Error.Message);
        }

        HashSet<long> current = observed.Result.Select(label => label.Sequence).ToHashSet();
        List<ModerationAppealData> superseded = [];
        long after = 0;
        while (true)
        {
            List<ModerationAppealData> page = [];
            DbCommand command = Command(
                connection: connection,
                transaction: transaction,
                sql: "SELECT a.payload FROM label_appeals a JOIN label_cases c ON c.case_id=a.case_id " +
                    "WHERE c.subject_hash=@subject AND a.status=0 AND a.appeal_id>@after ORDER BY a.appeal_id LIMIT 128",
                ("@subject", SHA256.HashData(CaseSubjectKey(subject))),
                ("@after", after));
            await using (command.ConfigureAwait(false))
            {
                DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        page.Add(DecodeAppeal((byte[])reader.GetValue(0)));
                    }
                }
            }

            foreach (ModerationAppealData appeal in page)
            {
                if (appeal.Subject != subject.Subject || appeal.Cid != subject.Cid?.ToString() ||
                    appeal.Challenges.Length == 0 || appeal.Challenges.Any(challenge => current.Contains(challenge.Sequence)))
                {
                    continue;
                }

                ModerationAppealData resolved = appeal with
                {
                    Status = LabelAppealStatus.Superseded,
                    UpdatedAt = completedAt,
                    DecisionOperationId = request.OperationId,
                    ResolutionReason = $"Automatically superseded by appeal decision {request.OperationId}: all captured label outcomes are no longer current."
                };
                await SaveAppealOutcomeAsync(
                    connection: connection,
                    transaction: transaction,
                    appeal: resolved,
                    token: token).ConfigureAwait(false);
                superseded.Add(resolved);
            }

            if (page.Count < 128)
            {
                return superseded;
            }

            after = page[^1].Id;
        }
    }

    /// <summary>Retains capture provenance while atomically recording an explicit moderator resolution.</summary>
    private static Task SaveAppealOutcomeAsync(
        DbConnection connection,
        DbTransaction transaction,
        ModerationAppealData appeal,
        CancellationToken token) =>
        ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "UPDATE label_appeals SET status=@status,payload=@payload WHERE appeal_id=@id",
            token: token,
            ("@status", (int)appeal.Status),
            ("@payload", JsonSerializer.SerializeToUtf8Bytes(appeal, ModerationJsonContext.Default.ModerationAppealData)),
            ("@id", appeal.Id));

    /// <summary>Rejects missing appeal payloads rather than inventing empty challenge provenance.</summary>
    private static ModerationAppealData DecodeAppeal(byte[] bytes) =>
        JsonSerializer.Deserialize(bytes, ModerationJsonContext.Default.ModerationAppealData)
            ?? throw new InvalidDataException("The appeal payload is null.");
}
