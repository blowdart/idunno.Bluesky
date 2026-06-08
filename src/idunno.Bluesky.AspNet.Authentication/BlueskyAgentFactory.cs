// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using idunno.AtProto.Authentication;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Creates a Bluesky agent.
/// </summary>
[SuppressMessage("Performance", "CA1812", Justification = "Used in dependency injection.")]
public sealed class BlueskyAgentFactory
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Creates a new instance of the <see cref="BlueskyAgentFactory"/> class.
    /// </summary>
    /// <param name="contextAccessor">The <see cref="IHttpContextAccessor"/></param>
    /// <param name="authenticationOptionsMonitor">The <see cref="IOptionsMonitor{BlueskyAuthenticationOptions}"/></param>
    /// <param name="agentOptionsMonitor">The <see cref="IOptionsMonitor{BlueskyAgentOptions}"/></param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/></param>
    /// <exception cref="ArgumentNullException">Thrown if any of the parameters are <see langword="null"/>.</exception>
    public BlueskyAgentFactory(
        IHttpContextAccessor contextAccessor,
        IOptionsMonitor<BlueskyAuthenticationOptions> authenticationOptionsMonitor,
        IOptionsMonitor<BlueskyAgentOptions> agentOptionsMonitor,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(contextAccessor);
        ArgumentNullException.ThrowIfNull(authenticationOptionsMonitor);
        ArgumentNullException.ThrowIfNull(authenticationOptionsMonitor.CurrentValue.IdentityStore);
        ArgumentNullException.ThrowIfNull(agentOptionsMonitor);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        IdentityStore = authenticationOptionsMonitor.CurrentValue.IdentityStore;
        AuthenticationOptions = authenticationOptionsMonitor.CurrentValue;
        BlueskyAgentOptions = agentOptionsMonitor.CurrentValue;
        BlueskyAgentOptions.LoggerFactory = loggerFactory;

        _httpContextAccessor = contextAccessor;
    }

    internal BlueskyAuthenticationOptions AuthenticationOptions { get; }

    internal BlueskyAgentOptions BlueskyAgentOptions { get; }

    internal HttpContext? Context => _httpContextAccessor.HttpContext;

    internal IIdentityStore IdentityStore { get; }

    /// <summary>
    /// Creates a <see cref="BlueskyAgent"/>.
    /// </summary>
    /// <returns>A new instance of <see cref="BlueskyAgent"/>.</returns>
    public BlueskyAgent CreateAgent()
    {
        BlueskyAgent agent;
        
        ClaimsIdentity? identity = null;

        if (Context is not null && Context.User is not null && Context.User.Identity is not null)
        {
            identity = Context.User.Identity as ClaimsIdentity;
        }

        if (identity is not null && identity.IsAuthenticated && identity.HasClaim(c => c.Type == AtProtoClaims.Did))
        {
            agent = new BlueskyAgent(identity: identity, BlueskyAgentOptions);
        }
        else
        {
            agent = new BlueskyAgent(options: BlueskyAgentOptions);
        }

        agent.CredentialsUpdated += IdentityStore.OnCredentialsUpdated;

        return agent;
    }
}
