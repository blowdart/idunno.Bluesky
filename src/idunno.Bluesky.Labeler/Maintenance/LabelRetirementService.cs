// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Signing;
using idunno.Bluesky.Labeler.Storage;

namespace idunno.Bluesky.Labeler.Maintenance;

/// <summary>Signs provider-selected retirement withdrawals without coupling execution to an administration transport.</summary>
internal sealed class LabelRetirementService(
    ILabelStore store,
    ILabelRetirementStore retirement,
    ILabelSigningKeyRing? keys)
{
    /// <summary>Gets the exact durable generation observed by the current bounded operation, if any.</summary>
    internal long? ObservedGeneration { get; private set; }

    /// <summary>Runs one bounded unit, always recovering durable observations before producing another signature.</summary>
    internal async Task<LabelServiceError?> AdvanceAsync(CancellationToken token)
    {
        ObservedGeneration = null;
        LabelServiceResult<LabelRetirementBatch> observed = await retirement.ReadRetirementBatchAsync(token).ConfigureAwait(false);
        if (!observed.Succeeded)
        {
            return observed.Error;
        }

        LabelRetirementBatch batch = observed.Result;
        ObservedGeneration = batch.Generation == 0 ? null : batch.Generation;
        if (batch.OperationId is null)
        {
            await retirement.AdvanceRetirementPurgeAsync(token).ConfigureAwait(false);
            return null;
        }

        List<SignedLabelSnapshot> signed = new(batch.Labels.Count);
        string? publicKey = null;
        try
        {
            if (!batch.Scanning && batch.Labels.Count != 0)
            {
                if (keys is null)
                {
                    throw new KeyNotFoundException("Retirement signing requires the selected private key.");
                }

                LabelSigningKey key = keys.GetSigningKey(keys.ActiveKeyId);
                publicKey = key.ExportPublicKeyMultibase();
                for (int i = 0; i < batch.Labels.Count; i++)
                {
                    Label prior = batch.Labels[i].SignedLabel.Label;
                    signed.Add(new(key.CreateNegation(
                        source: prior.Source,
                        uri: prior.Uri,
                        value: prior.Value,
                        creationTimestamp: batch.CreatedAt.AddTicks(i),
                        cid: prior.Cid)));
                }
            }
        }
        catch (Exception exception) when (exception is CryptographicException or KeyNotFoundException)
        {
            await retirement.PauseRetirementAsync(
                batch.Generation,
                "SigningFailed: restore the selected key, then explicitly resume retirement.",
                token).ConfigureAwait(false);
            return new(LabelServiceErrorCode.SigningFailed, "Retirement signing failed; restore the selected key and resume.", exception: exception);
        }

        LabelServiceResult<LabelRetirementView> committed = await retirement.CommitRetirementBatchAsync(
            batch: batch,
            negations: signed,
            signingPublicKey: publicKey,
            cancellationToken: token).ConfigureAwait(false);
        if (!committed.Succeeded)
        {
            return committed.Error;
        }

        await store.PublishAsync(limit: 250, cancellationToken: token).ConfigureAwait(false);
        return null;
    }
}
