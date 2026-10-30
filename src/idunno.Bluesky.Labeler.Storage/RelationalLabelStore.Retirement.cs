// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore : ILabelRetirementStore
{
    /// <summary>Reads issuer-bound workflow metadata under the stream lock.</summary>
    private static async Task<RetirementData?> ReadRetirementDataAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken token)
    {
        object? payload = await ScalarAsync(
            connection: connection,
            transaction: transaction,
            sql: "SELECT payload FROM label_retirement WHERE id=1",
            token: token).ConfigureAwait(false);
        return payload is byte[] bytes
            ? JsonSerializer.Deserialize(bytes, RetirementJsonContext.Default.RetirementData)
                ?? throw new InvalidDataException("The retirement state is null.")
            : null;
    }

    /// <summary>Replaces the singleton workflow without changing signed history or sequence identity.</summary>
    private static async Task SaveRetirementDataAsync(
        DbConnection connection,
        DbTransaction transaction,
        RetirementData data,
        CancellationToken token)
    {
        await ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "DELETE FROM label_retirement WHERE id=1",
            token: token).ConfigureAwait(false);
        await ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "INSERT INTO label_retirement(id,payload) VALUES(1,@payload)",
            token: token,
            ("@payload", JsonSerializer.SerializeToUtf8Bytes(data, RetirementJsonContext.Default.RetirementData))).ConfigureAwait(false);
    }

    /// <summary>Invalidates prior availability evidence when recovery maintenance interrupts delivery.</summary>
    private static async Task InvalidateRetirementAvailabilityAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken token)
    {
        RetirementData? data = await ReadRetirementDataAsync(connection, transaction, token).ConfigureAwait(false);
        if (data is not null)
        {
            await SaveRetirementDataAsync(
                connection: connection,
                transaction: transaction,
                data: data with { Generation = checked(data.Generation + 1), ControlId = Guid.NewGuid(), AvailabilityAttestation = null },
                token: token).ConfigureAwait(false);
        }
    }

    /// <summary>Projects storage checkpoints without disclosing retained credential evidence.</summary>
    private static LabelRetirementView RetirementView(LabelStoreState state, RetirementData? data) => new(
        OperationId: data?.OperationId,
        Generation: data?.Generation ?? 0,
        Phase: state.Phase.ToString(),
        Stage: data?.Stage ?? "NotStarted",
        Running: data?.Running ?? false,
        UpperBound: state.RetirementUpperBound,
        ScannedThrough: data?.ScannedThrough ?? 0,
        NegatedThrough: state.RetirementThrough,
        IssuedThrough: state.IssuedThrough,
        PublishedThrough: state.PublishedThrough,
        GraceDeadline: state.GraceDeadline,
        PauseReason: data?.PauseReason,
        LastActivityAt: data?.LastActivityAt,
        LastProgressAt: data?.LastProgressAt,
        KeyRemoval: data?.KeyRemoval ?? "None",
        Inventory: data?.Inventory,
        AvailabilityAttestation: data?.AvailabilityAttestation,
        SigningPublicKey: data?.SigningPublicKey,
        Issuer: state.Issuer);

    /// <summary>Encodes complete semantic command intent without its one-use confirmation token.</summary>
    private static string RetirementIntent(LabelAdministrationCaller caller, LabelRetirementCommand command) =>
        Convert.ToHexString(SHA256.HashData(Intent(
            caller.Issuer.Value,
            caller.Actor.Value,
            command.Action,
            command.OperationId,
            command.Reason,
            command.GracePeriodDays.ToString(CultureInfo.InvariantCulture),
            command.Attestation ?? string.Empty,
            command.Inventory ?? string.Empty,
            command.HostInstanceId?.ToString("N") ?? string.Empty)));

    /// <summary>Binds confirmation to derived signing evidence without making it part of caller retry identity.</summary>
    private static string RetirementConfirmationIntent(LabelAdministrationCaller caller, LabelRetirementCommand command) =>
        Convert.ToHexString(SHA256.HashData(Intent(RetirementIntent(caller, command), command.VerifiedSigningPublicKey ?? string.Empty)));

    /// <summary>Binds previews to lifecycle, signing selection, hosts and maintenance, not publisher polling revisions.</summary>
    private async Task<string> RetirementFingerprintAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelStoreState state,
        RetirementData? data,
        CancellationToken token)
    {
        LabelMaintenanceState maintenance = await ReadMaintenanceAsync(connection, transaction, token).ConfigureAwait(false);
        Signing.LabelSigningKeySelection? selection = await ReadSigningSelectionAsync(
            connection: connection,
            transaction: transaction,
            state: state,
            token: token).ConfigureAwait(false);
        DbCommand hostCommand = Command(
            connection: connection,
            transaction: transaction,
            sql: "SELECT h.host_id,r.requires_signing_key FROM label_hosts h JOIN label_host_roles r ON r.host_id=h.host_id WHERE h.stopped=0 ORDER BY h.host_id");
        StringBuilder registrations = new();
        await using (hostCommand.ConfigureAwait(false))
        {
            DbDataReader reader = await hostCommand.ExecuteReaderAsync(token).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    registrations.Append(reader.GetString(0)).Append(':').Append(reader.GetInt32(1)).Append(';');
                }
            }
        }

        return Convert.ToHexString(SHA256.HashData(Intent(
            state.StreamId.ToString("N"),
            state.Phase.ToString(),
            state.IssuedThrough.ToString(CultureInfo.InvariantCulture),
            (data?.Generation ?? 0).ToString(CultureInfo.InvariantCulture),
            state.GraceDeadline?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            maintenance.OperationId ?? string.Empty,
            maintenance.Phase.ToString(),
            selection?.PublicKey ?? string.Empty,
            registrations.ToString(),
            string.Join(",", maintenance.Hosts.OrderBy(host => host.InstanceId).Select(host => host.InstanceId.ToString("N") + ":" + host.Stopped)))));
    }

    /// <summary>Validates supported commands and bounds all retained human input.</summary>
    private static void ValidateRetirementCommand(LabelRetirementCommand command)
    {
        LabelInput.ValidateText(command.OperationId, nameof(command.OperationId), 256);
        LabelInput.ValidateText(command.Reason, nameof(command.Reason), 4096);
        if (command.Action is not ("beginRetirement" or "pauseRetirement" or "resumeRetirement" or
            "beginGracePeriod" or "extendGracePeriod" or "completeRetirement" or "purgeRetiredData" or
            "requestKeyRemoval" or "attestKeyRemoval" or "confirmRetirementHostStopped"))
        {
            throw new ArgumentException("The retirement action is unknown.", nameof(command));
        }

        if (command.Action is "beginGracePeriod" or "extendGracePeriod" && command.GracePeriodDays is < 1 or > 365)
        {
            throw new ArgumentException("Choose an explicit grace duration from 1 through 365 days.", nameof(command));
        }

        if (command.Action is "completeRetirement" or "attestKeyRemoval" or "confirmRetirementHostStopped")
        {
            LabelInput.ValidateText(command.Attestation!, nameof(command.Attestation), 4096);
        }

        if (command.Action is "requestKeyRemoval" or "attestKeyRemoval")
        {
            LabelInput.ValidateText(command.Inventory!, nameof(command.Inventory), 16384);
        }

        if (command.Confirmation is { Length: > 128 })
        {
            throw new ArgumentException("The confirmation exceeds its bound.", nameof(command));
        }

        if (command.Action == "confirmRetirementHostStopped" && (command.HostInstanceId is null || command.HostInstanceId == Guid.Empty))
        {
            throw new ArgumentException("Identify the exact registered host whose process was independently verified stopped.", nameof(command));
        }
    }

    /// <summary>Determines which transitions require a separate action-bound preflight confirmation.</summary>
    private static bool RequiresRetirementConfirmation(string action) =>
        action is "beginRetirement" or "purgeRetiredData" or "requestKeyRemoval" or "attestKeyRemoval" or "completeRetirement" or "confirmRetirementHostStopped";

    /// <summary>Records retirement administrative outcomes in the same transaction as their mutations.</summary>
    private Task<LabelAdministrationReceipt> AuditRetirementAsync(
        DbConnection connection,
        DbTransaction transaction,
        AdministrationState team,
        LabelAdministrationCaller caller,
        string operationId,
        string action,
        string reason,
        bool succeeded,
        CancellationToken token) =>
        AuditAdministrationAsync(
            connection: connection,
            transaction: transaction,
            administration: team,
            actor: caller.Actor,
            localIdentity: caller.LocalIdentity,
            operationId: operationId,
            action: action,
            reason: reason,
            succeeded: succeeded,
            token: token);

    /// <summary>Inspects progress and optionally issues a bounded, action-specific confirmation challenge.</summary>
    Task<LabelServiceResult<LabelRetirementView>> ILabelRetirementStore.InspectRetirementAsync(
        LabelAdministrationCaller caller,
        LabelRetirementCommand? preview,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (preview is not null)
        {
            ValidateRetirementCommand(preview);
        }

        return AdministrationAsync(async (connection, transaction, state) =>
        {
            AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            bool allowed = await HasPermissionAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                administration: team,
                caller: caller,
                permission: preview is null ? LabelAdministrationPermission.Inspect : LabelAdministrationPermission.Maintain,
                token: cancellationToken).ConfigureAwait(false);
            await AuditRetirementAsync(
                connection: connection,
                transaction: transaction,
                team: team,
                caller: caller,
                operationId: preview?.OperationId ?? string.Empty,
                action: "retirement-inspection",
                reason: preview?.Reason ?? "Inspect retirement progress.",
                succeeded: allowed,
                token: cancellationToken).ConfigureAwait(false);
            if (!allowed)
            {
                return AdminFailure<LabelRetirementView>("Current retirement authority is required.");
            }

            RetirementData? data = await ReadRetirementDataAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            LabelRetirementView view = RetirementView(state, data);
            Signing.LabelSigningKeySelection? selected = await ReadSigningSelectionAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                token: cancellationToken).ConfigureAwait(false);
            List<LabelRetirementHost> hosts = [];
            DbCommand hostCommand = Command(
                connection: connection,
                transaction: transaction,
                sql: "SELECT h.host_id,r.requires_signing_key,h.last_seen_ms FROM label_hosts h JOIN label_host_roles r ON r.host_id=h.host_id WHERE h.stopped=0 ORDER BY h.host_id LIMIT 1001");
            await using (hostCommand.ConfigureAwait(false))
            {
                DbDataReader reader = await hostCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        hosts.Add(new(Guid.Parse(reader.GetString(0)), reader.GetInt32(1) != 0, DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(2))));
                    }
                }
            }

            if (hosts.Count > 1000)
            {
                return CaseFailure<LabelRetirementView>(LabelServiceErrorCode.StorageUnavailable, "More than 1000 registered hosts require operator reconciliation before retirement.");
            }

            view = view with
            {
                Hosts = hosts.AsReadOnly(),
                SigningKeyId = selected?.KeyId,
                SigningPublicKey = selected?.PublicKey ?? preview?.VerifiedSigningPublicKey ?? data?.SigningPublicKey
            };
            LabelMaintenanceState maintenance = await ReadMaintenanceAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            ModerationPolicyGate? policy = await ReadPolicyGateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            string? blocker = policy?.Phase is LabelPolicyPhase.Publishing or LabelPolicyPhase.Withdrawing
                ? "Recover pending policy publication before retirement." : null;
            view = view with
            {
                Blocker = maintenance.IsActive ? "Recover the active rotation before retirement administration." : blocker
            };
            if (preview is null || view.Blocker is not null)
            {
                return LabelServiceResult<LabelRetirementView>.Success(view);
            }

            long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            string challenge = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            RetirementConfirmation confirmation = new(
                RetirementConfirmationIntent(caller, preview),
                await RetirementFingerprintAsync(
                    connection: connection,
                    transaction: transaction,
                    state: state,
                    data: data,
                    token: cancellationToken).ConfigureAwait(false),
                checked(now + 600_000));
            await ExecuteAsync(
                connection: connection,
                transaction: transaction,
                sql: "DELETE FROM label_retirement_confirmations WHERE token_hash IN (SELECT token_hash FROM (SELECT token_hash FROM label_retirement_confirmations WHERE expires_ms<=@now LIMIT 1000) AS expired_confirmations)",
                token: cancellationToken,
                ("@now", now)).ConfigureAwait(false);
            if (Integer(await ScalarAsync(
                connection: connection,
                transaction: transaction,
                sql: "SELECT COUNT(*) FROM label_retirement_confirmations",
                token: cancellationToken).ConfigureAwait(false)) >= 1000)
            {
                return CaseFailure<LabelRetirementView>(LabelServiceErrorCode.StorageUnavailable, "Confirmation capacity reached; allow outstanding confirmations to expire.");
            }

            await ExecuteAsync(
                connection: connection,
                transaction: transaction,
                sql: "INSERT INTO label_retirement_confirmations(token_hash,expires_ms,payload) VALUES(@hash,@expiry,@payload)",
                token: cancellationToken,
                ("@hash", SHA256.HashData(Encoding.UTF8.GetBytes(challenge))),
                ("@expiry", confirmation.ExpiresAt),
                ("@payload", JsonSerializer.SerializeToUtf8Bytes(confirmation, RetirementJsonContext.Default.RetirementConfirmation))).ConfigureAwait(false);
            return LabelServiceResult<LabelRetirementView>.Success(view with
            {
                Confirmation = challenge,
                ConfirmationExpiresAt = DateTimeOffset.FromUnixTimeMilliseconds(confirmation.ExpiresAt)
            });
        }, cancellationToken);
    }

    /// <summary>Recovers receipt identity independently of derived signing evidence and current lifecycle phase.</summary>
    private static async Task<LabelServiceResult<LabelRetirementRecovery>> RecoverRetirementReceiptAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelAdministrationCaller caller,
        LabelRetirementCommand command,
        CancellationToken token)
    {
        object? previous = await ScalarAsync(
            connection: connection,
            transaction: transaction,
            sql: "SELECT payload FROM label_retirement_receipts WHERE operation_hash=@hash",
            token: token,
            ("@hash", OperationHash(command.OperationId))).ConfigureAwait(false);
        if (previous is not byte[] receiptBytes)
        {
            return LabelServiceResult<LabelRetirementRecovery>.Success(new(null));
        }

        RetirementReceipt receipt = JsonSerializer.Deserialize(receiptBytes, RetirementJsonContext.Default.RetirementReceipt)
            ?? throw new InvalidDataException("The retirement receipt is null.");
        return receipt.OperationId == command.OperationId && receipt.Intent == RetirementIntent(caller, command)
            ? LabelServiceResult<LabelRetirementRecovery>.Success(new(receipt.View))
            : CaseFailure<LabelRetirementRecovery>(LabelServiceErrorCode.IdempotencyConflict, "The operation ID identifies different retirement intent.");
    }

    /// <summary>Checks current authority before returning an existing command outcome without executing new work.</summary>
    Task<LabelServiceResult<LabelRetirementRecovery>> ILabelRetirementStore.RecoverRetirementAsync(
        LabelAdministrationCaller caller,
        LabelRetirementCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ValidateRetirementCommand(command);
        return AdministrationAsync(async (connection, transaction, state) =>
        {
            AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            if (!await HasPermissionAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                administration: team,
                caller: caller,
                permission: LabelAdministrationPermission.Maintain,
                token: cancellationToken).ConfigureAwait(false))
            {
                await AuditRetirementAsync(
                    connection: connection, transaction: transaction, team: team, caller: caller,
                    operationId: command.OperationId, action: command.Action, reason: command.Reason,
                    succeeded: false, token: cancellationToken).ConfigureAwait(false);
                return AdminFailure<LabelRetirementRecovery>("Current retirement authority is required.");
            }

            return await RecoverRetirementReceiptAsync(
                connection: connection,
                transaction: transaction,
                caller: caller,
                command: command,
                token: cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    /// <summary>Commits only current, authorized immutable intent, recovering an earlier result before confirmation checks.</summary>
    Task<LabelServiceResult<LabelRetirementView>> ILabelRetirementStore.ExecuteRetirementAsync(
        LabelAdministrationCaller caller,
        LabelRetirementCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ValidateRetirementCommand(command);
        return AdministrationAsync(async (connection, transaction, state) =>
        {
            AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            if (!await HasPermissionAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                administration: team,
                caller: caller,
                permission: LabelAdministrationPermission.Maintain,
                token: cancellationToken).ConfigureAwait(false))
            {
                await AuditRetirementAsync(
                    connection: connection, transaction: transaction, team: team, caller: caller,
                    operationId: command.OperationId, action: command.Action, reason: command.Reason,
                    succeeded: false, token: cancellationToken).ConfigureAwait(false);
                return AdminFailure<LabelRetirementView>("Current retirement authority is required.");
            }

            string intent = RetirementIntent(caller, command);
            LabelServiceResult<LabelRetirementRecovery> recovered = await RecoverRetirementReceiptAsync(
                connection: connection,
                transaction: transaction,
                caller: caller,
                command: command,
                token: cancellationToken).ConfigureAwait(false);
            if (!recovered.Succeeded)
            {
                return LabelServiceResult<LabelRetirementView>.Failure(recovered.Error);
            }

            if (recovered.Result.Receipt is { } receipt)
            {
                return LabelServiceResult<LabelRetirementView>.Success(receipt);
            }

            RetirementData? data = await ReadRetirementDataAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            if (RequiresRetirementConfirmation(command.Action))
            {
                object? confirmationBytes = command.Confirmation is null ? null : await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT payload FROM label_retirement_confirmations WHERE token_hash=@hash",
                    token: cancellationToken,
                    ("@hash", SHA256.HashData(Encoding.UTF8.GetBytes(command.Confirmation)))).ConfigureAwait(false);
                RetirementConfirmation? confirmation = confirmationBytes is byte[] bytes
                    ? JsonSerializer.Deserialize(bytes, RetirementJsonContext.Default.RetirementConfirmation) : null;
                if (confirmation is null || confirmation.ExpiresAt <= now || confirmation.Intent != RetirementConfirmationIntent(caller, command) ||
                    confirmation.Fingerprint != await RetirementFingerprintAsync(
                        connection: connection,
                        transaction: transaction,
                        state: state,
                        data: data,
                        token: cancellationToken).ConfigureAwait(false))
                {
                    return CaseFailure<LabelRetirementView>(LabelServiceErrorCode.StaleDecision, "Obtain and explicitly confirm a fresh action-specific retirement preview.");
                }
            }

            string? blocker = await ApplyRetirementCommandAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                data: data,
                caller: caller,
                command: command,
                now: now,
                token: cancellationToken).ConfigureAwait(false);
            if (blocker is not null)
            {
                await AuditRetirementAsync(
                    connection: connection, transaction: transaction, team: team, caller: caller,
                    operationId: command.OperationId, action: command.Action, reason: command.Reason,
                    succeeded: false, token: cancellationToken).ConfigureAwait(false);
                return CaseFailure<LabelRetirementView>(LabelServiceErrorCode.StaleDecision, blocker);
            }

            LabelRetirementView view = RetirementView(
                await StateAsync(connection, transaction, cancellationToken).ConfigureAwait(false),
                await ReadRetirementDataAsync(connection, transaction, cancellationToken).ConfigureAwait(false));
            await AuditRetirementAsync(
                connection: connection, transaction: transaction, team: team, caller: caller,
                operationId: command.OperationId, action: command.Action, reason: command.Reason,
                succeeded: true, token: cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(
                connection: connection,
                transaction: transaction,
                sql: "INSERT INTO label_retirement_receipts(operation_hash,payload) VALUES(@hash,@payload)",
                token: cancellationToken,
                ("@hash", OperationHash(command.OperationId)),
                ("@payload", JsonSerializer.SerializeToUtf8Bytes(
                    new RetirementReceipt(
                        OperationId: command.OperationId,
                        Intent: intent,
                        View: view,
                        HostInstanceId: command.HostInstanceId,
                        Attestation: command.Attestation),
                    RetirementJsonContext.Default.RetirementReceipt))).ConfigureAwait(false);
            if (command.Confirmation is not null)
            {
                await ExecuteAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "DELETE FROM label_retirement_confirmations WHERE token_hash=@hash",
                    token: cancellationToken,
                    ("@hash", SHA256.HashData(Encoding.UTF8.GetBytes(command.Confirmation)))).ConfigureAwait(false);
            }

            return LabelServiceResult<LabelRetirementView>.Success(view);
        }, cancellationToken);
    }

    /// <summary>Applies phase-valid lifecycle changes without opening a second transaction.</summary>
    private async Task<string?> ApplyRetirementCommandAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelStoreState state,
        RetirementData? data,
        LabelAdministrationCaller caller,
        LabelRetirementCommand command,
        long now,
        CancellationToken token)
    {
        if ((await ReadMaintenanceAsync(connection, transaction, token).ConfigureAwait(false)).IsActive)
        {
            return "Recover the active rotation before retirement administration.";
        }

        if ((await ReadPolicyGateAsync(connection, transaction, token).ConfigureAwait(false))?.Phase is
            LabelPolicyPhase.Publishing or LabelPolicyPhase.Withdrawing)
        {
            return "Recover pending policy publication before retirement.";
        }

        DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeMilliseconds(now);
        RetirementData next = data ?? new(
            OperationId: command.OperationId,
            Generation: 0,
            Running: false,
            Stage: "TimestampScan",
            ScannedThrough: 0,
            CreatedAt: timestamp,
            Actor: caller.Actor.Value,
            AuthorityGeneration: caller.Generation,
            Credential: caller.Credential,
            LocalIdentity: caller.LocalIdentity,
            ExpiresAt: caller.ExpiresAt,
            Reason: command.Reason);
        switch (command.Action)
        {
            case "beginRetirement" when state.Phase == LabelerLifecyclePhase.Active:
                if (command.VerifiedSigningPublicKey is null && state.IssuedThrough != 0)
                {
                    return "Verify the selected label signing key and public DID publication before beginning retirement.";
                }

                state = state with
                {
                    Phase = state.IssuedThrough == 0 ? LabelerLifecyclePhase.Distributing : LabelerLifecyclePhase.Retiring,
                    RetirementUpperBound = state.IssuedThrough
                };
                next = next with
                {
                    Running = true,
                    Stage = state.IssuedThrough == 0 ? "Publication" : "TimestampScan",
                    SigningPublicKey = command.VerifiedSigningPublicKey
                };
                break;
            case "pauseRetirement" when state.Phase is LabelerLifecyclePhase.Retiring or LabelerLifecyclePhase.Distributing or LabelerLifecyclePhase.Purging:
                next = next with { Running = false, PauseReason = "Paused by an administrator." };
                break;
            case "resumeRetirement" when state.Phase is LabelerLifecyclePhase.Retiring or LabelerLifecyclePhase.Distributing or LabelerLifecyclePhase.Purging:
                next = next with
                {
                    Running = true,
                    PauseReason = null,
                    Actor = caller.Actor.Value,
                    AuthorityGeneration = caller.Generation,
                    Credential = caller.Credential,
                    LocalIdentity = caller.LocalIdentity,
                    ExpiresAt = caller.ExpiresAt,
                    Reason = command.Reason,
                    SigningPublicKey = command.VerifiedSigningPublicKey ?? next.SigningPublicKey
                };
                break;
            case "beginGracePeriod" when state.Phase == LabelerLifecyclePhase.Distributing && state.IssuedThrough == state.PublishedThrough:
                state = state with { Phase = LabelerLifecyclePhase.Grace, GraceDeadline = timestamp.AddDays(command.GracePeriodDays) };
                next = next with { Running = false, Stage = "Grace", AvailabilityAttestation = null };
                break;
            case "extendGracePeriod" when state.Phase == LabelerLifecyclePhase.Grace:
                state = state with { GraceDeadline = (state.GraceDeadline > timestamp ? state.GraceDeadline.Value : timestamp).AddDays(command.GracePeriodDays) };
                next = next with { AvailabilityAttestation = null };
                break;
            case "completeRetirement" when state.Phase == LabelerLifecyclePhase.Grace && state.GraceDeadline <= timestamp:
                state = state with { Phase = LabelerLifecyclePhase.Retired };
                next = next with { Running = false, Stage = "Retired", AvailabilityAttestation = command.Attestation };
                break;
            case "purgeRetiredData" when state.Phase == LabelerLifecyclePhase.Retired && next.KeyRemoval != "Requested":
                state = state with { Phase = LabelerLifecyclePhase.Purging };
                next = next with
                {
                    Running = true,
                    Stage = "Purge",
                    Actor = caller.Actor.Value,
                    AuthorityGeneration = caller.Generation,
                    Credential = caller.Credential,
                    LocalIdentity = caller.LocalIdentity,
                    ExpiresAt = caller.ExpiresAt,
                    Reason = command.Reason
                };
                break;
            case "requestKeyRemoval" when state.Phase is LabelerLifecyclePhase.Retired or LabelerLifecyclePhase.Purged &&
                state.IssuedThrough == state.PublishedThrough && next.KeyRemoval == "None":
                next = next with { Running = false, KeyRemoval = "Requested", Inventory = command.Inventory };
                break;
            case "attestKeyRemoval" when state.Phase is LabelerLifecyclePhase.Retired or LabelerLifecyclePhase.Purged &&
                next.KeyRemoval == "Requested" && next.Inventory == command.Inventory:
                if (Integer(await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT COUNT(*) FROM label_hosts h JOIN label_host_roles r ON r.host_id=h.host_id WHERE h.stopped=0 AND r.requires_signing_key=1",
                    token: token).ConfigureAwait(false)) != 0)
                {
                    return "Stop and independently verify every signing host before attesting removal. Keyless administration may remain running.";
                }

                next = next with { KeyRemoval = "Attested", RemovalAttestation = command.Attestation };
                break;
            case "confirmRetirementHostStopped" when state.Phase is LabelerLifecyclePhase.Retired or LabelerLifecyclePhase.Purged:
                if (Integer(await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT COUNT(*) FROM label_hosts WHERE host_id=@host AND stopped=0",
                    token: token,
                    ("@host", command.HostInstanceId!.Value.ToString("N"))).ConfigureAwait(false)) != 1)
                {
                    return "The selected host is not an unstopped registration. Refresh the retirement inventory.";
                }

                await ExecuteAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "UPDATE label_hosts SET stopped=1 WHERE host_id=@host AND stopped=0",
                    token: token,
                    ("@host", command.HostInstanceId.Value.ToString("N"))).ConfigureAwait(false);
                break;
            default:
                return "The current lifecycle does not permit this action. Inspect progress and recover the existing operation.";
        }

        next = next with
        {
            Generation = checked(next.Generation + 1),
            ControlId = Guid.NewGuid(),
            LastActivityAt = timestamp,
            LastProgressAt = timestamp
        };
        await SaveAsync(
            connection: connection,
            transaction: transaction,
            state: state with { Revision = checked(state.Revision + 1) },
            token: token).ConfigureAwait(false);
        await SaveRetirementDataAsync(
            connection: connection,
            transaction: transaction,
            data: next,
            token: token).ConfigureAwait(false);
        return null;
    }
}
