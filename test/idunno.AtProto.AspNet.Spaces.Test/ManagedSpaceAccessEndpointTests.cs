// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto;
using idunno.AtProto.AspNet.Spaces;
using idunno.AtProto.Space;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

#pragma warning disable BSKYSpaces

namespace idunno.AtProto.AspNet.Spaces.Test;

public sealed class ManagedSpaceAccessEndpointTests
{
    private const string Space = "at:space/did:plc:ewvi7nxzyoun6zhxsrcy6jgr/app.bsky.bookmark/bookmark";

    /// <summary>
    /// Confirms that non-HTTPS requests are rejected before managed-space authentication.
    /// </summary>
    [Fact]
    public async Task MapManagedSpaceCheckAccess_InsecureRequest_ReturnsForbidden()
    {
        using IHost host = await CreateHostAsync();
        using HttpClient client = host.GetTestClient();

        using HttpResponseMessage response = await client.GetAsync(
            $"{ManagedSpaceAccessHandler.Path}?space={Uri.EscapeDataString(Space)}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("HttpsRequired", (await response.Content.ReadFromJsonAsync<ErrorResponse>(
            TestContext.Current.CancellationToken))?.Error);
    }

    /// <summary>
    /// Confirms that repeated XRPC query values are rejected before invoking the neutral handler.
    /// </summary>
    [Fact]
    public async Task MapManagedSpaceCheckAccess_RepeatedQueryValue_ReturnsBadRequest()
    {
        using IHost host = await CreateHostAsync();
        using HttpClient client = host.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        string encodedSpace = Uri.EscapeDataString(Space);

        using HttpResponseMessage response = await client.GetAsync(
            $"{ManagedSpaceAccessHandler.Path}?space={encodedSpace}&space={encodedSpace}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("InvalidRequest", (await response.Content.ReadFromJsonAsync<ErrorResponse>(
            TestContext.Current.CancellationToken))?.Error);
    }

    /// <summary>
    /// Confirms that repeated authorization headers are rejected before invoking the neutral handler.
    /// </summary>
    [Fact]
    public async Task MapManagedSpaceCheckAccess_RepeatedAuthorizationHeaders_ReturnsBadRequest()
    {
        using IHost host = await CreateHostAsync();
        using HttpClient client = host.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"{ManagedSpaceAccessHandler.Path}?space={Uri.EscapeDataString(Space)}");
        request.Headers.TryAddWithoutValidation("Authorization", ["Bearer one", "Bearer two"]);

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Confirms that an HTTPS request without a service token returns an XRPC authentication error.
    /// </summary>
    [Fact]
    public async Task MapManagedSpaceCheckAccess_MissingToken_ReturnsAuthenticationRequired()
    {
        using IHost host = await CreateHostAsync();
        using HttpClient client = host.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");

        using HttpResponseMessage response = await client.GetAsync(
            $"{ManagedSpaceAccessHandler.Path}?space={Uri.EscapeDataString(Space)}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("AuthenticationRequired", (await response.Content.ReadFromJsonAsync<ErrorResponse>(
            TestContext.Current.CancellationToken))?.Error);
    }

    /// <summary>
    /// Confirms a valid service token reaches the configured decider and returns its XRPC response.
    /// </summary>
    [Fact]
    public async Task MapManagedSpaceCheckAccess_ValidToken_ReturnsDeciderAccess()
    {
        FakeTimeProvider time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using IHost resolverHost = await CreateResolverHostAsync(key);
        using HttpClient resolverClient = resolverHost.GetTestClient();
        using IHost host = await CreateHostAsync(resolverClient, time);
        using HttpClient client = host.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        string token = CreateToken(key, time, "valid-endpoint-token");
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            $"{ManagedSpaceAccessHandler.Path}?space={Uri.EscapeDataString(Space)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using JsonDocument body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(body.RootElement.GetProperty("read").GetBoolean());
        Assert.True(body.RootElement.GetProperty("write").GetBoolean());
    }

    /// <summary>
    /// Creates a test host with a valid managing-app configuration and the checkAccess route mapped.
    /// </summary>
    private static async Task<IHost> CreateHostAsync(
        HttpClient? resolverClient = null,
        TimeProvider? timeProvider = null) =>
        await new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddManagedSpaceAccess<AllowAccessDecider>(
                        options => options.ManagingApp = new AtProtoServiceReference(
                            new Did("did:plc:g6ylltenitt4tp27bpwalh7b"),
                            "managed-space"));
                    if (timeProvider is not null)
                    {
                        services.AddSingleton<TimeProvider>(timeProvider);
                    }

                    if (resolverClient is not null)
                    {
                        services.AddScoped(
                            provider => new ManagedSpaceAccessHandler(
                                options: provider.GetRequiredService<IOptions<ManagedSpaceAccessHandlerOptions>>().Value,
                                decider: provider.GetRequiredService<IManagedSpaceAccessDecider>(),
                                replayGuard: provider.GetRequiredService<IServiceTokenReplayGuard>(),
                                httpClient: resolverClient,
                                timeProvider: timeProvider));
                    }
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapManagedSpaceCheckAccess());
                }))
            .StartAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// Creates a test DID resolver that serves the managing authority's signing key.
    /// </summary>
    private static async Task<IHost> CreateResolverHostAsync(ECDsa key) =>
        await new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .Configure(app => app.Run(async context =>
                {
                    Assert.Equal("/did:plc:ewvi7nxzyoun6zhxsrcy6jgr", context.Request.Path);
                    context.Response.ContentType = "application/json";
                    JsonObject document = new()
                    {
                        ["id"] = "did:plc:ewvi7nxzyoun6zhxsrcy6jgr",
                        ["verificationMethod"] = new JsonArray(
                            new JsonObject
                            {
                                ["id"] = "did:plc:ewvi7nxzyoun6zhxsrcy6jgr#atproto",
                                ["type"] = "Multikey",
                                ["controller"] = "did:plc:ewvi7nxzyoun6zhxsrcy6jgr",
                                ["publicKeyMultibase"] = ToMultibase(key)
                            })
                    };
                    await context.Response.WriteAsync(
                        document.ToJsonString(),
                        TestContext.Current.CancellationToken);
                })))
            .StartAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// Creates a correctly signed service token for the test managing application.
    /// </summary>
    private static string CreateToken(ECDsa key, TimeProvider time, string tokenId)
    {
        JsonObject payload = new()
        {
            ["iss"] = "did:plc:ewvi7nxzyoun6zhxsrcy6jgr",
            ["aud"] = "did:plc:g6ylltenitt4tp27bpwalh7b#managed-space",
            ["lxm"] = ManagedSpaceAccessHandler.MethodName,
            ["iat"] = time.GetUtcNow().ToUnixTimeSeconds(),
            ["exp"] = time.GetUtcNow().AddSeconds(60).ToUnixTimeSeconds(),
            ["jti"] = tokenId
        };
        string unsigned = $"{Encode("""{"typ":"JWT","alg":"ES256"}""")}.{Encode(payload.ToJsonString())}";
        byte[] signature = key.SignData(
            Encoding.ASCII.GetBytes(unsigned),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{unsigned}.{Encode(signature)}";
    }

    /// <summary>
    /// Encodes the test authority's public key as a DID multibase value.
    /// </summary>
    private static string ToMultibase(ECDsa key)
    {
        ECPoint point = key.ExportParameters(false).Q;
        byte[] compressed = [(point.Y![^1] & 1) == 1 ? (byte)0x03 : (byte)0x02, .. point.X!];
        byte[] prefix = [0x80, 0x24];
        return $"z{SimpleBase.Base58.Bitcoin.Encode([.. prefix, .. compressed])}";
    }

    /// <summary>
    /// Encodes bytes as an unpadded base64url segment.
    /// </summary>
    private static string Encode(string value) => Encode(Encoding.UTF8.GetBytes(value));

    /// <summary>
    /// Encodes bytes as an unpadded base64url segment.
    /// </summary>
    private static string Encode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// Supplies a test decision when a request reaches the application policy layer.
    /// </summary>
    private sealed class AllowAccessDecider : IManagedSpaceAccessDecider
    {
        /// <summary>
        /// Returns read and write access for the test request.
        /// </summary>
        /// <param name="check">The authenticated access check.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The test access decision.</returns>
        public ValueTask<ManagedSpaceAccess> CheckAccessAsync(
            ManagedSpaceAccessCheck check,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ManagedSpaceAccess { Read = true, Write = true });
    }

    /// <summary>
    /// Represents the XRPC error response received by the endpoint test.
    /// </summary>
    private sealed record ErrorResponse(string? Error, string? Message);
}
