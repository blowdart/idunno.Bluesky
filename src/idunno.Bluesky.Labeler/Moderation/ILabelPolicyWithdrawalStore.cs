// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Storage;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Fences host-signed policy withdrawal batches against durable progress and live administrator authority.</summary>
internal interface ILabelPolicyWithdrawalStore
{
    /// <summary>Reads at most 250 frozen-history entries, first establishing a superseding timestamp and then selecting removed-value slots.</summary>
    Task<LabelServiceResult<LabelPolicyWithdrawalBatch>> ReadPolicyWithdrawalBatchAsync(CancellationToken cancellationToken);

    /// <summary>Atomically appends validated negations and advances their restart-safe checkpoint.</summary>
    Task<LabelServiceResult<LabelPolicyState>> CommitPolicyWithdrawalBatchAsync(
        LabelPolicyWithdrawalBatch batch,
        IReadOnlyList<SignedLabelSnapshot> negations,
        string? signingPublicKey,
        CancellationToken cancellationToken);

    /// <summary>Renews the pending operation's authority after expiration or revocation without replacing its intent or checkpoints.</summary>
    Task<LabelServiceResult<LabelPolicyState>> ResumePolicyWithdrawalAsync(
        LabelAdministrationCaller caller,
        string operationId,
        string reason,
        CancellationToken cancellationToken);
}

/// <summary>Exposes cleanup checkpoints and database-clock activity without exposing the retained authentication evidence.</summary>
/// <remarks><para>Activity records an attempt, not worker liveness. Progress changes only when checkpoints, observed publication or the phase advance.
/// Missing timestamps and publication observations remain unknown for operations saved by earlier versions.</para></remarks>
internal sealed record LabelPolicyWithdrawalProgress(
    string[] Values,
    long UpperBound,
    long ScannedThrough,
    long NegatedThrough,
    long NegationSequence,
    string? PauseReason,
    DateTimeOffset? LastActivityAt = null,
    DateTimeOffset? LastProgressAt = null,
    long? PublishedThrough = null);

/// <summary>Retains the exact observations a host may sign; an absent policy denotes no pending work.</summary>
internal sealed record LabelPolicyWithdrawalBatch(
    LabelPolicyState? Policy,
    long StoreRevision,
    long ScannedThrough,
    long NegatedThrough,
    DateTimeOffset CreatedAt,
    IReadOnlyList<StoredLabel> Labels);
