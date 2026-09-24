// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;

using Microsoft.Extensions.Hosting;

namespace idunno.Bluesky.AspNet.Authentication.Test;

[ExcludeFromCodeCoverage]
public class ReturnUriTests
{
    private const string LocalhostClientId = "http://localhost?redirect_uri=http://127.0.0.1/Bluesky/Callback&scope=atproto";

    private static OAuthOptions OAuthOptions(string clientId, string returnUri) =>
        new(clientId)
        {
            ReturnUri = new Uri(returnUri)
        };

    private static async Task<string> ReturnUriFor(AuthenticationTestHost host, string requestUri)
    {
        using HttpResponseMessage response = await host.Client.GetAsync(
            new Uri(requestUri),
            TestContext.Current.CancellationToken);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task InDevelopmentThePortTheApplicationIsListeningOnIsInsertedIntoALoopbackReturnUri()
    {
        // Bluesky's http://localhost client identifier requires a loopback return URI, and the atproto OAuth spec
        // matches those on path but not on port. A development return URI is therefore configured without a port and
        // the port the request arrived on is filled in, so the sample and template configuration work on whatever port
        // the launch profile happens to use.
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(
            oAuthOptions: OAuthOptions(LocalhostClientId, "http://127.0.0.1/Bluesky/Callback"),
            environmentName: Environments.Development);

        Assert.Equal(
            "http://127.0.0.1:5251/Bluesky/Callback",
            await ReturnUriFor(host, "http://127.0.0.1:5251/test/returnuri"));
    }

    [Fact]
    public async Task AConfiguredPortIsNeverReplacedByTheRequestPort()
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(
            oAuthOptions: OAuthOptions(LocalhostClientId, "http://127.0.0.1:1234/Bluesky/Callback"),
            environmentName: Environments.Development);

        Assert.Equal(
            "http://127.0.0.1:1234/Bluesky/Callback",
            await ReturnUriFor(host, "http://127.0.0.1:5251/test/returnuri"));
    }

    [Fact]
    public async Task OutsideDevelopmentTheConfiguredReturnUriIsUsedExactlyAsItIsWritten()
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(
            oAuthOptions: OAuthOptions(LocalhostClientId, "http://127.0.0.1/Bluesky/Callback"));

        Assert.Equal(
            "http://127.0.0.1/Bluesky/Callback",
            await ReturnUriFor(host, "http://127.0.0.1:5251/test/returnuri"));
    }

    [Fact]
    public async Task AReturnUriWhichIsNotLoopbackIsLeftAloneEvenInDevelopment()
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(
            oAuthOptions: OAuthOptions(LocalhostClientId, "http://localhost/Bluesky/Callback"),
            environmentName: Environments.Development);

        Assert.Equal(
            "http://localhost/Bluesky/Callback",
            await ReturnUriFor(host, "http://localhost:5251/test/returnuri"));
    }

    [Fact]
    public async Task AHostedClientIdentifierDoesNotGetTheRequestPort()
    {
        await using AuthenticationTestHost host = await AuthenticationTestHost.Create(
            oAuthOptions: OAuthOptions("https://example.org/client-metadata.json", "http://127.0.0.1/Bluesky/Callback"),
            environmentName: Environments.Development);

        Assert.Equal(
            "http://127.0.0.1/Bluesky/Callback",
            await ReturnUriFor(host, "http://127.0.0.1:5251/test/returnuri"));
    }
}
