// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;

using idunno.AtProto.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Creates a Bluesky agent.
/// </summary>
public sealed class BlueskyAgentFactory
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOptionsMonitor<BlueskyAuthenticationOptions> _authenticationOptionsMonitor;
    private readonly string _authenticationScheme;

    /// <summary>
    /// Creates a new instance of the <see cref="BlueskyAgentFactory"/> class, for the authentication scheme specified by
    /// <see cref="BlueskyAuthenticationDefaults.AuthenticationScheme"/>.
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
        ILoggerFactory loggerFactory) :
            this(contextAccessor, authenticationOptionsMonitor, agentOptionsMonitor, loggerFactory, BlueskyAuthenticationDefaults.AuthenticationScheme)
    {
    }

    /// <summary>
    /// Creates a new instance of the <see cref="BlueskyAgentFactory"/> class for the specified authentication scheme.
    /// </summary>
    /// <param name="contextAccessor">The <see cref="IHttpContextAccessor"/></param>
    /// <param name="authenticationOptionsMonitor">The <see cref="IOptionsMonitor{BlueskyAuthenticationOptions}"/></param>
    /// <param name="agentOptionsMonitor">The <see cref="IOptionsMonitor{BlueskyAgentOptions}"/></param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/></param>
    /// <param name="authenticationScheme">
    ///   The name of the authentication scheme whose <see cref="BlueskyAuthenticationOptions"/> the factory should use when the current request has no
    ///   authenticated Bluesky user to take the scheme name from.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown if any of the parameters are <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="authenticationScheme"/> is empty or white space.</exception>
    public BlueskyAgentFactory(
        IHttpContextAccessor contextAccessor,
        IOptionsMonitor<BlueskyAuthenticationOptions> authenticationOptionsMonitor,
        IOptionsMonitor<BlueskyAgentOptions> agentOptionsMonitor,
        ILoggerFactory loggerFactory,
        string authenticationScheme)
    {
        ArgumentNullException.ThrowIfNull(contextAccessor);
        ArgumentNullException.ThrowIfNull(authenticationOptionsMonitor);
        ArgumentNullException.ThrowIfNull(agentOptionsMonitor);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticationScheme);

        BlueskyAgentOptions = agentOptionsMonitor.CurrentValue;
        BlueskyAgentOptions.LoggerFactory = loggerFactory;

        _authenticationOptionsMonitor = authenticationOptionsMonitor;
        _authenticationScheme = authenticationScheme;
        _httpContextAccessor = contextAccessor;
    }

    internal BlueskyAgentOptions BlueskyAgentOptions { get; }

    internal HttpContext? Context => _httpContextAccessor.HttpContext;

    /// <summary>
    /// Gets the <see cref="BlueskyAuthenticationOptions"/> configured for the specified authentication scheme.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   <see cref="BlueskyAuthenticationOptions"/> are configured against the name of the authentication scheme they belong to, so the unnamed options
    ///   instance exposed by <see cref="IOptionsMonitor{TOptions}.CurrentValue"/> is not the instance the authentication handler is using, and would not
    ///   carry any configuration the application applied in its call to <c>AddBluesky</c>.
    /// </para>
    /// </remarks>
    /// <param name="authenticationScheme">The name of the authentication scheme to get the options for, if known.</param>
    internal BlueskyAuthenticationOptions GetAuthenticationOptions(string? authenticationScheme)
    {
        authenticationScheme = string.IsNullOrEmpty(authenticationScheme) ? _authenticationScheme : authenticationScheme;

        return _authenticationOptionsMonitor.Get(authenticationScheme);
    }

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
            var userIdentity = Context.User.Identity as ClaimsIdentity;
            identity = new ClaimsIdentity(userIdentity);
        }

        if (identity is not null &&
            identity.IsAuthenticated &&
            identity.HasClaim(c => c.Type == AtProtoClaims.Did))
        {
            agent = new BlueskyAgent(identity: identity, BlueskyAgentOptions);
        }
        else
        {
            identity = null;
            agent = new BlueskyAgent(options: BlueskyAgentOptions);
        }

        // An identity issued by the authentication handler carries the name of the scheme which issued it as its authentication type,
        // which is the name the options for that scheme, and so its identity store, are configured against.
        IIdentityStore? identityStore = GetAuthenticationOptions(identity?.AuthenticationType).IdentityStore;

        if (identityStore is not null)
        {
            agent.CredentialsUpdatedAsync = identityStore.OnCredentialsUpdated;
        }

        return agent;
    }
}
