// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler;

/// <summary>Verifies enrollment identities before applying provider-backed administrative changes.</summary>
/// <param name="store">The issuer-bound administrative provider.</param>
/// <param name="httpClient">The caller-owned, configured identity-resolution client.</param>
/// <exception cref="ArgumentNullException">A required dependency is <see langword="null"/>.</exception>
public sealed class LabelTeamService(ILabelAdministrationStore store, HttpClient httpClient)
{
    private readonly ILabelAdministrationStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    /// <summary>Verifies and bootstraps the first administrator through an OS-authorized setup call.</summary>
    /// <param name="administrator">The administrator's DID or handle.</param>
    /// <param name="localIdentity">The OS-established setup UID or SID.</param>
    /// <param name="operationId">The stable setup retry identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The durable bootstrap outcome.</returns>
    /// <remarks><para>This is a trusted setup API, never an anonymous HTTP endpoint. The caller must
    /// enforce OS setup authorization before invoking it. Root-only VPS setup is supported.</para></remarks>
    public async Task<LabelServiceResult<LabelAdministrationReceipt>> BootstrapAsync(
        AtIdentifier administrator,
        string localIdentity,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        LabelServiceResult<Did> identity = await VerifyAsync(administrator, cancellationToken).ConfigureAwait(false);
        return identity.Succeeded
            ? await _store.BootstrapAsync(
                administrator: identity.Result,
                localIdentity: localIdentity,
                operationId: operationId,
                cancellationToken: cancellationToken).ConfigureAwait(false)
            : LabelServiceResult<LabelAdministrationReceipt>.Failure(identity.Error);
    }

    /// <summary>Resolves and verifies a new member without trusting mutable handles for authorization.</summary>
    /// <param name="caller">The authenticated administrator.</param>
    /// <param name="member">The member DID or handle.</param>
    /// <param name="role">The assigned role.</param>
    /// <param name="operationId">The stable retry identity.</param>
    /// <param name="reason">The audit reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The durable enrollment outcome.</returns>
    public async Task<LabelServiceResult<LabelAdministrationReceipt>> EnrollAsync(
        LabelAdministrationCaller caller,
        AtIdentifier member,
        LabelTeamRole role,
        string operationId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        LabelServiceResult<LabelAuthorizationGrant> admission = await _store.AuthorizeAsync(
            caller: caller,
            permission: LabelAdministrationPermission.ManageTeam,
            reason: reason,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!admission.Succeeded)
        {
            return LabelServiceResult<LabelAdministrationReceipt>.Failure(admission.Error);
        }

        LabelServiceResult<Did> identity = await VerifyAsync(member, cancellationToken).ConfigureAwait(false);
        return identity.Succeeded
            ? await _store.ChangeTeamAsync(
                caller: caller,
                change: new(
                    operationId: operationId,
                    action: LabelTeamAction.Enroll,
                    target: identity.Result,
                    reason: reason,
                    role: role),
                cancellationToken: cancellationToken).ConfigureAwait(false)
            : LabelServiceResult<LabelAdministrationReceipt>.Failure(identity.Error);
    }

    /// <summary>Verifies matching DID documents and bidirectional handles using the shared identity client.</summary>
    private async Task<LabelServiceResult<Did>> VerifyAsync(AtIdentifier identifier, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        try
        {
            Did? did = identifier as Did;
            if (identifier is Handle handle)
            {
                did = await IdentityResolution.ResolveHandleAsync(
                    handle: handle, httpClient: _httpClient, cancellationToken: token).ConfigureAwait(false);
                if (did is null || !await IdentityResolution.VerifyHandleAsync(
                    handle: handle, did: did, httpClient: _httpClient, cancellationToken: token).ConfigureAwait(false))
                {
                    return InvalidIdentity("The handle is not verified in both directions.");
                }
            }

            if (did is null)
            {
                return InvalidIdentity("The account identifier is not supported.");
            }

            DidDocument? document = await IdentityResolution.ResolveDidDocumentAsync(
                did: did, httpClient: _httpClient, cancellationToken: token).ConfigureAwait(false);
            return document?.Id == did ? LabelServiceResult<Did>.Success(did) :
                InvalidIdentity("The DID document does not establish the requested identity.");
        }
        catch (HttpRequestException exception)
        {
            return LabelServiceResult<Did>.Failure(new(
                code: LabelServiceErrorCode.TargetResolutionFailed,
                message: "Member identity verification failed.",
                exception: exception));
        }
        catch (OperationCanceledException exception) when (!token.IsCancellationRequested)
        {
            return LabelServiceResult<Did>.Failure(new(
                code: LabelServiceErrorCode.TargetResolutionFailed,
                message: "Member identity verification timed out.",
                exception: exception));
        }
    }

    /// <summary>Returns an identity failure without granting partial enrollment.</summary>
    private static LabelServiceResult<Did> InvalidIdentity(string message) =>
        LabelServiceResult<Did>.Failure(new(code: LabelServiceErrorCode.TargetResolutionFailed, message: message));
}

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
