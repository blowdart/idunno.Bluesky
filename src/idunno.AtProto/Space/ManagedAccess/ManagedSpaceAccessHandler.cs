// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>
/// Handles <c>com.atproto.managedspace.checkAccess</c> requests for a managing application, independently of any web framework.
/// </summary>
/// <remarks>
/// <para>A space whose policy is <see cref="ManagedSpacePolicy"/> delegates access decisions to the application named in
/// the policy's <c>managingApp</c> property, a <c>did#serviceId</c> service reference. The space authority's PDS resolves that
/// DID, reads the matching service entry from the DID document, and sends
/// <c>GET {serviceEndpoint}/xrpc/com.atproto.managedspace.checkAccess?space=…&amp;repo=…&amp;clientId=…</c> with a service
/// token issued by the space authority, addressed to the <c>managingApp</c> value, and bound to this method.</para>
/// <para>This handler verifies that token, requires its issuer to be the requested space's authority, rejects replays, and then
/// asks an <see cref="IManagedSpaceAccessDecider"/> for the decision. A check for the space authority's own repository is always
/// granted read and write access without calling the decider.</para>
/// <para>Hosts must serve the handler over HTTPS, pass the request's single <c>Authorization</c> header and query values, and
/// reject repeated values before calling <see cref="HandleAsync(string?, string?, string?, string?, CancellationToken)"/>.</para>
/// </remarks>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
public sealed class ManagedSpaceAccessHandler
{
    /// <summary>
    /// The XRPC method handled by this class.
    /// </summary>
    public const string MethodName = "com.atproto.managedspace.checkAccess";

    /// <summary>
    /// The request path at which the method must be served.
    /// </summary>
    public const string Path = "/xrpc/" + MethodName;

    private const string BearerPrefix = "Bearer ";
    private const int MaximumAuthorizationLength = 8192 + 7;
    private const int MaximumSpaceLength = 8192;
    private const int MaximumRepoLength = 2048;
    private const int MaximumClientIdLength = 2048;

    private static readonly Nsid s_method = new(MethodName);
    private static readonly ManagedSpaceAccess s_fullAccess = new() { Read = true, Write = true };

