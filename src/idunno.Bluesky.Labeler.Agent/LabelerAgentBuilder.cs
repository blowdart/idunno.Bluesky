// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler.Agent;

/// <summary>Builds a remote labeler client and its borrowed or owned AT Protocol agent.</summary>
public sealed class LabelerAgentBuilder
{
    private readonly AtProtoAgentBuilder _accountAgentBuilder = AtProtoAgentBuilder.Create();
    private AtProtoAgent? _accountAgent;
    private Did? _labelerDid;
    private Uri? _labelerService;

    /// <summary>Initializes a new instance of the <see cref="LabelerAgentBuilder"/> class.</summary>
    private LabelerAgentBuilder()
    {
    }

    /// <summary>Creates a new labeler-agent builder.</summary>
    /// <returns>A builder with an owned default <see cref="AtProtoAgent"/> configuration.</returns>
    public static LabelerAgentBuilder Create() => new();

    /// <summary>Configures the labeler endpoint and issuer DID for issuance-scoped service authentication.</summary>
    /// <param name="service">The HTTPS origin of the labeler host.</param>
    /// <param name="labelerDid">The DID reported by the labeler host.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="service"/> or <paramref name="labelerDid"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="service"/> is not a root HTTPS URI without credentials, query, or fragment.</exception>
    public LabelerAgentBuilder WithLabelerService(Uri service, Did labelerDid)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(labelerDid);
        if (!service.IsAbsoluteUri ||
            service.Scheme != Uri.UriSchemeHttps ||
            service.AbsolutePath != "/" ||
            service.UserInfo.Length != 0 ||
            service.Query.Length != 0 ||
            service.Fragment.Length != 0)
        {
            throw new ArgumentException("The labeler service must be a root HTTPS URI without credentials, query, or fragment.", nameof(service));
        }

        _labelerService = service;
        _labelerDid = labelerDid;
        return this;
    }

    /// <summary>Uses an existing authenticated agent without taking ownership of it.</summary>
    /// <param name="agent">The authenticated account agent.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="agent"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException"><paramref name="agent"/> has no active authenticated session.</exception>
    public LabelerAgentBuilder WithAccountAgent(AtProtoAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        if (!agent.IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        _accountAgent = agent;
        return this;
    }

    /// <summary>Configures the owned <see cref="AtProtoAgent"/> used by bots without an existing agent.</summary>
    /// <param name="configure">A callback that configures the owned account-agent builder.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    public LabelerAgentBuilder ConfigureAccountAgent(Action<AtProtoAgentBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _accountAgent = null;
        configure(_accountAgentBuilder);
        return this;
    }

    /// <summary>Builds the configured labeler agent.</summary>
    /// <returns>A labeler client.</returns>
    /// <exception cref="InvalidOperationException">The labeler service has not been configured.</exception>
    /// <exception cref="AuthenticationRequiredException">A borrowed account agent is no longer authenticated.</exception>
    public LabelerAgent Build()
    {
        Uri service = _labelerService ?? throw new InvalidOperationException("Configure the labeler service before building the agent.");
        Did labelerDid = _labelerDid ?? throw new InvalidOperationException("Configure the labeler service before building the agent.");
        bool ownsAccountAgent = _accountAgent is null;
        AtProtoAgent accountAgent = _accountAgent ?? _accountAgentBuilder.Build();

        return new LabelerAgent(
            accountAgent,
            service,
            new AtProtoServiceReference(labelerDid, Audience.Issuance),
            ownsAccountAgent);
    }
}
