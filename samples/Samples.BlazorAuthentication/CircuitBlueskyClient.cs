// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Authentication;
using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky;
using idunno.Bluesky.AspNet.Authentication;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;

namespace Samples.BlazorAuthentication;

/// <summary>
/// Executes circuit operations with current server-side credentials, without writing cookies.
/// </summary>
/// <param name="authenticationStateProvider">The circuit authentication state provider.</param>
/// <param name="authenticationOptions">The named authentication options.</param>
/// <param name="agentOptions">The agent options with background refresh disabled.</param>
/// <param name="httpClientFactory">The HTTP client factory.</param>
/// <param name="logger">The logger.</param>
public sealed partial class CircuitBlueskyClient(
    AuthenticationStateProvider authenticationStateProvider,
    IOptionsMonitor<BlueskyAuthenticationOptions> authenticationOptions,
    IOptions<BlueskyAgentOptions> agentOptions,
    IHttpClientFactory httpClientFactory,
    ILogger<CircuitBlueskyClient> logger)
{
    /// <summary>
    /// Runs an operation after validating the session and refreshing its credentials if necessary.
    /// </summary>
    /// <typeparam name="T">The operation result type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The operation result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationException">The session has expired or credential refresh failed.</exception>
    /// <exception cref="InvalidOperationException">The required identity store or circuit provider is missing.</exception>
    public async Task<T> ExecuteAsync<T>(Func<BlueskyAgent, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var options = authenticationOptions.Get(BlueskyAuthenticationDefaults.AuthenticationScheme);
        var store = options.IdentityStore ?? throw new InvalidOperationException("An identity store is required.");
        var provider = authenticationStateProvider as CircuitAuthenticationStateProvider ??
            throw new InvalidOperationException("The circuit authentication provider is required.");
        var user = (await provider.GetAuthenticationStateAsync()).User;
        var timeProvider = options.TimeProvider ?? TimeProvider.System;

        if (!await provider.IsCurrentAsync(user, cancellationToken))
        {
            throw SessionExpired();
        }

        var did = new Did(user.FindFirstValue(AtProtoClaims.Did)!);
        string? refreshLock = null;
        for (int attempt = 0; attempt < options.MaxRefreshChecks; attempt++)
        {
            refreshLock = await store.StartRefresh(did, cancellationToken);
            if (refreshLock is not null)
            {
                break;
            }

            await Task.Delay(options.RefreshCheckWait, timeProvider, cancellationToken);
        }

        if (refreshLock is null)
        {
            throw new AuthenticationException("Your session is busy refreshing. Try again.");
        }

        try
        {
            if (!await provider.IsCurrentAsync(user, cancellationToken))
            {
                throw SessionExpired();
            }

            var identity = await store.GetIdentity(did, cancellationToken) ?? throw SessionExpired();
            if (identity.FindFirst(AtProtoClaims.DPoPProof)?.Value != user.FindFirstValue(AtProtoClaims.DPoPProof))
            {
                throw SessionExpired();
            }

            using var agent = new BlueskyAgent(new ClaimsIdentity(identity), httpClientFactory: httpClientFactory, options: agentOptions.Value);
            agent.CredentialsUpdatedAsync = async (e, token) =>
            {
                // Recheck circuit expiry; SessionIdentityStore atomically guards the proof check and write.
                if (!await provider.IsCurrentAsync(user, token))
                {
                    throw SessionExpired();
                }

                await store.OnCredentialsUpdated(e, token);
            };
            if (agent.Credentials is null)
            {
                throw SessionExpired();
            }

            if (agent.Credentials.ExpiresOn - TimeSpan.FromMinutes(5) < timeProvider.GetUtcNow() &&
                !await agent.RefreshCredentials(cancellationToken: cancellationToken))
            {
                RefreshFailed(logger, did);
                throw new AuthenticationException("Could not refresh your session. Sign in again.");
            }

            return await operation(agent, cancellationToken);
        }
        finally
        {
            if (!await store.EndRefresh(did, refreshLock, CancellationToken.None))
            {
                RefreshLockLost(logger, did);
            }
        }
    }

    private static AuthenticationException SessionExpired() =>
        new("Your session has ended. Reload the page and sign in again.");

    [LoggerMessage(Level = LogLevel.Warning, Message = "Credential refresh failed for circuit user {Did}.")]
    private static partial void RefreshFailed(ILogger logger, Did did);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The circuit operation lost its credential refresh lock for {Did}.")]
    private static partial void RefreshLockLost(ILogger logger, Did did);
}
