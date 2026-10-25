// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler;

/// <summary>Identifies the durable phase of a coordinated signing-key rotation.</summary>
public enum LabelMaintenancePhase
{
    /// <summary>No rotation is active.</summary>
    None,
    /// <summary>Issuance and delivery are draining and participating hosts must acknowledge quiescence.</summary>
    Draining,
    /// <summary>The drained stream boundary is fixed and the replacement key must be published and verified.</summary>
    AwaitingKeyPublication,
    /// <summary>Retained published labels are being re-signed in bounded batches.</summary>
    Resigning,
    /// <summary>All retained labels are re-signed and the replacement key can be activated.</summary>
    ReadyToReopen,
    /// <summary>The rotation completed and maintenance was safely released.</summary>
    Completed
}

/// <summary>Describes a host participating in durable labeler maintenance.</summary>
/// <param name="InstanceId">The process-unique host identity.</param>
/// <param name="Acknowledged">Whether the host confirmed that it drained in-flight work.</param>
/// <param name="KeyActivated">Whether the host activated the replacement signing key.</param>
/// <param name="Stopped">Whether the host has been explicitly confirmed as stopped.</param>
/// <param name="LastSeen">The time the host last refreshed its durable presence.</param>
public sealed record LabelMaintenanceHost(Guid InstanceId, bool Acknowledged, bool KeyActivated, bool Stopped, DateTimeOffset LastSeen);

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

/// <summary>Provides durable maintenance coordination shared by all hosts using one label store.</summary>
/// <remarks>
/// <para>Only trusted hosting and administration adapters should use this infrastructure contract. Administrative
/// operations must authenticate, authorize maintenance permission and record audit outcomes before mutation.</para>
/// <para>Host presence is process-unique. A missing acknowledgement is not evidence that a host is stopped or drained;
/// only a deliberate, independently verified host-stop action may acknowledge an unresponsive participant.</para>
/// </remarks>
public interface ILabelMaintenanceStore
{
    /// <summary>Registers a process and refreshes its presence while returning the current durable maintenance state.</summary>
    /// <param name="instanceId">The process-unique host identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current maintenance state.</returns>
    Task<LabelMaintenanceState> RegisterHostAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>Refreshes a registered host's presence and reads current maintenance progress.</summary>
    /// <param name="instanceId">The registered process identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current maintenance state.</returns>
    Task<LabelMaintenanceState> HeartbeatHostAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>Records a cleanly stopped host after its in-flight work has drained.</summary>
    /// <param name="instanceId">The registered process identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current maintenance state.</returns>
    Task<LabelMaintenanceState> StopHostAsync(Guid instanceId, CancellationToken cancellationToken = default);

    /// <summary>Reads the durable maintenance state and its participating hosts.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current maintenance state.</returns>
    Task<LabelMaintenanceState> GetMaintenanceStateAsync(CancellationToken cancellationToken = default);

    /// <summary>Acquires exclusive maintenance and snapshots every currently registered host as a participant.</summary>
    /// <param name="operationId">The stable rotation operation identity.</param>
    /// <param name="previousKeyId">The currently active key identity.</param>
    /// <param name="replacementKeyId">The preinstalled replacement key identity.</param>
    /// <param name="replacementPublicKey">The replacement key's public multibase value.</param>
    /// <param name="caller">The authorized maintenance caller, or <see langword="null"/> for local administration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The durable rotation state.</returns>
    /// <exception cref="InvalidOperationException">Another maintenance operation is active or the labeler is not active.</exception>
    Task<LabelMaintenanceState> BeginRotationAsync(
        string operationId,
        string previousKeyId,
        string replacementKeyId,
        string replacementPublicKey,
        LabelAdministrationCaller? caller = null,
        CancellationToken cancellationToken = default);

    /// <summary>Acknowledges that a participating host has blocked admission, drained work and closed subscriptions.</summary>
    /// <param name="operationId">The active rotation identity.</param>
    /// <param name="instanceId">The participating process identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated maintenance state.</returns>
    Task<LabelMaintenanceState> AcknowledgeHostDrainedAsync(
        string operationId,
        Guid instanceId,
        CancellationToken cancellationToken = default);

    /// <summary>Records that a drained host activated the replacement key before maintenance is released.</summary>
    /// <param name="operationId">The active rotation identity.</param>
    /// <param name="instanceId">The participating process identity.</param>
    /// <param name="keyId">The replacement key identity activated by the host.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated maintenance state.</returns>
    Task<LabelMaintenanceState> AcknowledgeHostKeyActivatedAsync(
        string operationId,
        Guid instanceId,
        string keyId,
        CancellationToken cancellationToken = default);

