using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using idunno.AtProto.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Creates a Bluesky agent.
/// </summary>
[SuppressMessage("Performance", "CA1812", Justification = "Used in dependency injection.")]
internal sealed class BlueskyAgentFactory
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<BlueskyAgentFactory> _logger;

    public BlueskyAgentFactory(
        IHttpContextAccessor contextAccessor,
        IOptionsMonitor<BlueskyAuthenticationOptions> authenticationOptionsMonitor,
        IOptionsMonitor<BlueskyAgentOptions> agentOptionsMonitor,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(authenticationOptionsMonitor.CurrentValue.IdentityStore);

        IdentityStore = authenticationOptionsMonitor.CurrentValue.IdentityStore;
        AuthenticationOptions = authenticationOptionsMonitor.CurrentValue;
        BlueskyAgentOptions = agentOptionsMonitor.CurrentValue;
        BlueskyAgentOptions.LoggerFactory = loggerFactory;

        _httpContextAccessor = contextAccessor;
        _logger = loggerFactory.CreateLogger<BlueskyAgentFactory>();

    }

    internal BlueskyAuthenticationOptions AuthenticationOptions { get; }

    internal BlueskyAgentOptions BlueskyAgentOptions { get; }

    internal HttpContext? Context => _httpContextAccessor.HttpContext;

    internal IIdentityStore IdentityStore { get; }

    public BlueskyAgent CreateBlueskyAgent()
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

        agent.CredentialsUpdated += async (s, e) =>
        {
            if (e.AccessCredentials is not DPoPAccessCredentials accessCredentials)
            {
                _logger.CredentialsRefreshedNotDPoP(e.Did);
                return;
            }

            await IdentityStore.Renew(IIdentityStore.BuildClaimsIdentity(accessCredentials)).ConfigureAwait(false);
        };

        //if (agent.Credentials is not null &&  DateTime.UtcNow >= agent.Credentials.ExpiresOn)
        //{
        //    await agent.RefreshCredentials();
        //}

        return agent;
    }
}
