// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Identity;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Signing;
using idunno.Bluesky.Labeler.Storage;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Dispatches both transports through the same typed authorization and application services.</summary>
internal sealed partial class AdministrationDispatcher(
    ILabelStore store,
    ILabelAdministrationStore administration,
    ILabelMaintenanceStore maintenance,
    HttpClient httpClient,
    ILabelSigningKeyRing? signingKeyRing,
    ILabelIssuancePolicyProvider policy,
    Uri? publicEndpoint,
    TimeProvider clock,
    LabelerHost? host,
    RetirementWorker retirementWorker)
{
    /// <summary>Requires a real installed key only for an operation that actually signs.</summary>
    private ILabelSigningKeyRing SigningKeys => signingKeyRing
        ?? throw new InvalidOperationException("This retired host has no private signing keys.");

    // Bounded POST bodies keep confidential moderation filters out of URLs on both adapters.
    internal static readonly string[] s_moderationMethods =
    [
        "getCase", "getCaseLabels", "getManualLabelChoices", "getModerationOverview", "getCaseActivity", "searchModeration", "getCases", "getCaseHistory", "getCaseOperation", "changeCase", "decideCase", "getCaseDecision",
        "getAppeal", "getAppeals", "getAppealReports", "getCaseQueue",
        "getPolicyVersion", "getPolicyVersions", "getPolicyHistory", "getPolicyOperation",
        "getReport", "getReports", "getPolicy", "savePolicy", "deletePolicyDraft", "beginPolicyPublication", "resumePolicyPublication", "abandonPolicyPublication", "verifyPolicy"
    ];
    internal static readonly string[] s_methods =
    [
        "getTeam", "getAudit", "changeTeam", "setTeamNotificationEmail", "enroll", "issueLabel", "negateLabel",
        "getOperation", "getPublicationState", "getMaintenanceState", "authorizeMaintenance",
        "beginRetirement", "advanceRetirement", "beginGracePeriod", "completeRetirement",
        "purgeRetiredData", "getRetirementState", "prepareRetirement", "pauseRetirement", "resumeRetirement",
        "extendGracePeriod", "requestKeyRemoval", "attestKeyRemoval", "confirmRetirementHostStopped", "beginRotation", "confirmHostStopped", "captureRotationBoundary",
        "verifyRotationKeyPublication", "resignRotationBatch", "completeRotation",
        .. s_moderationMethods
    ];
    internal static readonly string[] s_getMethods =
    [
        "getTeam", "getAudit", "getOperation", "getPublicationState", "getMaintenanceState", "getRetirementState"
    ];
    internal static readonly string[] s_postMethods =
    [
        "changeTeam", "setTeamNotificationEmail", "enroll", "issueLabel", "negateLabel", "authorizeMaintenance",
        "beginRetirement", "advanceRetirement", "beginGracePeriod", "completeRetirement", "purgeRetiredData",
        "prepareRetirement", "pauseRetirement", "resumeRetirement", "extendGracePeriod", "requestKeyRemoval", "attestKeyRemoval",
        "confirmRetirementHostStopped",
        "beginRotation", "confirmHostStopped", "captureRotationBoundary", "verifyRotationKeyPublication",
        "resignRotationBatch", "completeRotation",
        .. s_moderationMethods
    ];

    /// <summary>Validates one bounded command and invokes no transport-specific business logic.</summary>
    internal async Task<AdministrationResponse> DispatchAsync(
        LabelAdministrationCaller caller,
        AdministrationCommand command,
        CancellationToken token)
    {
        command.Validate();
        if (s_getMethods.Contains(command.Method, StringComparer.Ordinal) && string.IsNullOrWhiteSpace(command.Reason))
        {
            command = command with { Reason = "Administrative inspection." };
        }

        AdministrationResponse response;
        bool labelBearing = command.Method is "issueLabel" or "negateLabel" or "advanceRetirement" ||
            command.Method == "decideCase" && command.Moderation?.Actions is { Length: > 0 };
        if (labelBearing && host is not null)
        {
            using IDisposable? admission = host.TryAdmitAdministrativeIssuance();
            response = admission is null
                ? new(false, "MaintenanceActive", "Label issuance is unavailable during coordinated maintenance.")
                : await DispatchCoreAsync(caller, command, token).ConfigureAwait(false);
        }
        else
        {
            response = await DispatchCoreAsync(caller, command, token).ConfigureAwait(false);
        }

        if (!response.Succeeded)
        {
            LabelServiceResult<LabelAdministrationReceipt> audit = await administration.RecordAdministrationDenialAsync(
                reason: "Administrative invocation failed (" + response.Error + ").",
                caller: caller,
                cancellationToken: token).ConfigureAwait(false);
            if (!audit.Succeeded)
            {
                return Failure(audit.Error);
            }
        }

        return response;
    }

    /// <summary>Runs a validated command without allowing either transport to bypass the common denial audit.</summary>
    private async Task<AdministrationResponse> DispatchCoreAsync(
        LabelAdministrationCaller caller,
        AdministrationCommand command,
        CancellationToken token)
    {
        if (!s_methods.Contains(command.Method, StringComparer.Ordinal))
        {
            return new(false, "InvalidRequest", "The administrative method is unknown.");
        }

        if (s_moderationMethods.Contains(command.Method, StringComparer.Ordinal))
        {
            return await DispatchModerationAsync(caller, command, token).ConfigureAwait(false);
        }

        switch (command.Method)
        {
            case "getTeam":
                {
                    LabelServiceResult<IReadOnlyList<LabelTeamMember>> result = await administration.GetTeamAsync(caller, token).ConfigureAwait(false);
                    return result.Succeeded
                        ? new(true, Members: [.. result.Result.Select(member => new AdministrationMemberView(
                            member.Did.Value, member.Role, member.Active, member.Removed, member.NotificationEmail))])
                        : Failure(result.Error);
                }
            case "getAudit":
                {
                    if (command.Limit is < 1 or > AdministrationCommand.MaximumAuditPageSize)
                    {
                        return new(false, "InvalidRequest", "The audit page size must be between 1 and 16.");
                    }

                    LabelServiceResult<IReadOnlyList<LabelAdministrationReceipt>> result = await administration.GetAdministrationAuditAsync(
                        caller: caller, afterSequence: command.AfterSequence, limit: command.Limit, cancellationToken: token).ConfigureAwait(false);
                    return result.Succeeded
                        ? new(true, Audit: [.. result.Result.Select(AuditView)])
                        : Failure(result.Error);
                }
            case "changeTeam":
                {
                    if (!Did.TryParse(command.Target, out Did? did) || command.Action is null ||
                        command.Action == LabelTeamAction.Enroll)
                    {
                        return new(false, "InvalidRequest", "Use a DID and a non-enrollment team action.");
                    }

                    LabelServiceResult<LabelAdministrationReceipt> result = await administration.ChangeTeamAsync(
                        caller: caller,
                        change: new(
                            operationId: command.OperationId, action: command.Action.Value, target: did,
                            reason: command.Reason, role: command.Role, credential: command.Credential),
                        cancellationToken: token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Sequence: result.Result.Sequence, OperationId: result.Result.OperationId) : Failure(result.Error);
                }
            case "setTeamNotificationEmail":
                {
                    if (!Did.TryParse(command.Target, out Did? did))
                    {
                        return new(false, "InvalidRequest", "Supply the active administrator's DID.");
                    }

                    LabelTeamNotificationEmailChange change;
                    try
                    {
                        change = new(
                            operationId: command.OperationId,
                            target: did,
                            notificationEmail: command.NotificationEmail,
                            reason: command.Reason);
                    }
                    catch (ArgumentException)
                    {
                        return new(false, "InvalidRequest", "Supply a valid email address and an audit reason without email addresses.");
                    }

                    LabelServiceResult<LabelAdministrationReceipt> result = await administration.SetTeamNotificationEmailAsync(
                        caller: caller,
                        change: change,
                        cancellationToken: token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Sequence: result.Result.Sequence, OperationId: result.Result.OperationId) : Failure(result.Error);
                }
            case "enroll":
                {
                    if (!AtIdentifier.TryParse(command.Target, out AtIdentifier? member))
                    {
                        return new(false, "InvalidRequest", "Supply a member DID or handle.");
                    }

                    LabelServiceResult<LabelAdministrationReceipt> result = await new LabelTeamService(administration, httpClient).EnrollAsync(
                        caller: caller, member: member, role: command.Role,
                        operationId: command.OperationId, reason: command.Reason, cancellationToken: token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Sequence: result.Result.Sequence, OperationId: result.Result.OperationId) : Failure(result.Error);
                }
            case "issueLabel":
            case "negateLabel":
                {
                    if (signingKeyRing is null)
                    {
                        return new(false, "IssuanceClosed", "This retired host has no private signing keys.");
                    }

                    LabelIssuanceService issuance = new(
                        issuer: caller.Issuer,
                        signingKey: signingKeyRing.GetSigningKey(signingKeyRing.ActiveKeyId),
                        store: store,
                        authorizer: new LabelTeamAuthorizer(administration, caller),
                        policyProvider: policy,
                        httpClient: httpClient,
                        timeProvider: clock);
                    Cid? cid = command.Cid is null ? null : new(command.Cid);
                    LabelTarget target;
                    if (command.Target.StartsWith("at://", StringComparison.Ordinal))
                    {
                        if (!AtUri.TryParse(command.Target, out AtUri? atUri))
                        {
                            return new(false, "InvalidRequest", "The record target is not a valid AT URI.");
                        }

                        target = LabelTarget.Record(atUri, cid);
                    }
                    else
                    {
                        target = LabelTarget.Account(AtIdentifier.Create(command.Target));
                    }

                    LabelOperationContext context = new(caller.Actor, command.Reason, command.CorrelationId);
                    LabelOperationRequest request = command.Method == "issueLabel"
                        ? LabelOperationRequest.Issue(
                            operationId: command.OperationId, target: target, value: command.Value,
                            context: context, expiresAt: command.ExpiresAt)
                        : LabelOperationRequest.Negate(
                            operationId: command.OperationId, target: target, value: command.Value, context: context);
                    LabelServiceResult<LabelOperationReceipt> result = command.ExpectedLabelSequence is { } sequence
                        ? await issuance.ExecuteObservedAsync(request, sequence, token).ConfigureAwait(false)
                        : await issuance.ExecuteAsync(request, token).ConfigureAwait(false);
                    return result.Succeeded ? Receipt(result.Result) : Failure(result.Error);
                }
            case "getOperation":
                {
                    LabelServiceResult<LabelAuthorizationGrant> grant = await administration.AuthorizeAsync(
                        caller: caller, permission: LabelAdministrationPermission.Inspect, reason: command.Reason, cancellationToken: token).ConfigureAwait(false);
                    if (!grant.Succeeded)
                    {
                        return Failure(grant.Error);
                    }

                    LabelServiceResult<LabelOperationReceipt> result = await store.GetOperationAsync(command.OperationId, token).ConfigureAwait(false);
                    if (result.Succeeded)
                    {
                        grant = await administration.AuthorizeAsync(
                            caller: caller, permission: LabelAdministrationPermission.Inspect, reason: command.Reason, cancellationToken: token).ConfigureAwait(false);
                        if (!grant.Succeeded)
                        {
                            return Failure(grant.Error);
                        }
                    }

                    return result.Succeeded ? Receipt(result.Result) : Failure(result.Error);
                }
            case "getPublicationState":
                {
                    LabelServiceResult<LabelAuthorizationGrant> grant = await administration.AuthorizeAsync(
                        caller: caller, permission: LabelAdministrationPermission.Inspect, reason: command.Reason, cancellationToken: token).ConfigureAwait(false);
                    if (!grant.Succeeded)
                    {
                        return Failure(grant.Error);
                    }

                    try
                    {
                        return new(true, State: PublicationView(await store.GetStateAsync(token).ConfigureAwait(false)));
                    }
                    catch (DbException)
                    {
                        return new(false, "StorageUnavailable", "Publication state could not be read; restore database availability.");
                    }
                }
            case "getMaintenanceState":
                {
                    LabelServiceResult<LabelAuthorizationGrant> authorization = await administration.AuthorizeAsync(
                        caller: caller,
                        permission: LabelAdministrationPermission.Inspect,
                        reason: command.Reason,
                        cancellationToken: token).ConfigureAwait(false);
                    if (!authorization.Succeeded)
                    {
                        return Failure(authorization.Error);
                    }

                    LabelMaintenanceState state = await maintenance.GetMaintenanceStateAsync(token).ConfigureAwait(false);
                    return new(true, Maintenance: MaintenanceView(state));
                }
            case "beginRetirement":
            case "advanceRetirement":
            case "beginGracePeriod":
            case "completeRetirement":
            case "purgeRetiredData":
            case "getRetirementState":
            case "prepareRetirement":
            case "pauseRetirement":
            case "resumeRetirement":
            case "extendGracePeriod":
            case "requestKeyRemoval":
            case "attestKeyRemoval":
            case "confirmRetirementHostStopped":
                {
                    return await DispatchRetirementAsync(caller, command, token).ConfigureAwait(false);
                }
            case "beginRotation":
            case "confirmHostStopped":
            case "captureRotationBoundary":
            case "verifyRotationKeyPublication":
            case "resignRotationBatch":
            case "completeRotation":
                {
                    return await DispatchRotationAsync(caller, command, token).ConfigureAwait(false);
                }
            default:
                {
                    LabelServiceResult<LabelAdministrationReceipt> result = await administration.AuditMaintenanceAsync(
                        caller: caller, operationId: command.OperationId, reason: command.Reason, cancellationToken: token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Sequence: result.Result.Sequence, OperationId: result.Result.OperationId) : Failure(result.Error);
                }
        }
    }

    /// <summary>Audits, authorizes and executes one resumable signing-key maintenance step.</summary>
    private async Task<AdministrationResponse> DispatchRotationAsync(
        LabelAdministrationCaller caller,
        AdministrationCommand command,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(command.OperationId) ||
            string.IsNullOrWhiteSpace(command.Reason) ||
            command.Method is "resignRotationBatch" && command.Limit is < 1 or > 1000 ||
            command.Method is "confirmHostStopped" && !command.Confirmed)
        {
            return new(false, "InvalidRequest", "Supply a stable operation ID, audit reason, and valid rotation options.");
        }

        LabelServiceResult<LabelAdministrationReceipt> intent = await administration.AuditMaintenanceAsync(
            caller: caller,
            operationId: command.OperationId,
            reason: command.Reason,
            phase: LabelMaintenanceAuditPhase.Intent,
            cancellationToken: token).ConfigureAwait(false);
        if (!intent.Succeeded)
        {
            return Failure(intent.Error);
        }

        LabelMaintenanceState state;
        try
        {
            state = command.Method switch
            {
                "beginRotation" => await BeginRotationAsync(command, caller, token).ConfigureAwait(false),
                "confirmHostStopped" => await ConfirmHostStoppedAsync(command, caller, token).ConfigureAwait(false),
                "captureRotationBoundary" => await maintenance.CaptureRotationBoundaryAsync(
                    operationId: command.OperationId,
                    cancellationToken: token,
                    caller: caller).ConfigureAwait(false),
                "verifyRotationKeyPublication" => await VerifyRotationKeyPublicationAsync(command, caller, token).ConfigureAwait(false),
                "resignRotationBatch" => await ResignRotationBatchAsync(command, caller, token).ConfigureAwait(false),
                "completeRotation" => await CompleteRotationAsync(command, caller, token).ConfigureAwait(false),
                _ => throw new InvalidOperationException("The rotation operation is not supported.")
            };
        }
        catch (ArgumentException exception)
        {
            await RecordRotationFailureAsync(caller, command, token).ConfigureAwait(false);
            return new(false, "InvalidRequest", exception.Message);
        }
        catch (KeyNotFoundException exception)
        {
            await RecordRotationFailureAsync(caller, command, token).ConfigureAwait(false);
            return new(false, "InvalidRequest", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            await RecordRotationFailureAsync(caller, command, token).ConfigureAwait(false);
            return new(false, "StaleDecision", exception.Message);
        }
        catch (InvalidDataException exception)
        {
            await RecordRotationFailureAsync(caller, command, token).ConfigureAwait(false);
            return new(false, "StorageUnavailable", exception.Message);
        }
        catch (HttpRequestException)
        {
            await RecordRotationFailureAsync(caller, command, token).ConfigureAwait(false);
            return new(false, "ServiceUnavailable", "Identity publication could not be resolved.");
        }

        LabelServiceResult<LabelAdministrationReceipt> completed = await administration.AuditMaintenanceAsync(
            caller: caller,
            operationId: command.OperationId,
            reason: command.Reason,
            phase: LabelMaintenanceAuditPhase.Completed,
            cancellationToken: token).ConfigureAwait(false);
        return completed.Succeeded
            ? new(true, OperationId: command.OperationId, Maintenance: MaintenanceView(state))
            : Failure(completed.Error);
    }

    /// <summary>Starts maintenance with a preinstalled replacement key and its public value.</summary>
    private Task<LabelMaintenanceState> BeginRotationAsync(
        AdministrationCommand command,
        LabelAdministrationCaller caller,
        CancellationToken token)
    {
        if (publicEndpoint is null)
        {
            throw new InvalidOperationException("Configure the public HTTPS labeler endpoint before starting key rotation.");
        }

        string replacementKeyId = command.ReplacementKeyId
            ?? throw new ArgumentException("A preinstalled replacement key identity is required.", nameof(command));
        string activeKeyId = SigningKeys.ActiveKeyId;
        return maintenance.BeginRotationAsync(
            operationId: command.OperationId,
            previousKeyId: activeKeyId,
            replacementKeyId: replacementKeyId,
            replacementPublicKey: SigningKeys.GetPublicKeyMultibase(replacementKeyId),
            cancellationToken: token,
            caller: caller);
    }

    /// <summary>Records an explicit, audited operator confirmation that a host is stopped.</summary>
    private Task<LabelMaintenanceState> ConfirmHostStoppedAsync(
        AdministrationCommand command,
        LabelAdministrationCaller caller,
        CancellationToken token)
    {
        if (!Guid.TryParse(command.Target, out Guid instanceId))
        {
            throw new ArgumentException("A participating host GUID is required.", nameof(command));
        }

        return maintenance.ConfirmHostStoppedAsync(
            operationId: command.OperationId,
            instanceId: instanceId,
            cancellationToken: token,
            caller: caller);
    }

    /// <summary>Resolves and verifies the configured public identity before recording key publication.</summary>
    private async Task<LabelMaintenanceState> VerifyRotationKeyPublicationAsync(
        AdministrationCommand command,
        LabelAdministrationCaller caller,
        CancellationToken token)
    {
        if (publicEndpoint is null)
        {
            throw new InvalidOperationException("The public labeler endpoint is not configured.");
        }

        LabelMaintenanceState state = await maintenance.GetMaintenanceStateAsync(token).ConfigureAwait(false);
        if (!string.Equals(state.OperationId, command.OperationId, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(state.ReplacementKeyId) ||
            state.ReplacementPublicKey is null)
        {
            throw new InvalidOperationException("The requested rotation does not have a fixed replacement key.");
        }

        DidDocument? document = await IdentityResolution.ResolveDidDocumentAsync(
            did: caller.Issuer,
            httpClient: httpClient,
            cancellationToken: token).ConfigureAwait(false) ?? throw new InvalidDataException("The labeler's public DID document could not be resolved.");
        LabelerIdentityDocument.VerifyPublication(
            didDocument: document,
            did: caller.Issuer,
            signingKey: SigningKeys.GetSigningKey(state.ReplacementKeyId),
            labelerEndpoint: publicEndpoint);
        return await maintenance.VerifyRotationKeyPublicationAsync(
            operationId: command.OperationId,
            replacementPublicKey: state.ReplacementPublicKey,
            cancellationToken: token,
            caller: caller).ConfigureAwait(false);
    }

    /// <summary>Re-signs one bounded batch from the persisted stream boundary and checkpoint.</summary>
    private async Task<LabelMaintenanceState> ResignRotationBatchAsync(
        AdministrationCommand command,
        LabelAdministrationCaller caller,
        CancellationToken token)
    {
        LabelMaintenanceState state = await maintenance.GetMaintenanceStateAsync(token).ConfigureAwait(false);
        if (!string.Equals(state.OperationId, command.OperationId, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(state.ReplacementKeyId))
        {
            throw new InvalidOperationException("The requested rotation has no replacement key.");
        }

        await LabelSigningKeyRotation.ResignNextBatchAsync(
            store: store,
            maintenanceStore: maintenance,
            operationId: command.OperationId,
            replacementKey: SigningKeys.GetSigningKey(state.ReplacementKeyId),
            batchSize: command.Limit,
            cancellationToken: token,
            caller: caller).ConfigureAwait(false);
        return await maintenance.GetMaintenanceStateAsync(token).ConfigureAwait(false);
    }

    /// <summary>Activates the local host key before releasing a fully acknowledged rotation.</summary>
    private async Task<LabelMaintenanceState> CompleteRotationAsync(
        AdministrationCommand command,
        LabelAdministrationCaller caller,
        CancellationToken token)
    {
        LabelMaintenanceState state = await maintenance.GetMaintenanceStateAsync(token).ConfigureAwait(false);
        if (!string.Equals(state.OperationId, command.OperationId, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(state.ReplacementKeyId))
        {
            throw new InvalidOperationException("The requested rotation has no replacement key.");
        }

        if (state.Phase != LabelMaintenancePhase.ReadyToReopen ||
            !state.KeyPublicationVerified ||
            !state.AllHostsDrained ||
            state.LastSequence != state.ThroughSequence)
        {
            throw new InvalidOperationException("Rotation re-signing and key publication must complete before activating the replacement key.");
        }

        if (!string.Equals(
            SigningKeys.GetPublicKeyMultibase(state.ReplacementKeyId),
            state.ReplacementPublicKey,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The locally installed replacement key does not match the verified published key.");
        }

        if (!string.Equals(SigningKeys.ActiveKeyId, state.ReplacementKeyId, StringComparison.Ordinal))
        {
            await SigningKeys.ActivateAsync(state.ReplacementKeyId, token).ConfigureAwait(false);
        }

        return await maintenance.CompleteRotationAsync(
            operationId: command.OperationId,
            activeKeyId: SigningKeys.ActiveKeyId,
            cancellationToken: token,
            caller: caller).ConfigureAwait(false);
    }

    /// <summary>Persists a failure phase without replacing the original operation outcome.</summary>
    private async Task RecordRotationFailureAsync(
        LabelAdministrationCaller caller,
        AdministrationCommand command,
        CancellationToken token)
    {
        LabelServiceResult<LabelAdministrationReceipt> result = await administration.AuditMaintenanceAsync(
            caller: caller,
            operationId: command.OperationId,
            reason: command.Reason,
            phase: LabelMaintenanceAuditPhase.Failed,
            cancellationToken: token).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("The rotation failure could not be audited.");
        }
    }

    /// <summary>Authorizes, audits and advances one bounded retirement lifecycle operation.</summary>
    private async Task<AdministrationResponse> DispatchRetirementAsync(
        LabelAdministrationCaller caller,
        AdministrationCommand command,
        CancellationToken token)
    {
        if (store is not ILabelRetirementStore retirement)
        {
            return new(false, "StorageUnavailable", "The provider does not implement durable retirement.");
        }

        try
        {
            if (command.Method == "advanceRetirement")
            {
                LabelServiceResult<LabelAuthorizationGrant> authority = await administration.AuthorizeAsync(
                    caller: caller, permission: LabelAdministrationPermission.Maintain, reason: command.Reason, cancellationToken: token).ConfigureAwait(false);
                if (!authority.Succeeded)
                {
                    return Failure(authority.Error);
                }

                LabelServiceError? error = await retirementWorker.AdvanceAsync(token).ConfigureAwait(false);
                if (error is not null)
                {
                    return Failure(error);
                }
            }

            string action = command.Method == "prepareRetirement" ? command.Target : command.Method;
            LabelRetirementCommand request = new(
                Action: action,
                OperationId: command.OperationId,
                Reason: command.Reason,
                GracePeriodDays: command.GracePeriodDays,
                Confirmation: command.Confirmation,
                Attestation: command.Attestation,
                Inventory: command.Inventory,
                HostInstanceId: command.HostInstanceId);
            if (command.Method is not ("prepareRetirement" or "getRetirementState" or "advanceRetirement"))
            {
                LabelServiceResult<LabelRetirementRecovery> recovered = await retirement.RecoverRetirementAsync(caller, request, token).ConfigureAwait(false);
                if (!recovered.Succeeded)
                {
                    return Failure(recovered.Error);
                }

                if (recovered.Result.Receipt is { } receipt)
                {
                    return new(true, OperationId: command.OperationId, State: PublicationView(await store.GetStateAsync(token).ConfigureAwait(false)), Retirement: receipt);
                }
            }

            string? verifiedKey = null;
            bool needsSigning = action == "beginRetirement" ||
                action == "resumeRetirement" && (await store.GetStateAsync(token).ConfigureAwait(false)).Phase == LabelerLifecyclePhase.Retiring;
            if (needsSigning)
            {
                if (signingKeyRing is null || publicEndpoint is null)
                {
                    return new(false, "SigningFailed", "Configure the selected signing key and public endpoint before starting or resuming retirement.");
                }

                LabelServiceResult<LabelAuthorizationGrant> authority = await administration.AuthorizeAsync(
                    caller: caller,
                    permission: LabelAdministrationPermission.Maintain,
                    reason: command.Reason,
                    cancellationToken: token).ConfigureAwait(false);
                if (!authority.Succeeded)
                {
                    return Failure(authority.Error);
                }

                try
                {
                    DidDocument? document = await IdentityResolution.ResolveDidDocumentAsync(
                        did: caller.Issuer,
                        httpClient: httpClient,
                        cancellationToken: token).ConfigureAwait(false);
                    if (document is null)
                    {
                        return new(false, "SigningFailed", "The public labeler DID document could not be resolved.");
                    }

                    LabelerIdentityDocument.VerifyPublication(
                        didDocument: document,
                        did: caller.Issuer,
                        signingKey: SigningKeys.GetSigningKey(SigningKeys.ActiveKeyId),
                        labelerEndpoint: publicEndpoint);
                    verifiedKey = SigningKeys.GetPublicKeyMultibase(SigningKeys.ActiveKeyId);
                }
                catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or KeyNotFoundException or HttpRequestException)
                {
                    return new(false, "SigningFailed", "Verify the selected key, public DID verification method and labeler endpoint before starting or resuming retirement.");
                }
            }

            request = request with { VerifiedSigningPublicKey = verifiedKey };
            LabelServiceResult<LabelRetirementView> result = command.Method switch
            {
                "getRetirementState" or "advanceRetirement" => await retirement.InspectRetirementAsync(caller, null, token).ConfigureAwait(false),
                "prepareRetirement" => await retirement.InspectRetirementAsync(caller, request, token).ConfigureAwait(false),
                _ => await retirement.ExecuteRetirementAsync(caller, request, token).ConfigureAwait(false)
            };
            return result.Succeeded
                ? new(true, OperationId: command.OperationId, State: PublicationView(await store.GetStateAsync(token).ConfigureAwait(false)), Retirement: result.Result)
                : Failure(result.Error);
        }
        catch (ArgumentException exception)
        {
            return new(false, "InvalidRequest", exception.Message);
        }
        catch (InvalidDataException)
        {
            return new(false, "StorageUnavailable", "Retirement history is inconsistent; preserve diagnostics and recover before resuming.");
        }
        catch (DbException)
        {
            return new(false, "StorageUnavailable", "Retirement state could not be read; restore database availability.");
        }
    }

    /// <summary>Projects every durable stream lifecycle checkpoint to the shared administration wire view.</summary>
    private static AdministrationPublicationView PublicationView(LabelStoreState state) =>
        new(state.StreamId, state.Issuer, state.IssuedThrough, state.PublishedThrough, state.Revision,
            state.ReplayFloor, state.Phase.ToString(), state.RetirementUpperBound, state.RetirementThrough, state.GraceDeadline);

    /// <summary>Projects durable key-rotation state without exposing private key material.</summary>
    private static AdministrationMaintenanceView MaintenanceView(LabelMaintenanceState state) =>
        new(state.OperationId, state.Phase.ToString(), state.StreamId, state.PreviousKeyId, state.ReplacementKeyId,
            state.ReplacementPublicKey, state.ThroughSequence, state.LastSequence, state.KeyPublicationVerified,
            [.. state.Hosts.Select(host => new AdministrationMaintenanceHostView(
                host.InstanceId, host.Acknowledged, host.KeyActivated, host.Stopped, host.LastSeen))]);

    /// <summary>Projects a successful label receipt without exposing private diagnostics.</summary>
    private static AdministrationResponse Receipt(LabelOperationReceipt receipt) =>
        new(true, Sequence: receipt.Label?.Sequence, OperationId: receipt.Request.OperationId, Changed: receipt.Changed);

    /// <summary>Projects a durable audit event into its wire representation.</summary>
    private static AdministrationAuditView AuditView(LabelAdministrationReceipt receipt) =>
        new(
            Sequence: receipt.Sequence,
            OperationId: receipt.OperationId,
            Actor: receipt.Actor?.Value,
            LocalIdentity: receipt.LocalIdentity,
            Action: receipt.Action,
            Reason: receipt.Reason,
            Succeeded: receipt.Succeeded,
            CreatedAt: receipt.CreatedAt);

    /// <summary>Surfaces only classified operational errors at the transport boundary.</summary>
    private static AdministrationResponse Failure(LabelServiceError error) =>
        new(false, error.Code.ToString(), error.Message);
}

/// <summary>Defines the bounded shared local/HTTPS command shape; actor identity is never accepted.</summary>
internal sealed record AdministrationCommand(
    string Method = "",
    string OperationId = "",
    string Reason = "",
    string Target = "",
    LabelTeamRole Role = LabelTeamRole.Viewer,
    LabelTeamAction? Action = null,
    string? Credential = null,
    string Value = "",
    string? Cid = null,
    DateTimeOffset? ExpiresAt = null,
    string? CorrelationId = null,
    long AfterSequence = 0,
    int Limit = 16,
    int GracePeriodDays = 0,
    bool Confirmed = false,
    string? ReplacementKeyId = null,
    ModerationCommand? Moderation = null,
    long? ExpectedLabelSequence = null,
    string? NotificationEmail = null,
    string? Confirmation = null,
    string? Attestation = null,
    string? Inventory = null,
    Guid? HostInstanceId = null)
{
    internal const int MaximumAuditPageSize = 16;

    /// <summary>Rejects JSON nulls explicitly on every supported target framework.</summary>
    internal void Validate()
    {
        if (Method is null || OperationId is null || Reason is null || Target is null || Value is null)
        {
            throw new System.Text.Json.JsonException("Command strings must not be null.");
        }
    }
}

/// <summary>Defines explicit transport outcomes without serializing internal exceptions.</summary>
internal sealed record AdministrationResponse(
    bool Succeeded,
    string? Error = null,
    string? Message = null,
    long? Sequence = null,
    string? OperationId = null,
    bool? Changed = null,
    AdministrationMemberView[]? Members = null,
    AdministrationAuditView[]? Audit = null,
    AdministrationPublicationView? State = null,
    AdministrationMaintenanceView? Maintenance = null,
    ModerationResponse? Moderation = null,
    long? TotalCount = null,
    long? PageNumber = null,
    AdministrationAuthorSummaryView[]? AuthorSummaries = null,
    string? Target = null,
    LabelRetirementView? Retirement = null);

/// <summary>Contains all-time issuer-local report counts for the authors on one authorized case page.</summary>
internal sealed record AdministrationAuthorSummaryView(string Did, long Reports, long Records, IReadOnlyDictionary<string, long> ReportKinds);

/// <summary>Contains public team inspection fields.</summary>
internal sealed record AdministrationMemberView(
    string Did,
    LabelTeamRole Role,
    bool Active,
    bool Removed,
    string? NotificationEmail = null);
/// <summary>Contains authorized audit inspection fields.</summary>
internal sealed record AdministrationAuditView(long Sequence, string OperationId, string? Actor, string? LocalIdentity, string Action, string Reason, bool Succeeded, DateTimeOffset? CreatedAt = null);
/// <summary>Contains authorized publication and lifecycle checkpoints.</summary>
internal sealed record AdministrationPublicationView(
    Guid StreamId,
    string Issuer,
    long IssuedThrough,
    long PublishedThrough,
    long Revision,
    long ReplayFloor,
    string Phase,
    long RetirementUpperBound,
    long RetirementThrough,
    DateTimeOffset? GraceDeadline);
/// <summary>Contains the resumable public signing-key rotation state.</summary>
internal sealed record AdministrationMaintenanceView(
    string? OperationId,
    string Phase,
    Guid StreamId,
    string? PreviousKeyId,
    string? ReplacementKeyId,
    string? ReplacementPublicKey,
    long ThroughSequence,
    long LastSequence,
    bool KeyPublicationVerified,
    AdministrationMaintenanceHostView[] Hosts);
/// <summary>Contains one participating host's maintenance and key activation status.</summary>
internal sealed record AdministrationMaintenanceHostView(
    Guid InstanceId,
    bool Acknowledged,
    bool KeyActivated,
    bool Stopped,
    DateTimeOffset LastSeen);

/// <summary>Supplies source-generated metadata for administration wire messages.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AdministrationCommand))]
[JsonSerializable(typeof(AdministrationResponse))]
[JsonSerializable(typeof(AdministrationAuditView))]
internal sealed partial class AdministrationJsonContext : JsonSerializerContext;

/// <summary>Bounds messages independently from untrusted content-length values.</summary>
internal static class AdministrationWire
{
    internal const int MaximumRequestBytes = 128 * 1024;
    internal const int MaximumResponseBytes = 4 * 1024 * 1024;

    /// <summary>Reads one length-prefixed local message or a bounded HTTP request body.</summary>
    internal static async Task<byte[]> ReadAsync(Stream stream, int maximum, CancellationToken token)
    {
        byte[] bytes = new byte[maximum + 1];
        int count = 0;
        while (count < bytes.Length)
        {
            int read = await stream.ReadAsync(bytes.AsMemory(count), token).ConfigureAwait(false);
            if (read == 0)
            {
                return bytes.AsSpan(0, count).ToArray();
            }

            count += read;
        }

        throw new System.Text.Json.JsonException("The administrative message exceeds its size limit.");
    }

    /// <summary>Serializes a response within the shared transport frame bound.</summary>
    internal static byte[] SerializeResponse(AdministrationResponse response, out AdministrationResponse boundedResponse)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(response, AdministrationJsonContext.Default.AdministrationResponse);
        if (payload.Length <= MaximumResponseBytes)
        {
            boundedResponse = response;
            return payload;
        }

        boundedResponse = new(false, "ResponseTooLarge", "The administrative response exceeds its size limit.");
        return JsonSerializer.SerializeToUtf8Bytes(boundedResponse, AdministrationJsonContext.Default.AdministrationResponse);
    }
}