    /// <summary>Explicitly acknowledges a host that an operator independently verified as stopped.</summary>
    /// <param name="operationId">The active rotation identity.</param>
    /// <param name="instanceId">The unresponsive participating process identity.</param>
    /// <param name="caller">The authorized maintenance caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated maintenance state.</returns>
    /// <remarks><para>The caller must require maintenance authorization, explicit operator confirmation and an audit record.</para></remarks>
    Task<LabelMaintenanceState> ConfirmHostStoppedAsync(
        string operationId,
        Guid instanceId,
        LabelAdministrationCaller? caller = null,
        CancellationToken cancellationToken = default);

    /// <summary>Fixes the stream boundary after every host has drained and pending publication has completed.</summary>
    /// <param name="operationId">The active rotation identity.</param>
    /// <param name="caller">The authorized maintenance caller, or <see langword="null"/> for local administration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The maintenance state containing the fixed published boundary.</returns>
    /// <exception cref="InvalidOperationException">A host has not drained or publication remains pending.</exception>
    Task<LabelMaintenanceState> CaptureRotationBoundaryAsync(
        string operationId,
        LabelAdministrationCaller? caller = null,
        CancellationToken cancellationToken = default);

    /// <summary>Records verification that the replacement public key is published for the bound issuer and endpoint.</summary>
    /// <param name="operationId">The active rotation identity.</param>
    /// <param name="replacementPublicKey">The public key value observed in the resolved identity document.</param>
    /// <param name="caller">The authorized maintenance caller, or <see langword="null"/> for local administration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated maintenance state.</returns>
    Task<LabelMaintenanceState> VerifyRotationKeyPublicationAsync(
        string operationId,
        string replacementPublicKey,
        LabelAdministrationCaller? caller = null,
        CancellationToken cancellationToken = default);

    /// <summary>Atomically replaces one bounded signature batch and advances the durable rotation checkpoint.</summary>
    /// <param name="operationId">The active rotation identity.</param>
    /// <param name="expectedAfterSequence">The checkpoint from which the batch was read.</param>
    /// <param name="replacementLabels">The re-signed labels, in strictly increasing sequence order.</param>
    /// <param name="checkpoint">The last sequence inspected by this batch.</param>
    /// <param name="caller">The authorized maintenance caller, or <see langword="null"/> for local administration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated maintenance state.</returns>
    Task<LabelMaintenanceState> ReplaceRotationBatchAsync(
        string operationId,
        long expectedAfterSequence,
        IReadOnlyList<StoredLabel> replacementLabels,
        long checkpoint,
        LabelAdministrationCaller? caller = null,
        CancellationToken cancellationToken = default);

    /// <summary>Marks re-signing complete after the replacement key is active, then releases maintenance.</summary>
    /// <param name="operationId">The active rotation identity.</param>
    /// <param name="activeKeyId">The key identity made active by the host's key ring.</param>
    /// <param name="caller">The authorized maintenance caller, or <see langword="null"/> for local administration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The completed rotation state.</returns>
    /// <exception cref="InvalidOperationException">Re-signing, publication verification or key activation is incomplete.</exception>
    Task<LabelMaintenanceState> CompleteRotationAsync(
        string operationId,
        string activeKeyId,
        LabelAdministrationCaller? caller = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Resolves signing keys installed in a host-controlled secret store and atomically activates one key identity.</summary>
/// <remarks>
/// <para>The provider owns every returned <see cref="idunno.AtProto.Labels.LabelSigningKey"/>; callers must not dispose it.
/// Remote administration sends key identities and public values only, never private key material.</para>
/// <para>Implementations must persist active-key selection so a restart preserves the selected signing identity.</para>
/// </remarks>
public interface ILabelSigningKeyRing
{
    /// <summary>Gets the identity of the currently active signing key.</summary>
    string ActiveKeyId { get; }

    /// <summary>Gets a host-owned signing key by its configured identity.</summary>
    /// <param name="keyId">The configured key identity.</param>
    /// <returns>The host-owned signing key.</returns>
    /// <exception cref="KeyNotFoundException">The key identity is not configured.</exception>
    idunno.AtProto.Labels.LabelSigningKey GetSigningKey(string keyId);

    /// <summary>Gets the public multibase value for a configured signing key.</summary>
    /// <param name="keyId">The configured key identity.</param>
    /// <returns>The public key value.</returns>
    /// <exception cref="KeyNotFoundException">The key identity is not configured.</exception>
    string GetPublicKeyMultibase(string keyId);

    /// <summary>Persists the active key identity without exporting or transferring private key material.</summary>
    /// <param name="keyId">The configured key identity to activate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the activation.</returns>
    /// <exception cref="KeyNotFoundException">The key identity is not configured.</exception>
    Task ActivateAsync(string keyId, CancellationToken cancellationToken = default);
}
