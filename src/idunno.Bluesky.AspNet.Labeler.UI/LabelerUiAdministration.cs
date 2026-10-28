// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Administration;
namespace idunno.Bluesky.AspNet.Labeler.UI;

/// <summary>Bridges validated browser identities to the shared administration dispatcher.</summary>
/// <param name="administration">The provider that issues live browser caller evidence.</param>
/// <param name="dispatcher">The shared command dispatcher.</param>
internal sealed class LabelerUiAdministration(
    ILabelAdministrationStore administration,
    idunno.Bluesky.AspNet.Labeler.AdministrationDispatcher dispatcher)
{
    /// <summary>Checks live administrator publication authority without treating policy inspection as permission to write.</summary>
    internal async Task<AdministrationResponse> AuthorizePolicyPublicationAsync(
        ClaimsPrincipal principal,
        string reason,
        CancellationToken token)
    {
        LabelServiceResult<LabelAdministrationCaller> caller = await AuthenticateAsync(principal, token).ConfigureAwait(false);
        if (!caller.Succeeded)
        {
            return new(false, caller.Error.Code.ToString(), caller.Error.Message);
        }
        LabelServiceResult<idunno.Bluesky.Labeler.Issuance.LabelAuthorizationGrant> grant = await administration.AuthorizeAsync(
            caller: caller.Result,
            permission: LabelAdministrationPermission.ManagePolicy,
            reason: reason,
            cancellationToken: token).ConfigureAwait(false);
        return grant.Succeeded ? new(true) : new(false, grant.Error.Code.ToString(), grant.Error.Message);
    }

    /// <summary>Resolves a table filter identity only after checking current inspection authority.</summary>
    internal async Task<AdministrationResponse> ResolveTableIdentityAsync(
        ClaimsPrincipal principal,
        string actor,
        LabelTableKind kind,
        CancellationToken token)
    {
        AdministrationResponse authority = await ReadTableAsync(
            principal: principal,
            query: new(kind, Sort: kind == LabelTableKind.Team ? LabelTableSort.Subject : LabelTableSort.Number),
            cancellationToken: token).ConfigureAwait(false);
        return authority.Succeeded
            ? await dispatcher.ResolveTableIdentityAsync(actor, token).ConfigureAwait(false)
            : authority;
    }

    /// <summary>Authenticates and audits a browser export request before any download bytes are sent.</summary>
    internal async Task<AdministrationResponse> BeginAuditExportAsync(
        ClaimsPrincipal principal,
        LabelTableQuery query,
        CancellationToken cancellationToken)
    {
        LabelServiceResult<LabelAdministrationCaller> caller = await AuthenticateAsync(principal, cancellationToken).ConfigureAwait(false);
        if (!caller.Succeeded)
        {
            return new(false, caller.Error.Code.ToString(), caller.Error.Message);
        }

        return await dispatcher.BeginAuditExportAsync(caller.Result, query, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reauthenticates the browser before every bounded export read so revocation cannot be hidden by a long-lived response.</summary>
    internal async Task<AdministrationResponse> ReadAuditExportPageAsync(
        ClaimsPrincipal principal,
        LabelTableQuery query,
        CancellationToken cancellationToken)
    {
        LabelServiceResult<LabelAdministrationCaller> caller = await AuthenticateAsync(principal, cancellationToken).ConfigureAwait(false);
        if (!caller.Succeeded)
        {
            return new(false, caller.Error.Code.ToString(), caller.Error.Message);
        }

        return await dispatcher.ReadAuditExportPageAsync(caller.Result, query, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Authenticates browser caller evidence before invoking an authorized, counted table read.</summary>
    internal async Task<AdministrationResponse> ReadTableAsync(
        ClaimsPrincipal principal,
        LabelTableQuery query,
        CancellationToken cancellationToken)
    {
        LabelServiceResult<LabelAdministrationCaller> caller = await AuthenticateAsync(principal, cancellationToken).ConfigureAwait(false);
        if (!caller.Succeeded)
        {
            return new(false, caller.Error.Code.ToString(), caller.Error.Message);
        }

        return await dispatcher.ReadTableAsync(caller.Result, query, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Dispatches one operation without accepting actor identity from form data.</summary>
    internal async Task<idunno.Bluesky.AspNet.Labeler.AdministrationResponse> DispatchAsync(
        ClaimsPrincipal principal,
        idunno.Bluesky.AspNet.Labeler.AdministrationCommand command,
        CancellationToken cancellationToken)
    {
        LabelServiceResult<LabelAdministrationCaller> caller = await AuthenticateAsync(principal, cancellationToken).ConfigureAwait(false);
        if (!caller.Succeeded)
        {
            return new(false, caller.Error.Code.ToString(), caller.Error.Message);
        }

        return await dispatcher.DispatchAsync(caller.Result, command, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Validates the authenticated principal and obtains fresh, provider-backed caller evidence rather than trusting browser-supplied actor values.</summary>
    private Task<LabelServiceResult<LabelAdministrationCaller>> AuthenticateAsync(ClaimsPrincipal principal, CancellationToken token)
    {
        if (principal.Identity?.IsAuthenticated is not true ||
            principal.FindFirst(AtProtoClaims.Did)?.Value is not string value ||
            !Did.TryParse(value, out Did? actor))
        {
            return Task.FromResult(LabelServiceResult<LabelAdministrationCaller>.Failure(new(
                code: LabelServiceErrorCode.Unauthorized,
                message: "Sign in with an authenticated Bluesky identity.")));
        }

        return administration.AuthenticateBrowserAsync(actor, token);
    }
}
