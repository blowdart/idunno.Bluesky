// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Storage;

namespace idunno.Bluesky.Labeler.Maintenance;

/// <summary>Fences retirement commands and host-signed batches against durable progress and live authority.</summary>
internal interface ILabelRetirementStore
{
    /// <summary>Recovers an authorized immutable receipt before consulting current signing configuration or phase.</summary>
    Task<LabelServiceResult<LabelRetirementRecovery>> RecoverRetirementAsync(
        LabelAdministrationCaller caller,
        LabelRetirementCommand command,
        CancellationToken cancellationToken);

    /// <summary>Acquires a read-only recovery fence before admitting any batch read, signing or deletion.</summary>
    Task<LabelServiceResult<LabelRetirementAdmission>> ReadRetirementCheckpointAsync(CancellationToken cancellationToken);

    /// <summary>Recovers and pauses the same administrative execution epoch without advancing work.</summary>
    Task PauseRetirementCheckpointAsync(LabelRetirementCheckpoint checkpoint, string error, CancellationToken cancellationToken);

    /// <summary>Inspects progress or prepares an action-specific, expiring confirmation.</summary>
    Task<LabelServiceResult<LabelRetirementView>> InspectRetirementAsync(
        LabelAdministrationCaller caller,
        LabelRetirementCommand? preview,
        CancellationToken cancellationToken);

    /// <summary>Commits an authorized immutable command, its outcome and its audit together.</summary>
    Task<LabelServiceResult<LabelRetirementView>> ExecuteRetirementAsync(
        LabelAdministrationCaller caller,
        LabelRetirementCommand command,
        CancellationToken cancellationToken);

    /// <summary>Reads one bounded page under revocable saved worker authority.</summary>
    Task<LabelServiceResult<LabelRetirementBatch>> ReadRetirementBatchAsync(
        CancellationToken cancellationToken,
        LabelRetirementCheckpoint? checkpoint = null);

    /// <summary>Revalidates and commits one signed page, checkpoint and audit atomically.</summary>
    Task<LabelServiceResult<LabelRetirementView>> CommitRetirementBatchAsync(
        LabelRetirementBatch batch,
        IReadOnlyList<SignedLabelSnapshot> negations,
        string? signingPublicKey,
        CancellationToken cancellationToken);

    /// <summary>Persists a classified worker failure without overwriting newer progress.</summary>
    Task PauseRetirementAsync(long generation, string error, CancellationToken cancellationToken);

    /// <summary>Deletes one bounded confirmed local-history batch under saved live authority.</summary>
    Task AdvanceRetirementPurgeAsync(CancellationToken cancellationToken, LabelRetirementCheckpoint? checkpoint = null);
}

/// <summary>Fences failure recovery across uncertain batch commits but not across later administrative commands.</summary>
internal sealed record LabelRetirementCheckpoint(long Generation, Guid ControlId);

/// <summary>Distinguishes an authorized missing receipt from a failed recovery lookup.</summary>
internal sealed record LabelRetirementRecovery(LabelRetirementView? Receipt);

/// <summary>Distinguishes idle retirement from an unavailable worker-admission checkpoint.</summary>
internal sealed record LabelRetirementAdmission(LabelRetirementCheckpoint? Checkpoint);

/// <summary>Retains immutable administrative intent independently of transport flags.</summary>
internal sealed record LabelRetirementCommand(
    string Action,
    string OperationId,
    string Reason,
    int GracePeriodDays = 0,
    string? Confirmation = null,
    string? Attestation = null,
    string? Inventory = null,
    string? VerifiedSigningPublicKey = null,
    Guid? HostInstanceId = null);

/// <summary>Exposes progress and consequences without retained authentication evidence or private keys.</summary>
internal sealed record LabelRetirementView(
    string? OperationId,
    long Generation,
    string Phase,
    string Stage,
    bool Running,
    long UpperBound,
    long ScannedThrough,
    long NegatedThrough,
    long IssuedThrough,
    long PublishedThrough,
    DateTimeOffset? GraceDeadline,
    string? PauseReason,
    DateTimeOffset? LastActivityAt,
    DateTimeOffset? LastProgressAt,
    string KeyRemoval,
    string? Inventory,
    string? AvailabilityAttestation,
    string? Confirmation = null,
    DateTimeOffset? ConfirmationExpiresAt = null,
    string? Blocker = null,
    string? SigningKeyId = null,
    string? SigningPublicKey = null,
    IReadOnlyList<LabelRetirementHost>? Hosts = null,
    string? Issuer = null)
{
    /// <summary>Gets the exact local cleanup scope, which deliberately retains moderation and administrative content.</summary>
    public string PurgeScope { get; } = "Label history and label-operation receipts only. Reports, cases, appeals, policy history, team, administrative audit, backups and downstream copies remain.";
}

/// <summary>Exposes registered signing processes without treating a stale heartbeat as stopped evidence.</summary>
internal sealed record LabelRetirementHost(Guid InstanceId, bool RequiresSigningKey, DateTimeOffset LastSeen);

/// <summary>Captures a bounded provider observation that may be signed outside its transaction.</summary>
internal sealed record LabelRetirementBatch(
    string? OperationId,
    long Generation,
    long StoreRevision,
    long ScannedThrough,
    long NegatedThrough,
    DateTimeOffset CreatedAt,
    IReadOnlyList<StoredLabel> Labels,
    bool Scanning = false,
    bool Publishing = false);