    private readonly AtProtoServiceReference _managingApp;
    private readonly TimeSpan? _maximumTokenLifetime;
    private readonly IManagedSpaceAccessDecider _decider;
    private readonly IServiceTokenReplayGuard _replayGuard;
    private readonly HttpClient? _httpClient;
    private readonly TimeProvider? _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManagedSpaceAccessHandler"/> class.
    /// </summary>
    /// <param name="options">The handler options.</param>
    /// <param name="decider">The application's access decider.</param>
    /// <param name="replayGuard">The guard that ensures each service token is accepted only once.</param>
    /// <param name="httpClient">
    /// The client used to resolve the issuer's DID document, or <see langword="null"/> to use an SSRF-protected client.
    /// A supplied client is not disposed and must provide its own SSRF protection.
    /// </param>
    /// <param name="timeProvider">The clock used to validate token lifetimes, or <see langword="null"/> to use the system clock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/>, <paramref name="decider"/> or <paramref name="replayGuard"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><see cref="ManagedSpaceAccessHandlerOptions.ManagingApp"/> is not set.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ManagedSpaceAccessHandlerOptions.MaximumTokenLifetime"/> is zero or negative.</exception>
    public ManagedSpaceAccessHandler(
        ManagedSpaceAccessHandlerOptions options,
        IManagedSpaceAccessDecider decider,
        IServiceTokenReplayGuard replayGuard,
        HttpClient? httpClient = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(decider);
        ArgumentNullException.ThrowIfNull(replayGuard);

        if (options.ManagingApp is null)
        {
            throw new ArgumentException("The managing application service reference must be set.", nameof(options));
        }

        if (options.MaximumTokenLifetime is TimeSpan lifetime && lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), lifetime, "The maximum token lifetime must be positive.");
        }

        _managingApp = options.ManagingApp;
        _maximumTokenLifetime = options.MaximumTokenLifetime;
        _decider = decider;
        _replayGuard = replayGuard;
        _httpClient = httpClient;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Authenticates and answers a <c>com.atproto.managedspace.checkAccess</c> request.
    /// </summary>
    /// <param name="authorization">The request's <c>Authorization</c> header value.</param>
    /// <param name="space">The value of the request's space query parameter.</param>
    /// <param name="repo">The optional value of the request's repo query parameter.</param>
    /// <param name="clientId">The optional value of the request's client ID query parameter.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The response to send, including its status code and JSON body.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="InvalidOperationException">The decider returned <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Returns 401 <c>AuthenticationRequired</c> without a bearer token, 400 <c>InvalidRequest</c> for malformed
    /// parameters, 401 <c>InvalidToken</c> for a token that fails verification, was not issued by the space authority or
    /// was already used, and 503 <c>ServiceUnavailable</c> when the issuer's DID document cannot be retrieved. Exceptions
    /// thrown by the decider are not caught.</para>
    /// </remarks>
    public async Task<ManagedSpaceAccessResponse> HandleAsync(
        string? authorization,
        string? space,
        string? repo,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (authorization is null ||
            authorization.Length > MaximumAuthorizationLength ||
            !authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(authorization[BearerPrefix.Length..]))
        {
            return ManagedSpaceAccessResponse.Failure(401, "AuthenticationRequired", "A bearer service token is required.");
        }

        string token = authorization[BearerPrefix.Length..].Trim();

        if (space is null || space.Length > MaximumSpaceLength ||
            !SpaceAtUri.TryParse(space, out SpaceAtUri? spaceUri) ||
            spaceUri.IsRecord)
        {
            return ManagedSpaceAccessResponse.Failure(400, "InvalidRequest", "The space parameter must be a space URI.");
        }

        Did? repoDid = null;
        if (repo is not null && (repo.Length > MaximumRepoLength || !Did.TryParse(repo, out repoDid)))
        {
            return ManagedSpaceAccessResponse.Failure(400, "InvalidRequest", "The repo parameter must be a DID.");
        }

        Uri? clientUri = null;
        if (clientId is not null &&
            (clientId.Length > MaximumClientIdLength || !Uri.TryCreate(clientId, UriKind.Absolute, out clientUri)))
        {
            return ManagedSpaceAccessResponse.Failure(400, "InvalidRequest", "The clientId parameter must be an absolute URI.");
        }

        ServiceAuthenticationResult authentication;
        try
        {
            authentication = await VerifyAsync(token, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidDataException or CryptographicException or ArgumentException)
        {
            return ManagedSpaceAccessResponse.Failure(401, "InvalidToken", "The service token is invalid.");
        }
        catch (HttpRequestException)
        {
            return ManagedSpaceAccessResponse.Failure(503, "ServiceUnavailable", "The token issuer could not be resolved.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ManagedSpaceAccessResponse.Failure(503, "ServiceUnavailable", "The token issuer could not be resolved.");
        }

        if (authentication.Issuer != spaceUri.SpaceDid)
        {
            return ManagedSpaceAccessResponse.Failure(401, "InvalidToken", "The service token was not issued by the space authority.");
        }

        if (!await _replayGuard.TryReserveAsync(authentication, cancellationToken).ConfigureAwait(false))
        {
            return ManagedSpaceAccessResponse.Failure(401, "InvalidToken", "The service token has already been used.");
        }

        if (repoDid is not null && repoDid == spaceUri.SpaceDid)
        {
            return ManagedSpaceAccessResponse.Success(s_fullAccess);
        }

        ManagedSpaceAccess access = await _decider.CheckAccessAsync(
            new ManagedSpaceAccessCheck(
                space: spaceUri,
                repo: repoDid,
                clientId: clientUri,
                authentication: authentication),
            cancellationToken).ConfigureAwait(false) ??
            throw new InvalidOperationException("The managed space access decider returned null.");

        return ManagedSpaceAccessResponse.Success(access);
    }

    /// <summary>
    /// Verifies a service token addressed to the managing application for this method.
    /// </summary>
    /// <param name="token">The compact service token.</param>
    /// <param name="cancellationToken">A cancellation token for DID resolution.</param>
    /// <returns>The verified token identity.</returns>
    private Task<ServiceAuthenticationResult> VerifyAsync(string token, CancellationToken cancellationToken)
    {
        if (_httpClient is null)
        {
            return ServiceAuthentication.VerifyAsync(
                token: token,
                audience: _managingApp,
                method: s_method,
                timeProvider: _timeProvider,
                maximumLifetime: _maximumTokenLifetime,
                cancellationToken: cancellationToken);
        }

        return ServiceAuthentication.VerifyAsync(
            token: token,
            audience: _managingApp,
            method: s_method,
            httpClient: _httpClient,
            timeProvider: _timeProvider,
            maximumLifetime: _maximumTokenLifetime,
            cancellationToken: cancellationToken);
    }
}
