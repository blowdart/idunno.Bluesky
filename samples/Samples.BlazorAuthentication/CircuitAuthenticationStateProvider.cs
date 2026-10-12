// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using System.Security.Authentication;
using System.Security.Cryptography;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.Extensions.Options;

namespace Samples.BlazorAuthentication;

/// <summary>
/// Revalidates a circuit against its cookie expiry and the server-side identity store.
/// </summary>
public sealed class CircuitAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
    private readonly IIdentityStore _store;
    private DateTimeOffset? _expiresUtc;
    private readonly TimeProvider _timeProvider;
    private readonly SessionRenewal _renewal;

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitAuthenticationStateProvider"/> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="contextAccessor">The accessor for the initial HTTP request only.</param>
    /// <param name="options">The named authentication options.</param>
    public CircuitAuthenticationStateProvider(
        ILoggerFactory loggerFactory,
        IHttpContextAccessor contextAccessor,
        IOptionsMonitor<BlueskyAuthenticationOptions> options) : base(loggerFactory)
    {
        var authentication = options.Get(BlueskyAuthenticationDefaults.AuthenticationScheme);
        _store = authentication.IdentityStore ?? throw new InvalidOperationException("An identity store is required.");
        _timeProvider = authentication.TimeProvider ?? TimeProvider.System;
        _renewal = contextAccessor.HttpContext?.RequestServices.GetRequiredService<SessionRenewal>() ??
            throw new InvalidOperationException("An initial HTTP request is required.");
        // Snapshot the ticket while establishing the circuit, never retain or consult its HttpContext later.
        _expiresUtc = contextAccessor.HttpContext?.Features.Get<IAuthenticateResultFeature>()
            ?.AuthenticateResult?.Properties?.ExpiresUtc;
    }

    /// <inheritdoc/>
    protected override TimeSpan RevalidationInterval => TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    protected override Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken) =>
        IsCurrentAsync(authenticationState.User, cancellationToken);

    /// <summary>
    /// Updates the circuit expiry from a server-protected receipt issued to the same login.
    /// </summary>
    /// <param name="receipt">The protected expiry returned by the HTTP renewal endpoint.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationException">The receipt or session is invalid.</exception>
    public async Task AcceptRenewalAsync(string receipt)
    {
        var user = (await GetAuthenticationStateAsync()).User;
        if (!await IsCurrentAsync(user, CancellationToken.None))
        {
            throw new AuthenticationException("Your session has ended. Sign in again.");
        }

        DateTimeOffset expiry;
        try
        {
            expiry = _renewal.ReadExpiry(user, receipt);
        }
        catch (CryptographicException ex)
        {
            throw new AuthenticationException("The session renewal receipt is invalid.", ex);
        }

        if (expiry <= _timeProvider.GetUtcNow())
        {
            throw new AuthenticationException("The session renewal has expired.");
        }

        if (expiry > _expiresUtc)
        {
            _expiresUtc = expiry;
        }
    }

    /// <summary>
    /// Checks whether the original session still authorizes a circuit operation.
    /// </summary>
    /// <param name="user">The circuit principal.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if the session is current; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> IsCurrentAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (user.Identity?.IsAuthenticated != true ||
            _expiresUtc is null || _expiresUtc <= _timeProvider.GetUtcNow() ||
            user.FindFirstValue(AtProtoClaims.Did) is not string didValue ||
            !Did.TryParse(didValue, out Did? did))
        {
            return false;
        }

        var identity = await _store.GetIdentity(did, cancellationToken);
        string? proof = user.FindFirstValue(AtProtoClaims.DPoPProof);

        return proof is not null && identity?.FindFirst(AtProtoClaims.DPoPProof)?.Value == proof;
    }
}
