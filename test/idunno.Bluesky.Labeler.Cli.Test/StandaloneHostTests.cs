// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.AspNet.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using HostProgram = Samples.LabelerHost.Program;

namespace idunno.Bluesky.Labeler.Cli.Test;

/// <summary>Exercises the standalone host's public native-client metadata and explicit proxy trust boundary.</summary>
public class StandaloneHostTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlyExplicitlyTrustedProxiesCanForwardHttps(bool trusted)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        if (trusted)
        {
            builder.Configuration["Labeler:TrustedProxies:0"] = "127.0.0.1";
            builder.Configuration["Labeler:TrustedProxies:1"] = "::ffff:127.0.0.1";
        }

        await using WebApplication app = builder.Build();
        HostProgram.ConfigureForwardedHeaders(app);
        app.UseBlueskyOAuthClientMetadata(
            HostProgram.CreateNativeOAuthOptions(
                new Uri("https://labeler.example"),
                new AtProtoServiceReference(new Did("did:web:labeler.example"), "labeler_admin")),
            new BlueskyOAuthClientMetadataOptions { ApplicationType = BlueskyOAuthClientApplicationType.Native });
        app.MapGet("/scheme", (HttpContext context) => context.Request.Scheme);
        await app.StartAsync(token);
        Uri address = new(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        using HttpClient client = new() { BaseAddress = address };
        using HttpRequestMessage request = new(HttpMethod.Get, "/scheme");
        request.Headers.Add("X-Forwarded-For", "198.51.100.1");
        request.Headers.Add("X-Forwarded-Proto", "https");
        using HttpResponseMessage response = await client.SendAsync(request, token);
        Assert.Equal(trusted ? "https" : "http", await response.Content.ReadAsStringAsync(token));
        using HttpResponseMessage metadataResponse = await client.GetAsync("/native-client-metadata.json", token);
        Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);
        Assert.Equal("application/json", metadataResponse.Content.Headers.ContentType!.MediaType);
        using JsonDocument metadata = JsonDocument.Parse(await metadataResponse.Content.ReadAsStringAsync(token));
        Assert.Equal("https://labeler.example/native-client-metadata.json", metadata.RootElement.GetProperty("client_id").GetString());
        Assert.Equal("native", metadata.RootElement.GetProperty("application_type").GetString());
        Assert.Equal("none", metadata.RootElement.GetProperty("token_endpoint_auth_method").GetString());
        Assert.Equal("http://127.0.0.1/", metadata.RootElement.GetProperty("redirect_uris")[0].GetString());
        string[] scopes = metadata.RootElement.GetProperty("scope").GetString()!.Split(' ');
        Assert.Contains("atproto", scopes);
        Assert.Equal(23, scopes.Length);
        Assert.Contains("identity:*", scopes);
        Assert.Subset(scopes.ToHashSet(StringComparer.Ordinal),
            Program.CreatePlcOAuthOptions(new Uri("https://labeler.example/native-client-metadata.json")).Scopes.ToHashSet(StringComparer.Ordinal));
        Assert.Contains("rpc:dev.idunno.labeler.admin.issueLabel?aud=did%3Aweb%3Alabeler.example%23labeler_admin", scopes);
        Assert.Contains("rpc:dev.idunno.labeler.admin.completeRotation?aud=did%3Aweb%3Alabeler.example%23labeler_admin", scopes);
        await app.StopAsync(token);
    }

    [Fact]
    public async Task IdentityClientRefusesLoopbackDestinations()
    {
        using HttpClient client = HostProgram.CreateIdentityClient();
        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetAsync("https://127.0.0.1/.well-known/did.json", TestContext.Current.CancellationToken));
        Assert.NotNull(exception.InnerException);
        Assert.Contains("Unsafe", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
