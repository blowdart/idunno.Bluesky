// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    private const long HandleSearchLifetime = 3_600_000;

    /// <summary>Searches normalized text and fresh verified subject handles without loading whole histories into the application.</summary>
    Task<LabelServiceResult<LabelSearchPage>> ILabelSearchStore.SearchAsync(
        LabelAdministrationCaller caller,
        string query,
        long page,
        CancellationToken cancellationToken)
    {
        LabelInput.ValidateText(query, nameof(query), 512);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        string textPattern = SearchPattern(query.Trim());
        string handlePattern = SearchPattern(query.Trim().TrimStart('@'));
        return ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Search retained reports and appeals.",
            action: async (connection, transaction, _) =>
            {
                long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                string fresh = "h.checked_ms>@old AND h.checked_ms<=@now";
                string handleMatch = query.Trim().TrimStart('@').Length == 0 ? "1=0" :
                    "EXISTS(SELECT 1 FROM label_search_handles h WHERE " +
                    $"h.subject_hash=c.subject_hash AND h.subject_key=c.subject_key AND {fresh} AND h.handle LIKE @handle ESCAPE '!')";
                string reports = "SELECT 0 AS kind,t.report_id AS number,t.payload," + TableJsonField("CreatedAt") +
                    " AS received FROM label_reports t JOIN label_cases c ON c.case_id=t.case_id WHERE " +
                    handleMatch + " OR EXISTS(SELECT 1 FROM label_search_text s WHERE s.kind=0 AND s.source_id=t.report_id AND s.content LIKE @text ESCAPE '!')";
                string appeals = "SELECT 1 AS kind,t.appeal_id AS number,t.payload," + TableJsonField("CreatedAt") +
                    " AS received FROM label_appeals t JOIN label_cases c ON c.case_id=t.case_id WHERE " +
                    handleMatch + $" OR UPPER({TableJsonField("Subject")}) LIKE @text ESCAPE '!'" +
                    " OR EXISTS(SELECT 1 FROM label_appeal_reports ar JOIN label_search_text s ON s.kind=0 AND s.source_id=ar.report_id " +
                    "WHERE ar.appeal_id=t.appeal_id AND s.content LIKE @text ESCAPE '!')" +
                    " OR EXISTS(SELECT 1 FROM label_case_events e JOIN label_search_text s ON s.kind=1 AND s.source_id=e.seq " +
                    $"WHERE e.case_id=t.case_id AND e.operation_id={TableJsonField("DecisionOperationId")} AND s.content LIKE @text ESCAPE '!')";
                string dataset = reports + " UNION ALL " + appeals;
                (string, object?)[] parameters =
                [
                    ("@text", textPattern), ("@handle", handlePattern),
                    ("@old", now - HandleSearchLifetime), ("@now", now)
                ];
                long total = Integer(await ScalarAsync(
                    connection: connection, transaction: transaction,
                    sql: $"SELECT COUNT(*) FROM ({dataset}) matches", token: cancellationToken, parameters).ConfigureAwait(false));
                long current = Math.Min(page, Math.Max(1, (total + 15) / 16));
                List<LabelSearchHit> hits = [];
                DbCommand command = Command(
                    connection: connection, transaction: transaction,
                    sql: $"SELECT kind,payload FROM ({dataset}) matches ORDER BY received DESC,kind DESC,number DESC LIMIT 16 OFFSET @offset",
                    [.. parameters, ("@offset", (current - 1) * 16)]);
                await using (command.ConfigureAwait(false))
                {
                    DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    await using (reader.ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            byte[] payload = (byte[])reader.GetValue(1);
                            hits.Add(Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture) == 0
                                ? new(Report: DecodeReport(payload).ToReport())
                                : new(Appeal: DecodeAppeal(payload).ToAppeal()));
                        }
                    }
                }

                for (int index = 0; index < hits.Count; index++)
                {
                    if (hits[index].Appeal is { } appeal)
                    {
                        DbCommand excerpt = Command(
                            connection: connection, transaction: transaction,
                            sql: "SELECT r.payload FROM label_appeal_reports ar JOIN label_reports r ON r.report_id=ar.report_id " +
                                "JOIN label_search_text s ON s.kind=0 AND s.source_id=r.report_id WHERE ar.appeal_id=@id " +
                                "ORDER BY CASE WHEN s.content LIKE @text ESCAPE '!' THEN 0 ELSE 1 END,r.report_id DESC LIMIT 1",
                            ("@id", appeal.Id), ("@text", textPattern));
                        await using (excerpt.ConfigureAwait(false))
                        {
                            if (await excerpt.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is byte[] payload)
                            {
                                ModerationReportData report = DecodeReport(payload);
                                hits[index] = hits[index] with { MatchText = report.Reason, RelatedReportId = report.Id };
                            }
                        }
                    }
                }

                long pending = Integer(await ScalarAsync(
                    connection: connection, transaction: transaction,
                    sql: "SELECT COUNT(*) FROM label_cases c WHERE NOT EXISTS(SELECT 1 FROM label_search_handles h " +
                        $"WHERE h.subject_hash=c.subject_hash AND h.subject_key=c.subject_key AND {fresh})",
                    token: cancellationToken, ("@old", now - HandleSearchLifetime), ("@now", now)).ConfigureAwait(false));
                long unavailable = Integer(await ScalarAsync(
                    connection: connection, transaction: transaction,
                    sql: "SELECT COUNT(*) FROM label_cases c JOIN label_search_handles h ON h.subject_hash=c.subject_hash AND h.subject_key=c.subject_key " +
                        $"WHERE {fresh} AND h.handle IS NULL",
                    token: cancellationToken, ("@old", now - HandleSearchLifetime), ("@now", now)).ConfigureAwait(false));
                return LabelServiceResult<LabelSearchPage>.Success(new(current, total, hits, pending, unavailable));
            },
            token: cancellationToken,
            auditAction: "moderation-search");
    }

    /// <summary>Reads only public DIDs needed by the host indexer, never exporting moderation text or authorizing a browser.</summary>
    Task<LabelServiceResult<LabelHandleIndexBatch>> ILabelSearchStore.ReadHandleIndexBatchAsync(CancellationToken cancellationToken) =>
        AdministrationAsync(async (connection, transaction, _) =>
        {
            long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            List<LabelHandleIndexEntry> entries = [];
            DbCommand command = Command(
                connection: connection, transaction: transaction,
                sql: "SELECT c.payload FROM label_cases c WHERE NOT EXISTS(SELECT 1 FROM label_search_handles h " +
                    "WHERE h.subject_hash=c.subject_hash AND h.subject_key=c.subject_key AND h.checked_ms>@old AND h.checked_ms<=@now) " +
                    "ORDER BY c.case_id LIMIT 32",
                ("@old", now - HandleSearchLifetime), ("@now", now));
            await using (command.ConfigureAwait(false))
            {
                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        LabelModerationCase moderationCase = DecodeCase((byte[])reader.GetValue(0)).ToCase();
                        entries.Add(new(moderationCase.Id, moderationCase.Subject.Repository.Value));
                    }
                }
            }

            return LabelServiceResult<LabelHandleIndexBatch>.Success(new(now, entries));
        }, cancellationToken);

    /// <summary>Stores collision-checked, non-regressing public handle observations; failed verification clears the previous match.</summary>
    Task<LabelServiceResult<LabelHandleIndexBatch>> ILabelSearchStore.SaveHandleIndexBatchAsync(
        LabelHandleIndexBatch batch,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(batch.Entries.Count, 32);
        return AdministrationAsync(async (connection, transaction, _) =>
        {
            foreach (LabelHandleIndexEntry entry in batch.Entries)
            {
                ModerationCaseData? current = await ReadCaseAsync(connection, transaction, entry.CaseId, cancellationToken).ConfigureAwait(false);
                if (current is null || current.ToCase().Subject.Repository.Value != entry.Did ||
                    entry.Handle is not null && (!Handle.TryParse(entry.Handle, out Handle? parsed) || parsed == Handle.Invalid))
                {
                    return CaseFailure<LabelHandleIndexBatch>(LabelServiceErrorCode.InvalidTarget, "The public handle observation does not match its case.");
                }

                byte[] key = CaseSubjectKey(current.ToCase().Subject);
                string sql = Provider switch
                {
                    "mysql" => "INSERT INTO label_search_handles(subject_hash,subject_key,handle,checked_ms) VALUES(@hash,@key,@handle,@time) " +
                        "ON DUPLICATE KEY UPDATE handle=IF(subject_key=@key AND checked_ms<=@time,@handle,handle),checked_ms=IF(subject_key=@key AND checked_ms<=@time,@time,checked_ms)",
                    _ => "INSERT INTO label_search_handles(subject_hash,subject_key,handle,checked_ms) VALUES(@hash,@key,@handle,@time) " +
                        "ON CONFLICT(subject_hash) DO UPDATE SET handle=@handle,checked_ms=@time WHERE label_search_handles.subject_key=@key AND label_search_handles.checked_ms<=@time"
                };
                await ExecuteAsync(
                    connection: connection, transaction: transaction, sql: sql, token: cancellationToken,
                    ("@hash", SHA256.HashData(key)), ("@key", key),
                    ("@handle", entry.Handle?.ToUpperInvariant()), ("@time", batch.ObservedAt)).ConfigureAwait(false);
            }

            return LabelServiceResult<LabelHandleIndexBatch>.Success(batch);
        }, cancellationToken);
    }

    /// <summary>Escapes substring wildcards after normalizing both stored text and the search needle identically.</summary>
    private static string SearchPattern(string text) =>
        "%" + text.ToUpperInvariant().Replace("!", "!!", StringComparison.Ordinal)
            .Replace("%", "!%", StringComparison.Ordinal).Replace("_", "!_", StringComparison.Ordinal) + "%";

    /// <summary>Keeps handling reasons searchable without replacing the submitted evidence.</summary>
    private static Task UpdateReportSearchTextAsync(
        DbConnection connection,
        DbTransaction transaction,
        ModerationReportData report,
        CancellationToken token) =>
        ExecuteAsync(
            connection: connection, transaction: transaction,
            sql: "UPDATE label_search_text SET content=@content WHERE kind=0 AND source_id=@id",
            token: token,
            ("@content", (report.Subject + "\n" + report.Reason + "\n" + report.ResolutionReason).ToUpperInvariant()),
            ("@id", report.Id));

    /// <summary>Projects text in the same transaction as its immutable source, retaining exact originals independently.</summary>
    private static Task IndexSearchTextAsync(
        DbConnection connection,
        DbTransaction? transaction,
        int kind,
        long id,
        string content,
        CancellationToken token) =>
        ExecuteAsync(
            connection: connection, transaction: transaction,
            sql: "INSERT INTO label_search_text(kind,source_id,content) VALUES(@kind,@id,@content)",
            token: token, ("@kind", kind), ("@id", id), ("@content", content.ToUpperInvariant()));

    /// <summary>Resumes additive text indexing in bounded batches without changing source evidence or requiring network access.</summary>
    private static async Task BackfillSearchTextAsync(DbConnection connection, DbTransaction? transaction, CancellationToken token)
    {
        foreach (int kind in new[] { 0, 1 })
        {
            string table = kind == 0 ? "label_reports" : "label_case_events";
            string column = kind == 0 ? "report_id" : "seq";
            while (true)
            {
                List<(long Id, string Content)> page = [];
                DbCommand command = Command(
                    connection: connection, transaction: transaction,
                    sql: $"SELECT t.payload FROM {table} t WHERE NOT EXISTS(SELECT 1 FROM label_search_text s WHERE s.kind=@kind AND s.source_id=t.{column}) ORDER BY t.{column} LIMIT 100",
                    ("@kind", kind));
                await using (command.ConfigureAwait(false))
                {
                    DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                    await using (reader.ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            byte[] bytes = (byte[])reader.GetValue(0);
                            if (kind == 0)
                            {
                                ModerationReportData report = DecodeReport(bytes);
                                page.Add((report.Id, report.Subject + "\n" + report.Reason + "\n" + report.ResolutionReason));
                            }
                            else
                            {
                                ModerationCaseEvent history = DecodeCaseEvent(bytes);
                                page.Add((history.Sequence, history.Reason));
                            }
                        }
                    }
                }

                foreach ((long id, string content) in page)
                {
                    await IndexSearchTextAsync(
                        connection: connection, transaction: transaction, kind: kind, id: id, content: content, token: token).ConfigureAwait(false);
                }

                if (page.Count < 100)
                {
                    break;
                }
            }
        }
    }
}
