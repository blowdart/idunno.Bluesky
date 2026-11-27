// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto.Space;

namespace idunno.AtProto;

public partial class AtProtoAgent
{
    /// <summary>Obtains a verified, single-use delegation token using the account session.</summary>
    /// <param name="space">The space reference, not a record URI.</param>
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The verified delegation token and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException">The space is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The URI addresses a record rather than a space.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    /// <exception cref="InvalidDataException">The returned token cannot be verified.</exception>
    /// <remarks>
    /// <para>The delegation is not a membership grant. The token expires after at most one minute and can be
    /// passed to <see cref="AuthorizeSpaceSync"/> only once.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    public Task<AtProtoHttpResult<SpaceDelegationToken>> GetSpaceDelegationToken(
        SpaceAtUri space,
        string? serviceProxy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(space);
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return AtProtoServer.GetSpaceDelegationTokenCore(
            space: space,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            serviceProxy: serviceProxy,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            clock: Clock,
            plcDirectory: Options?.PlcDirectoryServer,
            cancellationToken: cancellationToken);
    }

    /// <summary>Discovers the space authority and obtains a fresh, verified credential for space reads.</summary>
    /// <param name="space">The space reference, not a record URI.</param>
    /// <param name="delegationToken">The optional user delegation token, consumed by this attempt.</param>
    /// <param name="clientAttestation">The optional application-signed client-attestation JWT.</param>
    /// <param name="notify">The optional notification registration.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The verified credential owned by the caller, and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException">The space is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The URI addresses a record or the authority cannot be discovered.</exception>
    /// <exception cref="InvalidOperationException">The delegation is expired or has already been consumed.</exception>
    /// <exception cref="InvalidDataException">The issued credential cannot be verified.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>SpaceNotFound</c>, <c>SpaceDeleted</c>, <c>AccountNotAuthorized</c>,
    /// <c>AppNotAuthorized</c>, <c>NotAuthorized</c>, <c>InvalidDelegationToken</c>, <c>InvalidClientAttestation</c>,
    /// <c>RegistrationNotConfirmed</c>, <c>RepoTakendown</c> or <c>RepoDeactivated</c>.</para>
    /// <para>This method does not require an account session and never forwards one to the authority.
    /// Dispose successful credentials when finished. Authorize again to renew; every authorization uses a new key.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    public async Task<AtProtoHttpResult<SpaceCredential>> AuthorizeSpaceSync(
        SpaceAtUri space,
        SpaceDelegationToken? delegationToken = null,
        string? clientAttestation = null,
        SpaceNotificationRegistration? notify = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(space);
        if (space.IsRecord)
        {
            throw new ArgumentException("A space reference is required.", nameof(space));
        }

        (_, Uri service) = await ResolveSyncRepo(space.SpaceDid, cancellationToken).ConfigureAwait(false);
        return await AtProtoServer.AuthorizeSpaceSyncCore(
            space: space,
            service: service,
            httpClient: HttpClient,
            delegationToken: delegationToken,
            clientAttestation: clientAttestation,
            notify: notify,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            clock: Clock,
            plcDirectory: Options?.PlcDirectoryServer,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
