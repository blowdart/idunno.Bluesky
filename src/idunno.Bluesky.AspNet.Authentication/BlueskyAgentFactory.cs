// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;

using idunno.AtProto.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _authenticationScheme;

    /// <summary>
    /// Creates a new instance of the <see cref="BlueskyAgentFactory"/> class, for the authentication scheme specified by
    /// <see cref="BlueskyAuthenticationDefaults.AuthenticationScheme"/>.
    /// </summary>
    /// <param name="contextAccessor">The <see cref="IHttpContextAccessor"/></param>
    /// <param name="authenticationOptionsMonitor">The <see cref="IOptionsMonitor{BlueskyAuthenticationOptions}"/></param>
    /// <param name="agentOptionsMonitor">The <see cref="IOptionsMonitor{BlueskyAgentOptions}"/></param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/></param>
    /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> the agents it creates should make their requests through.</param>
    /// <exception cref="ArgumentNullException">Thrown if any of the parameters are <see langword="null"/>.</exception>
    public BlueskyAgentFactory(
        IHttpContextAccessor contextAccessor,
        IOptionsMonitor<BlueskyAuthenticationOptions> authenticationOptionsMonitor,
        IOptionsMonitor<BlueskyAgentOptions> agentOptionsMonitor,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory) :
            this(contextAccessor, authenticationOptionsMonitor, agentOptionsMonitor, loggerFactory, httpClientFactory, BlueskyAuthenticationDefaults.AuthenticationScheme)
    {
    }

    /// <summary>
    /// Creates a new instance of the <see cref="BlueskyAgentFactory"/> class for the specified authentication scheme.
    /// </summary>
    /// <param name="contextAccessor">The <see cref="IHttpContextAccessor"/></param>
    /// <param name="authenticationOptionsMonitor">The <see cref="IOptionsMonitor{BlueskyAuthenticationOptions}"/></param>
    /// <param name="agentOptionsMonitor">The <see cref="IOptionsMonitor{BlueskyAgentOptions}"/></param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/></param>
    /// <param name="httpClientFactory">The <see cref="IHttpClientFactory"/> the agents it creates should make their requests through.</param>
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
        IHttpClientFactory httpClientFactory,
        string authenticationScheme)
    {
        ArgumentNullException.ThrowIfNull(contextAccessor);
        ArgumentNullException.ThrowIfNull(authenticationOptionsMonitor);
        ArgumentNullException.ThrowIfNull(agentOptionsMonitor);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticationScheme);

        BlueskyAgentOptions = agentOptionsMonitor.CurrentValue;

        _authenticationOptionsMonitor = authenticationOptionsMonitor;
        _authenticationScheme = authenticationScheme;
        _httpContextAccessor = contextAccessor;
        _httpClientFactory = httpClientFactory;
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
    /// Resolves the <see cref="IIdentityStore"/> credential updates should be written to.
    /// </summary>
    /// <param name="authenticationScheme">The authentication type carried by the identity the agent was built from, if any.</param>
    /// <remarks>
    /// <para>
    ///   An identity issued by the authentication handler carries the name of the scheme which issued it as its authentication type, which is the name the
    ///   options for that scheme, and so its identity store, are configured against. An identity issued by anything else carries a name no Bluesky scheme
    ///   was registered under, and asking the options monitor for it would build a fresh, default options instance with an identity store of its own which
    ///   nothing ever reads, silently discarding every credential update. The scheme the factory was registered for is used in that case instead.
    /// </para>
    /// </remarks>
    private IIdentityStore? ResolveIdentityStore(string? authenticationScheme)
    {
        if (!string.IsNullOrEmpty(authenticationScheme) &&
            !string.Equals(authenticationScheme, _authenticationScheme, StringComparison.Ordinal) &&
            !IsBlueskyScheme(authenticationScheme))
        {
            authenticationScheme = _authenticationScheme;
        }

        return GetAuthenticationOptions(authenticationScheme).IdentityStore;
    }

    /// <summary>
    /// Returns a flag indicating whether <paramref name="authenticationScheme"/> is a registered scheme handled by <see cref="BlueskyAuthenticationHandler"/>.
    /// </summary>
    /// <param name="authenticationScheme">The name of the authentication scheme to check.</param>
    private bool IsBlueskyScheme(string authenticationScheme)
    {
        // The scheme provider is resolved from the request rather than injected, because the factory is a singleton and
        // this is only consulted for a principal which did not come from the scheme it was registered for.
        if (Context?.RequestServices.GetService<IAuthenticationSchemeProvider>() is not IAuthenticationSchemeProvider schemeProvider)
        {
            return false;
        }

        // CreateAgent is synchronous, so the task has to be unwrapped here. The scheme provider's contract is a lookup
        // over schemes registered at startup, and every implementation in ASP.NET Core returns an already completed
        // task, so this does not block. Only a principal whose authentication type is not the scheme the factory was
        // registered for reaches this at all.
        AuthenticationScheme? scheme = schemeProvider.GetSchemeAsync(authenticationScheme).GetAwaiter().GetResult();

        return scheme is not null && typeof(BlueskyAuthenticationHandler).IsAssignableFrom(scheme.HandlerType);
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
            agent = new BlueskyAgent(identity: identity, httpClientFactory: _httpClientFactory, options: BlueskyAgentOptions);
        }
        else
        {
            identity = null;
            agent = new BlueskyAgent(httpClientFactory: _httpClientFactory, options: BlueskyAgentOptions);
        }

        // An identity issued by the authentication handler carries the name of the scheme which issued it as its authentication type,
        // which is the name the options for that scheme, and so its identity store, are configured against.
        IIdentityStore? identityStore = ResolveIdentityStore(identity?.AuthenticationType);

        if (identityStore is not null)
        {
            agent.CredentialsUpdatedAsync = identityStore.OnCredentialsUpdated;
        }

        return agent;
    }
}
