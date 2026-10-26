// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Administration;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Provides issuer-bound case persistence with live team authorization and atomic audit.</summary>
/// <remarks>
/// <para>These trusted infrastructure APIs share the label provider's serialization boundary.
/// They do not authenticate a request-body DID, issue labels or accept public reports.
/// Workflow adapters must obtain provider-issued caller evidence through the shared authentication boundary.</para>
/// <para>Receipts and history survive provider recreation. A successful retry returns the original snapshot,
/// not current case state. An uncertain commit must be recovered using its original operation identifier.</para>
/// </remarks>
public interface ILabelModerationStore
{
    /// <summary>Reads a fixed workflow view with filter-bound live pagination.</summary>
    /// <param name="caller">The provider-issued viewer or higher.</param>
    /// <param name="query">The immutable workflow filters.</param>
    /// <param name="cursor">The opaque continuation for exactly these filters, caller and issuer.</param>
    /// <param name="limit">The maximum results, between one and one hundred.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The protected live page or a classified failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside its bounds.</exception>
    Task<LabelServiceResult<LabelCasePage>> GetCaseQueueAsync(
        LabelAdministrationCaller caller,
        LabelCaseQuery query,
        string? cursor = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>Commits a prepared case decision and every label, receipt and audit entry atomically.</summary>
    /// <param name="decision">The complete trusted preparation and authenticated moderator.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The historical same-intent receipt or an explicit failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="decision"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A preparation changes the case subject or requested action intent.</exception>
    Task<LabelServiceResult<LabelCaseDecisionReceipt>> CommitCaseDecisionAsync(
        LabelCaseDecisionCommit decision,
        CancellationToken cancellationToken = default);

    /// <summary>Recovers an atomic decision after checking live inspection authority.</summary>
    /// <param name="caller">The provider-issued viewer or higher.</param>
    /// <param name="operationId">The original case-decision identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The original decision and exact signed outcomes or a classified failure.</returns>
    /// <exception cref="ArgumentException"><paramref name="operationId"/> is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="operationId"/> exceeds its bound.</exception>
    Task<LabelServiceResult<LabelCaseDecisionReceipt>> GetCaseDecisionAsync(
        LabelAdministrationCaller caller,
        string operationId,
        CancellationToken cancellationToken = default);

    /// <summary>Applies a case change and its receipt, history and administrative audit atomically.</summary>
    /// <param name="caller">The authenticated invocation evidence.</param>
    /// <param name="change">The immutable case intent.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The original or newly committed receipt, or a classified failure.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    Task<LabelServiceResult<LabelCaseReceipt>> ChangeCaseAsync(
        LabelAdministrationCaller caller,
        LabelCaseChange change,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a case after checking live inspection authority.</summary>
    /// <param name="caller">The authenticated invocation evidence.</param>
    /// <param name="caseId">The positive case identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current snapshot, or a classified failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="caller"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="caseId"/> is not positive.</exception>
    Task<LabelServiceResult<LabelModerationCase>> GetCaseAsync(
        LabelAdministrationCaller caller,
        long caseId,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a bounded live case page ordered by creation identifier.</summary>
    /// <param name="caller">The authenticated invocation evidence.</param>
    /// <param name="status">An optional workflow status restriction.</param>
    /// <param name="afterId">The exclusive case identifier from the previous page.</param>
    /// <param name="limit">The maximum number of cases, between one and one hundred.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authorized page, or a classified failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="caller"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A status, identifier or limit is invalid.</exception>
    /// <remarks><para>Keep filters fixed while traversing. These are live views, not snapshots:
    /// concurrent transitions may move cases between queues. Successful reads append no audit events.</para></remarks>
    Task<LabelServiceResult<IReadOnlyList<LabelModerationCase>>> GetCasesAsync(
        LabelAdministrationCaller caller,
        LabelCaseStatus? status = null,
        long afterId = 0,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>Reads immutable case history without appending an event to that history.</summary>
    /// <param name="caller">The authenticated invocation evidence.</param>
    /// <param name="caseId">The positive case identifier.</param>
    /// <param name="afterSequence">The exclusive history sequence.</param>
    /// <param name="limit">The maximum number of events, between one and one hundred.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authorized history page, or a classified failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="caller"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identifier, sequence or limit is invalid.</exception>
    Task<LabelServiceResult<IReadOnlyList<LabelCaseReceipt>>> GetCaseHistoryAsync(
        LabelAdministrationCaller caller,
        long caseId,
        long afterSequence = 0,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>Recovers a committed case-operation receipt after checking live inspection authority.</summary>
    /// <param name="caller">The authenticated invocation evidence.</param>
    /// <param name="operationId">The original retry identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The original receipt, or a classified failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="caller"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="operationId"/> is invalid text.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="operationId"/> exceeds 256 UTF-8 bytes.</exception>
    Task<LabelServiceResult<LabelCaseReceipt>> GetCaseOperationAsync(
        LabelAdministrationCaller caller,
        string operationId,
        CancellationToken cancellationToken = default);
}
