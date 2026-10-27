// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Moderation;
using idunno.Bluesky.Labeler.Storage;

namespace idunno.Bluesky.Labeler.Administration;

/// <summary>Reads counted, globally ordered administrative tables under the same live authority as other inspections.</summary>
internal interface ILabelTableStore
{
    /// <summary>Records an authorized export start and captures its exclusive audit-event cutoff in the same transaction.</summary>
    Task<LabelServiceResult<LabelAdministrationReceipt>> BeginAuditExportAsync(
        LabelAdministrationCaller caller,
        LabelTableQuery query,
        CancellationToken cancellationToken);

    /// <summary>Reads one sequence-bounded export page without recounting the remaining trail.</summary>
    Task<LabelServiceResult<IReadOnlyList<LabelAdministrationReceipt>>> ReadAuditExportPageAsync(
        LabelAdministrationCaller caller,
        LabelTableQuery query,
        CancellationToken cancellationToken);

    /// <summary>Reads active labels for a manual target across record versions, including retired policy values.</summary>
    Task<LabelServiceResult<StoredLabel[]>> ReadTargetLabelsAsync(
        LabelAdministrationCaller caller,
        LabelTarget target,
        CancellationToken cancellationToken);
    /// <summary>Reads a newest-first, bounded page of case events and its original report submissions.</summary>
    Task<LabelServiceResult<LabelCaseActivityPage>> ReadCaseActivityAsync(
        LabelAdministrationCaller caller,
        long caseId,
        long page,
        CancellationToken cancellationToken);
    /// <summary>Reads all active exact-version labels, including values removed from the current declaration.</summary>
    Task<LabelServiceResult<StoredLabel[]>> ReadSubjectLabelsAsync(
        LabelAdministrationCaller caller,
        LabelReportSubject subject,
        CancellationToken cancellationToken);
    /// <summary>Reads open workflow totals under live inspection authority in one transaction.</summary>
    Task<LabelServiceResult<LabelNavigationCounts>> ReadNavigationCountsAsync(
        LabelAdministrationCaller caller,
        CancellationToken cancellationToken);
    /// <summary>Counts and reads one page within a single provider transaction, never loading an entire table into the UI.</summary>
    Task<LabelServiceResult<LabelTablePage>> ReadTableAsync(
        LabelAdministrationCaller caller,
        LabelTableQuery query,
        CancellationToken cancellationToken);
}

/// <summary>Identifies the authorized dataset being inspected.</summary>
internal enum LabelTableKind
{
    Cases,
    Reports,
    Appeals,
    Audit,
    Team
}

/// <summary>Defines the fixed, parameter-independent set of sortable table fields.</summary>
internal enum LabelTableSort
{
    Number,
    Received,
    Subject,
    Actor,
    Role,
    Status,
    Action,
    Outcome,
    Case,
    Assignee
}

/// <summary>Retains validated sorting, paging and workflow filters without accepting SQL expressions.</summary>
internal sealed record LabelTableQuery(
    LabelTableKind Kind,
    long Page = 1,
    LabelTableSort Sort = LabelTableSort.Received,
    bool Ascending = false,
    LabelCaseWorkflow Queue = LabelCaseWorkflow.Unresolved,
    LabelAppealStatus? AppealStatus = null,
    bool? ReportHandled = null,
    string? AuditActor = null,
    bool? AuditSucceeded = null,
    DateTimeOffset? AuditFrom = null,
    DateTimeOffset? AuditTo = null,
    long AuditAfterSequence = 0,
    long? AuditThroughSequence = null)
{
    internal const int PageSize = 16;

    /// <summary>Gets the sortable columns supported by this dataset.</summary>
    internal LabelTableSort[] Columns => Kind switch
    {
        LabelTableKind.Cases => [LabelTableSort.Number, LabelTableSort.Subject, LabelTableSort.Received, LabelTableSort.Status, LabelTableSort.Assignee],
        LabelTableKind.Reports => [LabelTableSort.Number, LabelTableSort.Case, LabelTableSort.Subject, LabelTableSort.Received, LabelTableSort.Status],
        LabelTableKind.Appeals => [LabelTableSort.Number, LabelTableSort.Case, LabelTableSort.Subject, LabelTableSort.Actor, LabelTableSort.Received, LabelTableSort.Status],
        LabelTableKind.Audit => [LabelTableSort.Number, LabelTableSort.Received, LabelTableSort.Actor, LabelTableSort.Action, LabelTableSort.Outcome],
        LabelTableKind.Team => [LabelTableSort.Subject, LabelTableSort.Role, LabelTableSort.Status],
        _ => []
    };

    /// <summary>Rejects unsupported fields, malformed filters and invalid page numbers before storage access.</summary>
    internal void Validate()
    {
        if (Page <= 0 || !Columns.Contains(Sort) || !Enum.IsDefined(Queue) ||
            AuditActor is { Length: > 2048 } ||
            AuditAfterSequence < 0 || AuditThroughSequence is < 0 ||
            AuditFrom is { } from && AuditTo is { } to && from > to ||
            AppealStatus is { } status && !Enum.IsDefined(status))
        {
            throw new ArgumentException("Choose a valid page, sort column and filter.");
        }
    }
}

/// <summary>Contains one bounded page and a count from the same authorized database snapshot.</summary>
internal sealed record LabelTablePage(
    long Total,
    long Page,
    IReadOnlyList<LabelModerationCase>? Cases = null,
    IReadOnlyList<LabelModerationReport>? Reports = null,
    IReadOnlyList<LabelAppeal>? Appeals = null,
    IReadOnlyList<LabelAdministrationReceipt>? Audit = null,
    IReadOnlyList<LabelTeamMember>? Team = null,
    IReadOnlyList<LabelAuthorReportSummary>? AuthorSummaries = null,
    IReadOnlyList<long>? PendingAppealCaseIds = null);

/// <summary>Contains live unresolved-case, unhandled-report and pending-appeal totals.</summary>
internal sealed record LabelNavigationCounts(long Cases, long Reports, long Appeals);

/// <summary>Contains one chronological case event or report, with original decision provenance when applicable.</summary>
internal sealed record LabelCaseActivity(LabelCaseReceipt? Event = null, LabelModerationReport? Report = null, LabelCaseDecisionReceipt? Decision = null);

/// <summary>Contains a counted page of case and report activity, without claiming to include independent manual labeling.</summary>
internal sealed record LabelCaseActivityPage(long Page, long Total, IReadOnlyList<LabelCaseActivity> Entries);

/// <summary>Contains issuer-local all-time report volume, counting record URIs independently of their reported CIDs.</summary>
internal sealed record LabelAuthorReportSummary(string Did, long Reports, long Records, IReadOnlyDictionary<string, long> ReportKinds);
