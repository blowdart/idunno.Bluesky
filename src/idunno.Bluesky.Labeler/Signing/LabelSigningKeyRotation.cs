// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Storage;

namespace idunno.Bluesky.Labeler.Signing;

/// <summary>Re-signs retained published labels in fixed, resumable batches.</summary>
public static class LabelSigningKeyRotation
{
    /// <summary>
    /// Re-signs the next published-label batch without changing any signed field or sequence position.
    /// </summary>
    /// <param name="store">The initialized label store.</param>
    /// <param name="maintenanceStore">The durable maintenance coordinator.</param>
    /// <param name="operationId">The active rotation operation identity.</param>
    /// <param name="replacementKey">The replacement label-signing key.</param>
    /// <param name="batchSize">The maximum number of labels to process, from 1 through 1,000.</param>
    /// <param name="caller">The authorized maintenance caller, when re-signing through administration.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The durable rotation checkpoint after the next batch.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="operationId"/> is empty or consists only of white-space characters.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="batchSize"/> is outside its supported range.</exception>
    /// <exception cref="InvalidDataException">The fixed boundary cannot be replayed because required history is unavailable.</exception>
    /// <exception cref="InvalidOperationException">The rotation is not ready for re-signing or the replacement key does not match its durable identity.</exception>
    /// <remarks>
    /// <para>The durable coordinator owns the fixed stream, publication boundary and checkpoint. It accepts re-signing only after every host drained and the replacement key was verified as published. A retry reads the committed checkpoint rather than trusting caller-supplied progress.</para>
    /// <para>Re-signing does not change <c>cts</c>, other signed fields, or stream sequence, and it does not create new label events. Keep the previous public key available until retained history has been processed and consumers have had time to resolve it.</para>
    /// </remarks>
    public static async Task<LabelSigningKeyRotationProgress> ResignNextBatchAsync(
        ILabelStore store,
        ILabelMaintenanceStore maintenanceStore,
        string operationId,
        LabelSigningKey replacementKey,
        int batchSize = 1_000,
        LabelAdministrationCaller? caller = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(maintenanceStore);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentNullException.ThrowIfNull(replacementKey);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(batchSize, 1_000);

        LabelMaintenanceState state = await maintenanceStore.GetMaintenanceStateAsync(cancellationToken).ConfigureAwait(false);
        if (state.Phase != LabelMaintenancePhase.Resigning ||
            !string.Equals(state.OperationId, operationId, StringComparison.Ordinal) ||
            !state.KeyPublicationVerified ||
            !string.Equals(state.ReplacementPublicKey, replacementKey.ExportPublicKeyMultibase(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The rotation is not ready for this replacement key.");
        }

        LabelPage page = await store.ReplayAsync(
            streamId: state.StreamId,
            after: state.LastSequence,
            through: state.ThroughSequence,
            limit: batchSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        StoredLabel[] replacements =
        [
            .. page.Labels.Select(label => new StoredLabel(
                label.Sequence,
                new SignedLabelSnapshot(replacementKey.Sign(label.SignedLabel.Label))))
        ];
        long checkpoint = replacements.Length == 0 ? state.ThroughSequence : replacements[^1].Sequence;
        LabelMaintenanceState updated = await maintenanceStore.ReplaceRotationBatchAsync(
            operationId: operationId,
            expectedAfterSequence: state.LastSequence,
            replacementLabels: replacements,
            checkpoint: checkpoint,
            cancellationToken: cancellationToken,
            caller: caller).ConfigureAwait(false);

        return new(updated.StreamId, updated.ThroughSequence, updated.LastSequence);
    }
}
