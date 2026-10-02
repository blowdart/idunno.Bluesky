// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;

using idunno.AtProto;

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
