// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Retains a bounded reasoned decision that updates one exact-subject case atomically.</summary>
/// <remarks><para>Zero actions represent an audited no-action decision. Each label value may occur
/// once, because issuer, URI and value identify a label slot independently of record CID.</para></remarks>
public sealed class LabelCaseDecision
{
    /// <summary>Initializes a new instance of the <see cref="LabelCaseDecision"/> class.</summary>
    /// <param name="operationId">The stable same-intent decision identifier.</param>
    /// <param name="caseId">The positive case identifier.</param>
    /// <param name="expectedRevision">The observed positive case revision.</param>
    /// <param name="policyVersion">The selected immutable policy version.</param>
    /// <param name="reason">The nonblank moderation reason.</param>
    /// <param name="actions">The ordered collection of up to sixteen distinct label values.</param>
    /// <param name="appealIds">The optional set of up to sixteen pending appeals explicitly reviewed.</param>
    /// <param name="appealOutcome">The explicit outcome applied to those appeals.</param>
    /// <param name="reportId">The optional current report explicitly reviewed by this decision.</param>
    /// <param name="handleOtherReports"><see langword="true"/> to also handle other open reports for the exact subject and CID; otherwise, <see langword="false"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="actions"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Text, actions or explicit appeal resolution arguments are invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identifier, revision, text, action count or appeal count exceeds its bounds.</exception>
    /// <remarks><para>Pending appeals require explicit review. Upholding unchanged outcomes and acknowledging
    /// superseded outcomes cannot mutate labels. Other pending appeals keep the case in review.</para></remarks>
    public LabelCaseDecision(
        string operationId,
        long caseId,
        long expectedRevision,
        string policyVersion,
        string reason,
        IReadOnlyCollection<LabelCaseLabelAction> actions,
        IReadOnlyCollection<long>? appealIds = null,
        LabelAppealStatus? appealOutcome = null,
        long? reportId = null,
        bool handleOtherReports = false)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedRevision);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(actions.Count, 16);
        if (reportId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reportId));
        }
        LabelCaseLabelAction[] copy = actions.ToArray();
        if (copy.Any(action => action is null) || copy.Select(action => action.Value).Distinct(StringComparer.Ordinal).Count() != copy.Length)
        {
            throw new ArgumentException("Actions must be non-null and identify distinct label values.", nameof(actions));
        }

        long[] appeals = appealIds?.Order().ToArray() ?? [];
        ArgumentOutOfRangeException.ThrowIfGreaterThan(appeals.Length, 16);
        if (appeals.Any(id => id <= 0) || appeals.Distinct().Count() != appeals.Length ||
            (appeals.Length == 0) != (appealOutcome is null) ||
            appealOutcome is { } outcome && (!Enum.IsDefined(outcome) || outcome == LabelAppealStatus.Pending))
        {
            throw new ArgumentException("Appeals require distinct positive identities and an explicit resolved outcome.", nameof(appealIds));
        }

        if (appealOutcome is LabelAppealStatus.Upheld or LabelAppealStatus.Superseded && copy.Length != 0 ||
            appealOutcome == LabelAppealStatus.Withdrawn && (copy.Length == 0 || copy.Any(action => action.Kind != Issuance.LabelOperationKind.Negate)) ||
            appealOutcome == LabelAppealStatus.Replaced && copy.Length == 0)
        {
            throw new ArgumentException("Label actions do not match the explicit appeal outcome.", nameof(actions));
        }

        OperationId = LabelInput.ValidateText(operationId, nameof(operationId), 256);
        CaseId = caseId;
        ExpectedRevision = expectedRevision;
        PolicyVersion = LabelInput.ValidateText(policyVersion, nameof(policyVersion), 256);
        Reason = LabelInput.ValidateText(reason, nameof(reason), 4096);
        Actions = Array.AsReadOnly(copy);
        AppealIds = Array.AsReadOnly(appeals);
        AppealOutcome = appealOutcome;
        ReportId = reportId;
        HandleOtherReports = handleOtherReports;
    }

    /// <summary>Gets the stable retry identifier.</summary>
    /// <value>The immutable operation identity.</value>
    public string OperationId { get; }
    /// <summary>Gets the exact case to review.</summary>
    /// <value>The positive case identifier.</value>
    public long CaseId { get; }
    /// <summary>Gets the observed case revision.</summary>
    /// <value>The optimistic workflow fence.</value>
    public long ExpectedRevision { get; }
    /// <summary>Gets the immutable policy version applied to this decision.</summary>
    /// <value>The selected policy version.</value>
    public string PolicyVersion { get; }
    /// <summary>Gets the reason retained with every action and the case outcome.</summary>
    /// <value>The nonblank moderation reason.</value>
    public string Reason { get; }
    /// <summary>Gets the ordered immutable action list.</summary>
    /// <value>At most sixteen distinct label-slot actions.</value>
    public IReadOnlyList<LabelCaseLabelAction> Actions { get; }
    /// <summary>Gets the pending appeals explicitly reviewed by this decision.</summary>
    /// <value>The immutable sorted appeal identifiers.</value>
    public IReadOnlyList<long> AppealIds { get; }
    /// <summary>Gets the explicit review outcome.</summary>
    /// <value>The resolved status, or <see langword="null"/> for an ordinary non-appeal decision.</value>
    public LabelAppealStatus? AppealOutcome { get; }
    /// <summary>Gets the current report explicitly handled by this decision.</summary>
    /// <value>The report identifier, or <see langword="null"/> for a case-level decision.</value>
    public long? ReportId { get; }
    /// <summary>Gets whether other open reports for the exact target are handled by this decision.</summary>
    /// <value><see langword="true"/> to acknowledge matching reports without dismissing them as unfounded.</value>
    public bool HandleOtherReports { get; }

    /// <summary>Compares complete semantic intent without depending on signed-label regeneration.</summary>
    internal bool SemanticallyEquals(LabelCaseDecision other) =>
        CaseId == other.CaseId && ExpectedRevision == other.ExpectedRevision &&
        PolicyVersion == other.PolicyVersion && Reason == other.Reason &&
        AppealOutcome == other.AppealOutcome && AppealIds.SequenceEqual(other.AppealIds) &&
        ReportId == other.ReportId && HandleOtherReports == other.HandleOtherReports &&
        Actions.Count == other.Actions.Count && Actions.Zip(other.Actions).All(pair =>
            pair.First.Kind == pair.Second.Kind && pair.First.Value == pair.Second.Value &&
            pair.First.ExpiresAt == pair.Second.ExpiresAt && pair.First.ExpectedLabelSequence == pair.Second.ExpectedLabelSequence);

    /// <summary>Derives bounded stable action identities without truncating the original decision identifier.</summary>
    internal string ActionOperationId(int index) =>
        "xenon:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(OperationId))) +
        ":" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
