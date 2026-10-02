// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;

using idunno.AtProto;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.Test;

public class BlueskyAgentBuilderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuilderPassesTimeProviderToAgent(bool withHttpClientFactory)
    {
        TestTimeProvider timeProvider = new();
        BlueskyAgentBuilder builder = BlueskyAgent.CreateBuilder()
            .WithTimeProvider(timeProvider);
        builder.DisableBackgroundTokenRefresh();

        if (withHttpClientFactory)
        {
            builder.WithHttpClientFactory(new TestHttpClientFactory());
        }

        using BlueskyAgent agent = builder.Build();

        Assert.Same(timeProvider, GetAgentOptions(agent).TimeProvider);
    }

    [Fact]
    public void WithTimeProviderPreservesBlueskyBuilderType()
    {
        BlueskyAgentBuilder builder = BlueskyAgent.CreateBuilder()
            .WithTimeProvider(new TestTimeProvider());

        Assert.IsType<BlueskyAgentBuilder>(builder);
    }

    [Fact]
    public void AddBlueskyAgentOptionsCopiesTimeProvider()
    {
        TestTimeProvider timeProvider = new();
        BlueskyAgentOptions options = new()
        {
            LoggerFactory = NullLoggerFactory.Instance,
            TimeProvider = timeProvider
        };

        ServiceCollection services = new();
        services.AddBlueskyAgentOptions(options);

        using ServiceProvider serviceProvider = services.BuildServiceProvider();

        Assert.Same(
            timeProvider,
            serviceProvider.GetRequiredService<IOptionsMonitor<BlueskyAgentOptions>>().CurrentValue.TimeProvider);
    }

    private sealed class TestTimeProvider : TimeProvider
    {
    }

    private static AtProtoAgentOptions GetAgentOptions(BlueskyAgent agent) =>
        (AtProtoAgentOptions)typeof(AtProtoAgent)
            .GetProperty("Options", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(agent)!;

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
