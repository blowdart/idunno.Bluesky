// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Dispatches both transports through the same typed authorization and application services.</summary>
internal sealed class AdministrationDispatcher(
    ILabelStore store,
    ILabelAdministrationStore administration,
    HttpClient httpClient,
    LabelSigningKey signingKey,
    ILabelIssuancePolicyProvider policy,
    TimeProvider clock)
{
    internal static readonly string[] s_methods =
    [
        "getTeam", "getAudit", "changeTeam", "enroll", "issueLabel", "negateLabel",
        "getOperation", "getPublicationState", "authorizeMaintenance"
    ];

    /// <summary>Validates one bounded command and invokes no transport-specific business logic.</summary>
    internal async Task<AdministrationResponse> DispatchAsync(
        LabelAdministrationCaller caller,
        AdministrationCommand command,
        CancellationToken token)
    {
        command.Validate();
        AdministrationResponse response = await DispatchCoreAsync(caller, command, token).ConfigureAwait(false);
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

        LabelIssuanceService issuance = new(
            issuer: caller.Issuer,
            signingKey: signingKey,
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
                    LabelTarget target = command.Target.StartsWith("at://", StringComparison.Ordinal)
                        ? LabelTarget.Record(new AtUri(command.Target), cid)
                        : LabelTarget.Account(AtIdentifier.Create(command.Target));
                    LabelOperationContext context = new(caller.Actor, command.Reason, command.CorrelationId);
                    LabelOperationRequest request = command.Method == "issueLabel"
                        ? LabelOperationRequest.Issue(
                            operationId: command.OperationId, target: target, value: command.Value,
                            context: context, expiresAt: command.ExpiresAt)
                        : LabelOperationRequest.Negate(
                            operationId: command.OperationId, target: target, value: command.Value, context: context);
                    LabelServiceResult<LabelOperationReceipt> result = await issuance.ExecuteAsync(request, token).ConfigureAwait(false);
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
                    return result.Succeeded ? new(true, State: new(result.Result.IssuedThrough, result.Result.PublishedThrough)) : Failure(result.Error);
                }
            default:
                {
                    LabelServiceResult<LabelAdministrationReceipt> result = await administration.AuditMaintenanceAsync(
                        caller: caller, operationId: command.OperationId, reason: command.Reason, cancellationToken: token).ConfigureAwait(false);
                    return result.Succeeded ? new(true, Sequence: result.Result.Sequence, OperationId: result.Result.OperationId) : Failure(result.Error);
                }
        }
    }

    /// <summary>Projects a successful label receipt without exposing private diagnostics.</summary>
    private static AdministrationResponse Receipt(LabelOperationReceipt receipt) =>
        new(true, Sequence: receipt.Label?.Sequence, OperationId: receipt.Request.OperationId, Changed: receipt.Changed);

    /// <summary>Projects a durable audit event into its wire representation.</summary>
    private static AdministrationAuditView AuditView(LabelAdministrationReceipt receipt) =>
        new(receipt.Sequence, receipt.OperationId, receipt.Actor?.Value, receipt.LocalIdentity, receipt.Action, receipt.Reason, receipt.Succeeded);

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
    int Limit = 100)
{
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
    AdministrationPublicationView? State = null);

/// <summary>Contains public team inspection fields.</summary>
internal sealed record AdministrationMemberView(string Did, LabelTeamRole Role, bool Active, bool Removed);
/// <summary>Contains authorized audit inspection fields.</summary>
internal sealed record AdministrationAuditView(long Sequence, string OperationId, string? Actor, string? LocalIdentity, string Action, string Reason, bool Succeeded);
/// <summary>Contains durable publication boundaries.</summary>
internal sealed record AdministrationPublicationView(long IssuedThrough, long PublishedThrough);

/// <summary>Supplies source-generated metadata for administration wire messages.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AdministrationCommand))]
[JsonSerializable(typeof(AdministrationResponse))]
internal sealed partial class AdministrationJsonContext : JsonSerializerContext;

/// <summary>Bounds messages independently from untrusted content-length values.</summary>
internal static class AdministrationWire
{
    internal const int MaximumRequestBytes = 16 * 1024;

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
}
