// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Moderation;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.Bluesky.AspNet.Labeler;

internal sealed partial class AdministrationDispatcher
{
    /// <summary>Loads policy choices and issuer-local label state without requiring a report or a remotely available record.</summary>
    private async Task<AdministrationResponse> ReadManualLabelChoicesAsync(
        LabelAdministrationCaller caller,
        AdministrationCommand command,
        ILabelPolicyStore policies,
        CancellationToken token)
    {
        LabelServiceResult<LabelPolicyState> gate = await policies.GetPolicyStateAsync(caller, token).ConfigureAwait(false);
        if (!gate.Succeeded)
        {
            return Failure(gate.Error);
        }

        if (gate.Result.Policy is not { } definition || store is not ILabelTableStore labels)
        {
            return new(false, "PolicyUnavailable", "The current label choices are unavailable.");
        }

        if (command.Target.Length == 0)
        {
            return new(true, Moderation: new(LabelChoices: [.. definition.Issuance.DeclaredValues
                .Order(StringComparer.Ordinal).Select(value => new ModerationLabelChoiceView(
                    Value: value,
                    Applied: false,
                    Sequence: 0,
                    OtherVersion: false))]));
        }

        if (command.Target.Length > 2048 || command.Cid is { Length: > 128 } ||
            command.Cid is not null && !Cid.TryParse(command.Cid, out _))
        {
            return new(false, "InvalidRequest", "Enter a valid target and optional record CID.");
        }

        LabelTarget target;
        if (command.Target.StartsWith("at://", StringComparison.Ordinal))
        {
            if (!AtUri.TryParse(command.Target, out AtUri? uri) || uri.Collection is null || uri.RecordKey is null)
            {
                return new(false, "InvalidRequest", "Enter a complete record AT URI.");
            }

            target = LabelTarget.Record(uri, command.Cid is { } cid ? new Cid(cid) : null);
        }
        else
        {
            if (!AtIdentifier.TryParse(command.Target, out AtIdentifier? account) || command.Cid is not null)
            {
                return new(false, "InvalidRequest", "Enter a valid account DID or handle, without a record CID.");
            }

            target = LabelTarget.Account(account);
        }

        if (target.Repository is not Did)
        {
            LabelServiceResult<LabelTarget> resolved = await new LabelTargetResolver(httpClient, NullLoggerFactory.Instance)
                .ResolveAsync(LabelTarget.Account(target.Repository), token).ConfigureAwait(false);
            if (!resolved.Succeeded)
            {
                return Failure(resolved.Error);
            }

            target = target.WithRepository((Did)resolved.Result.Repository);
        }

        LabelServiceResult<StoredLabel[]> applied = await labels.ReadTargetLabelsAsync(caller, target, token).ConfigureAwait(false);
        if (!applied.Succeeded)
        {
            return Failure(applied.Error);
        }

        List<ModerationLabelChoiceView> choices = [];
        foreach (string[] values in definition.Issuance.DeclaredValues
            .Concat(applied.Result.Select(label => label.SignedLabel.Label.Value))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Chunk(400))
        {
            IReadOnlyList<LabelServiceResult<LabelDecisionSnapshot>> snapshots = await store.ReadDecisionBatchAsync(
                [.. values.Select(value => (target, value))], token).ConfigureAwait(false);
            for (int index = 0; index < values.Length; index++)
            {
                LabelServiceResult<LabelDecisionSnapshot> snapshot = snapshots[index];
                if (!snapshot.Succeeded)
                {
                    return Failure(snapshot.Error);
                }

                StoredLabel? current = snapshot.Result.CurrentLabel;
                Label? label = current?.SignedLabel.Label;
                bool active = label is { IsNegationLabel: false } &&
                    (label.ExpiresAt is null || label.ExpiresAt > gate.Result.DatabaseTime);
                choices.Add(new(
                    Value: values[index],
                    Applied: active && (target.Cid is null || label!.Cid == target.Cid),
                    Sequence: current?.Sequence ?? 0,
                    OtherVersion: active && target.Cid is not null && label!.Cid != target.Cid));
            }
        }

        return new(true, Moderation: new(LabelChoices: [.. choices]), Target: target.Subject);
    }
}
