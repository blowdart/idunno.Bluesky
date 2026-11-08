// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    private const string AuditQueryTime = "q.created_ms";
    /// <summary>Reads caller-scoped settings under the issuer lock and current membership checks.</summary>
    Task<LabelServiceResult<LabelMediaPreferences>> ILabelTableStore.ReadMediaPreferencesAsync(
        LabelAdministrationCaller caller,
        CancellationToken cancellationToken) =>
        ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Read personal media display preferences.",
            action: (_, _, team) => Task.FromResult(LabelServiceResult<LabelMediaPreferences>.Success(
                team.Members[caller.Actor.Value].MediaPreferences ?? new())),
            token: cancellationToken,
            auditAction: "personal-media-preferences");

    /// <summary>Changes only the authenticated member's settings in the existing bounded team aggregate.</summary>
    Task<LabelServiceResult<LabelMediaPreferences>> ILabelTableStore.SaveMediaPreferencesAsync(
        LabelAdministrationCaller caller,
        LabelMediaPreferences preferences,
        CancellationToken cancellationToken)
    {
        preferences.Validate();
        return ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Save personal media display preferences.",
            action: async (connection, transaction, team) =>
            {
                team.Members[caller.Actor.Value] = team.Members[caller.Actor.Value] with { MediaPreferences = preferences };
                await SaveAdministrationAsync(connection, transaction, team, cancellationToken).ConfigureAwait(false);
                return LabelServiceResult<LabelMediaPreferences>.Success(preferences);
            },
            token: cancellationToken,
            auditAction: "personal-media-preferences");
    }

    /// <summary>Reads issuer-local manual label state under live team authorization, without resolving remote records.</summary>
    Task<LabelServiceResult<StoredLabel[]>> ILabelTableStore.ReadTargetLabelsAsync(
        LabelAdministrationCaller caller,
        LabelTarget target,
        CancellationToken cancellationToken) =>
        ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect manual target labels.",
            action: async (connection, transaction, _) =>
                await ReadTargetLabelsAsync(
                    connection: connection,
                    transaction: transaction,
                    state: await StateAsync(connection, transaction, cancellationToken).ConfigureAwait(false),
                    target: target,
                    exactVersion: false,
                    now: await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false),
                    token: cancellationToken).ConfigureAwait(false),
            token: cancellationToken,
            auditAction: "manual-labels");

    /// <summary>Combines bounded report and workflow history with deterministic newest-first pagination.</summary>
    Task<LabelServiceResult<LabelCaseActivityPage>> ILabelTableStore.ReadCaseActivityAsync(
        LabelAdministrationCaller caller,
        long caseId,
        long page,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        return ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect case and report activity.",
            action: async (connection, transaction, _) =>
            {
                if (await ReadCaseAsync(connection, transaction, caseId, cancellationToken).ConfigureAwait(false) is null)
                {
                    return CaseFailure<LabelCaseActivityPage>(LabelServiceErrorCode.CaseNotFound, "The case was not found.");
                }

                long total = Integer(await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT (SELECT COUNT(*) FROM label_reports WHERE case_id=@case)+(SELECT COUNT(*) FROM label_case_events WHERE case_id=@case)",
                    token: cancellationToken,
                    ("@case", caseId)).ConfigureAwait(false));
                long current = Math.Min(page, Math.Max(1, (total + 15) / 16));
                List<LabelCaseActivity> entries = [];
                long offset = (current - 1) * 16;
                long branchLimit = checked(offset + 16);
                DbCommand command = Command(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT kind,payload FROM (" +
                        "SELECT * FROM (SELECT 0 AS kind,t.report_id AS number,t.payload,q.created_at AS event_time " +
                        "FROM label_report_queries q JOIN label_reports t ON t.report_id=q.report_id WHERE q.case_id=@case " +
                        "ORDER BY q.created_at DESC,q.report_id DESC LIMIT @branchLimit) reports " +
                        "UNION ALL SELECT * FROM (" +
                        "SELECT 1 AS kind,t.seq AS number,t.payload,q.created_at AS event_time " +
                        "FROM label_activity_queries q JOIN label_case_events t ON t.seq=q.seq WHERE q.case_id=@case " +
                        "ORDER BY q.created_at DESC,q.seq DESC LIMIT @branchLimit) events" +
                        ") activity ORDER BY event_time DESC,kind DESC,number DESC LIMIT 16 OFFSET @offset",
                    ("@case", caseId),
                    ("@branchLimit", branchLimit),
                    ("@offset", offset));
                await using (command.ConfigureAwait(false))
                {
                    DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    await using (reader.ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            byte[] payload = (byte[])reader.GetValue(1);
                            entries.Add(Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture) == 0
                                ? new(Report: DecodeReport(payload).ToReport())
                                : new(Event: DecodeCaseEvent(payload).ToReceipt()));
                        }
                    }
                }

                for (int index = 0; index < entries.Count; index++)
                {
                    LabelCaseActivity entry = entries[index];
                    if (entry.Event is { Action: LabelCaseAction.Decide } decision)
                    {
                        ModerationDecisionData? provenance = await ReadCaseDecisionAsync(
                            connection, transaction, decision.OperationId, cancellationToken).ConfigureAwait(false);
                        entries[index] = entry with { Decision = provenance?.ToReceipt() };
                    }
                }

                return LabelServiceResult<LabelCaseActivityPage>.Success(new(current, total, entries));
            },
            token: cancellationToken,
            auditAction: "case-activity");
    }

    /// <summary>Handles omitted fields and MySQL's text representation of an explicit JSON null identically.</summary>
    private static string UnhandledReportFilter(string alias = "t") =>
        alias == "t" ? "q.handled=0" :
            $"EXISTS(SELECT 1 FROM label_report_queries rq WHERE rq.report_id={alias}.report_id AND rq.handled=0)";

    /// <summary>Keeps subject-wide temporary mutes consistent across counted workflows and exact-CID cases.</summary>
    /// <remarks><para>MySQL counts at most one primary-key match to preserve an indexed correlated probe.
    /// Its EXISTS transformation can otherwise sort the entire case queue before applying the page limit.</para></remarks>
    private string UnmutedSubjectFilter(string alias = "t")
    {
        string match = $"FROM label_mutes m WHERE m.kind=0 AND m.subject_hash={alias}.mute_subject_hash " +
            $"AND m.subject_key={alias}.mute_subject_key AND m.until_ms>@muteNow";
        return Provider == "mysql" ? $"(SELECT COUNT(*) {match})=0" : $"NOT EXISTS(SELECT 1 {match})";
    }

    /// <summary>Reuses bounded indexed label capture under live authorization without creating an appeal.</summary>
    Task<LabelServiceResult<StoredLabel[]>> ILabelTableStore.ReadSubjectLabelsAsync(
        LabelAdministrationCaller caller,
        LabelReportSubject subject,
        CancellationToken cancellationToken) =>
        ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect active subject labels.",
            action: async (connection, transaction, _) =>
                await ReadAppealChallengesAsync(
                    connection: connection,
                    transaction: transaction,
                    state: await StateAsync(connection, transaction, cancellationToken).ConfigureAwait(false),
                    subject: subject,
                    now: await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false),
                    token: cancellationToken).ConfigureAwait(false),
            token: cancellationToken,
            auditAction: "subject-labels");

    /// <summary>Counts open work without loading confidential payloads or relying on stale browser authority.</summary>
    Task<LabelServiceResult<LabelNavigationCounts>> ILabelTableStore.ReadNavigationCountsAsync(
        LabelAdministrationCaller caller,
        CancellationToken cancellationToken) =>
        ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect open moderation totals.",
            action: async (connection, transaction, _) =>
            {
                long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                DbCommand command = Command(
                    connection: connection,
                    transaction: transaction,
                    sql: $"SELECT (SELECT COUNT(*) FROM label_cases t WHERE status<>2 AND {UnmutedSubjectFilter()})," +
                        $"(SELECT COUNT(*) FROM label_report_queries q JOIN label_reports t ON t.report_id=q.report_id WHERE {UnhandledReportFilter()} AND {UnmutedSubjectFilter()})+" +
                        "(SELECT COUNT(*) FROM label_pending_reports WHERE pending=TRUE)," +
                        "(SELECT COUNT(*) FROM label_appeals WHERE status=0)",
                    ("@muteNow", now));
                await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    throw new InvalidDataException("The navigation aggregate did not return its counts.");
                }
                long cases = Integer(reader.GetValue(0));
                long reports = Integer(reader.GetValue(1));
                long appeals = Integer(reader.GetValue(2));
                return LabelServiceResult<LabelNavigationCounts>.Success(new(cases, reports, appeals));
            },
            token: cancellationToken,
            auditAction: "navigation-counts");

    /// <summary>Audits an export start before any response is sent, excluding its own entry from the fixed cutoff.</summary>
    async Task<LabelServiceResult<LabelAdministrationReceipt>> ILabelTableStore.BeginAuditExportAsync(
        LabelAdministrationCaller caller,
        LabelTableQuery query,
        CancellationToken cancellationToken)
    {
        query.Validate();
        if (query.Kind != LabelTableKind.Audit)
        {
            throw new ArgumentException("Only audit events can be exported.", nameof(query));
        }

        return await ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Start an audit JSON download.",
            action: async (connection, transaction, team) =>
            {
                long cutoff = team.Sequence;
                LabelAdministrationReceipt receipt = await AuditAdministrationAsync(
                    connection: connection,
                    transaction: transaction,
                    administration: team,
                    actor: caller.Actor,
                    localIdentity: caller.LocalIdentity,
                    operationId: "audit-export/" + Guid.NewGuid().ToString("N"),
                    action: "audit-export-started",
                    reason: string.Create(CultureInfo.InvariantCulture,
                        $"JSON download started; completion is not confirmed. Through sequence: {cutoff}; actor: {query.AuditActor ?? "(all)"}; succeeded: {query.AuditSucceeded?.ToString() ?? "(all)"}; from UTC: {query.AuditFrom?.ToUniversalTime():O}; to UTC: {query.AuditTo?.ToUniversalTime():O}."),
                    succeeded: true,
                    token: cancellationToken).ConfigureAwait(false);
                return LabelServiceResult<LabelAdministrationReceipt>.Success(receipt);
            },
            token: cancellationToken,
            auditAction: "audit-export").ConfigureAwait(false);
    }

    /// <summary>Reads an immutable export prefix in sequence order without repeated full-table counts or long-held transactions.</summary>
    async Task<LabelServiceResult<IReadOnlyList<LabelAdministrationReceipt>>> ILabelTableStore.ReadAuditExportPageAsync(
        LabelAdministrationCaller caller,
        LabelTableQuery query,
        CancellationToken cancellationToken)
    {
        query.Validate();
        if (query.Kind != LabelTableKind.Audit || query.AuditThroughSequence is null)
        {
            throw new ArgumentException("An audit export requires a fixed sequence cutoff.", nameof(query));
        }

        return await ModerationAsync<IReadOnlyList<LabelAdministrationReceipt>>(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Read an audit JSON download page.",
            action: async (connection, transaction, _) =>
            {
                DbCommand command = Command(
                    connection: connection,
                    transaction: transaction,
                    sql: $"SELECT t.payload FROM label_audit_queries q JOIN label_admin_audit t ON t.seq=q.seq WHERE {TableFilter(query)} ORDER BY t.seq LIMIT @limit",
                    ("@auditActor", query.AuditActor),
                    ("@auditSucceeded", query.AuditSucceeded switch { true => "true", false => "false", null => null }),
                    ("@auditFrom", AuditLowerBound(query.AuditFrom)),
                    ("@auditTo", query.AuditTo?.ToUnixTimeMilliseconds()),
                    ("@auditAfter", query.AuditAfterSequence),
                    ("@auditThrough", query.AuditThroughSequence),
                    ("@limit", LabelTableQuery.PageSize));
                await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                List<LabelAdministrationReceipt> entries = [];
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    entries.Add((JsonSerializer.Deserialize((byte[])reader.GetValue(0), AdministrationJsonContext.Default.AdministrationAudit)
                        ?? throw new InvalidDataException("The administrative audit is null.")).ToReceipt());
                }

                return LabelServiceResult<IReadOnlyList<LabelAdministrationReceipt>>.Success(entries);
            },
            token: cancellationToken,
            auditAction: "audit-export-read").ConfigureAwait(false);
    }

    /// <summary>Counts and sorts the full filtered dataset before selecting a bounded page under live inspection authority.</summary>
    async Task<LabelServiceResult<LabelTablePage>> ILabelTableStore.ReadTableAsync(
        LabelAdministrationCaller caller,
        LabelTableQuery query,
        CancellationToken cancellationToken)
    {
        query.Validate();
        return await ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect an ordered administrative table.",
            action: async (connection, transaction, team) =>
            {
                if (query.Kind == LabelTableKind.Team)
                {
                    bool canManageTeam = team.Members.TryGetValue(caller.Actor.Value, out AdministrationMember? viewer) &&
                        viewer is { Active: true, Removed: false, Role: LabelTeamRole.Administrator };
                    IEnumerable<KeyValuePair<string, AdministrationMember>> members = team.Members;
                    if (query.TeamMember is not null)
                    {
                        members = members.Where(entry => entry.Key == query.TeamMember);
                    }

                    int memberCount = query.TeamMember is null ? team.Members.Count : members.Count();
                    IOrderedEnumerable<KeyValuePair<string, AdministrationMember>> sorted = query.Sort switch
                    {
                        LabelTableSort.Role => query.Ascending ? members.OrderBy(entry => entry.Value.Role) : members.OrderByDescending(entry => entry.Value.Role),
                        LabelTableSort.Status => query.Ascending ? members.OrderBy(entry => entry.Value.Active) : members.OrderByDescending(entry => entry.Value.Active),
                        _ => query.Ascending ? members.OrderBy(entry => entry.Key, StringComparer.Ordinal) : members.OrderByDescending(entry => entry.Key, StringComparer.Ordinal)
                    };
                    long page = ClampTablePage(query.Page, memberCount);
                    return LabelServiceResult<LabelTablePage>.Success(new(
                        Total: memberCount,
                        Page: page,
                        Team: [.. sorted.ThenBy(entry => entry.Key, StringComparer.Ordinal)
                            .Skip(checked((int)((page - 1) * LabelTableQuery.PageSize))).Take(LabelTableQuery.PageSize)
                            .Select(entry => new LabelTeamMember(new(entry.Key), entry.Value.Role, entry.Value.Active, entry.Value.Removed)
                            {
                                NotificationEmail = canManageTeam ? entry.Value.NotificationEmail : null
                            })]));
                }

                string table = query.Kind switch
                {
                    LabelTableKind.Cases => "label_cases",
                    LabelTableKind.Reports => "label_reports",
                    LabelTableKind.PendingReports => "label_pending_reports",
                    LabelTableKind.Appeals => "label_appeals",
                    _ => "label_admin_audit"
                };
                string number = query.Kind switch
                {
                    LabelTableKind.Cases => "case_id",
                    LabelTableKind.Reports => "report_id",
                    LabelTableKind.PendingReports => "report_id",
                    LabelTableKind.Appeals => "appeal_id",
                    _ => "seq"
                };
                string filter = TableFilter(query);
                string source = query.Kind switch
                {
                    LabelTableKind.Cases => "label_case_queries q JOIN label_cases t ON t.case_id=q.case_id",
                    LabelTableKind.Reports => "label_report_queries q JOIN label_reports t ON t.report_id=q.report_id",
                    LabelTableKind.Audit => "label_audit_queries q JOIN label_admin_audit t ON t.seq=q.seq",
                    _ => table + " t"
                };
                string? projection = query.Kind switch
                {
                    LabelTableKind.Cases => "label_case_queries q",
                    LabelTableKind.Reports => "label_report_queries q",
                    LabelTableKind.Audit => "label_audit_queries q",
                    _ => null
                };
                string countSource = projection is not null && !filter.Contains("t.", StringComparison.Ordinal)
                    ? projection : source;
                long muteNow = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                object? count = await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: $"SELECT COUNT(*) FROM {countSource} WHERE {filter}",
                    token: cancellationToken,
                    ("@muteNow", muteNow),
                    ("@actor", s_strictUtf8.GetBytes(caller.Actor.Value)),
                    ("@actorHash", SHA256.HashData(s_strictUtf8.GetBytes(caller.Actor.Value))),
                    ("@auditActor", query.AuditActor),
                    ("@auditAfter", query.AuditAfterSequence),
                    ("@auditThrough", query.AuditThroughSequence),
                    ("@auditSucceeded", query.AuditSucceeded switch { true => "true", false => "false", null => null }),
                    ("@auditFrom", AuditLowerBound(query.AuditFrom)),
                    ("@auditTo", query.AuditTo?.ToUnixTimeMilliseconds()),
                    ("@status", query.AppealStatus is { } status ? (int)status : null)).ConfigureAwait(false);
                long total = Convert.ToInt64(count, CultureInfo.InvariantCulture);
                long current = ClampTablePage(query.Page, total);
                string orderedNumber = query.Kind is LabelTableKind.Cases or LabelTableKind.Reports or LabelTableKind.Audit
                    ? "q." + number : "t." + number;
                string column = query.Sort switch
                {
                    LabelTableSort.Number => orderedNumber,
                    LabelTableSort.Received when query.Kind == LabelTableKind.Audit => AuditQueryTime,
                    LabelTableSort.Case => "t.case_id",
                    LabelTableSort.Subject => query.Kind == LabelTableKind.Reports ? QueryTextSort("q.subject") : TableJsonField("Subject"),
                    LabelTableSort.Assignee => $"COALESCE(NULLIF({TableJsonField("Assignee")},'null'),'')",
                    LabelTableSort.Actor when query.Kind == LabelTableKind.Audit => QueryTextSort("q.actor"),
                    LabelTableSort.Actor => TableJsonField(query.Kind == LabelTableKind.Appeals ? "Initiator" : "Actor"),
                    LabelTableSort.Action => TableJsonField("Action"),
                    LabelTableSort.Outcome => query.Kind == LabelTableKind.Audit ? "q.succeeded" : TableJsonField("Succeeded"),
                    LabelTableSort.Status => query.Kind switch
                    {
                        LabelTableKind.Cases => "CASE WHEN EXISTS(SELECT 1 FROM label_appeals a WHERE a.case_id=t.case_id AND a.status=0) THEN 3 ELSE t.status END",
                        LabelTableKind.Reports => $"CASE WHEN {UnhandledReportFilter()} THEN 0 ELSE 1 END",
                        LabelTableKind.PendingReports => "t.pending",
                        _ => "t.status"
                    },
                    _ => query.Kind is LabelTableKind.Cases or LabelTableKind.Reports ? "q.created_at" : TableJsonField("CreatedAt")
                };
                string direction = query.Ascending ? "ASC" : "DESC";
                string subjectPrefixOrder = Provider == "postgresql" && query.Kind == LabelTableKind.Reports && query.Sort == LabelTableSort.Subject
                    ? $"left(q.subject,512) {direction},"
                    : string.Empty;
                string unknownTimesLast = query.Kind == LabelTableKind.Audit && query.Sort == LabelTableSort.Received
                    ? $"CASE WHEN {column} IS NULL THEN 1 ELSE 0 END ASC,"
                    : string.Empty;
                string appealCount = query.Kind == LabelTableKind.Cases
                    ? "(SELECT COUNT(*) FROM label_appeals a WHERE a.case_id=t.case_id AND a.status=0)"
                    : "0";
                string escalated = query.Kind is LabelTableKind.Cases or LabelTableKind.Reports or LabelTableKind.Appeals
                    ? "(SELECT COUNT(*) FROM label_cases c WHERE c.case_id=t.case_id AND c.status=3)" : "0";
                string pageSource = projection is not null &&
                    !filter.Contains("t.", StringComparison.Ordinal) && !column.Contains("t.", StringComparison.Ordinal)
                    ? projection : source;
                DbCommand command = Command(
                    connection: connection,
                    transaction: transaction,
                    sql: $"SELECT t.payload,{appealCount},{escalated} FROM (" +
                        $"SELECT {orderedNumber} AS source_id,{column} AS sort_value FROM {pageSource} WHERE {filter} " +
                        $"ORDER BY {unknownTimesLast}{subjectPrefixOrder}{column} {direction},{orderedNumber} {direction} LIMIT @limit OFFSET @offset" +
                        $") page JOIN {table} t ON t.{number}=page.source_id ORDER BY " +
                        (unknownTimesLast.Length == 0 ? string.Empty : "CASE WHEN page.sort_value IS NULL THEN 1 ELSE 0 END ASC,") +
                        $"page.sort_value {direction},page.source_id {direction}",
                    ("@muteNow", muteNow),
                    ("@actor", s_strictUtf8.GetBytes(caller.Actor.Value)),
                    ("@actorHash", SHA256.HashData(s_strictUtf8.GetBytes(caller.Actor.Value))),
                    ("@auditActor", query.AuditActor),
                    ("@auditAfter", query.AuditAfterSequence),
                    ("@auditThrough", query.AuditThroughSequence),
                    ("@auditSucceeded", query.AuditSucceeded switch { true => "true", false => "false", null => null }),
                    ("@auditFrom", AuditLowerBound(query.AuditFrom)),
                    ("@auditTo", query.AuditTo?.ToUnixTimeMilliseconds()),
                    ("@status", query.AppealStatus is { } selected ? (int)selected : null),
                    ("@limit", LabelTableQuery.PageSize),
                    ("@offset", (current - 1) * LabelTableQuery.PageSize));
                List<LabelModerationCase> cases = [];
                List<LabelModerationReport> reports = [];
                List<LabelPendingModerationReport> pendingReports = [];
                List<LabelAppeal> appeals = [];
                List<LabelAdministrationReceipt> audit = [];
                List<long> appealedCases = [];
                HashSet<long> escalatedCases = [];
                await using (command.ConfigureAwait(false))
                {
                    DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    await using (reader.ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            byte[] bytes = (byte[])reader.GetValue(0);
                            switch (query.Kind)
                            {
                                case LabelTableKind.Cases:
                                    LabelModerationCase moderationCase = DecodeCase(bytes).ToCase();
                                    cases.Add(moderationCase);
                                    if (moderationCase.Status == LabelCaseStatus.Escalated)
                                    {
                                        escalatedCases.Add(moderationCase.Id);
                                    }
                                    if (Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture) > 0)
                                    {
                                        appealedCases.Add(moderationCase.Id);
                                    }
                                    break;
                                case LabelTableKind.Reports:
                                    LabelModerationReport report = DecodeReport(bytes).ToReport();
                                    reports.Add(report);
                                    if (Integer(reader.GetValue(2)) > 0)
                                    {
                                        escalatedCases.Add(report.CaseId);
                                    }
                                    break;
                                case LabelTableKind.PendingReports:
                                    pendingReports.Add(DecodePendingReport(bytes).ToPendingReport());
                                    break;
                                case LabelTableKind.Appeals:
                                    LabelAppeal appeal = DecodeAppeal(bytes).ToAppeal();
                                    appeals.Add(appeal);
                                    if (Integer(reader.GetValue(2)) > 0)
                                    {
                                        escalatedCases.Add(appeal.CaseId);
                                    }
                                    break;
                                default:
                                    audit.Add((JsonSerializer.Deserialize(bytes, AdministrationJsonContext.Default.AdministrationAudit)
                                        ?? throw new InvalidDataException("The administrative audit is null.")).ToReceipt());
                                    break;
                            }
                        }
                    }
                }

                IReadOnlyList<LabelAuthorReportSummary>? summaries = query.Kind == LabelTableKind.Cases
                    ? await ReadAuthorSummariesAsync(connection, transaction, cases, cancellationToken).ConfigureAwait(false)
                    : null;
                return LabelServiceResult<LabelTablePage>.Success(new(
                    Total: total,
                    Page: current,
                    Cases: cases,
                    Reports: reports,
                    PendingReports: pendingReports,
                    Appeals: appeals,
                    Audit: audit,
                    AuthorSummaries: summaries,
                    PendingAppealCaseIds: appealedCases,
                    EscalatedCaseIds: [.. escalatedCases]));
            },
            token: cancellationToken,
            auditAction: "table-read").ConfigureAwait(false);
    }

    /// <summary>Aggregates all retained reports in SQL for only the current page's distinct authors, never fetching report histories into the UI.</summary>
    private static async Task<IReadOnlyList<LabelAuthorReportSummary>> ReadAuthorSummariesAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlyList<LabelModerationCase> cases,
        CancellationToken token)
    {
        List<LabelAuthorReportSummary> summaries = [];
        string[] authors = [.. cases.Select(item => item.Subject.Repository.Value).Distinct(StringComparer.Ordinal)];
        if (authors.Length == 0)
        {
            return summaries;
        }
        List<string> branches = [];
        List<(string, object?)> parameters = [];
        for (int index = 0; index < authors.Length; index++)
        {
            string author = authors[index];
            string prefix = "at://" + author + "/";
            string pattern = prefix.Replace("!", "!!", StringComparison.Ordinal)
                .Replace("%", "!%", StringComparison.Ordinal).Replace("_", "!_", StringComparison.Ordinal) + "%";
            branches.Add(
                $"SELECT {index} AS ordinal,COUNT(*) AS total,COUNT(DISTINCT CASE WHEN q.subject LIKE @records{index} ESCAPE '!' THEN q.subject END) AS records," +
                $"COALESCE(SUM(CASE WHEN q.handled=0 AND q.subject=@author{index} THEN 1 ELSE 0 END),0) AS accounts," +
                $"COALESCE(SUM(CASE WHEN q.handled=0 AND q.subject LIKE @post{index} ESCAPE '!' THEN 1 ELSE 0 END),0) AS posts," +
                $"COALESCE(SUM(CASE WHEN q.handled=0 AND q.subject LIKE @profile{index} ESCAPE '!' THEN 1 ELSE 0 END),0) AS profiles," +
                $"COALESCE(SUM(CASE WHEN q.handled=0 AND q.subject LIKE @list{index} ESCAPE '!' THEN 1 ELSE 0 END),0) AS lists," +
                $"COALESCE(SUM(CASE WHEN q.handled=0 AND q.subject LIKE @feed{index} ESCAPE '!' THEN 1 ELSE 0 END),0) AS feeds," +
                $"COALESCE(SUM(CASE WHEN q.handled=0 AND q.subject LIKE @starterpack{index} ESCAPE '!' THEN 1 ELSE 0 END),0) AS starterpacks," +
                "COALESCE(SUM(CASE WHEN q.handled=0 THEN 1 ELSE 0 END),0) AS open_reports " +
                $"FROM label_report_queries q WHERE q.repository_hash=@hash{index} AND q.repository_key=@key{index}");
            byte[] key = s_strictUtf8.GetBytes(author);
            parameters.AddRange(
            [
                ($"@hash{index}", SHA256.HashData(key)), ($"@key{index}", key), ($"@author{index}", author),
                ($"@records{index}", pattern), ($"@post{index}", pattern[..^1] + "app.bsky.feed.post/%"),
                ($"@profile{index}", pattern[..^1] + "app.bsky.actor.profile/%"),
                ($"@list{index}", pattern[..^1] + "app.bsky.graph.list/%"),
                ($"@feed{index}", pattern[..^1] + "app.bsky.feed.generator/%"),
                ($"@starterpack{index}", pattern[..^1] + "app.bsky.graph.starterpack/%")
            ]);
        }
        DbCommand command = Command(
            connection: connection,
            transaction: transaction,
            sql: "SELECT ordinal,total,records,accounts,posts,profiles,lists,feeds,starterpacks,open_reports FROM (" +
                string.Join(" UNION ALL ", branches) + ") authors ORDER BY ordinal",
            [.. parameters]);
        await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
        DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            Dictionary<string, long> kinds = new(StringComparer.Ordinal)
            {
                ["profile"] = Integer(reader.GetValue(3)) + Integer(reader.GetValue(5)),
                ["post"] = Integer(reader.GetValue(4)),
                ["list"] = Integer(reader.GetValue(6)),
                ["feed"] = Integer(reader.GetValue(7)),
                ["starter pack"] = Integer(reader.GetValue(8))
            };
            kinds["other record"] = Integer(reader.GetValue(9)) - kinds.Values.Sum();
            summaries.Add(new(
                authors[checked((int)Integer(reader.GetValue(0)))],
                Integer(reader.GetValue(1)),
                Integer(reader.GetValue(2)),
                kinds));
        }
        if (summaries.Count != authors.Length)
        {
            throw new InvalidDataException("The report aggregate did not return every author.");
        }

        return summaries;
    }

    /// <summary>Clamps stale links to the last available page, with one empty page for an empty dataset.</summary>
    private static long ClampTablePage(long requested, long total) =>
        Math.Min(requested, Math.Max(1, total / LabelTableQuery.PageSize + (total % LabelTableQuery.PageSize == 0 ? 0 : 1)));

    /// <summary>Extracts a fixed persisted JSON property without accepting identifiers or expressions from callers.</summary>
    private string TableJsonField(string property) => Provider switch
    {
        "postgresql" => $"(convert_from(t.payload,'UTF8')::jsonb->>'{property}') COLLATE \"C\"",
        "mysql" => $"JSON_UNQUOTE(JSON_EXTRACT(CONVERT(t.payload USING utf8mb4),'$.{property}')) COLLATE utf8mb4_bin",
        _ => $"json_extract(CAST(t.payload AS TEXT),'$.{property}') COLLATE BINARY"
    };

    /// <summary>Prevents MySQL filesorts from truncating collation keys for bounded projection strings.</summary>
    private string QueryTextSort(string column) => Provider == "mysql" ? $"CAST({column} AS BINARY)" : column;

    /// <summary>Builds only fixed workflow predicates shared by the count and selected page.</summary>
    private string TableFilter(LabelTableQuery query)
    {
        if (query.Kind == LabelTableKind.Audit)
        {
            string milliseconds = AuditQueryTime;
            string succeeded = "q.succeeded";
            List<string> filters = [];
            if (query.AuditAfterSequence > 0)
            {
                filters.Add("t.seq>@auditAfter");
            }
            if (query.AuditThroughSequence is not null)
            {
                filters.Add("t.seq<=@auditThrough");
            }
            if (query.AuditActor is not null)
            {
                filters.Add("q.actor=@auditActor");
            }
            if (query.AuditSucceeded is not null)
            {
                filters.Add($"{succeeded}=CASE WHEN @auditSucceeded='true' THEN 1 ELSE 0 END");
            }
            if (query.AuditFrom is not null)
            {
                filters.Add($"{milliseconds}>=@auditFrom");
            }
            if (query.AuditTo is not null)
            {
                filters.Add($"{milliseconds}<=@auditTo");
            }
            return filters.Count == 0 ? "1=1" : string.Join(" AND ", filters);
        }

        if (query.Kind == LabelTableKind.Reports)
        {
            string handled = query.ReportHandled switch
            {
                false => UnhandledReportFilter(),
                true => $"NOT ({UnhandledReportFilter()})",
                null => "1=1"
            };
            string workflow = query.Workflow switch
            {
                LabelCaseWorkflow.Unresolved => UnhandledReportFilter(),
                LabelCaseWorkflow.InReview => $"({UnhandledReportFilter()}) AND EXISTS(SELECT 1 FROM label_cases c WHERE c.case_id=t.case_id AND c.status=1)",
                LabelCaseWorkflow.Escalated => $"({UnhandledReportFilter()}) AND EXISTS(SELECT 1 FROM label_cases c WHERE c.case_id=t.case_id AND c.status=3)",
                LabelCaseWorkflow.Resolved => $"NOT ({UnhandledReportFilter()})",
                _ => "1=1"
            };
            string muted = query.Workflow is LabelCaseWorkflow.Unresolved or LabelCaseWorkflow.InReview or LabelCaseWorkflow.Escalated ||
                query.ReportHandled == false ? UnmutedSubjectFilter() : "1=1";
            return $"({handled}) AND ({workflow}) AND ({muted})";
        }

        if (query.Kind == LabelTableKind.PendingReports)
        {
            return "1=1";
        }

        if (query.Kind == LabelTableKind.Appeals)
        {
            string status = query.AppealStatus is null ? "1=1" : "t.status=@status";
            string workflow = query.Workflow switch
            {
                LabelCaseWorkflow.Unresolved => "t.status=0",
                LabelCaseWorkflow.InReview => "t.status=0 AND EXISTS(SELECT 1 FROM label_cases c WHERE c.case_id=t.case_id AND c.status=1)",
                LabelCaseWorkflow.Escalated => "t.status=0 AND EXISTS(SELECT 1 FROM label_cases c WHERE c.case_id=t.case_id AND c.status=3)",
                LabelCaseWorkflow.Resolved => "t.status<>0",
                _ => "1=1"
            };
            return $"({status}) AND ({workflow})";
        }

        if (query.Kind != LabelTableKind.Cases)
        {
            return "1=1";
        }

        string caseStatus = query.Queue switch
        {
            LabelCaseWorkflow.All => "1=1",
            LabelCaseWorkflow.Open => "t.status=0",
            LabelCaseWorkflow.InReview => "t.status=1",
            LabelCaseWorkflow.Escalated => "t.status=3",
            LabelCaseWorkflow.Resolved => "t.status=2",
            LabelCaseWorkflow.Unassigned => "t.status<>2 AND t.assignee_hash IS NULL",
            LabelCaseWorkflow.AssignedToMe => "t.status<>2 AND q.assignee_hash=@actorHash AND q.assignee_key=@actor",
            LabelCaseWorkflow.PendingAppeals => "t.status<>2 AND EXISTS(SELECT 1 FROM label_appeals a WHERE a.case_id=t.case_id AND a.status=0)",
            _ => "t.status<>2"
        };
        return query.Queue is LabelCaseWorkflow.All or LabelCaseWorkflow.PendingAppeals
            ? caseStatus : $"({caseStatus}) AND {UnmutedSubjectFilter()}";
    }

    /// <summary>Preserves the original provider conversion when projecting immutable audit timestamps.</summary>
    private string AuditSourceTimeMilliseconds()
    {
        string timestamp = $"NULLIF({TableJsonField("CreatedAt")},'null')";
        return Provider switch
        {
            "postgresql" => $"(EXTRACT(EPOCH FROM ({timestamp})::timestamptz)*1000)",
            "mysql" => $"(TIMESTAMPDIFF(MICROSECOND,'1970-01-01',CAST(REPLACE(SUBSTRING_INDEX(REPLACE({timestamp},'Z','+00:00'),'+',1),'T',' ') AS DATETIME(6)))/1000)",
            _ => $"ROUND((julianday({timestamp})-2440587.5)*86400000)"
        };
    }

    /// <summary>Rounds an inclusive lower bound up rather than including an earlier millisecond when the caller supplied finer precision.</summary>
    private static long? AuditLowerBound(DateTimeOffset? timestamp)
    {
        if (timestamp is not { } time)
        {
            return null;
        }

        return time.ToUnixTimeMilliseconds() + (time.Ticks % TimeSpan.TicksPerMillisecond == 0 ? 0 : 1);
    }
}
