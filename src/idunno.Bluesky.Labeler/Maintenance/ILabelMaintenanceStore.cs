// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Storage;
namespace idunno.Bluesky.Labeler.Maintenance;

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

    /// <summary>Registers a process with an immutable signing role while returning current maintenance progress.</summary>
    /// <param name="instanceId">The process-unique host identity.</param>
    /// <param name="requiresSigningKey"><see langword="true"/> to require replacement-key activation; otherwise, <see langword="false"/> for a keyless distribution replica.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current maintenance state.</returns>
    /// <exception cref="InvalidOperationException">The host has stopped or was registered with a different signing role.</exception>
    /// <remarks><para>Every host must drain regardless of its signing role. <see cref="RegisterHostAsync"/> registers a signing participant.</para></remarks>
    Task<LabelMaintenanceState> RegisterHostWithSigningRoleAsync(
        Guid instanceId,
        bool requiresSigningKey,
        CancellationToken cancellationToken = default);

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
