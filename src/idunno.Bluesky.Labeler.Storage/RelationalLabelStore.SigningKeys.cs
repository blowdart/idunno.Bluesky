// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Runtime.CompilerServices;

using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Signing;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    /// <inheritdoc/>
    public Task<LabelSigningKeySelection?> GetSigningKeySelectionAsync(CancellationToken cancellationToken = default) =>
        RunAsync(
            operation: "read-signing-selection",
            action: (connection, transaction, state) => ReadSigningSelectionAsync(connection, transaction, state, cancellationToken),
            token: cancellationToken);

    /// <inheritdoc/>
    public Task<LabelSigningKeySelection> InitializeSigningKeySelectionAsync(
        string keyId,
        string publicKey,
        CancellationToken cancellationToken = default)
    {
        ValidateSelection(keyId, publicKey);
        return RunAsync(
            operation: "initialize-signing-selection",
            action: async (connection, transaction, state) =>
            {
                LabelSigningKeySelection? current = await ReadSigningSelectionAsync(connection, transaction, state, cancellationToken).ConfigureAwait(false);
                if (current is not null)
                {
                    return current;
                }

                LabelMaintenanceState maintenance = await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                string? expected = maintenance.Phase == LabelMaintenancePhase.Completed ? maintenance.ReplacementKeyId : maintenance.PreviousKeyId;
                if ((expected is not null && keyId != expected) ||
                    (maintenance.Phase == LabelMaintenancePhase.Completed && publicKey != maintenance.ReplacementPublicKey))
                {
                    throw new InvalidOperationException("The initial key does not match persisted rotation state.");
                }

                await ExecuteAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "INSERT INTO label_signing_selection(id, stream_id, issuer, key_id, public_key) VALUES(1,@stream,@issuer,@key,@public)",
                    token: cancellationToken,
                    ("@stream", state.StreamId.ToString("N")),
                    ("@issuer", state.Issuer),
                    ("@key", keyId),
                    ("@public", publicKey)).ConfigureAwait(false);
                return new LabelSigningKeySelection(
                    streamId: state.StreamId,
                    issuer: state.Issuer,
                    keyId: keyId,
                    publicKey: publicKey);
            },
            token: cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LabelSigningKeySelection> ActivateSigningKeySelectionAsync(
        string operationId,
        string previousKeyId,
        string keyId,
        string publicKey,
        CancellationToken cancellationToken = default)
    {
        OperationId(operationId);
        KeyIdentity(previousKeyId, nameof(previousKeyId));
        ValidateSelection(keyId, publicKey);
        return RunAsync(
            operation: "activate-signing-selection",
            action: async (connection, transaction, state) =>
            {
                LabelMaintenanceState maintenance = await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (maintenance.OperationId != operationId ||
                    maintenance.StreamId != state.StreamId ||
                    maintenance.PreviousKeyId != previousKeyId ||
                    maintenance.ReplacementKeyId != keyId ||
                    maintenance.ReplacementPublicKey != publicKey ||
                    maintenance.Phase is not (LabelMaintenancePhase.ReadyToReopen or LabelMaintenancePhase.Completed) ||
                    !maintenance.KeyPublicationVerified ||
                    !maintenance.AllHostsDrained ||
                    maintenance.LastSequence != maintenance.ThroughSequence)
                {
                    throw new InvalidOperationException("The persisted rotation does not permit signing-key activation.");
                }

                LabelSigningKeySelection current = await ReadSigningSelectionAsync(connection, transaction, state, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Initialize the signing selection before activation.");
                if (current.KeyId == keyId && current.PublicKey == publicKey)
                {
                    return current;
                }

                if (current.KeyId != previousKeyId)
                {
                    throw new InvalidOperationException("The active signing selection changed.");
                }

                await ExecuteAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "UPDATE label_signing_selection SET key_id=@key, public_key=@public WHERE id=1",
                    token: cancellationToken,
                    ("@key", keyId),
                    ("@public", publicKey)).ConfigureAwait(false);
                return new LabelSigningKeySelection(
                    streamId: state.StreamId,
                    issuer: state.Issuer,
                    keyId: keyId,
                    publicKey: publicKey);
            },
            token: cancellationToken);
    }

    /// <summary>Reads and validates the selection while holding the provider-owned stream lock.</summary>
    private static async Task<LabelSigningKeySelection?> ReadSigningSelectionAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelStoreState state,
        CancellationToken token)
    {
        using DbCommand command = Command(connection, transaction, "SELECT stream_id, issuer, key_id, public_key FROM label_signing_selection WHERE id=1");
        DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false))
        {
            return null;
        }

        Guid stream = Guid.ParseExact(reader.GetString(0), "N");
        string issuer = reader.GetString(1);
        if (stream != state.StreamId || issuer != state.Issuer)
        {
            throw new InvalidDataException("The signing selection does not belong to this issuer and stream.");
        }

        return new LabelSigningKeySelection(
            streamId: stream,
            issuer: issuer,
            keyId: reader.GetString(2),
            publicKey: reader.GetString(3));
    }

    /// <summary>Validates public selection input without accepting private key material.</summary>
    private static void ValidateSelection(string keyId, string publicKey)
    {
        KeyIdentity(keyId, nameof(keyId));
        PublicKey(publicKey, nameof(publicKey));
        LabelSignatureVerifier.ValidatePublicKey(publicKey);
    }
}
