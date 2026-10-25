// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Maintenance;

/// <summary>Describes durable maintenance and signing-key rotation progress.</summary>
/// <param name="OperationId">The stable rotation operation identity, or <see langword="null"/> when no rotation exists.</param>
/// <param name="Phase">The current maintenance phase.</param>
/// <param name="StreamId">The stream identity captured for this rotation.</param>
/// <param name="PreviousKeyId">The active signing-key identity when the rotation began.</param>
/// <param name="ReplacementKeyId">The preinstalled replacement signing-key identity.</param>
/// <param name="ReplacementPublicKey">The replacement key's public multibase value.</param>
/// <param name="ThroughSequence">The fixed published boundary for re-signing.</param>
/// <param name="LastSequence">The last sequence durably re-signed.</param>
/// <param name="KeyPublicationVerified">Whether the replacement key publication was verified.</param>
/// <param name="Hosts">The hosts required to acknowledge the drain.</param>
public sealed record LabelMaintenanceState(
    string? OperationId,
    LabelMaintenancePhase Phase,
    Guid StreamId,
    string? PreviousKeyId,
    string? ReplacementKeyId,
    string? ReplacementPublicKey,
    long ThroughSequence,
    long LastSequence,
    bool KeyPublicationVerified,
    IReadOnlyList<LabelMaintenanceHost> Hosts)
{
    /// <summary>Gets a value that indicates whether maintenance currently blocks labeler traffic.</summary>
    /// <value><see langword="true"/> if the phase is active; otherwise, <see langword="false"/>.</value>
    public bool IsActive => Phase is not (LabelMaintenancePhase.None or LabelMaintenancePhase.Completed);

    /// <summary>Gets a value that indicates whether every participating host has acknowledged the drain.</summary>
    /// <value><see langword="true"/> if all participating hosts acknowledged; otherwise, <see langword="false"/>.</value>
    public bool AllHostsDrained => Hosts.All(host => host.Acknowledged);
}
