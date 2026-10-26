// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Labeler.Storage;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Retains an exact-subject appeal and the active signed label outcomes captured at intake.</summary>
/// <remarks><para>The captured set includes all currently active labels for the exact account or record
/// CID, not labels inferred from report text. An empty set remains an explicit review request.
/// Intake never issues or withdraws labels. Multiple reports may join one pending captured outcome.</para></remarks>
public sealed class LabelAppeal
{
    /// <summary>Initializes a provider-owned snapshot without reconstructing signed labels.</summary>
    internal LabelAppeal(
        long id,
        long caseId,
        Did initiator,
        LabelReportSubject subject,
        IReadOnlyCollection<StoredLabel> challenges,
        LabelAppealStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string? decisionOperationId)
    {
        Id = id;
        CaseId = caseId;
        Initiator = initiator;
        Subject = subject;
        Challenges = Array.AsReadOnly(challenges.ToArray());
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        DecisionOperationId = decisionOperationId;
    }

    /// <summary>Gets the durable appeal identifier.</summary>
    /// <value>The positive appeal identifier.</value>
    public long Id { get; }
    /// <summary>Gets the exact-subject review case.</summary>
    /// <value>The positive case identifier.</value>
    public long CaseId { get; }
    /// <summary>Gets the authenticated initiator of the original challenge.</summary>
    /// <value>The affected reporter or internal reviewer DID.</value>
    public Did Initiator { get; }
    /// <summary>Gets the submitted exact subject.</summary>
    /// <value>The account or versioned record identity.</value>
    public LabelReportSubject Subject { get; }
    /// <summary>Gets the immutable original active label snapshots.</summary>
    /// <value>The ordered challenges, including their exact stream sequences and signatures.</value>
    public IReadOnlyList<StoredLabel> Challenges { get; }
    /// <summary>Gets the explicit current review status.</summary>
    /// <value>The pending or resolved review outcome.</value>
    public LabelAppealStatus Status { get; }
    /// <summary>Gets the original database-clock capture time.</summary>
    /// <value>The immutable capture instant.</value>
    public DateTimeOffset CreatedAt { get; }
    /// <summary>Gets the database-clock time of the latest review transition.</summary>
    /// <value>The latest persisted transition instant.</value>
    public DateTimeOffset UpdatedAt { get; }
    /// <summary>Gets the atomic moderation decision that resolved this appeal.</summary>
    /// <value>The decision identity, or <see langword="null"/> while pending.</value>
    public string? DecisionOperationId { get; }
}
