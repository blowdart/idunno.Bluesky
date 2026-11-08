// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    /// <summary>Resumes bounded query projection backfills without changing retained source payloads.</summary>
    private async Task BackfillQueryProjectionsAsync(DbConnection connection, DbTransaction? transaction, CancellationToken token)
    {
        foreach ((string source, string projection, string id) in new[]
        {
            ("label_cases", "label_case_queries", "case_id"),
            ("label_reports", "label_report_queries", "report_id"),
            ("label_case_events", "label_activity_queries", "seq"),
            ("label_admin_audit", "label_audit_queries", "seq")
        })
        {
            long after = 0;
            while (true)
            {
                List<(long Id, byte[] Payload)> rows = [];
                DbCommand command = Command(
                    connection: connection,
                    transaction: transaction,
                    sql: $"SELECT t.{id},t.payload FROM {source} t WHERE t.{id}>@after AND " +
                        $"NOT EXISTS(SELECT 1 FROM {projection} q WHERE q.{id}=t.{id}) ORDER BY t.{id} LIMIT 100",
                    ("@after", after));
                await using (command.ConfigureAwait(false))
                {
                    DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                    await using (reader.ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            rows.Add((Integer(reader.GetValue(0)), (byte[])reader.GetValue(1)));
                        }
                    }
                }
                foreach ((long number, byte[] payload) in rows)
                {
                    if (source == "label_cases")
                    {
                        await SaveCaseQueryAsync(
                            connection: connection,
                            transaction: transaction,
                            item: DecodeCase(payload),
                            payload: payload,
                            token: token).ConfigureAwait(false);
                    }
                    else if (source == "label_reports")
                    {
                        await SaveReportQueryAsync(
                            connection: connection,
                            transaction: transaction,
                            item: DecodeReport(payload),
                            payload: payload,
                            token: token).ConfigureAwait(false);
                    }
                    else if (source == "label_case_events")
                    {
                        await SaveActivityQueryAsync(
                            connection: connection,
                            transaction: transaction,
                            item: DecodeCaseEvent(payload),
                            payload: payload,
                            token: token).ConfigureAwait(false);
                    }
                    else
                    {
                        await SaveAuditQueryAsync(
                            connection: connection,
                            transaction: transaction,
                            sequence: number,
                            token: token).ConfigureAwait(false);
                    }
                    after = number;
                }
                if (rows.Count < 100)
                {
                    break;
                }
            }
        }
    }

    /// <summary>Preserves the persisted timestamp's lexical ordering rather than normalizing legacy offsets or precision.</summary>
    private static string? QueryTimestamp(byte[] payload)
    {
        using JsonDocument document = JsonDocument.Parse(payload);
        return document.RootElement.TryGetProperty(nameof(ModerationReportData.CreatedAt), out JsonElement value) ? value.GetString() : null;
    }

    /// <summary>Maintains query timestamps and exact assignment guards atomically with case state changes.</summary>
    private Task SaveCaseQueryAsync(
        DbConnection connection,
        DbTransaction? transaction,
        ModerationCaseData item,
        byte[] payload,
        CancellationToken token)
    {
        byte[]? assignee = item.Assignee is null ? null : s_strictUtf8.GetBytes(item.Assignee);
        return ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "INSERT INTO label_case_queries(case_id,created_at,assignee_hash,assignee_key) VALUES(@id,@created,@hash,@key) " +
                (Provider == "mysql"
                    ? "ON DUPLICATE KEY UPDATE created_at=@created,assignee_hash=@hash,assignee_key=@key"
                    : "ON CONFLICT(case_id) DO UPDATE SET created_at=@created,assignee_hash=@hash,assignee_key=@key"),
            token: token,
            ("@id", item.Id),
            ("@created", QueryTimestamp(payload)),
            ("@hash", assignee is null ? null : SHA256.HashData(assignee)),
            ("@key", assignee));
    }

    /// <summary>Maintains report handling, exact subject and author projections without changing submitted evidence.</summary>
    private Task SaveReportQueryAsync(
        DbConnection connection,
        DbTransaction? transaction,
        ModerationReportData item,
        byte[] payload,
        CancellationToken token)
    {
        string repository = ModerationSubjectEncoding.Decode(item.Subject, item.Cid).Repository.Value;
        byte[] key = s_strictUtf8.GetBytes(repository);
        return ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "INSERT INTO label_report_queries(report_id,case_id,subject,repository_hash,repository_key,handled,created_at) " +
                "VALUES(@id,@case,@subject,@hash,@key,@handled,@created) " +
                (Provider == "mysql"
                    ? "ON DUPLICATE KEY UPDATE handled=@handled,created_at=@created"
                    : "ON CONFLICT(report_id) DO UPDATE SET handled=@handled,created_at=@created"),
            token: token,
            ("@id", item.Id),
            ("@case", item.CaseId),
            ("@subject", item.Subject),
            ("@hash", SHA256.HashData(key)),
            ("@key", key),
            ("@handled", item.DecisionOperationId is null or "null" ? 0 : 1),
            ("@created", QueryTimestamp(payload)));
    }

    /// <summary>Persists immutable activity ordering keys so history pages need not decode every event.</summary>
    private static Task SaveActivityQueryAsync(
        DbConnection connection,
        DbTransaction? transaction,
        ModerationCaseEvent item,
        byte[] payload,
        CancellationToken token) =>
        ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "INSERT INTO label_activity_queries(seq,case_id,created_at) VALUES(@seq,@case,@created)",
            token: token,
            ("@seq", item.Sequence),
            ("@case", item.Case.Id),
            ("@created", QueryTimestamp(payload)));

    /// <summary>Projects audit filters with the original provider's timestamp conversion, preserving legacy null times and precision.</summary>
    private Task SaveAuditQueryAsync(
        DbConnection connection,
        DbTransaction? transaction,
        long sequence,
        CancellationToken token) =>
        ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: $"INSERT INTO label_audit_queries(seq,actor,succeeded,created_ms) SELECT t.seq,{TableJsonField("Actor")}," +
                (Provider == "sqlite" ? $"CASE WHEN {TableJsonField("Succeeded")}=1 THEN 1 WHEN {TableJsonField("Succeeded")}=0 THEN 0 ELSE NULL END," :
                    $"CASE WHEN {TableJsonField("Succeeded")}='true' THEN 1 WHEN {TableJsonField("Succeeded")}='false' THEN 0 ELSE NULL END,") +
                $"{AuditSourceTimeMilliseconds()} FROM label_admin_audit t WHERE t.seq=@seq",
            token: token,
            ("@seq", sequence));

    /// <summary>Updates report evidence and its handling projection in the same transaction on every resolution path.</summary>
    private async Task SaveReportPayloadAsync(
        DbConnection connection,
        DbTransaction transaction,
        ModerationReportData report,
        CancellationToken token)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(report, ModerationJsonContext.Default.ModerationReportData);
        await ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "UPDATE label_reports SET payload=@payload WHERE report_id=@id",
            token: token,
            ("@payload", payload),
            ("@id", report.Id)).ConfigureAwait(false);
        await SaveReportQueryAsync(
            connection: connection,
            transaction: transaction,
            item: report,
            payload: payload,
            token: token).ConfigureAwait(false);
    }
}
