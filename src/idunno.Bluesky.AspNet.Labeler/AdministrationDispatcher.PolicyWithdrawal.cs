// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Security.Cryptography;

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Moderation;
using idunno.Bluesky.Labeler.Storage;

namespace idunno.Bluesky.AspNet.Labeler;

internal sealed partial class AdministrationDispatcher
{
    /// <summary>Signs only a provider-selected bounded page and recovers ambiguous commits by rereading durable progress.</summary>
    internal async Task<LabelServiceError?> AdvancePolicyWithdrawalAsync(CancellationToken token)
    {
        if (store is not ILabelPolicyWithdrawalStore withdrawals)
        {
            return null;
        }
        LabelServiceResult<LabelPolicyWithdrawalBatch> observed = await withdrawals.ReadPolicyWithdrawalBatchAsync(token).ConfigureAwait(false);
        if (!observed.Succeeded)
        {
            return observed.Error;
        }
        LabelPolicyWithdrawalBatch batch = observed.Result;
        if (batch.Policy is null)
        {
            return null;
        }
        using IDisposable? admission = host?.TryAdmitAdministrativeIssuance();
        if (host is not null && admission is null)
        {
            return new(LabelServiceErrorCode.IssuanceClosed, "Publication cleanup is paused during coordinated maintenance.");
        }
        List<SignedLabelSnapshot> negations = new(batch.Labels.Count);
        string? publicKey = null;
        try
        {
            if (batch.Labels.Count != 0)
            {
                LabelSigningKey key = SigningKeys.GetSigningKey(SigningKeys.ActiveKeyId);
                publicKey = key.ExportPublicKeyMultibase();
                for (int i = 0; i < batch.Labels.Count; i++)
                {
                    Label prior = batch.Labels[i].SignedLabel.Label;
                    negations.Add(new(key.CreateNegation(
                        source: batch.Policy.Issuer,
                        uri: prior.Uri,
                        value: prior.Value,
                        creationTimestamp: batch.CreatedAt.AddTicks(i),
                        cid: prior.Cid)));
                }
            }
        }
        catch (Exception exception) when (exception is CryptographicException or KeyNotFoundException)
        {
            return new(LabelServiceErrorCode.SigningFailed, "Publication cleanup could not sign its next batch.", exception: exception);
        }
        LabelServiceResult<LabelPolicyState> committed = await withdrawals.CommitPolicyWithdrawalBatchAsync(
            batch: batch,
            negations: negations,
            signingPublicKey: publicKey,
            cancellationToken: token).ConfigureAwait(false);
        if (!committed.Succeeded)
        {
            return committed.Error;
        }
        try
        {
            await store.PublishAsync(limit: 250, cancellationToken: token).ConfigureAwait(false);
        }
        catch (DbException exception)
        {
            return new(LabelServiceErrorCode.StorageUnavailable,
                "Publication cleanup could not confirm stream publication; its durable checkpoint will be reread before retrying.", exception: exception);
        }
        return null;
    }
}
