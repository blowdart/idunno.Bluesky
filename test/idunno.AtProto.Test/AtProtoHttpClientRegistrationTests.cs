// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;

using idunno.AtProto.Jetstream;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace idunno.AtProto.Test;

/// <summary>
/// Covers the registration which lets an agent or a jetstream share an application's <see cref="IHttpClientFactory"/>.
/// </summary>
/// <remarks>
/// <para>
///   An agent and a jetstream build an SSRF protected <see cref="HttpClient"/> when they are left to create one for
///   themselves, because the services they call are reached at addresses taken from DID documents. Handing one an
///   <see cref="IHttpClientFactory"/> replaces that client wholesale, so the name they resolve and the handler the
///   registration produces both have to line up with what they would have built.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public class AtProtoHttpClientRegistrationTests
{
    [Fact]
    public void AddAtProtoHttpClientConfiguresTheClientAgentsAndJetstreamsResolve()
    {
        ServiceCollection services = new();
        services.AddAtProtoHttpClient(new HttpClientOptions { HttpUserAgent = "test/1.0", Timeout = TimeSpan.FromSeconds(17) });

        using ServiceProvider provider = services.BuildServiceProvider();

        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(Agent.HttpClientName);

        Assert.Equal("test/1.0", client.DefaultRequestHeaders.UserAgent.ToString());
        Assert.Equal(TimeSpan.FromSeconds(17), client.Timeout);
    }

    [Fact]
    public void AddAtProtoHttpClientBuildsAnSsrfProtectedPrimaryHandler()
    {
        // The registration exists so that supplying an IHttpClientFactory cannot quietly drop the SSRF protections an
        // agent or a jetstream would otherwise build for itself. The protection is a connect callback which vets the
        // address a host resolves to, so a handler without one is an unprotected handler.
        ServiceCollection services = new();
        services.AddAtProtoHttpClient();

        using ServiceProvider provider = services.BuildServiceProvider();

        SocketsHttpHandler handler = Assert.IsType<SocketsHttpHandler>(PrimaryHandlerFor(provider));

        Assert.NotNull(handler.ConnectCallback);
        Assert.Equal(DecompressionMethods.All, handler.AutomaticDecompression);
    }

    [Fact]
    public void AddAtProtoHttpClientBuildsAProxiedSsrfHandlerWhenAProxyIsConfigured()
    {
        ServiceCollection services = new();
        services.AddAtProtoHttpClient(new HttpClientOptions { ProxyUri = new Uri("http://localhost:8888") });

        using ServiceProvider provider = services.BuildServiceProvider();

        using HttpMessageHandler expected =
            Agent.CreateHttpMessageHandler(new HttpClientOptions { ProxyUri = new Uri("http://localhost:8888") }, null);

        Assert.IsType(expected.GetType(), PrimaryHandlerFor(provider));
    }

    private static HttpMessageHandler PrimaryHandlerFor(IServiceProvider provider)
    {
        HttpClientFactoryOptions options =
            provider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>().Get(Agent.HttpClientName);

        RecordingHttpMessageHandlerBuilder builder = new(provider);

        foreach (Action<HttpMessageHandlerBuilder> action in options.HttpMessageHandlerBuilderActions)
        {
            action(builder);
        }

        Assert.NotNull(builder.PrimaryHandler);

        return builder.PrimaryHandler;
    }

    [Fact]
    public void AJetstreamGivenAnHttpClientFactoryResolvesTheSharedAtProtoClient()
    {
        RecordingHttpClientFactory httpClientFactory = new();

        using AtProtoJetstream jetstream = new(httpClientFactory);

        Assert.Equal(Agent.HttpClientName, Assert.Single(httpClientFactory.RequestedNames));
    }

    [Fact]
    public void AJetstreamCannotBeGivenANullHttpClientFactory() =>
        Assert.Throws<ArgumentNullException>("httpClientFactory", () => new AtProtoJetstream(httpClientFactory: null!));

    [Fact]
    public void AJetstreamWithoutAnHttpClientFactoryStillConfiguresItsOwnClientTheSameWay()
    {
        using TestJetstream jetstream = new(new HttpClientOptions { HttpUserAgent = "test/2.0" });

        using HttpClient client = jetstream.Factory.CreateClient(Agent.HttpClientName);

        Assert.Equal("test/2.0", client.DefaultRequestHeaders.UserAgent.ToString());
        Assert.Null(jetstream.MessageLastReceived);
    }

    private sealed class TestJetstream(HttpClientOptions httpClientOptions)
        : AtProtoJetstream(httpClientOptions: httpClientOptions)
    {
        internal IHttpClientFactory Factory => HttpClientFactory;
    }

    private sealed class RecordingHttpMessageHandlerBuilder(IServiceProvider services) : HttpMessageHandlerBuilder
    {
        public override string? Name { get; set; }

        public override IServiceProvider Services => services;

        public override HttpMessageHandler PrimaryHandler { get; set; } = default!;

        public override IList<DelegatingHandler> AdditionalHandlers { get; } = [];

        public override HttpMessageHandler Build() => PrimaryHandler;
    }
}
