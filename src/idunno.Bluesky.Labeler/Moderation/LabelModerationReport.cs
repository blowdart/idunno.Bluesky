// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Retains a confidential accepted report and its exact policy, case and reporter provenance.</summary>
public sealed class LabelModerationReport
{
    /// <summary>Initializes a retained provider report.</summary>
    internal LabelModerationReport(
        long id,
        long caseId,
        LabelReportSubmission submission,
        Did reportedBy,
        DateTimeOffset createdAt,
        string policyVersion,
        string audience)
    {
        Id = id;
        CaseId = caseId;
        Submission = submission;
        ReportedBy = reportedBy;
        CreatedAt = createdAt;
        PolicyVersion = policyVersion;
        Audience = audience;
    }

    /// <summary>Gets the stable accepted report ID.</summary>
    /// <value>The positive issuer-local report ID.</value>
    public long Id { get; }
    /// <summary>Gets the linked exact-subject review case.</summary>
    /// <value>The retained case ID.</value>
    public long CaseId { get; }
    /// <summary>Gets the immutable submitted report intent.</summary>
    /// <value>The original subject, reason and optional tool metadata.</value>
    public LabelReportSubmission Submission { get; }
    /// <summary>Gets the verified reporter DID.</summary>
    /// <value>The service-authenticated reporter, not a body-supplied identity.</value>
    public Did ReportedBy { get; }
    /// <summary>Gets the database-assigned acceptance time.</summary>
    /// <value>The retained creation timestamp.</value>
    public DateTimeOffset CreatedAt { get; }
    /// <summary>Gets the verified policy version at acceptance.</summary>
    /// <value>The immutable applicable policy version.</value>
    public string PolicyVersion { get; }
    /// <summary>Gets the actual signed service audience.</summary>
    /// <value>The exact-service or legacy bare-DID audience used by this report.</value>
    public string Audience { get; }
}
