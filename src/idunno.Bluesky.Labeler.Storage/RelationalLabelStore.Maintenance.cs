// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Runtime.CompilerServices;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Signing;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    private async Task RequireMaintenancePermissionAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelStoreState state,
        LabelAdministrationCaller? caller,
        CancellationToken token)
    {
        if (caller is null)
        {
            return;
        }

        AdministrationState administration = await ReadAdministrationAsync(connection, transaction, token).ConfigureAwait(false);
        if (!await HasPermissionAsync(
            connection: connection,
            transaction: transaction,
            state: state,
            administration: administration,
            caller: caller,
            permission: LabelAdministrationPermission.Maintain,
            token: token).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Current maintenance authority is required.");
        }
    }

    public Task<LabelMaintenanceState> RegisterHostAsync(Guid instanceId, CancellationToken cancellationToken = default) =>
        RegisterHostWithSigningRoleAsync(instanceId, requiresSigningKey: true, cancellationToken);

    public Task<LabelMaintenanceState> RegisterHostWithSigningRoleAsync(
        Guid instanceId,
        bool requiresSigningKey,
        CancellationToken cancellationToken = default)
    {
        if (instanceId == Guid.Empty)
        {
            throw new ArgumentException("A process-unique host identity is required.", nameof(instanceId));
        }

        return RunAsync(
            operation: "register-host",
            action: async (connection, transaction, _) =>
            {
                if (requiresSigningKey &&
                    (await ReadRetirementDataAsync(connection, transaction, cancellationToken).ConfigureAwait(false)) is { KeyRemoval: not "None" })
                {
                    throw new InvalidOperationException("Signing hosts cannot register after signing-key removal has begun.");
                }

                long now = await DatabaseNowAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                string hostId = instanceId.ToString("N");
                object? existing = await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT stopped FROM label_hosts WHERE host_id=@host",
                    token: cancellationToken,
                    ("@host", hostId)).ConfigureAwait(false);
                if (existing is null)
                {
                    await ExecuteAsync(
                        connection: connection,
                        transaction: transaction,
                        sql: "INSERT INTO label_hosts(host_id, started_ms, last_seen_ms, stopped) VALUES(@host,@now,@now,0)",
                        token: cancellationToken,
                        ("@host", hostId),
                        ("@now", now)).ConfigureAwait(false);
                    await ExecuteAsync(
                        connection: connection,
                        transaction: transaction,
                        sql: "INSERT INTO label_host_roles(host_id, requires_signing_key) VALUES(@host,@signing)",
                        token: cancellationToken,
                        ("@host", hostId),
                        ("@signing", requiresSigningKey ? 1 : 0)).ConfigureAwait(false);
                }
                else if (Integer(existing) != 0)
                {
                    throw new InvalidOperationException("A stopped host identity cannot be registered again.");
                }
                else
                {
                    object? signing = await ScalarAsync(
                        connection: connection,
                        transaction: transaction,
                        sql: "SELECT requires_signing_key FROM label_host_roles WHERE host_id=@host",
                        token: cancellationToken,
                        ("@host", hostId)).ConfigureAwait(false);
                    if (Integer(signing) != (requiresSigningKey ? 1 : 0))
                    {
                        throw new InvalidOperationException("A registered host's signing role cannot change.");
                    }

                    await ExecuteAsync(
                        connection: connection,
                        transaction: transaction,
                        sql: "UPDATE label_hosts SET last_seen_ms=@now WHERE host_id=@host",
                        token: cancellationToken,
                        ("@host", hostId),
                        ("@now", now)).ConfigureAwait(false);
                }

                LabelMaintenanceState state = await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (state.IsActive &&
                    await ScalarAsync(
                        connection: connection,
                        transaction: transaction,
                        sql: "SELECT acknowledged FROM label_maintenance_hosts WHERE operation_id=@operation AND host_id=@host",
                        token: cancellationToken,
                        ("@operation", state.OperationId),
                        ("@host", hostId)).ConfigureAwait(false) is null)
                {
                    await ExecuteAsync(
                        connection: connection,
                        transaction: transaction,
                        sql: "INSERT INTO label_maintenance_hosts(operation_id, host_id, acknowledged) VALUES(@operation,@host,0)",
                        token: cancellationToken,
                        ("@operation", state.OperationId),
                        ("@host", hostId)).ConfigureAwait(false);
                }

                return await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            },
            token: cancellationToken);
    }

    public Task<LabelMaintenanceState> HeartbeatHostAsync(Guid instanceId, CancellationToken cancellationToken = default) =>
        RunAsync(
            operation: "heartbeat-host",
            action: async (connection, transaction, _) =>
            {
                long now = await DatabaseNowAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                int updated = await ExecuteCountAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "UPDATE label_hosts SET last_seen_ms=CASE WHEN last_seen_ms<@now THEN @now ELSE last_seen_ms+1 END WHERE host_id=@host AND stopped=0",
                    token: cancellationToken,
                    ("@host", instanceId.ToString("N")),
                    ("@now", now)).ConfigureAwait(false);
                if (updated != 1)
                {
                    throw new InvalidOperationException("The host is not registered or has already stopped.");
                }

                return await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            },
            token: cancellationToken);

    public Task<LabelMaintenanceState> StopHostAsync(Guid instanceId, CancellationToken cancellationToken = default) =>
        RunAsync(
            operation: "stop-host",
            action: async (connection, transaction, _) =>
            {
                int updated = await ExecuteCountAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "UPDATE label_hosts SET stopped=1 WHERE host_id=@host AND stopped=0",
                    token: cancellationToken,
                    ("@host", instanceId.ToString("N"))).ConfigureAwait(false);
                if (updated != 1)
                {
                    throw new InvalidOperationException("The host is not registered or has already stopped.");
                }

                LabelMaintenanceState state = await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (state.IsActive)
                {
                    await ExecuteAsync(
                        connection: connection,
                        transaction: transaction,
                        sql: "UPDATE label_maintenance_hosts SET acknowledged=1 WHERE operation_id=@operation AND host_id=@host",
                        token: cancellationToken,
                        ("@operation", state.OperationId),
                        ("@host", instanceId.ToString("N"))).ConfigureAwait(false);
                }

                return await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            },
            token: cancellationToken);

    public Task<LabelMaintenanceState> GetMaintenanceStateAsync(CancellationToken cancellationToken = default) =>
        RunAsync(
            operation: "maintenance-state",
            action: (connection, transaction, _) => ReadMaintenanceAsync(connection, transaction, cancellationToken),
            token: cancellationToken);

    public Task<LabelMaintenanceState> BeginRotationAsync(
        string operationId,
        string previousKeyId,
        string replacementKeyId,
        string replacementPublicKey,
        LabelAdministrationCaller? caller = null,
        CancellationToken cancellationToken = default)
    {
        OperationId(operationId);
        KeyIdentity(previousKeyId, nameof(previousKeyId));
        KeyIdentity(replacementKeyId, nameof(replacementKeyId));
        PublicKey(replacementPublicKey, nameof(replacementPublicKey));
        if (string.Equals(previousKeyId, replacementKeyId, StringComparison.Ordinal))
        {
            throw new ArgumentException("The replacement key must differ from the active key.", nameof(replacementKeyId));
        }

        return RunAsync(
            operation: "begin-rotation",
            action: async (connection, transaction, state) =>
            {
                await RequireMaintenancePermissionAsync(connection, transaction, state, caller, cancellationToken).ConfigureAwait(false);
                LabelMaintenanceState current = await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (string.Equals(current.OperationId, operationId, StringComparison.Ordinal))
                {
                    if (current.PreviousKeyId != previousKeyId ||
                        current.ReplacementKeyId != replacementKeyId ||
                        current.ReplacementPublicKey != replacementPublicKey)
                    {
                        throw new InvalidOperationException("The rotation identity already has different key input.");
                    }

                    return current;
                }

                if (current.IsActive || state.Phase is LabelerLifecyclePhase.Purging or LabelerLifecyclePhase.Purged)
                {
                    throw new InvalidOperationException("Another maintenance operation is active or the labeler lifecycle is closed.");
                }

                if ((await ReadRetirementDataAsync(connection, transaction, cancellationToken).ConfigureAwait(false)) is { KeyRemoval: not "None" })
                {
                    throw new InvalidOperationException("Signing-key removal has begun; recovery rotation is closed.");
                }

                if ((await ReadPolicyGateAsync(connection, transaction, cancellationToken).ConfigureAwait(false))?.Phase is
                    Moderation.LabelPolicyPhase.Publishing or Moderation.LabelPolicyPhase.Withdrawing)
                {
                    throw new InvalidOperationException("Recover the pending policy publication before beginning rotation.");
                }

                LabelSigningKeySelection? selection = await ReadSigningSelectionAsync(connection, transaction, state, cancellationToken).ConfigureAwait(false);
                if (selection is not null && selection.KeyId != previousKeyId)
                {
                    throw new InvalidOperationException("The requested previous key is not the persisted active selection.");
                }

                object? activeHostCount = await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT COUNT(*) FROM label_hosts WHERE stopped=0",
                    token: cancellationToken).ConfigureAwait(false);
                if (activeHostCount is null || Integer(activeHostCount) == 0)
                {
                    throw new InvalidOperationException("At least one registered host is required to coordinate maintenance.");
                }

                await InvalidateRetirementAvailabilityAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                await ExecuteAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: """
                    UPDATE label_maintenance SET operation_id=@operation, phase=@phase, stream_id=@stream,
                        previous_key_id=@previous, replacement_key_id=@replacement, replacement_public_key=@public,
                        through_seq=0, checkpoint=0, key_verified=0 WHERE id=1
                    """,
                    token: cancellationToken,
                    ("@operation", operationId),
                    ("@phase", (int)LabelMaintenancePhase.Draining),
                    ("@stream", state.StreamId.ToString("N")),
                    ("@previous", previousKeyId),
                    ("@replacement", replacementKeyId),
                    ("@public", replacementPublicKey)).ConfigureAwait(false);
                await ExecuteAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: """
                    INSERT INTO label_maintenance_hosts(operation_id, host_id, acknowledged)
                    SELECT @operation, host_id, 0 FROM label_hosts WHERE stopped=0
                    """,
                    token: cancellationToken,
                    ("@operation", operationId)).ConfigureAwait(false);
                return await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            },
            token: cancellationToken);
    }

    public Task<LabelMaintenanceState> AcknowledgeHostDrainedAsync(
        string operationId,
        Guid instanceId,
        CancellationToken cancellationToken = default) =>
        AcknowledgeHostAsync(operationId, instanceId, confirmedStopped: false, cancellationToken);

    public Task<LabelMaintenanceState> AcknowledgeHostKeyActivatedAsync(
        string operationId,
        Guid instanceId,
        string keyId,
        CancellationToken cancellationToken = default)
    {
        OperationId(operationId);
        KeyIdentity(keyId, nameof(keyId));
        return RunAsync(
            operation: "acknowledge-host-key-activated",
            action: async (connection, transaction, _) =>
            {
                LabelMaintenanceState state = await RequireRotationAsync(connection, transaction, operationId, cancellationToken).ConfigureAwait(false);
                if (state.Phase != LabelMaintenancePhase.ReadyToReopen ||
                    state.ReplacementKeyId != keyId ||
                    !state.AllHostsDrained ||
                    state.Hosts.FirstOrDefault(host => host.InstanceId == instanceId) is not { RequiresSigningKey: true })
                {
                    throw new InvalidOperationException("The host can activate only the verified replacement key after the full stream is re-signed.");
                }

                string hostId = instanceId.ToString("N");
                object? active = await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT stopped FROM label_hosts WHERE host_id=@host",
                    token: cancellationToken,
                    ("@host", hostId)).ConfigureAwait(false);
                if (active is null || Integer(active) != 0)
                {
                    throw new InvalidOperationException("A stopped or unregistered host cannot acknowledge key activation.");
                }

                int updated = await ExecuteCountAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "UPDATE label_maintenance_hosts SET key_activated=1 WHERE operation_id=@operation AND host_id=@host AND acknowledged=1",
                    token: cancellationToken,
                    ("@operation", operationId),
                    ("@host", hostId)).ConfigureAwait(false);
                if (updated != 1)
                {
                    throw new InvalidOperationException("The host has not acknowledged this rotation's drain.");
                }

                return await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            },
            token: cancellationToken);
    }

    public Task<LabelMaintenanceState> ConfirmHostStoppedAsync(
        string operationId,
        Guid instanceId,
        LabelAdministrationCaller? caller = null,
        CancellationToken cancellationToken = default) =>
        AcknowledgeHostAsync(operationId, instanceId, confirmedStopped: true, cancellationToken, caller);

    private Task<LabelMaintenanceState> AcknowledgeHostAsync(
        string operationId,
        Guid instanceId,
        bool confirmedStopped,
        CancellationToken cancellationToken,
        LabelAdministrationCaller? caller = null) =>
        RunAsync(
            operation: confirmedStopped ? "confirm-host-stopped" : "acknowledge-host-drained",
            action: async (connection, transaction, storeState) =>
            {
                if (confirmedStopped)
                {
                    await RequireMaintenancePermissionAsync(connection, transaction, storeState, caller, cancellationToken).ConfigureAwait(false);
                }

                LabelMaintenanceState state = await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (!state.IsActive || !string.Equals(state.OperationId, operationId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("The maintenance operation is not active.");
                }

                string hostId = instanceId.ToString("N");
                _ = await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT acknowledged FROM label_maintenance_hosts WHERE operation_id=@operation AND host_id=@host",
                    token: cancellationToken,
                    ("@operation", operationId),
                    ("@host", hostId)).ConfigureAwait(false) ?? throw new InvalidOperationException("The host is not a participant in this maintenance operation.");
                if (confirmedStopped)
                {
                    await ExecuteAsync(
                        connection: connection,
                        transaction: transaction,
                        sql: "UPDATE label_hosts SET stopped=1 WHERE host_id=@host",
                        token: cancellationToken,
                        ("@host", hostId)).ConfigureAwait(false);
                }

                await ExecuteAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "UPDATE label_maintenance_hosts SET acknowledged=1 WHERE operation_id=@operation AND host_id=@host",
                    token: cancellationToken,
                    ("@operation", operationId),
                    ("@host", hostId)).ConfigureAwait(false);
                return await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            },
            token: cancellationToken);

    public Task<LabelMaintenanceState> CaptureRotationBoundaryAsync(
        string operationId,
        LabelAdministrationCaller? caller = null,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            operation: "capture-rotation-boundary",
            action: async (connection, transaction, state) =>
            {
                await RequireMaintenancePermissionAsync(connection, transaction, state, caller, cancellationToken).ConfigureAwait(false);
                LabelMaintenanceState current = await RequireRotationAsync(connection, transaction, operationId, cancellationToken).ConfigureAwait(false);
                if (current.Phase != LabelMaintenancePhase.Draining)
                {
                    return current;
                }

                if (!current.AllHostsDrained ||
                    state.Phase is LabelerLifecyclePhase.Purging or LabelerLifecyclePhase.Purged ||
                    state.PublishedThrough != state.IssuedThrough ||
                    (await ReadRetirementDataAsync(connection, transaction, cancellationToken).ConfigureAwait(false)) is { KeyRemoval: not "None" })
                {
                    throw new InvalidOperationException("Every host must drain and all issued labels must be published before fixing the rotation boundary.");
                }

                await ExecuteAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "UPDATE label_maintenance SET phase=@phase, stream_id=@stream, through_seq=@through, checkpoint=0 WHERE id=1",
                    token: cancellationToken,
                    ("@phase", (int)LabelMaintenancePhase.AwaitingKeyPublication),
                    ("@stream", state.StreamId.ToString("N")),
                    ("@through", state.PublishedThrough)).ConfigureAwait(false);
                return await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            },
            token: cancellationToken);

    public Task<LabelMaintenanceState> VerifyRotationKeyPublicationAsync(
        string operationId,
        string replacementPublicKey,
        LabelAdministrationCaller? caller = null,
        CancellationToken cancellationToken = default)
    {
        PublicKey(replacementPublicKey, nameof(replacementPublicKey));
        return RunAsync(
            operation: "verify-rotation-key",
            action: async (connection, transaction, state) =>
            {
                await RequireMaintenancePermissionAsync(connection, transaction, state, caller, cancellationToken).ConfigureAwait(false);
                LabelMaintenanceState current = await RequireRotationAsync(connection, transaction, operationId, cancellationToken).ConfigureAwait(false);
                if (current.KeyPublicationVerified)
                {
                    if (current.ReplacementPublicKey != replacementPublicKey)
                    {
                        throw new InvalidOperationException("The verified rotation key cannot be changed.");
                    }

                    return current;
                }

                if (current.Phase != LabelMaintenancePhase.AwaitingKeyPublication ||
                    !current.AllHostsDrained ||
                    current.ReplacementPublicKey != replacementPublicKey)
                {
                    throw new InvalidOperationException("The published key does not match the fixed rotation key.");
                }

                LabelMaintenancePhase nextPhase = current.ThroughSequence == 0
                    ? LabelMaintenancePhase.ReadyToReopen
                    : LabelMaintenancePhase.Resigning;
                await ExecuteAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "UPDATE label_maintenance SET phase=@phase, key_verified=1 WHERE id=1",
                    token: cancellationToken,
                    ("@phase", (int)nextPhase)).ConfigureAwait(false);
                return await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            },
            token: cancellationToken);
    }

    public Task<LabelMaintenanceState> ReplaceRotationBatchAsync(
        string operationId,
        long expectedAfterSequence,
        IReadOnlyList<StoredLabel> replacementLabels,
        long checkpoint,
        LabelAdministrationCaller? caller = null,
        CancellationToken cancellationToken = default)
    {
        OperationId(operationId);
        ArgumentNullException.ThrowIfNull(replacementLabels);
        StoredLabel[] batch = [.. replacementLabels];
        ArgumentOutOfRangeException.ThrowIfGreaterThan(batch.Length, 1000);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedAfterSequence);
        ArgumentOutOfRangeException.ThrowIfNegative(checkpoint);

        long previousSequence = expectedAfterSequence;
        foreach (StoredLabel label in batch)
        {
            ArgumentNullException.ThrowIfNull(label);
            ArgumentNullException.ThrowIfNull(label.SignedLabel);
            if (label.Sequence <= previousSequence)
            {
                throw new ArgumentException("Replacement labels must have strictly increasing sequence positions after the checkpoint.", nameof(replacementLabels));
            }

            previousSequence = label.Sequence;
        }

        if (checkpoint < previousSequence || (batch.Length == 0 && checkpoint == expectedAfterSequence))
        {
            throw new ArgumentException("The rotation checkpoint must advance through the complete replacement batch.", nameof(checkpoint));
        }

        return RunAsync(
            operation: "replace-rotation-batch",
            action: async (connection, transaction, state) =>
            {
                await RequireMaintenancePermissionAsync(connection, transaction, state, caller, cancellationToken).ConfigureAwait(false);
                LabelMaintenanceState current = await RequireRotationAsync(connection, transaction, operationId, cancellationToken).ConfigureAwait(false);
                if (current.Phase != LabelMaintenancePhase.Resigning ||
                    !current.KeyPublicationVerified ||
                    !current.AllHostsDrained ||
                    current.StreamId != state.StreamId ||
                    current.LastSequence != expectedAfterSequence ||
                    checkpoint > current.ThroughSequence)
                {
                    throw new InvalidOperationException("The rotation checkpoint, stream, or phase changed.");
                }

                object? nextSequence = await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT MIN(seq) FROM label_entries WHERE seq>@checkpoint AND seq<=@through",
                    token: cancellationToken,
                    ("@checkpoint", expectedAfterSequence),
                    ("@through", current.ThroughSequence)).ConfigureAwait(false);
                object? countValue = await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT COUNT(*) FROM label_entries WHERE seq>@checkpoint AND seq<=@through",
                    token: cancellationToken,
                    ("@checkpoint", expectedAfterSequence),
                    ("@through", checkpoint)).ConfigureAwait(false);
                if ((batch.Length == 0 && nextSequence is not null &&
                        Integer(nextSequence) <= checkpoint) ||
                    (batch.Length > 0 &&
                        (nextSequence is null || Integer(nextSequence) != batch[0].Sequence ||
                            Integer(countValue) != batch.Length)))
                {
                    throw new InvalidOperationException("The replacement batch must cover the next contiguous retained history page.");
                }

                LabelSignatureVerifier.Verify(
                    issuer: new Did(state.Issuer),
                    publicKeyMultibase: current.ReplacementPublicKey!,
                    labels: batch.Select(label => label.SignedLabel.Label));
                foreach (StoredLabel label in batch)
                {
                    if (!string.Equals(label.SignedLabel.Label.Source.Value, state.Issuer, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("A replacement label has the wrong issuer.");
                    }

                    object? storedPayload = await ScalarAsync(
                        connection: connection,
                        transaction: transaction,
                        sql: "SELECT payload FROM label_entries WHERE seq=@seq AND seq<=@through",
                        token: cancellationToken,
                        ("@seq", label.Sequence),
                        ("@through", current.ThroughSequence)).ConfigureAwait(false);
                    if (storedPayload is not byte[] payload)
                    {
                        throw new InvalidOperationException("A replacement sequence is missing from the fixed rotation history.");
                    }

                    if (!payload.AsSpan().SequenceEqual(label.SignedLabel.GetCanonicalUnsignedBytes()))
                    {
                        throw new InvalidDataException("A replacement changes signed label fields.");
                    }

                    await ExecuteAsync(
                        connection: connection,
                        transaction: transaction,
                        sql: "UPDATE label_entries SET signed_json=@json WHERE seq=@seq",
                        token: cancellationToken,
                        ("@json", label.SignedLabel.GetJsonBytes()),
                        ("@seq", label.Sequence)).ConfigureAwait(false);
                }

                LabelMaintenancePhase nextPhase = checkpoint == current.ThroughSequence
                    ? LabelMaintenancePhase.ReadyToReopen
                    : LabelMaintenancePhase.Resigning;
                LabelStoreState next = state with { Revision = checked(state.Revision + 1) };
                await SaveAsync(connection, transaction, next, cancellationToken).ConfigureAwait(false);
                await ExecuteAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "UPDATE label_maintenance SET phase=@phase, checkpoint=@checkpoint WHERE id=1",
                    token: cancellationToken,
                    ("@phase", (int)nextPhase),
                    ("@checkpoint", checkpoint)).ConfigureAwait(false);
                return await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            },
            token: cancellationToken);
    }

    public Task<LabelMaintenanceState> CompleteRotationAsync(
        string operationId,
        string activeKeyId,
        LabelAdministrationCaller? caller = null,
        CancellationToken cancellationToken = default)
    {
        OperationId(operationId);
        KeyIdentity(activeKeyId, nameof(activeKeyId));
        return RunAsync(
            operation: "complete-rotation",
            action: async (connection, transaction, state) =>
            {
                await RequireMaintenancePermissionAsync(connection, transaction, state, caller, cancellationToken).ConfigureAwait(false);
                LabelMaintenanceState current = await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(current.OperationId, operationId, StringComparison.Ordinal) ||
                    current.Phase == LabelMaintenancePhase.None)
                {
                    throw new InvalidOperationException("The maintenance operation is not active.");
                }

                if (current.Phase == LabelMaintenancePhase.Completed)
                {
                    if (current.ReplacementKeyId != activeKeyId)
                    {
                        throw new InvalidOperationException("The completed rotation used a different active key.");
                    }

                    return current;
                }

                if (current.Phase != LabelMaintenancePhase.ReadyToReopen ||
                    !current.KeyPublicationVerified ||
                    !current.AllHostsDrained ||
                    current.Hosts.Any(host => !host.Stopped && host.RequiresSigningKey && !host.KeyActivated) ||
                    current.LastSequence != current.ThroughSequence ||
                    current.ReplacementKeyId != activeKeyId)
                {
                    throw new InvalidOperationException("The replacement key is not verified, active and fully applied.");
                }

                LabelSigningKeySelection? selection = await ReadSigningSelectionAsync(connection, transaction, state, cancellationToken).ConfigureAwait(false);
                if (selection is not null &&
                    (selection.KeyId != activeKeyId || selection.PublicKey != current.ReplacementPublicKey))
                {
                    throw new InvalidOperationException("The replacement selection has not been persisted.");
                }

                await ExecuteAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "UPDATE label_maintenance SET phase=@phase WHERE id=1",
                    token: cancellationToken,
                    ("@phase", (int)LabelMaintenancePhase.Completed)).ConfigureAwait(false);
                return await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            },
            token: cancellationToken);
    }

    private async Task<LabelMaintenanceState> RequireRotationAsync(
        DbConnection connection,
        DbTransaction transaction,
        string operationId,
        CancellationToken token)
    {
        LabelMaintenanceState state = await ReadMaintenanceAsync(connection, transaction, token).ConfigureAwait(false);
        if (!string.Equals(state.OperationId, operationId, StringComparison.Ordinal) ||
            state.Phase is LabelMaintenancePhase.None or LabelMaintenancePhase.Completed)
        {
            throw new InvalidOperationException("The maintenance operation is not active.");
        }

        return state;
    }

    private async Task<LabelMaintenanceState> ReadMaintenanceAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken token)
    {
        DbCommand command = Command(
            connection: connection,
            transaction: transaction,
            sql: "SELECT operation_id, phase, stream_id, previous_key_id, replacement_key_id, replacement_public_key, through_seq, checkpoint, key_verified FROM label_maintenance WHERE id=1" + LockSuffix);
        string? operationId;
        LabelMaintenancePhase phase;
        Guid streamId;
        string? previousKeyId;
        string? replacementKeyId;
        string? replacementPublicKey;
        long through;
        long checkpoint;
        bool keyVerified;
        await using (command.ConfigureAwait(false))
        {
            DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                if (!await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    throw new InvalidDataException("The durable maintenance record is missing.");
                }

                operationId = await reader.IsDBNullAsync(0, token).ConfigureAwait(false) ? null : reader.GetString(0);
                phase = (LabelMaintenancePhase)reader.GetInt32(1);
                streamId = Guid.ParseExact(reader.GetString(2), "N");
                previousKeyId = await reader.IsDBNullAsync(3, token).ConfigureAwait(false) ? null : reader.GetString(3);
                replacementKeyId = await reader.IsDBNullAsync(4, token).ConfigureAwait(false) ? null : reader.GetString(4);
                replacementPublicKey = await reader.IsDBNullAsync(5, token).ConfigureAwait(false) ? null : reader.GetString(5);
                through = reader.GetInt64(6);
                checkpoint = reader.GetInt64(7);
                keyVerified = reader.GetInt32(8) != 0;
            }
        }

        List<LabelMaintenanceHost> hosts = [];
        if (operationId is not null)
        {
            DbCommand hostCommand = Command(
                connection: connection,
                transaction: transaction,
                sql: """
                SELECT p.host_id, p.acknowledged, p.key_activated, h.stopped, h.last_seen_ms, r.requires_signing_key
                FROM label_maintenance_hosts p
                JOIN label_hosts h ON h.host_id=p.host_id
                LEFT JOIN label_host_roles r ON r.host_id=h.host_id
                WHERE p.operation_id=@operation
                ORDER BY p.host_id
                """,
                ("@operation", operationId));
            await using (hostCommand.ConfigureAwait(false))
            {
                DbDataReader hostReader = await hostCommand.ExecuteReaderAsync(token).ConfigureAwait(false);
                await using (hostReader.ConfigureAwait(false))
                {
                    while (await hostReader.ReadAsync(token).ConfigureAwait(false))
                    {
                        if (await hostReader.IsDBNullAsync(5, token).ConfigureAwait(false))
                        {
                            throw new InvalidDataException("A maintenance participant has no persisted signing role.");
                        }

                        hosts.Add(new(
                            InstanceId: Guid.ParseExact(hostReader.GetString(0), "N"),
                            Acknowledged: hostReader.GetInt32(1) != 0,
                            KeyActivated: hostReader.GetInt32(2) != 0,
                            Stopped: hostReader.GetInt32(3) != 0,
                            LastSeen: DateTimeOffset.FromUnixTimeMilliseconds(hostReader.GetInt64(4)))
                        {
                            RequiresSigningKey = hostReader.GetInt32(5) != 0
                        });
                    }
                }
            }
        }

        return new(
            OperationId: operationId,
            Phase: phase,
            StreamId: streamId,
            PreviousKeyId: previousKeyId,
            ReplacementKeyId: replacementKeyId,
            ReplacementPublicKey: replacementPublicKey,
            ThroughSequence: through,
            LastSequence: checkpoint,
            KeyPublicationVerified: keyVerified,
            Hosts: hosts);
    }

    private async Task<long> DatabaseNowAsync(DbConnection connection, DbTransaction transaction, CancellationToken token) =>
        await DatabaseTimeAsync(connection, transaction, token).ConfigureAwait(false);

    private static async Task<int> ExecuteCountAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken token,
        params (string Name, object? Value)[] values)
    {
        DbCommand command = Command(connection, transaction, sql, values);
        await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
        return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    private static void KeyIdentity(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A key identity is required.", parameterName);
        }

        if (value.Length > 128 || value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')))
        {
            throw new ArgumentException("A key identity must contain at most 128 ASCII letters, digits, dots, underscores or hyphens.", parameterName);
        }
    }

    private static void PublicKey(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A public key value is required.", parameterName);
        }

        if (value.Length > 512 || value.Any(char.IsControl))
        {
            throw new ArgumentException("The public key value is invalid or too long.", parameterName);
        }
    }
}
