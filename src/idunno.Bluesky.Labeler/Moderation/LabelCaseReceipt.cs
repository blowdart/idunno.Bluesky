// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Contains an immutable case-operation receipt and its resulting historical snapshot.</summary>
public sealed class LabelCaseReceipt
{
    /// <summary>Projects a committed operation without exposing provider or authentication secrets.</summary>
    internal LabelCaseReceipt(
        long sequence,
        string operationId,
        LabelCaseAction action,
        Did actor,
        string? localIdentity,
        string reason,
        DateTimeOffset createdAt,
        LabelModerationCase moderationCase)
    {
        Sequence = sequence;
        OperationId = operationId;
        Action = action;
        Actor = actor;
        LocalIdentity = localIdentity;
        Reason = reason;
        CreatedAt = createdAt;
        Case = moderationCase;
    }

    /// <summary>Gets the issuer-local case-history sequence.</summary>
    public long Sequence { get; }
    /// <summary>Gets the stable retry identifier.</summary>
    public string OperationId { get; }
    /// <summary>Gets the performed operation.</summary>
    public LabelCaseAction Action { get; }
    /// <summary>Gets the authenticated or explicitly delegated actor DID.</summary>
    public Did Actor { get; }
    /// <summary>Gets the authenticated OS identity, or <see langword="null"/> for remote callers.</summary>
    public string? LocalIdentity { get; }
    /// <summary>Gets the reason or note.</summary>
    public string Reason { get; }
    /// <summary>Gets the database-assigned event time.</summary>
    public DateTimeOffset CreatedAt { get; }
    /// <summary>Gets the historical case snapshot committed with this receipt.</summary>
    public LabelModerationCase Case { get; }
}
