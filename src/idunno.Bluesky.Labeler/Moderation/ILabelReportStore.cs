// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Atomically accepts independent reports and protects confidential report reads.</summary>
/// <remarks><para>The provider shares the issuer lock with policy, cases, team and label decisions.
/// Report acceptance does not require team membership and never automatically emits labels.</para></remarks>
public interface ILabelReportStore
{
    /// <summary>Accepts a protocol report, retaining it durably when independent policy verification is unavailable.</summary>
    /// <param name="authentication">The separate cryptographically verified reporter evidence.</param>
    /// <param name="submission">The bounded submitted intent.</param>
    /// <param name="expectedPolicyRevision">The independently verified policy revision, or <see langword="null" /> when verification is unavailable.</param>
    /// <param name="maximumPendingReports">The configured maximum number of reports awaiting policy verification.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A protocol receipt, or an explicit authentication, scope, capacity or storage failure.</returns>
    /// <remarks><para>Queued reports remain non-actionable until an independently verified policy is available.
    /// They are automatically checked against that policy before promotion to ordinary cases.</para></remarks>
    Task<LabelServiceResult<LabelReportIntakeReceipt>> AcceptReportAsync(
        ModerationReportAuthenticationResult authentication,
        LabelReportSubmission submission,
        long? expectedPolicyRevision,
        int maximumPendingReports,
        CancellationToken cancellationToken = default);

    /// <summary>Processes one bounded batch against the exact currently verified policy revision.</summary>
    /// <param name="expectedPolicyRevision">The active independently verified policy revision.</param>
    /// <param name="limit">The maximum number of pending reports to process.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number processed and whether more remain, or an explicit policy or storage failure.</returns>
    /// <remarks><para>In-scope reports are promoted atomically into ordinary case review. Subject-scope
    /// violations remain retained as closed not-applicable items without creating cases.</para></remarks>
    Task<LabelServiceResult<LabelPendingReportBatchResult>> ProcessPendingReportsAsync(
        long expectedPolicyRevision,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Commits verified reporter replay protection, scope, case linkage, report and audit together.</summary>
    /// <param name="authentication">The separate cryptographically verified reporter evidence.</param>
    /// <param name="submission">The bounded submitted intent.</param>
    /// <param name="expectedPolicyRevision">The independently verified policy revision, or <see langword="null"/> to record a fail-closed policy denial.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The accepted report or an explicit authentication, scope, policy or storage failure.</returns>
    /// <remarks><para>Tokens are single-use, including identical retries. The standard protocol has no
    /// operation ID, so a retry with a fresh token is a distinct report, not content-deduplicated.
    /// An uncertain commit is not evidence of rollback.</para>
    /// <para>An authenticated, in-scope subject submitted with a disallowed reason is retained as handled
    /// with resolution reason <c>Not Applicable</c>. Automatic handling does not change labels or existing
    /// case workflow and is audited under the issuer with <c>system:report-reason</c> provenance.
    /// Subject restrictions and ownership checks on appeals still apply.</para></remarks>
    Task<LabelServiceResult<LabelModerationReport>> SubmitReportAsync(
        ModerationReportAuthenticationResult authentication,
        LabelReportSubmission submission,
        long? expectedPolicyRevision,
        CancellationToken cancellationToken = default);

    /// <summary>Reads one retained report after checking current viewer authority.</summary>
    /// <param name="caller">The provider-issued viewer or higher.</param>
    /// <param name="reportId">The positive retained report ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The retained report or an explicit failure.</returns>
    Task<LabelServiceResult<LabelModerationReport>> GetReportAsync(
        LabelAdministrationCaller caller, long reportId, CancellationToken cancellationToken = default);

    /// <summary>Reads bounded issuer-local report pages without exposing public enumeration.</summary>
    /// <param name="caller">The provider-issued viewer or higher.</param>
    /// <param name="caseId">The optional exact case filter.</param>
    /// <param name="afterId">The exclusive report-ID continuation boundary.</param>
    /// <param name="limit">The page size from one to 100.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The bounded authorized report page.</returns>
    Task<LabelServiceResult<IReadOnlyList<LabelModerationReport>>> GetReportsAsync(
        LabelAdministrationCaller caller, long? caseId = null, long afterId = 0, int limit = 100, CancellationToken cancellationToken = default);
}
