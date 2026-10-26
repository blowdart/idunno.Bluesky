// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Retains historical policy intent and independent observation without storing publication credentials.</summary>
public sealed class LabelPolicyHistoryEntry
{
    /// <summary>Initializes a new instance of the <see cref="LabelPolicyHistoryEntry"/> class from a retained provider event.</summary>
    internal LabelPolicyHistoryEntry(
        long sequence,
        string? operationId,
        LabelPolicyHistoryAction action,
        Did? actor,
        string reason,
        DateTimeOffset createdAt,
        LabelPolicyDefinition? policy,
        LabelPolicyState? state,
        Cid? observedCid,
        string? observedDeclarationJson)
    {
        Sequence = sequence;
        OperationId = operationId;
        Action = action;
        Actor = actor;
        Reason = reason;
        CreatedAt = createdAt;
        Policy = policy;
        State = state;
        ObservedCid = observedCid;
        ObservedDeclarationJson = observedDeclarationJson;
    }

    /// <summary>Gets the ascending history position.</summary>
    /// <value>The positive issuer-local sequence.</value>
    public long Sequence { get; }
    /// <summary>Gets the optional stable administrative operation identity.</summary>
    /// <value>The original identity, or <see langword="null"/> for an independent observation.</value>
    public string? OperationId { get; }
    /// <summary>Gets the historical policy operation.</summary>
    /// <value>The draft, publication, abandonment or independent observation.</value>
    public LabelPolicyHistoryAction Action { get; }
    /// <summary>Gets the authenticated administrator responsible for an operation.</summary>
    /// <value>The administrator DID, or <see langword="null"/> for a trusted independent verifier.</value>
    public Did? Actor { get; }
    /// <summary>Gets the retained reason.</summary>
    /// <value>The original administrative reason or independent observation description.</value>
    public string Reason { get; }
    /// <summary>Gets the database-clock append time.</summary>
    /// <value>The historical creation instant.</value>
    public DateTimeOffset CreatedAt { get; }
    /// <summary>Gets the immutable policy selected or saved by the event.</summary>
    /// <value>The retained complete policy, or <see langword="null"/>.</value>
    public LabelPolicyDefinition? Policy { get; }
    /// <summary>Gets the gate immediately after the historical operation.</summary>
    /// <value>The historical gate evaluated at <see cref="CreatedAt"/>, or <see langword="null"/> for a draft.</value>
    /// <remarks><para>This snapshot is not current permission to issue labels or accept reports.</para></remarks>
    public LabelPolicyState? State { get; }
    /// <summary>Gets the independently observed authoritative record CID.</summary>
    /// <value>The observed CID, or <see langword="null"/> for absence or a non-observation event.</value>
    public Cid? ObservedCid { get; }
    /// <summary>Gets the complete independently observed canonical declaration.</summary>
    /// <value>The canonical JSON, including JSON <c>null</c> for authoritative absence, or <see langword="null"/> for a non-observation.</value>
    public string? ObservedDeclarationJson { get; }
}
