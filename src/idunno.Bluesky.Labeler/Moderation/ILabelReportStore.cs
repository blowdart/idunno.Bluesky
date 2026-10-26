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
    /// <summary>Commits verified reporter replay protection, scope, case linkage, report and audit together.</summary>
    /// <param name="authentication">The separate cryptographically verified reporter evidence.</param>
    /// <param name="submission">The bounded submitted intent.</param>
    /// <param name="expectedPolicyRevision">The independently verified policy revision, or <see langword="null"/> to record a fail-closed policy denial.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The accepted report or an explicit authentication, scope, policy or storage failure.</returns>
    /// <remarks><para>Tokens are single-use, including identical retries. The standard protocol has no
    /// operation ID, so a retry with a fresh token is a distinct report, not content-deduplicated.
    /// An uncertain commit is not evidence of rollback.</para></remarks>
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
