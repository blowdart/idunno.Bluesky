// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Maintenance;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    public Task<LabelServiceResult<LabelAdministrationReceipt>> AuditMaintenanceAsync(
        LabelAdministrationCaller caller,
        string operationId,
        string reason,
        LabelMaintenanceAuditPhase phase = LabelMaintenanceAuditPhase.Intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        LabelInput.ValidateText(operationId, nameof(operationId), 256);
        LabelInput.ValidateText(reason, nameof(reason), 4096);
        if (!Enum.IsDefined(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }

        return AdministrationAsync(async (connection, transaction, state) =>
        {
            AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            bool allowed = await HasPermissionAsync(
                connection: connection, transaction: transaction, state: state, administration: team,
                caller: caller, permission: LabelAdministrationPermission.Maintain, token: cancellationToken).ConfigureAwait(false);
            LabelAdministrationReceipt audit = await AuditAdministrationAsync(
                connection: connection, transaction: transaction, administration: team, actor: caller.Actor,
                localIdentity: caller.LocalIdentity, operationId: operationId, action: "maintenance-" + phase,
                reason: reason, succeeded: allowed && phase != LabelMaintenanceAuditPhase.Failed, token: cancellationToken).ConfigureAwait(false);
            return allowed ? LabelServiceResult<LabelAdministrationReceipt>.Success(audit)
                : AdminFailure<LabelAdministrationReceipt>("Current maintenance authority is required.");
        }, cancellationToken);
    }

    public Task<LabelServiceResult<LabelAdministrationReceipt>> RecordAdministrationDenialAsync(
        string reason,
        LabelAdministrationCaller? caller = null,
        CancellationToken cancellationToken = default)
    {
        LabelInput.ValidateText(reason, nameof(reason), 4096);
        return AdministrationAsync(async (connection, transaction, _) =>
        {
            AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            LabelAdministrationReceipt receipt = await AuditAdministrationAsync(
                connection: connection, transaction: transaction, administration: team, actor: caller?.Actor,
                localIdentity: caller?.LocalIdentity, operationId: string.Empty, action: "invocation-denied",
                reason: reason, succeeded: false, token: cancellationToken).ConfigureAwait(false);
            return LabelServiceResult<LabelAdministrationReceipt>.Success(receipt);
        }, cancellationToken);
    }

    /// <summary>Reads the bounded issuer-local team aggregate while holding its serialization lock.</summary>
    private static async Task<AdministrationState> ReadAdministrationAsync(DbConnection connection, DbTransaction transaction, CancellationToken token)
    {
        object? payload = await ScalarAsync(
            connection: connection,
            transaction: transaction,
            sql: "SELECT payload FROM label_team WHERE id=1",
            token: token).ConfigureAwait(false);
        return payload is byte[] bytes
            ? JsonSerializer.Deserialize(bytes, AdministrationJsonContext.Default.AdministrationState)
                ?? throw new InvalidDataException("The team aggregate is null.")
            : new();
    }

    /// <summary>Writes the bounded aggregate in the same transaction as receipts and audit.</summary>
    private static async Task SaveAdministrationAsync(
        DbConnection connection,
        DbTransaction transaction,
        AdministrationState administration,
        CancellationToken token)
    {
        await ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "DELETE FROM label_team WHERE id=1",
            token: token).ConfigureAwait(false);
        await ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "INSERT INTO label_team(id,payload) VALUES(1,@payload)",
            token: token,
            ("@payload", JsonSerializer.SerializeToUtf8Bytes(administration, AdministrationJsonContext.Default.AdministrationState))).ConfigureAwait(false);
    }

    /// <summary>Allocates an immutable administrative audit event under the issuer lock.</summary>
    private async Task<LabelAdministrationReceipt> AuditAdministrationAsync(
        DbConnection connection,
        DbTransaction transaction,
        AdministrationState administration,
        Did? actor,
        string? localIdentity,
        string operationId,
        string action,
        string reason,
        bool succeeded,
        CancellationToken token)
    {
        administration.Sequence = checked(administration.Sequence + 1);
        AdministrationAudit audit = new(administration.Sequence, operationId, actor?.Value, localIdentity, action, reason, succeeded)
        {
            CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(
                await DatabaseTimeAsync(connection, transaction, token).ConfigureAwait(false))
        };
        await ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "INSERT INTO label_admin_audit(seq,payload) VALUES(@seq,@payload)",
            token: token,
            ("@seq", audit.Sequence),
            ("@payload", JsonSerializer.SerializeToUtf8Bytes(audit, AdministrationJsonContext.Default.AdministrationAudit))).ConfigureAwait(false);
        await SaveAdministrationAsync(connection, transaction, administration, token).ConfigureAwait(false);
        return audit.ToReceipt();
    }

    /// <summary>Reads a retained same-intent receipt before applying another mutation.</summary>
    private static async Task<LabelServiceResult<LabelAdministrationReceipt>?> AdministrationReceiptAsync(
        DbConnection connection,
        DbTransaction transaction,
        string operationId,
        byte[] intent,
        CancellationToken token)
    {
        DbCommand command = Command(
            connection: connection,
            transaction: transaction,
            sql: "SELECT operation_id,intent,payload FROM label_admin_receipts WHERE operation_hash=@hash",
            ("@hash", OperationHash(operationId)));
        await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
        DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false))
        {
            return null;
        }

        if (reader.GetString(0) != operationId || !((byte[])reader.GetValue(1)).AsSpan().SequenceEqual(intent))
        {
            return LabelServiceResult<LabelAdministrationReceipt>.Failure(new(
                code: LabelServiceErrorCode.IdempotencyConflict,
                message: "The operation ID already identifies different administrative intent."));
        }

        AdministrationAudit audit = JsonSerializer.Deserialize((byte[])reader.GetValue(2), AdministrationJsonContext.Default.AdministrationAudit)
            ?? throw new InvalidDataException("The administrative receipt is null.");
        return LabelServiceResult<LabelAdministrationReceipt>.Success(audit.ToReceipt());
    }

    /// <summary>Retains a success receipt atomically with its audit and team mutation.</summary>
    private static Task SaveAdministrationReceiptAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelAdministrationReceipt receipt,
        byte[] intent,
        CancellationToken token) =>
        ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "INSERT INTO label_admin_receipts(operation_hash,operation_id,intent,payload) VALUES(@hash,@id,@intent,@payload)",
            token: token,
            ("@hash", OperationHash(receipt.OperationId)),
            ("@id", receipt.OperationId),
            ("@intent", intent),
            ("@payload", JsonSerializer.SerializeToUtf8Bytes(
                new AdministrationAudit(receipt.Sequence, receipt.OperationId, receipt.Actor?.Value, receipt.LocalIdentity, receipt.Action, receipt.Reason, receipt.Succeeded)
                {
                    CreatedAt = receipt.CreatedAt
                },
                AdministrationJsonContext.Default.AdministrationAudit)));

    /// <summary>Checks current membership, exact issuer, role and credential under the same mutation lock.</summary>
    private async Task<bool> HasPermissionAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelStoreState state,
        AdministrationState administration,
        LabelAdministrationCaller caller,
        LabelAdministrationPermission permission,
        CancellationToken token)
    {
        if (!administration.Bootstrapped || state.Issuer != caller.Issuer.Value ||
            !administration.Members.TryGetValue(caller.Actor.Value, out AdministrationMember? member) ||
            !member.Active || member.Removed || member.Generation != caller.Generation ||
            member.Role < (permission switch
            {
                LabelAdministrationPermission.Inspect => LabelTeamRole.Viewer,
                LabelAdministrationPermission.Moderate => LabelTeamRole.Moderator,
                _ => LabelTeamRole.Administrator
            }))
        {
            return false;
        }

        if (caller.LocalIdentity is { } local)
        {
            return administration.Bindings.TryGetValue(local, out AdministrationBinding? binding) &&
                binding.Did == caller.Actor.Value && binding.Credential == caller.Credential;
        }

        long now = await DatabaseTimeAsync(connection, transaction, token).ConfigureAwait(false);
        return caller.ExpiresAt.ToUnixTimeMilliseconds() > now &&
            Integer(await ScalarAsync(
                connection: connection,
                transaction: transaction,
                sql: "SELECT COUNT(*) FROM label_admin_tokens WHERE replay_hash=@hash AND actor=@actor AND expires_ms>@now",
                token: token,
                ("@hash", Convert.FromHexString(caller.Credential)),
                ("@actor", caller.Actor.Value),
                ("@now", now)).ConfigureAwait(false)) == 1;
    }

    /// <summary>Classifies storage failure without retrying an ambiguously submitted administration commit.</summary>
    private async Task<LabelServiceResult<T>> AdministrationAsync<T>(
        Func<DbConnection, DbTransaction, LabelStoreState, Task<LabelServiceResult<T>>> action,
        CancellationToken token) where T : class
    {
        try
        {
            return await RunAsync(
                operation: "administration",
                action: action,
                token: token,
                decisionOperation: true).ConfigureAwait(false);
        }
        catch (DecisionCommitUncertainException exception)
        {
            return LabelServiceResult<T>.Failure(new(
                code: LabelServiceErrorCode.OutcomeUnknown,
                message: "Recover the original administrative operation ID before retrying.",
                exception: exception.InnerException));
        }
        catch (DbException exception)
        {
            return LabelServiceResult<T>.Failure(new(
                code: LabelServiceErrorCode.StorageUnavailable,
                message: "Administrative storage is unavailable.",
                exception: exception));
        }
    }

    public Task<LabelServiceResult<LabelAdministrationReceipt>> BootstrapAsync(
        Did administrator,
        string localIdentity,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(administrator);
        LabelTeamChange.ValidateLocalIdentity(localIdentity);
        LabelInput.ValidateText(operationId, nameof(operationId), 256);
        byte[] intent = Intent("bootstrap", administrator.Value, localIdentity);
        return AdministrationAsync(async (connection, transaction, _) =>
        {
            LabelServiceResult<LabelAdministrationReceipt>? original = await AdministrationReceiptAsync(
                connection: connection,
                transaction: transaction,
                operationId: operationId,
                intent: intent,
                token: cancellationToken).ConfigureAwait(false);
            if (original is not null)
            {
                return original;
            }

            AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            if (team.Bootstrapped)
            {
                await AuditAdministrationAsync(
                    connection: connection, transaction: transaction, administration: team, actor: administrator,
                    localIdentity: localIdentity, operationId: operationId, action: "bootstrap",
                    reason: "Bootstrap is already complete.", succeeded: false, token: cancellationToken).ConfigureAwait(false);
                return AdminFailure<LabelAdministrationReceipt>("Bootstrap is permanently closed.");
            }

            team.Bootstrapped = true;
            team.Members.Add(administrator.Value, new(LabelTeamRole.Administrator, true, false, 1));
            team.Bindings.Add(localIdentity, new(administrator.Value, Guid.NewGuid().ToString("N")));
            LabelAdministrationReceipt receipt = await AuditAdministrationAsync(
                connection: connection, transaction: transaction, administration: team, actor: administrator,
                localIdentity: localIdentity, operationId: operationId, action: "bootstrap",
                reason: "Explicit OS-authorized initialization.", succeeded: true, token: cancellationToken).ConfigureAwait(false);
            await SaveAdministrationReceiptAsync(connection, transaction, receipt, intent, cancellationToken).ConfigureAwait(false);
            return LabelServiceResult<LabelAdministrationReceipt>.Success(receipt);
        }, cancellationToken);
    }

    public Task<LabelServiceResult<LabelAdministrationCaller>> AuthenticateLocalAsync(
        string localIdentity,
        CancellationToken cancellationToken = default)
    {
        LabelTeamChange.ValidateLocalIdentity(localIdentity);
        return AdministrationAsync(async (connection, transaction, state) =>
        {
            AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            if (!team.Bindings.TryGetValue(localIdentity, out AdministrationBinding? binding) ||
                !team.Members.TryGetValue(binding.Did, out AdministrationMember? member) || !member.Active || member.Removed)
            {
                await AuditAdministrationAsync(
                    connection: connection, transaction: transaction, administration: team, actor: null,
                    localIdentity: localIdentity, operationId: string.Empty, action: "authenticate-local",
                    reason: "The OS identity has no active team binding.", succeeded: false,
                    token: cancellationToken).ConfigureAwait(false);
                return AdminFailure<LabelAdministrationCaller>("The OS identity has no active team binding.");
            }

            return LabelServiceResult<LabelAdministrationCaller>.Success(new(
                issuer: new(state.Issuer),
                actor: new(binding.Did),
                generation: member.Generation,
                credential: binding.Credential,
                localIdentity: localIdentity,
                expiresAt: DateTimeOffset.MaxValue));
        }, cancellationToken);
    }

    public Task<LabelServiceResult<LabelAdministrationCaller>> AuthenticateBrowserAsync(
        Did actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return AdministrationAsync(async (connection, transaction, state) =>
        {
            AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            if (!team.Bootstrapped ||
                !team.Members.TryGetValue(actor.Value, out AdministrationMember? member) ||
                !member.Active || member.Removed)
            {
                await AuditAdministrationAsync(
                    connection: connection, transaction: transaction, administration: team, actor: actor,
                    localIdentity: null, operationId: string.Empty, action: "authenticate-browser",
                    reason: "The authenticated DID is not an active team member.", succeeded: false,
                    token: cancellationToken).ConfigureAwait(false);
                return AdminFailure<LabelAdministrationCaller>("The authenticated DID is not an active team member.");
            }

            long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            await ExecuteAsync(
                connection: connection, transaction: transaction,
                sql: "DELETE FROM label_admin_tokens WHERE expires_ms<=@now",
                token: cancellationToken, ("@now", now)).ConfigureAwait(false);

            byte[] nonce = RandomNumberGenerator.GetBytes(32);
            byte[] replay = SHA256.HashData(nonce);
            string tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("browser\\n" + Convert.ToHexString(nonce))));
            DateTimeOffset expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(checked(now + 10 * 60 * 1000));
            await ExecuteAsync(
                connection: connection, transaction: transaction,
                sql: "INSERT INTO label_admin_tokens(replay_hash,token_hash,actor,expires_ms) VALUES(@replay,@hash,@actor,@expiry)",
                token: cancellationToken,
                ("@replay", replay), ("@hash", tokenHash), ("@actor", actor.Value),
                ("@expiry", expiresAt.ToUnixTimeMilliseconds())).ConfigureAwait(false);

            return LabelServiceResult<LabelAdministrationCaller>.Success(new(
                issuer: new(state.Issuer),
                actor: actor,
                generation: member.Generation,
                credential: Convert.ToHexString(replay),
                localIdentity: null,
                expiresAt: expiresAt));
        }, cancellationToken);
    }

    public Task<LabelServiceResult<LabelAdministrationCaller>> AuthenticateServiceAsync(
        ServiceAuthenticationResult authentication,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        return AdministrationAsync(async (connection, transaction, state) =>
        {
            AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            byte[] replay = SHA256.HashData(Encoding.UTF8.GetBytes(authentication.Issuer.Value + "\n" + authentication.TokenId));
            long expiry = authentication.ExpiresAt.ToUnixTimeMilliseconds();
            if (!team.Bootstrapped || authentication.Audience.Did.Value != state.Issuer ||
                expiry <= now ||
                !team.Members.TryGetValue(authentication.Issuer.Value, out AdministrationMember? member) ||
                !member.Active || member.Removed ||
                await ScalarAsync(
                    connection: connection, transaction: transaction,
                    sql: "SELECT token_hash FROM label_admin_tokens WHERE replay_hash=@replay",
                    token: cancellationToken, ("@replay", replay)).ConfigureAwait(false) is not null)
            {
                await AuditAdministrationAsync(
                    connection: connection, transaction: transaction, administration: team, actor: authentication.Issuer,
                    localIdentity: null, operationId: string.Empty, action: "authenticate",
                    reason: "Invalid membership, audience or replayed credential.", succeeded: false,
                    token: cancellationToken).ConfigureAwait(false);
                return AdminFailure<LabelAdministrationCaller>("The service credential or membership is not valid.");
            }

            await ExecuteAsync(
                connection: connection, transaction: transaction,
                sql: "DELETE FROM label_admin_tokens WHERE expires_ms<=@now",
                token: cancellationToken, ("@now", now)).ConfigureAwait(false);
            await ExecuteAsync(
                connection: connection, transaction: transaction,
                sql: "INSERT INTO label_admin_tokens(replay_hash,token_hash,actor,expires_ms) VALUES(@replay,@hash,@actor,@expiry)",
                token: cancellationToken, ("@replay", replay), ("@hash", authentication.TokenHash),
                ("@actor", authentication.Issuer.Value), ("@expiry", expiry)).ConfigureAwait(false);
            return LabelServiceResult<LabelAdministrationCaller>.Success(new(
                issuer: new(state.Issuer), actor: authentication.Issuer, generation: member.Generation,
                credential: Convert.ToHexString(replay), localIdentity: null, expiresAt: authentication.ExpiresAt));
        }, cancellationToken);
    }

    public Task<LabelServiceResult<LabelAuthorizationGrant>> AuthorizeAsync(
        LabelAdministrationCaller caller,
        LabelAdministrationPermission permission,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (!Enum.IsDefined(permission))
        {
            throw new ArgumentOutOfRangeException(nameof(permission));
        }

        LabelInput.ValidateText(reason, nameof(reason), 4096);
        return AdministrationAsync(async (connection, transaction, state) =>
        {
            AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            bool allowed = await HasPermissionAsync(
                connection: connection, transaction: transaction, state: state, administration: team,
                caller: caller, permission: permission, token: cancellationToken).ConfigureAwait(false);
            LabelAdministrationReceipt audit = await AuditAdministrationAsync(
                connection: connection, transaction: transaction, administration: team, actor: caller.Actor,
                localIdentity: caller.LocalIdentity, operationId: string.Empty, action: permission.ToString(),
                reason: reason, succeeded: allowed, token: cancellationToken).ConfigureAwait(false);
            return allowed ? LabelServiceResult<LabelAuthorizationGrant>.Success(new(audit.Sequence.ToString(CultureInfo.InvariantCulture))
            {
                AdministrationCaller = caller
            }) : AdminFailure<LabelAuthorizationGrant>("The caller lacks the current required permission.");
        }, cancellationToken);
    }

    public Task<LabelServiceResult<LabelAdministrationReceipt>> ChangeTeamAsync(
        LabelAdministrationCaller caller,
        LabelTeamChange change,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(change);
        byte[] intent = Intent(
            caller.Actor.Value, change.Action.ToString(), change.Target.Value,
            change.Role.ToString(), change.Credential ?? string.Empty, change.Reason);
        return AdministrationAsync(async (connection, transaction, state) =>
        {
            AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            bool allowed = await HasPermissionAsync(
                connection: connection, transaction: transaction, state: state, administration: team,
                caller: caller, permission: LabelAdministrationPermission.ManageTeam, token: cancellationToken).ConfigureAwait(false);
            if (!allowed)
            {
                await AuditAdministrationAsync(
                    connection: connection, transaction: transaction, administration: team, actor: caller.Actor,
                    localIdentity: caller.LocalIdentity, operationId: change.OperationId, action: change.Action.ToString(),
                    reason: change.Reason, succeeded: false, token: cancellationToken).ConfigureAwait(false);
                return AdminFailure<LabelAdministrationReceipt>("Current administrator authority is required.");
            }

            LabelServiceResult<LabelAdministrationReceipt>? original = await AdministrationReceiptAsync(
                connection: connection, transaction: transaction, operationId: change.OperationId,
                intent: intent, token: cancellationToken).ConfigureAwait(false);
            if (original is not null)
            {
                return original;
            }

            string? failure = ApplyTeamChange(team, change);
            LabelAdministrationReceipt receipt = await AuditAdministrationAsync(
                connection: connection, transaction: transaction, administration: team, actor: caller.Actor,
                localIdentity: caller.LocalIdentity, operationId: change.OperationId, action: change.Action.ToString(),
                reason: change.Reason, succeeded: failure is null, token: cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return AdminFailure<LabelAdministrationReceipt>(failure);
            }

            await ReconcileAssignmentsAsync(connection, transaction, team, caller, cancellationToken).ConfigureAwait(false);
            await SaveAdministrationReceiptAsync(connection, transaction, receipt, intent, cancellationToken).ConfigureAwait(false);
            return LabelServiceResult<LabelAdministrationReceipt>.Success(receipt);
        }, cancellationToken);
    }

    /// <summary>Changes one administrator alert address without placing it in audit records.</summary>
    public Task<LabelServiceResult<LabelAdministrationReceipt>> SetTeamNotificationEmailAsync(
        LabelAdministrationCaller caller,
        LabelTeamNotificationEmailChange change,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(change);
        byte[] intent = Intent(
            caller.Actor.Value,
            change.Target.Value,
            change.NotificationEmail ?? string.Empty,
            change.Reason);
        return AdministrationAsync(async (connection, transaction, state) =>
        {
            AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            bool allowed = await HasPermissionAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                administration: team,
                caller: caller,
                permission: LabelAdministrationPermission.ManageTeam,
                token: cancellationToken).ConfigureAwait(false);
            if (!allowed)
            {
                await AuditAdministrationAsync(
                    connection: connection,
                    transaction: transaction,
                    administration: team,
                    actor: caller.Actor,
                    localIdentity: caller.LocalIdentity,
                    operationId: change.OperationId,
                    action: "set-team-notification-email",
                    reason: change.Reason,
                    succeeded: false,
                    token: cancellationToken).ConfigureAwait(false);
                return AdminFailure<LabelAdministrationReceipt>("Current administrator authority is required.");
            }

            LabelServiceResult<LabelAdministrationReceipt>? original = await AdministrationReceiptAsync(
                connection: connection,
                transaction: transaction,
                operationId: change.OperationId,
                intent: intent,
                token: cancellationToken).ConfigureAwait(false);
            if (original is not null)
            {
                return original;
            }

            if (!team.Members.TryGetValue(change.Target.Value, out AdministrationMember? member) ||
                member is not { Active: true, Removed: false, Role: LabelTeamRole.Administrator })
            {
                await AuditAdministrationAsync(
                    connection: connection,
                    transaction: transaction,
                    administration: team,
                    actor: caller.Actor,
                    localIdentity: caller.LocalIdentity,
                    operationId: change.OperationId,
                    action: "set-team-notification-email",
                    reason: change.Reason,
                    succeeded: false,
                    token: cancellationToken).ConfigureAwait(false);
                return AdminFailure<LabelAdministrationReceipt>("Only an active administrator can register a policy-alert email.");
            }

            team.Members[change.Target.Value] = member with { NotificationEmail = change.NotificationEmail };
            LabelAdministrationReceipt receipt = await AuditAdministrationAsync(
                connection: connection,
                transaction: transaction,
                administration: team,
                actor: caller.Actor,
                localIdentity: caller.LocalIdentity,
                operationId: change.OperationId,
                action: "set-team-notification-email",
                reason: change.Reason,
                succeeded: true,
                token: cancellationToken).ConfigureAwait(false);
            await SaveAdministrationReceiptAsync(connection, transaction, receipt, intent, cancellationToken).ConfigureAwait(false);
            return LabelServiceResult<LabelAdministrationReceipt>.Success(receipt);
        }, cancellationToken);
    }

    /// <summary>Applies a bounded team change after all invariant checks, leaving denied state unchanged.</summary>
    private static string? ApplyTeamChange(AdministrationState team, LabelTeamChange change)
    {
        string did = change.Target.Value;
        team.Members.TryGetValue(did, out AdministrationMember? member);
        if (change.Action == LabelTeamAction.Enroll)
        {
            if (member is { Removed: false } || member is null && team.Members.Count >= 1000)
            {
                return "The member exists or the team capacity is exhausted.";
            }

            team.Members[did] = new(change.Role, true, false, checked((member?.Generation ?? 0) + 1));
            return null;
        }

        if (member is null || member.Removed)
        {
            return "The target is not enrolled.";
        }

        bool losesAdmin = member is { Active: true, Role: LabelTeamRole.Administrator } &&
            (change.Action is LabelTeamAction.Suspend or LabelTeamAction.Remove ||
             change.Action == LabelTeamAction.SetRole && change.Role != LabelTeamRole.Administrator);
        if (losesAdmin && team.Members.Values.Count(entry => entry is { Active: true, Removed: false, Role: LabelTeamRole.Administrator }) == 1)
        {
            return "The final active administrator cannot be removed, suspended or demoted.";
        }

        if (change.Action == LabelTeamAction.BindLocal)
        {
            if (!member.Active || team.Bindings.Count >= 1000 && !team.Bindings.ContainsKey(change.Credential!))
            {
                return "An active member and available binding capacity are required.";
            }

            team.Bindings[change.Credential!] = new(did, Guid.NewGuid().ToString("N"));
        }
        else if (change.Action == LabelTeamAction.RevokeLocal)
        {
            if (!team.Bindings.TryGetValue(change.Credential!, out AdministrationBinding? binding) || binding.Did != did)
            {
                return "The binding does not belong to this member.";
            }

            team.Bindings.Remove(change.Credential!);
        }
        else
        {
            team.Members[did] = member with
            {
                Role = change.Action == LabelTeamAction.SetRole ? change.Role : member.Role,
                Active = change.Action switch
                {
                    LabelTeamAction.Suspend or LabelTeamAction.Remove => false,
                    LabelTeamAction.Reinstate => true,
                    _ => member.Active
                },
                Removed = change.Action == LabelTeamAction.Remove,
                NotificationEmail = change.Action == LabelTeamAction.Remove ? null : member.NotificationEmail,
                Generation = checked(member.Generation + 1)
            };
            if (change.Action is LabelTeamAction.Suspend or LabelTeamAction.Remove)
            {
                foreach (string local in team.Bindings.Where(entry => entry.Value.Did == did).Select(entry => entry.Key).ToArray())
                {
                    team.Bindings.Remove(local);
                }
            }
        }

        return null;
    }

    public Task<LabelServiceResult<IReadOnlyList<LabelTeamMember>>> GetTeamAsync(
        LabelAdministrationCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        return AdministrationAsync<IReadOnlyList<LabelTeamMember>>(async (connection, transaction, state) =>
        {
            AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            bool allowed = await HasPermissionAsync(
                connection: connection, transaction: transaction, state: state, administration: team,
                caller: caller, permission: LabelAdministrationPermission.Inspect, token: cancellationToken).ConfigureAwait(false);
            bool canManageTeam = allowed && await HasPermissionAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                administration: team,
                caller: caller,
                permission: LabelAdministrationPermission.ManageTeam,
                token: cancellationToken).ConfigureAwait(false);
            await AuditAdministrationAsync(
                connection: connection, transaction: transaction, administration: team, actor: caller.Actor,
                localIdentity: caller.LocalIdentity, operationId: string.Empty, action: "team-read",
                reason: "Inspect team.", succeeded: allowed, token: cancellationToken).ConfigureAwait(false);
            if (allowed)
            {
                await ReconcileAssignmentsAsync(connection, transaction, team, caller, cancellationToken).ConfigureAwait(false);
            }
            return allowed ? LabelServiceResult<IReadOnlyList<LabelTeamMember>>.Success(
                [.. team.Members.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry =>
                    new LabelTeamMember(new(entry.Key), entry.Value.Role, entry.Value.Active, entry.Value.Removed)
                    {
                        NotificationEmail = canManageTeam ? entry.Value.NotificationEmail : null
                    })])
                : AdminFailure<IReadOnlyList<LabelTeamMember>>("Inspection requires current team authority.");
        }, cancellationToken);
    }

    public Task<LabelServiceResult<IReadOnlyList<LabelAdministrationReceipt>>> GetAdministrationAuditAsync(
        LabelAdministrationCaller caller,
        long afterSequence = 0,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentOutOfRangeException.ThrowIfNegative(afterSequence);
        Limit(limit);
        return AdministrationAsync<IReadOnlyList<LabelAdministrationReceipt>>(async (connection, transaction, state) =>
        {
            AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            bool allowed = await HasPermissionAsync(
                connection: connection, transaction: transaction, state: state, administration: team,
                caller: caller, permission: LabelAdministrationPermission.Inspect, token: cancellationToken).ConfigureAwait(false);
            if (!allowed)
            {
                await AuditAdministrationAsync(
                    connection: connection, transaction: transaction, administration: team, actor: caller.Actor,
                    localIdentity: caller.LocalIdentity, operationId: string.Empty, action: "audit-read",
                    reason: "Inspect audit.", succeeded: false, token: cancellationToken).ConfigureAwait(false);
                return AdminFailure<IReadOnlyList<LabelAdministrationReceipt>>("Inspection requires current team authority.");
            }

            DbCommand command = Command(
                connection: connection, transaction: transaction,
                sql: "SELECT payload FROM label_admin_audit WHERE seq>@after ORDER BY seq LIMIT @limit",
                ("@after", afterSequence), ("@limit", limit));
            await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
            DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
            List<LabelAdministrationReceipt> page = [];
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                AdministrationAudit audit = JsonSerializer.Deserialize((byte[])reader.GetValue(0), AdministrationJsonContext.Default.AdministrationAudit)
                    ?? throw new InvalidDataException("The administrative audit is null.");
                page.Add(audit.ToReceipt());
            }

            return LabelServiceResult<IReadOnlyList<LabelAdministrationReceipt>>.Success(page);
        }, cancellationToken);
    }

    /// <summary>Encodes length-delimited primitive intent without culture-sensitive representations.</summary>
    private static byte[] Intent(params string[] values) =>
        JsonSerializer.SerializeToUtf8Bytes(values, AdministrationJsonContext.Default.StringArray);

    /// <summary>Returns an explicit denial without exposing identity or token diagnostics.</summary>
    private static LabelServiceResult<T> AdminFailure<T>(string message) where T : class =>
        LabelServiceResult<T>.Failure(new(code: LabelServiceErrorCode.Unauthorized, message: message));
}
