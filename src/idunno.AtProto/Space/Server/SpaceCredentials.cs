// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

using idunno.AtProto.Authentication;
using idunno.AtProto.Space;
using idunno.AtProto.Space.Models;

using Microsoft.Extensions.Logging;

namespace idunno.AtProto;

public static partial class AtProtoServer
{
    /// <summary>Obtains and verifies a single-use delegation token for a space.</summary>
    /// <param name="space">The space reference, not a record URI.</param>
    /// <param name="service">The authenticated user's PDS.</param>
    /// <param name="accessCredentials">The user's credentials for that PDS.</param>
    /// <param name="httpClient">The HTTP client used for the request and issuer DID resolution.</param>
    /// <param name="serviceProxy">The service proxy, if any.</param>
    /// <param name="onCredentialsUpdated">The callback for updated account credentials.</param>
    /// <param name="loggerFactory">The optional logger factory.</param>
    /// <param name="maximumResponseSize">The maximum buffered response size.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The verified delegation token and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The URI addresses a record rather than a space.</exception>
    /// <exception cref="AccessTokenException">The account credentials do not match the service.</exception>
    /// <exception cref="InvalidDataException">The returned token fails issuer, audience, subject, signature or lifetime verification.</exception>
    /// <remarks>
    /// <para>The client must protect DID resolution against SSRF and disable automatic redirects.
    /// Delegation does not grant membership; the space authority decides whether to issue a credential.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    public static Task<AtProtoHttpResult<SpaceDelegationToken>> GetSpaceDelegationToken(
        SpaceAtUri space,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        string? serviceProxy = null,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default) =>
        GetSpaceDelegationTokenCore(
            space: space,
            service: service,
            accessCredentials: accessCredentials,
            httpClient: httpClient,
            serviceProxy: serviceProxy,
            onCredentialsUpdated: onCredentialsUpdated,
            loggerFactory: loggerFactory,
            maximumResponseSize: maximumResponseSize,
            clock: TimeProvider.System,
            plcDirectory: null,
            cancellationToken: cancellationToken);

    /// <summary>Obtains a delegation token using the agent's clock for verification and expiry.</summary>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The response is registered in SpaceSourceGenerationContext.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The response is registered in SpaceSourceGenerationContext.")]
    internal static async Task<AtProtoHttpResult<SpaceDelegationToken>> GetSpaceDelegationTokenCore(
        SpaceAtUri space,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        string? serviceProxy,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated,
        ILoggerFactory? loggerFactory,
        int maximumResponseSize,
        TimeProvider clock,
        Uri? plcDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);
        ValidateSpaceReference(space);
        EnsureCredentialsMatchService(service, accessCredentials);
        cancellationToken.ThrowIfCancellationRequested();
        AtProtoHttpClient<SpaceDelegationResponse> client = CreateClient<SpaceDelegationResponse>(serviceProxy, loggerFactory, maximumResponseSize);
        AtProtoHttpResult<SpaceDelegationResponse> response = await client.Get(
            service: service,
            endpoint: $"/xrpc/com.atproto.space.getDelegationToken?space={Uri.EscapeDataString(space.ToString())}",
            credentials: accessCredentials,
            httpClient: httpClient,
            onCredentialsUpdated: onCredentialsUpdated,
            jsonSerializerOptions: AtProtoJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        SpaceDelegationToken? result = null;
        if (response.Succeeded)
        {
            string token = response.Result.Token ?? throw new InvalidDataException("The delegation response contains no token.");
            (_, DateTimeOffset expires, _) = await SpaceToken.Verify(
                token: token,
                space: space,
                issuer: accessCredentials.Did,
                credentialKey: null,
                httpClient: httpClient,
                clock: clock,
                plcDirectory: plcDirectory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            result = new SpaceDelegationToken(
                token: token,
                space: space,
                issuer: accessCredentials.Did,
                expiresAt: expires,
                clock: clock);
        }

        return new AtProtoHttpResult<SpaceDelegationToken>(
            result: result,
            statusCode: response.StatusCode,
            httpResponseHeaders: response.HttpResponseHeaders,
            atErrorDetail: response.AtErrorDetail,
            rateLimit: response.RateLimit);
    }

    /// <summary>Authorizes space reads and creates a fresh, privately bound credential.</summary>
    /// <param name="space">The space reference, not a record URI.</param>
    /// <param name="service">The space authority's host.</param>
    /// <param name="httpClient">The HTTP client used for the request and authority DID resolution.</param>
    /// <param name="delegationToken">The optional, single-use user delegation token.</param>
    /// <param name="clientAttestation">The optional client-attestation JWT signed by the application's published key.</param>
    /// <param name="notify">The optional notification registration.</param>
    /// <param name="loggerFactory">The optional logger factory.</param>
    /// <param name="maximumResponseSize">The maximum buffered response size.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The verified credential owned by the caller, and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The URI addresses a record or the delegation belongs to another space.</exception>
    /// <exception cref="InvalidOperationException">The delegation token is expired or has already been consumed.</exception>
    /// <exception cref="InvalidDataException">The returned credential fails token or key-binding verification.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>SpaceNotFound</c>, <c>SpaceDeleted</c>, <c>AccountNotAuthorized</c>,
    /// <c>AppNotAuthorized</c>, <c>NotAuthorized</c>, <c>InvalidDelegationToken</c>, <c>InvalidClientAttestation</c>,
    /// <c>RegistrationNotConfirmed</c>, <c>RepoTakendown</c> or <c>RepoDeactivated</c>.</para>
    /// <para>Each call generates a new P-256 key. Dispose successful credentials when no longer needed.
    /// The client must protect DID resolution against SSRF and disable automatic redirects.
    /// No account-session credentials are sent to the authority.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    public static Task<AtProtoHttpResult<SpaceCredential>> AuthorizeSpaceSync(
        SpaceAtUri space,
        Uri service,
        HttpClient httpClient,
        SpaceDelegationToken? delegationToken = null,
        string? clientAttestation = null,
        SpaceNotificationRegistration? notify = null,
        ILoggerFactory? loggerFactory = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default) =>
        AuthorizeSpaceSyncCore(
            space: space,
            service: service,
            httpClient: httpClient,
            delegationToken: delegationToken,
            clientAttestation: clientAttestation,
            notify: notify,
            loggerFactory: loggerFactory,
            maximumResponseSize: maximumResponseSize,
            clock: TimeProvider.System,
            plcDirectory: null,
            cancellationToken: cancellationToken);

    /// <summary>Authorizes with a fresh proof key and destroys it on every unsuccessful path.</summary>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The request and response are registered in SpaceSourceGenerationContext.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The request and response are registered in SpaceSourceGenerationContext.")]
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The verified SpaceCredential owns the key and its timer and is returned to the caller in the HTTP result.")]
    internal static async Task<AtProtoHttpResult<SpaceCredential>> AuthorizeSpaceSyncCore(
        SpaceAtUri space,
        Uri service,
        HttpClient httpClient,
        SpaceDelegationToken? delegationToken,
        string? clientAttestation,
        SpaceNotificationRegistration? notify,
        ILoggerFactory? loggerFactory,
        int maximumResponseSize,
        TimeProvider clock,
        Uri? plcDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);
        ValidateSpaceReference(space);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResponseSize);
        if (clientAttestation is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(clientAttestation);
        }

        cancellationToken.ThrowIfCancellationRequested();
        ECDsa? key = null;
        try
        {
            key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            Did keyId = SpaceCrypto.ToDidKey(key.ExportParameters(false));
            AuthorizeSpaceSyncRequest request = new(
                Space: space,
                CredentialKey: keyId,
                DelegationToken: delegationToken?.Consume(space),
                ClientAttestation: clientAttestation,
                Notify: notify);
            AtProtoHttpClient<AuthorizeSpaceSyncResponse> client = CreateClient<AuthorizeSpaceSyncResponse>(null, loggerFactory, maximumResponseSize);
            AtProtoHttpResult<AuthorizeSpaceSyncResponse> response = await client.Post(
                service: service,
                endpoint: "/xrpc/com.atproto.space.authorizeSpaceSync",
                record: request,
                requestHeaders: null,
                credentials: null,
                httpClient: httpClient,
                jsonSerializerOptions: AtProtoJsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            SpaceCredential? result = null;
            if (response.Succeeded)
            {
                string token = response.Result.Credential ?? throw new InvalidDataException("The authorization response contains no credential.");
                (DateTimeOffset issued, DateTimeOffset expires, string tokenId) = await SpaceToken.Verify(
                    token: token,
                    space: space,
                    issuer: space.SpaceDid,
                    credentialKey: keyId,
                    httpClient: httpClient,
                    clock: clock,
                    plcDirectory: plcDirectory,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                result = new SpaceCredential(
                    space: space,
                    keyId: keyId,
                    token: token,
                    key: key,
                    issuedAt: issued,
                    expiresAt: expires,
                    tokenId: tokenId,
                    spaceRev: response.Result.SpaceRev,
                    clock: clock);
                key = null;
            }

            return new AtProtoHttpResult<SpaceCredential>(
                result: result,
                statusCode: response.StatusCode,
                httpResponseHeaders: response.HttpResponseHeaders,
                atErrorDetail: response.AtErrorDetail,
                rateLimit: response.RateLimit);
        }
        finally
        {
            key?.Dispose();
        }
    }
}
