// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Labels;

namespace idunno.Bluesky.Labeler;

/// <summary>Reports a fixed sequence boundary for a resumable label-signature rotation.</summary>
public sealed class LabelSigningKeyRotationProgress
{
    internal LabelSigningKeyRotationProgress(Guid streamId, long through, long lastSequence)
    {
        StreamId = streamId;
        Through = through;
        LastSequence = lastSequence;
    }

    /// <summary>Gets the durable label stream identity.</summary>
    public Guid StreamId { get; }

    /// <summary>Gets the inclusive published boundary captured for this rotation.</summary>
    public long Through { get; }

    /// <summary>Gets the last sequence whose signature was replaced.</summary>
    public long LastSequence { get; }

    /// <summary>Gets a value indicating whether the fixed rotation boundary has been reached.</summary>
    public bool IsComplete => LastSequence >= Through;
}

/// <summary>Re-signs retained published labels in fixed, resumable batches.</summary>
public static class LabelSigningKeyRotation
{
    /// <summary>
    /// Re-signs the next published-label batch without changing any signed field or sequence position.
    /// </summary>
    /// <param name="store">The initialized label store.</param>
    /// <param name="replacementKey">The replacement label-signing key.</param>
    /// <param name="streamId">The store stream identity captured before rotation.</param>
    /// <param name="afterSequence">The last sequence completed by a previous batch, or zero to start.</param>
    /// <param name="throughSequence">The fixed inclusive publication boundary captured before rotation.</param>
    /// <param name="batchSize">The maximum number of labels to process, from 1 through 1,000.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The durable boundary to persist and use to resume the next batch.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> or <paramref name="replacementKey"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="afterSequence"/>, <paramref name="throughSequence"/>, or <paramref name="batchSize"/> is outside its supported range.</exception>
    /// <exception cref="InvalidDataException">The fixed boundary cannot be replayed because required history is unavailable.</exception>
    /// <exception cref="InvalidOperationException">The store is not active or a replacement changes label fields.</exception>
    /// <remarks>
    /// <para>Pause label creation and signing, publish all pending labels, publish and verify the replacement public key, then capture the stream and published boundary before calling this method. Persist <see cref="LabelSigningKeyRotationProgress.LastSequence"/> after each successful batch and resume from that value after interruption.</para>
    /// <para>Keep the previous public key available until retained history has been processed and consumers have had time to resolve it. Re-signing does not change <c>cts</c>, other signed fields, or stream sequence, and it does not create new label events. Labels outside retained local history cannot be re-signed by this helper.</para>
    /// </remarks>
    public static async Task<LabelSigningKeyRotationProgress> ResignNextBatchAsync(
        ILabelStore store,
        LabelSigningKey replacementKey,
        Guid streamId,
        long afterSequence,
        long throughSequence,
        int batchSize = 1_000,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(replacementKey);
        ArgumentOutOfRangeException.ThrowIfNegative(afterSequence);
        ArgumentOutOfRangeException.ThrowIfNegative(throughSequence);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(afterSequence, throughSequence);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(batchSize, 1_000);

        LabelPage page = await store.ReplayAsync(
            streamId: streamId,
            after: afterSequence,
            through: throughSequence,
            limit: batchSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (page.Labels.Count > 0)
        {
            StoredLabel[] replacements =
            [
                .. page.Labels.Select(label => new StoredLabel(
                    label.Sequence,
                    new SignedLabelSnapshot(replacementKey.Sign(label.SignedLabel.Label))))
            ];
            await store.ReplaceSignaturesAsync(replacements, cancellationToken).ConfigureAwait(false);
            afterSequence = replacements[^1].Sequence;
        }

        return new(streamId, throughSequence, afterSequence);
    }
}
