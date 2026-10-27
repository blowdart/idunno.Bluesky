// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Globalization;
using System.Text.Json;

using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    /// <summary>Schedules public existence checks for open post work without returning report text to the checker.</summary>
    Task<LabelServiceResult<LabelDeletedPostBatch>> ILabelDeletedPostStore.ReadDeletedPostBatchAsync(CancellationToken cancellationToken) =>
        AdministrationAsync(async (connection, transaction, _) =>
        {
            long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            List<LabelDeletedPostCandidate> entries = [];
            string postFilter = Provider == "sqlite"
                ? $"{TableJsonField("Subject")} GLOB 'at://*/app.bsky.feed.post/*'"
                : $"{TableJsonField("Subject")} LIKE 'at://%/app.bsky.feed.post/%'";
            DbCommand command = Command(
                connection: connection, transaction: transaction,
                sql: "SELECT t.payload FROM label_cases t LEFT JOIN label_post_review_checks k ON k.case_id=t.case_id " +
                    $"WHERE {postFilter} " +
                    "AND (k.checked_ms IS NULL OR k.checked_ms<=@old OR k.checked_ms>@now) " +
                    $"AND (EXISTS(SELECT 1 FROM label_reports r WHERE r.case_id=t.case_id AND {UnhandledReportFilter("r")}) " +
                    "OR EXISTS(SELECT 1 FROM label_appeals a WHERE a.case_id=t.case_id AND a.status=0)) " +
                    "ORDER BY COALESCE(k.checked_ms,0),t.case_id LIMIT 16",
                ("@old", now - 300_000), ("@now", now));
            await using (command.ConfigureAwait(false))
            {
                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        LabelModerationCase moderationCase = DecodeCase((byte[])reader.GetValue(0)).ToCase();
                        entries.Add(new(moderationCase.Id, moderationCase.Revision, moderationCase.Subject.RecordUri!));
                    }
                }
            }

            return LabelServiceResult<LabelDeletedPostBatch>.Success(new(now, entries));
        }, cancellationToken);

    /// <summary>Uses the host-only confirmation path to close reports and appeals atomically, never issuing or negating labels.</summary>
    Task<LabelServiceResult<LabelDeletedPostCheck>> ILabelDeletedPostStore.CompleteDeletedPostCheckAsync(
        LabelDeletedPostCandidate candidate,
        long observedAt,
        bool deleted,
        CancellationToken cancellationToken) =>
        AdministrationAsync(async (connection, transaction, state) =>
        {
            const string reason = "Post deleted";
            string operation = string.Create(CultureInfo.InvariantCulture, $"post-deleted/{candidate.CaseId}/{candidate.Revision}");
            byte[] operationHash = CaseOperationHash(operation, "deleted-post");
            if (await ScalarAsync(
                connection: connection, transaction: transaction,
                sql: "SELECT payload FROM label_case_events WHERE operation_hash=@hash",
                token: cancellationToken, ("@hash", operationHash)).ConfigureAwait(false) is byte[] payload)
            {
                ModerationCaseEvent original = DecodeCaseEvent(payload);
                if (original.OperationId != operation || original.Case.Id != candidate.CaseId ||
                    original.Case.Subject != candidate.Uri.ToString() || original.LocalIdentity != "system:deleted-post")
                {
                    return CaseFailure<LabelDeletedPostCheck>(LabelServiceErrorCode.IdempotencyConflict, "The closure operation identifies different retained intent.");
                }

                return LabelServiceResult<LabelDeletedPostCheck>.Success(new(candidate.CaseId, true));
            }

            ModerationCaseData? current = await ReadCaseAsync(connection, transaction, candidate.CaseId, cancellationToken).ConfigureAwait(false);
            if (current is null || current.ToCase().Subject.RecordUri != candidate.Uri ||
                candidate.Uri.Collection?.ToString() != "app.bsky.feed.post")
            {
                return CaseFailure<LabelDeletedPostCheck>(LabelServiceErrorCode.InvalidTarget, "The checked post does not match its stored case.");
            }

            long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            if (observedAt > now || now - observedAt > 300_000)
            {
                return CaseFailure<LabelDeletedPostCheck>(LabelServiceErrorCode.CaseConflict, "The existence check is no longer fresh; check the authoritative PDS again.");
            }

            string saveCheck = Provider == "mysql"
                ? "INSERT INTO label_post_review_checks(case_id,checked_ms) VALUES(@case,@now) ON DUPLICATE KEY UPDATE checked_ms=GREATEST(checked_ms,@now)"
                : "INSERT INTO label_post_review_checks(case_id,checked_ms) VALUES(@case,@now) ON CONFLICT(case_id) DO UPDATE SET checked_ms=@now WHERE label_post_review_checks.checked_ms<=@now";
            await ExecuteAsync(
                connection: connection, transaction: transaction, sql: saveCheck, token: cancellationToken,
                ("@case", current.Id), ("@now", now)).ConfigureAwait(false);
            if (!deleted)
            {
                return LabelServiceResult<LabelDeletedPostCheck>.Success(new(current.Id, false));
            }

            if (current.Revision != candidate.Revision)
            {
                return CaseFailure<LabelDeletedPostCheck>(LabelServiceErrorCode.CaseConflict, "The case changed during the PDS check; its new work has not been closed.");
            }

            DateTimeOffset completedAt = DateTimeOffset.FromUnixTimeMilliseconds(now);
            long after = 0;
            while (true)
            {
                List<ModerationReportData> reports = [];
                DbCommand command = Command(
                    connection: connection, transaction: transaction,
                    sql: "SELECT payload FROM label_reports WHERE case_id=@case AND report_id>@after ORDER BY report_id LIMIT 128",
                    ("@case", current.Id), ("@after", after));
                await using (command.ConfigureAwait(false))
                {
                    DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    await using (reader.ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            reports.Add(DecodeReport((byte[])reader.GetValue(0)));
                        }
                    }
                }

                foreach (ModerationReportData report in reports.Where(report => report.DecisionOperationId is null))
                {
                    ModerationReportData handled = report with { DecisionOperationId = operation, HandledAt = completedAt, ResolutionReason = reason };
                    await ExecuteAsync(
                        connection: connection, transaction: transaction,
                        sql: "UPDATE label_reports SET payload=@payload WHERE report_id=@id", token: cancellationToken,
                        ("@payload", JsonSerializer.SerializeToUtf8Bytes(handled, ModerationJsonContext.Default.ModerationReportData)),
                        ("@id", report.Id)).ConfigureAwait(false);
                    await UpdateReportSearchTextAsync(connection, transaction, handled, cancellationToken).ConfigureAwait(false);
                }

                if (reports.Count < 128)
                {
                    break;
                }

                after = reports[^1].Id;
            }

            while (true)
            {
                List<ModerationAppealData> appeals = [];
                DbCommand command = Command(
                    connection: connection, transaction: transaction,
                    sql: "SELECT payload FROM label_appeals WHERE case_id=@case AND status=0 ORDER BY appeal_id LIMIT 128",
                    ("@case", current.Id));
                await using (command.ConfigureAwait(false))
                {
                    DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    await using (reader.ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            appeals.Add(DecodeAppeal((byte[])reader.GetValue(0)));
                        }
                    }
                }

                foreach (ModerationAppealData appeal in appeals)
                {
                    await SaveAppealOutcomeAsync(
                        connection: connection, transaction: transaction,
                        appeal: appeal with
                        {
                            Status = LabelAppealStatus.Superseded,
                            UpdatedAt = completedAt,
                            DecisionOperationId = operation,
                            ResolutionReason = reason
                        },
                        token: cancellationToken).ConfigureAwait(false);
                }

                if (appeals.Count < 128)
                {
                    break;
                }
            }

            ModerationCaseData next = current with { Revision = checked(current.Revision + 1), Status = LabelCaseStatus.Resolved, UpdatedAt = completedAt };
            await SaveCaseAsync(
                connection: connection, transaction: transaction, next: next, subject: next.ToCase().Subject,
                insert: false, token: cancellationToken).ConfigureAwait(false);
            await AppendCaseEventAsync(
                connection: connection, transaction: transaction, next: next, operationId: operation,
                action: LabelCaseAction.Resolve, actor: state.Issuer, localIdentity: "system:deleted-post", reason: reason,
                intent: operationHash, operationNamespace: "deleted-post", token: cancellationToken).ConfigureAwait(false);
            await AuditAdministrationAsync(
                connection: connection, transaction: transaction,
                administration: await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false),
                actor: state.Issuer, localIdentity: "system:deleted-post", operationId: operation,
                action: "post-deleted", reason: reason, succeeded: true, token: cancellationToken).ConfigureAwait(false);
            return LabelServiceResult<LabelDeletedPostCheck>.Success(new(current.Id, true));
        }, cancellationToken);
}
