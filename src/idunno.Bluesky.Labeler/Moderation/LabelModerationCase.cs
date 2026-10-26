// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Contains a durable case snapshot without mutable report or audit collections.</summary>
public sealed class LabelModerationCase
{
    /// <summary>Projects validated provider state into an immutable case snapshot.</summary>
    internal LabelModerationCase(
        long id,
        long revision,
        LabelReportSubject subject,
        LabelCaseStatus status,
        Did? assignee,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Revision = revision;
        Subject = subject;
        Status = status;
        Assignee = assignee;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>Gets the issuer-local case identifier.</summary>
    public long Id { get; }
    /// <summary>Gets the optimistic case revision.</summary>
    public long Revision { get; }
    /// <summary>Gets the exact subject.</summary>
    public LabelReportSubject Subject { get; }
    /// <summary>Gets the workflow status.</summary>
    public LabelCaseStatus Status { get; }
    /// <summary>Gets the assigned DID, which may subsequently have lost team authority.</summary>
    public Did? Assignee { get; }
    /// <summary>Gets the database-assigned creation time.</summary>
    public DateTimeOffset CreatedAt { get; }
    /// <summary>Gets the database-assigned last mutation time.</summary>
    public DateTimeOffset UpdatedAt { get; }
}
