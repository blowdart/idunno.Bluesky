// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Confirms a report accepted by the public protocol intake.</summary>
public sealed class LabelReportIntakeReceipt
{
    /// <summary>Initializes a report intake receipt.</summary>
    internal LabelReportIntakeReceipt(long id, DateTimeOffset createdAt, bool pendingVerification)
    {
        Id = id;
        CreatedAt = createdAt;
        PendingVerification = pendingVerification;
    }

    /// <summary>Gets the stable issuer-local report identifier.</summary>
    /// <value>The positive report identifier.</value>
    public long Id { get; }

    /// <summary>Gets the database-assigned receipt time.</summary>
    /// <value>The report's original acceptance time.</value>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Gets a value that indicates whether the report awaits policy verification.</summary>
    /// <value><see langword="true" /> if the report is durable but not actionable; otherwise, <see langword="false" />.</value>
    public bool PendingVerification { get; }
}

/// <summary>Describes the progress of one bounded pending-report processing batch.</summary>
public sealed class LabelPendingReportBatchResult
{
    /// <summary>Initializes a pending-report batch result.</summary>
    internal LabelPendingReportBatchResult(int processed, bool hasMore)
    {
        Processed = processed;
        HasMore = hasMore;
    }

    /// <summary>Gets the number of pending reports processed by the batch.</summary>
    /// <value>The number of promoted or policy-closed reports.</value>
    public int Processed { get; }

    /// <summary>Gets a value that indicates whether more pending reports remain.</summary>
    /// <value><see langword="true" /> if another batch may be required; otherwise, <see langword="false" />.</value>
    public bool HasMore { get; }
}
