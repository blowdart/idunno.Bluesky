// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Issuance;

namespace idunno.Bluesky.Labeler.Administration;

/// <summary>Connects authenticated administration evidence to the shared label issuance boundary.</summary>
/// <param name="store">The transaction-capable team provider.</param>
/// <param name="caller">The authenticated invocation evidence.</param>
/// <exception cref="ArgumentNullException">A required dependency is <see langword="null"/>.</exception>
public sealed class LabelTeamAuthorizer(ILabelAdministrationStore store, LabelAdministrationCaller caller) : ILabelIssuanceAuthorizer
{
    private readonly ILabelAdministrationStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly LabelAdministrationCaller _caller = caller ?? throw new ArgumentNullException(nameof(caller));

    /// <inheritdoc/>
    public Task<LabelServiceResult<LabelAuthorizationGrant>> AuthorizeAsync(
        LabelAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Issuer != _caller.Issuer || request.Context.Actor != _caller.Actor)
        {
            return Task.FromResult(LabelServiceResult<LabelAuthorizationGrant>.Failure(new(
                code: LabelServiceErrorCode.Unauthorized,
                message: "The supplied actor or issuer differs from the authenticated invocation.")));
        }

        return _store.AuthorizeAsync(
            caller: _caller,
            permission: request.Action is LabelAuthorizationAction.Issue or LabelAuthorizationAction.Negate
                ? LabelAdministrationPermission.Moderate : LabelAdministrationPermission.Inspect,
            reason: request.Context.Reason,
            cancellationToken: cancellationToken);
    }
}
