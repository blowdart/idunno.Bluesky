// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
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
    ILabelSigningKeyRing signingKeyRing,
    ILabelIssuancePolicyProvider policy,
    Uri? publicEndpoint,
    TimeProvider clock,
    LabelerHost? host)
{
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
        "getTeam", "getAudit", "changeTeam", "enroll", "issueLabel", "negateLabel",
        "getOperation", "getPublicationState", "getMaintenanceState", "authorizeMaintenance",
        "beginRetirement", "advanceRetirement", "beginGracePeriod", "completeRetirement",
        "purgeRetiredData", "beginRotation", "confirmHostStopped", "captureRotationBoundary",
        "verifyRotationKeyPublication", "resignRotationBatch", "completeRotation",
        .. s_moderationMethods
    ];
    internal static readonly string[] s_getMethods =
    [
        "getTeam", "getAudit", "getOperation", "getPublicationState", "getMaintenanceState"
    ];
    internal static readonly string[] s_postMethods =
    [
        "changeTeam", "enroll", "issueLabel", "negateLabel", "authorizeMaintenance",
        "beginRetirement", "advanceRetirement", "beginGracePeriod", "completeRetirement", "purgeRetiredData",
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
        bool labelBearing = command.Method is "issueLabel" or "negateLabel" ||
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

        LabelIssuanceService issuance = new(
            issuer: caller.Issuer,
            signingKey: signingKeyRing.GetSigningKey(signingKeyRing.ActiveKeyId),
            store: store,
            authorizer: new LabelTeamAuthorizer(administration, caller),
            policyProvider: policy,
            httpClient: httpClient,
            timeProvider: clock);
        switch (command.Method)
        {
            case "getTeam":
                {
                    LabelServiceResult<IReadOnlyList<LabelTeamMember>> result = await administration.GetTeamAsync(caller, token).ConfigureAwait(false);
                    return result.Succeeded
                        ? new(true, Members: [.. result.Result.Select(member => new AdministrationMemberView(
                            member.Did.Value, member.Role, member.Active, member.Removed))])
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
                    LabelServiceResult<LabelOperationReceipt> result = await issuance.GetOperationAsync(
                        operationId: command.OperationId, context: new(caller.Actor, command.Reason), cancellationToken: token).ConfigureAwait(false);
                    return result.Succeeded ? Receipt(result.Result) : Failure(result.Error);
                }
            case "getPublicationState":
                {
                    LabelServiceResult<LabelStoreState> result = await issuance.GetPublicationStateAsync(new(caller.Actor, command.Reason), token).ConfigureAwait(false);
                    return result.Succeeded
                        ? new(true, State: new(
                            result.Result.StreamId,
                            result.Result.Issuer,
                            result.Result.IssuedThrough,
                            result.Result.PublishedThrough,
                            result.Result.Revision,
                            result.Result.ReplayFloor,
                            result.Result.Phase.ToString(),
                            result.Result.RetirementUpperBound,
                            result.Result.RetirementThrough,
                            result.Result.GraceDeadline))
                        : Failure(result.Error);
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
        string activeKeyId = signingKeyRing.ActiveKeyId;
        return maintenance.BeginRotationAsync(
            operationId: command.OperationId,
            previousKeyId: activeKeyId,
            replacementKeyId: replacementKeyId,
            replacementPublicKey: signingKeyRing.GetPublicKeyMultibase(replacementKeyId),
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
            signingKey: signingKeyRing.GetSigningKey(state.ReplacementKeyId),
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
            replacementKey: signingKeyRing.GetSigningKey(state.ReplacementKeyId),
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
            signingKeyRing.GetPublicKeyMultibase(state.ReplacementKeyId),
            state.ReplacementPublicKey,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The locally installed replacement key does not match the verified published key.");
        }

        if (!string.Equals(signingKeyRing.ActiveKeyId, state.ReplacementKeyId, StringComparison.Ordinal))
        {
            await signingKeyRing.ActivateAsync(state.ReplacementKeyId, token).ConfigureAwait(false);
        }

        return await maintenance.CompleteRotationAsync(
            operationId: command.OperationId,
            activeKeyId: signingKeyRing.ActiveKeyId,
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
        if (string.IsNullOrWhiteSpace(command.Reason) ||
            string.IsNullOrWhiteSpace(command.OperationId) ||
            command.Method is "purgeRetiredData" && (!command.Confirmed || command.Limit is < 1 or > 1000) ||
            command.Method is "beginGracePeriod" && command.GracePeriodDays is not (>= 1 and <= 365))
        {
            return new(false, "InvalidRequest", "Supply an operation ID, reason, valid lifecycle options, and explicit purge confirmation.");
        }

        LabelServiceResult<LabelAuthorizationGrant> authorization = await administration.AuthorizeAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Maintain,
            reason: command.Reason,
            cancellationToken: token).ConfigureAwait(false);
        if (!authorization.Succeeded)
        {
            return Failure(authorization.Error);
        }

        string operationId = command.OperationId;
        LabelStoreState state;
        try
        {
            state = await store.GetStateAsync(token).ConfigureAwait(false);
            state = command.Method switch
            {
                "beginRetirement" => await store.BeginRetirementAsync(state.Revision, caller, token).ConfigureAwait(false),
                "advanceRetirement" => await AdvanceRetirementAsync(caller, state, token).ConfigureAwait(false),
                "beginGracePeriod" when command.GracePeriodDays is >= 1 and <= 365 =>
                    await store.BeginGracePeriodAsync(
                        expectedRevision: state.Revision,
                        gracePeriod: TimeSpan.FromDays(command.GracePeriodDays),
                        caller: caller,
                        cancellationToken: token).ConfigureAwait(false),
                "beginGracePeriod" => throw new InvalidOperationException("A valid grace period is required."),
                "completeRetirement" => await store.CompleteRetirementAsync(state.Revision, caller, token).ConfigureAwait(false),
                "purgeRetiredData" => await store.PurgeAsync(
                    expectedRevision: state.Revision,
                    confirmed: command.Confirmed,
                    limit: command.Limit,
                    cancellationToken: token,
                    caller: caller).ConfigureAwait(false),
                _ => throw new InvalidOperationException("The retirement operation is not supported.")
            };
        }
        catch (ArgumentException exception)
        {
            LabelServiceError error = new(
                code: LabelServiceErrorCode.InvalidTarget,
                message: "The retirement operation contains invalid input.",
                exception: exception);
            return Failure(error);
        }
        catch (InvalidOperationException exception)
        {
            LabelServiceError error = new(
                code: LabelServiceErrorCode.StaleDecision,
                message: "The retirement state changed or does not permit this transition.",
                exception: exception);
            LabelServiceResult<LabelAdministrationReceipt> failedAudit = await administration.AuditMaintenanceAsync(
                caller: caller,
                operationId: operationId,
                reason: command.Reason,
                phase: LabelMaintenanceAuditPhase.Failed,
                cancellationToken: token).ConfigureAwait(false);
            return failedAudit.Succeeded ? Failure(error) : Failure(failedAudit.Error);
        }
        catch (InvalidDataException exception)
        {
            LabelServiceError error = new(
                code: LabelServiceErrorCode.StorageUnavailable,
                message: "Retirement history or durable state is unavailable.",
                exception: exception);
            LabelServiceResult<LabelAdministrationReceipt> failedAudit = await administration.AuditMaintenanceAsync(
                caller: caller,
                operationId: operationId,
                reason: command.Reason,
                phase: LabelMaintenanceAuditPhase.Failed,
                cancellationToken: token).ConfigureAwait(false);
            return failedAudit.Succeeded ? Failure(error) : Failure(failedAudit.Error);
        }

        LabelServiceResult<LabelAdministrationReceipt> audit = await administration.AuditMaintenanceAsync(
            caller: caller,
            operationId: operationId,
            reason: command.Reason,
            phase: LabelMaintenanceAuditPhase.Completed,
            cancellationToken: token).ConfigureAwait(false);
        return audit.Succeeded ? new(true, State: PublicationView(state)) : Failure(audit.Error);
    }

    /// <summary>Publishes and examines one retirement page, appending only authorized signed negations with its checkpoint.</summary>
    private async Task<LabelStoreState> AdvanceRetirementAsync(
        LabelAdministrationCaller caller,
        LabelStoreState state,
        CancellationToken token)
    {
        LabelSigningKey signingKey = signingKeyRing.GetSigningKey(signingKeyRing.ActiveKeyId);
        if (state.Phase != LabelerLifecyclePhase.Retiring)
        {
            throw new InvalidOperationException("The labeler is not in the retirement enumeration phase.");
        }

        if (state.PublishedThrough < state.RetirementUpperBound)
        {
            state = await store.PublishAsync(limit: 250, cancellationToken: token).ConfigureAwait(false);
            if (state.PublishedThrough < state.RetirementUpperBound)
            {
                return state;
            }
        }

        LabelDistributionPage page = await store.ReadDistributionAsync(
            streamId: state.StreamId,
            after: state.RetirementThrough,
            through: state.RetirementUpperBound,
            uriPatterns: [],
            sources: [],
            limit: 250,
            scanLimit: 1_000,
            cancellationToken: token).ConfigureAwait(false);
        if (page.ScannedThrough <= state.RetirementThrough)
        {
            throw new InvalidDataException("Retirement enumeration did not advance its durable checkpoint.");
        }

        string batchOperationId = $"retire:{state.StreamId:N}:{state.RetirementUpperBound}:{page.ScannedThrough}";
        LabelAppendReceipt? receipt = await store.GetReceiptAsync(batchOperationId, token).ConfigureAwait(false);
        if (receipt is not null)
        {
            return await store.GetStateAsync(token).ConfigureAwait(false);
        }

        Dictionary<string, DateTimeOffset> latestTimes = new(StringComparer.Ordinal);
        List<StoredLabel> candidates = [];
        List<(LabelTarget Target, string Value)> decisionQueries = [];
        List<SignedLabelSnapshot> negations = [];
        DateTimeOffset now = clock.GetUtcNow();
        foreach (StoredLabel retained in page.Labels)
        {
            Label prior = retained.SignedLabel.Label;
            if (prior.IsNegationLabel ||
                prior.ExpiresAt is { } expiry && expiry <= now)
            {
                continue;
            }

            if (!TryCreateTarget(prior, out LabelTarget? target) || target is null)
            {
                throw new InvalidDataException("A retained label has an invalid subject URI.");
            }

            candidates.Add(retained);
            decisionQueries.Add((target, prior.Value));
        }

        IReadOnlyList<DateTimeOffset?> latestTimestamps = await store.ReadRetirementTimestampsAsync(
            decisionQueries,
            token).ConfigureAwait(false);
        for (int index = 0; index < candidates.Count; index++)
        {
            StoredLabel retained = candidates[index];
            Label prior = retained.SignedLabel.Label;
            string decisionKey = prior.Uri + "\0" + prior.Value;
            DateTimeOffset latestTimestamp = latestTimestamps[index] ?? DateTimeOffset.MinValue;
            if (latestTimes.TryGetValue(decisionKey, out DateTimeOffset batchPrevious) &&
                batchPrevious > latestTimestamp)
            {
                latestTimestamp = batchPrevious;
            }

            DateTimeOffset created;
            if (now > latestTimestamp)
            {
                created = now;
            }
            else
            {
                if (latestTimestamp == DateTimeOffset.MaxValue)
                {
                    throw new InvalidOperationException("No representable timestamp can supersede retained history.");
                }

                created = latestTimestamp.AddTicks(1);
            }

            latestTimes[decisionKey] = created;
            negations.Add(new SignedLabelSnapshot(signingKey.CreateNegation(
                source: caller.Issuer,
                uri: prior.Uri,
                value: prior.Value,
                creationTimestamp: created,
                cid: prior.Cid)));
        }

        await store.AppendAsync(
            operationId: batchOperationId,
            labels: negations,
            expectedRevision: state.Revision,
            retirementThrough: page.ScannedThrough,
            cancellationToken: token,
            caller: caller).ConfigureAwait(false);
        return await store.GetStateAsync(token).ConfigureAwait(false);
    }

    /// <summary>Builds a typed decision target from a signed label's account DID or record AT URI.</summary>
    private static bool TryCreateTarget(Label label, out LabelTarget? target)
    {
        target = null;
        if (Did.TryParse(label.Uri, out Did? did))
        {
            target = LabelTarget.Account(did);
            return label.Cid is null;
        }

        if (AtUri.TryParse(label.Uri, out AtUri? uri))
        {
            target = LabelTarget.Record(uri, label.Cid);
            return true;
        }

        return false;
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
    long? ExpectedLabelSequence = null)
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
    string? Target = null);

/// <summary>Contains all-time issuer-local report counts for the authors on one authorized case page.</summary>
internal sealed record AdministrationAuthorSummaryView(string Did, long Reports, long Records, IReadOnlyDictionary<string, long> ReportKinds);

/// <summary>Contains public team inspection fields.</summary>
internal sealed record AdministrationMemberView(string Did, LabelTeamRole Role, bool Active, bool Removed);
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
