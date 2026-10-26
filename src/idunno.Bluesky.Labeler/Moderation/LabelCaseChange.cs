// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Captures immutable case-management intent with an optimistic revision and retry identity.</summary>
public sealed class LabelCaseChange
{
    /// <summary>Initializes a new instance of the <see cref="LabelCaseChange"/> class.</summary>
    /// <param name="operationId">The stable operation identifier.</param>
    /// <param name="subject">The exact subject, retained on every operation to prevent redirection.</param>
    /// <param name="action">The requested workflow operation.</param>
    /// <param name="reason">The audit reason or case note.</param>
    /// <param name="caseId">The existing case identifier, or zero for opening a case or internal appeal.</param>
    /// <param name="expectedRevision">The observed revision, or zero for opening a case or internal appeal.</param>
    /// <param name="assignee">The eligible team DID for assignment, or <see langword="null"/> to release it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="subject"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Text or action-specific arguments are invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identifier, revision, action or text length is invalid.</exception>
    public LabelCaseChange(
        string operationId,
        LabelReportSubject subject,
        LabelCaseAction action,
        string reason,
        long caseId = 0,
        long expectedRevision = 0,
        Did? assignee = null)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentOutOfRangeException.ThrowIfNegative(caseId);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedRevision);
        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }

        bool opening = action is LabelCaseAction.Open or LabelCaseAction.OpenAppeal;
        if (opening ? caseId != 0 || expectedRevision != 0 : caseId == 0 || expectedRevision == 0)
        {
            throw new ArgumentException("Opening requires zero identifiers; other changes require an existing case and revision.", nameof(caseId));
        }

        if (assignee is not null && action != LabelCaseAction.Assign)
        {
            throw new ArgumentException("Only assignment accepts an assignee.", nameof(assignee));
        }

        OperationId = LabelInput.ValidateText(operationId, nameof(operationId), 256);
        Reason = LabelInput.ValidateText(reason, nameof(reason), 4096);
        Subject = subject;
        Action = action;
        CaseId = caseId;
        ExpectedRevision = expectedRevision;
        Assignee = assignee;
    }

    /// <summary>Gets the stable retry identifier.</summary>
    public string OperationId { get; }
    /// <summary>Gets the exact subject.</summary>
    public LabelReportSubject Subject { get; }
    /// <summary>Gets the requested operation.</summary>
    public LabelCaseAction Action { get; }
    /// <summary>Gets the audit reason or note.</summary>
    public string Reason { get; }
    /// <summary>Gets the existing case identifier, or zero for an open or internal appeal operation.</summary>
    public long CaseId { get; }
    /// <summary>Gets the observed case revision.</summary>
    public long ExpectedRevision { get; }
    /// <summary>Gets the assignment target, or <see langword="null"/>.</summary>
    public Did? Assignee { get; }
}
