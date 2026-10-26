// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Retains immutable filters for a live workflow view and its continuation cursor.</summary>
public sealed class LabelCaseQuery
{
    /// <summary>Initializes a new instance of the <see cref="LabelCaseQuery"/> class.</summary>
    /// <param name="queue">One of the fixed workflow views.</param>
    /// <param name="subject">The optional exact account or record version.</param>
    /// <param name="reasonType">The optional exact reason of an associated report.</param>
    /// <param name="assignee">The optional manually assigned moderator.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="queue"/> is undefined or text is too long.</exception>
    /// <exception cref="ArgumentException">A reason is invalid, or an unassigned view specifies an assignee.</exception>
    public LabelCaseQuery(
        LabelCaseWorkflow queue = LabelCaseWorkflow.Unresolved,
        LabelReportSubject? subject = null,
        string? reasonType = null,
        Did? assignee = null)
    {
        if (!Enum.IsDefined(queue))
        {
            throw new ArgumentOutOfRangeException(nameof(queue));
        }

        if (queue == LabelCaseWorkflow.Unassigned && assignee is not null)
        {
            throw new ArgumentException("An unassigned view cannot specify an assignee.", nameof(assignee));
        }

        Queue = queue;
        Subject = subject;
        ReasonType = reasonType is null ? null : LabelInput.ValidateText(reasonType, nameof(reasonType), 1024);
        Assignee = assignee;
    }

    /// <summary>Gets the fixed workflow view.</summary>
    /// <value>The selected workflow state or assignment view.</value>
    public LabelCaseWorkflow Queue { get; }
    /// <summary>Gets the optional exact subject filter.</summary>
    /// <value>An account DID or record URI and CID, or <see langword="null"/>.</value>
    public LabelReportSubject? Subject { get; }
    /// <summary>Gets the optional report reason filter.</summary>
    /// <value>The exact reason identifier, or <see langword="null"/>.</value>
    public string? ReasonType { get; }
    /// <summary>Gets the optional assignment filter.</summary>
    /// <value>The manually assigned moderator DID, or <see langword="null"/>.</value>
    public Did? Assignee { get; }
}
