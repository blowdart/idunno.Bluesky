// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable BSKYSpaces

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto.Space;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Time.Testing;

using static idunno.AtProto.Integration.Test.FirehoseTestData;

namespace idunno.AtProto.Integration.Test;

public sealed class ManagedSpaceAccessHandlerTests
{
    private const string SpaceUri = "at:space/did:plc:ewvi7nxzyoun6zhxsrcy6jgr/app.bsky.bookmark/bookmark";
    private static readonly AtProtoServiceReference s_managingApp = new(new Did(OtherDid), "managed-space");
    private static readonly Nsid s_method = new(ManagedSpaceAccessHandler.MethodName);

    /// <summary>
    /// Confirms a valid authority token reaches the decider and returns its access decision.
    /// </summary>
    [Fact]
    public async Task Handle_ValidToken_ReturnsDeciderAccess()
    {
        FakeTimeProvider time = CreateClock();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using TestServer resolver = CreateResolver(TestDid, key);
        using HttpClient client = resolver.CreateClient();
        CountingDecider decider = new(new ManagedSpaceAccess { Read = true, Write = false });
        ManagedSpaceAccessHandler handler = CreateHandler(decider, client, time);
        string token = CreateToken(key, time, TestDid, s_managingApp.ToString(), s_method.ToString(), "valid-token");

        ManagedSpaceAccessResponse response = await handler.Handle(
            authorization: $"Bearer {token}",
            space: SpaceUri,
            repo: OtherDid,
            clientId: "https://example.com/client.json",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(200, response.StatusCode);
        Assert.True(response.Succeeded);
        ManagedSpaceAccess access = Assert.IsType<ManagedSpaceAccess>(response.Access);
        Assert.True(access.Read);
        Assert.False(access.Write);
        Assert.Equal(1, decider.Calls);
        Assert.Equal(new SpaceAtUri(SpaceUri), decider.LastCheck?.Space);
        Assert.Equal(new Did(OtherDid), decider.LastCheck?.Repo);
        Assert.Equal(new Uri("https://example.com/client.json"), decider.LastCheck?.ClientId);
        using JsonDocument body = JsonDocument.Parse(response.Body);
        Assert.True(body.RootElement.GetProperty("read").GetBoolean());
        Assert.False(body.RootElement.GetProperty("write").GetBoolean());
    }

    /// <summary>
    /// Confirms the authority repository receives full access without invoking the decider.
    /// </summary>
    [Fact]
    public async Task Handle_AuthorityRepository_ReturnsFullAccessWithoutDecider()
    {
        FakeTimeProvider time = CreateClock();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using TestServer resolver = CreateResolver(TestDid, key);
        using HttpClient client = resolver.CreateClient();
        CountingDecider decider = new(new ManagedSpaceAccess { Read = false, Write = false });
        ManagedSpaceAccessHandler handler = CreateHandler(decider, client, time);
        string token = CreateToken(key, time, TestDid, s_managingApp.ToString(), s_method.ToString(), "authority-token");

        ManagedSpaceAccessResponse response = await handler.Handle(
            authorization: $"Bearer {token}",
            space: SpaceUri,
            repo: TestDid,
            clientId: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(200, response.StatusCode);
        ManagedSpaceAccess access = Assert.IsType<ManagedSpaceAccess>(response.Access);
        Assert.True(access.Read);
        Assert.True(access.Write);
        Assert.Equal(0, decider.Calls);
    }

    /// <summary>
    /// Confirms a service token cannot be accepted more than once by one replay guard.
    /// </summary>
    [Fact]
    public async Task Handle_ReplayedToken_RejectsSecondRequest()
    {
        FakeTimeProvider time = CreateClock();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using TestServer resolver = CreateResolver(TestDid, key);
        using HttpClient client = resolver.CreateClient();
        CountingDecider decider = new(new ManagedSpaceAccess { Read = true, Write = true });
        ManagedSpaceAccessHandler handler = CreateHandler(decider, client, time);
        string token = CreateToken(key, time, TestDid, s_managingApp.ToString(), s_method.ToString(), "replayed-token");
        string authorization = $"Bearer {token}";

        ManagedSpaceAccessResponse first = await handler.Handle(
            authorization,
            SpaceUri,
            repo: null,
            clientId: null,
            cancellationToken: TestContext.Current.CancellationToken);
        ManagedSpaceAccessResponse second = await handler.Handle(
            authorization,
            SpaceUri,
            repo: null,
            clientId: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(200, first.StatusCode);
        Assert.Equal(401, second.StatusCode);
        Assert.Equal("InvalidToken", second.Error);
        Assert.Equal(1, decider.Calls);
    }

    /// <summary>
    /// Confirms malformed space and requester values are rejected as XRPC invalid requests.
    /// </summary>
    [Theory]
    [InlineData("at://did:plc:ewvi7nxzyoun6zhxsrcy6jgr/app.bsky.bookmark/key", null, null)]
    [InlineData(SpaceUri, "not-a-did", null)]
    [InlineData(SpaceUri, null, "/relative-client")]
    public async Task Handle_InvalidRequestValues_ReturnsBadRequest(string space, string? repo, string? clientId)
    {
        FakeTimeProvider time = CreateClock();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        CountingDecider decider = new(new ManagedSpaceAccess { Read = true, Write = true });
        ManagedSpaceAccessHandler handler = CreateHandler(decider, httpClient: null, time);
        string token = CreateToken(key, time, TestDid, s_managingApp.ToString(), s_method.ToString(), "invalid-request-token");

        ManagedSpaceAccessResponse response = await handler.Handle(
            authorization: $"Bearer {token}",
            space: space,
            repo: repo,
            clientId: clientId,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("InvalidRequest", response.Error);
        Assert.Equal(0, decider.Calls);
    }

    /// <summary>
    /// Confirms service tokens bound to another audience or XRPC method are rejected.
    /// </summary>
    [Theory]
    [InlineData("did:plc:g6ylltenitt4tp27bpwalh7b#another-service", "com.atproto.managedspace.checkAccess")]
    [InlineData("did:plc:g6ylltenitt4tp27bpwalh7b#managed-space", "com.atproto.repo.getRecord")]
    public async Task Handle_WrongAudienceOrMethod_ReturnsUnauthorized(string audience, string method)
    {
        FakeTimeProvider time = CreateClock();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        CountingDecider decider = new(new ManagedSpaceAccess { Read = true, Write = true });
        ManagedSpaceAccessHandler handler = CreateHandler(decider, httpClient: null, time);
        string token = CreateToken(key, time, TestDid, audience, method, "wrong-scope-token");

        ManagedSpaceAccessResponse response = await handler.Handle(
            authorization: $"Bearer {token}",
            space: SpaceUri,
            repo: null,
            clientId: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(401, response.StatusCode);
        Assert.Equal("InvalidToken", response.Error);
        Assert.Equal(0, decider.Calls);
    }

    /// <summary>
    /// Confirms a valid token issued by a different DID is not accepted for the space authority.
    /// </summary>
    [Fact]
    public async Task Handle_TokenIssuerIsNotSpaceAuthority_ReturnsUnauthorized()
    {
        FakeTimeProvider time = CreateClock();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using TestServer resolver = CreateResolver(OtherDid, key);
        using HttpClient client = resolver.CreateClient();
        CountingDecider decider = new(new ManagedSpaceAccess { Read = true, Write = true });
        ManagedSpaceAccessHandler handler = CreateHandler(decider, client, time);
        string token = CreateToken(key, time, OtherDid, s_managingApp.ToString(), s_method.ToString(), "wrong-issuer-token");

        ManagedSpaceAccessResponse response = await handler.Handle(
            authorization: $"Bearer {token}",
            space: SpaceUri,
            repo: null,
            clientId: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(401, response.StatusCode);
        Assert.Equal("InvalidToken", response.Error);
        Assert.Equal(0, decider.Calls);
    }

    /// <summary>
    /// Creates a handler with the supplied decider, test HTTP client and clock.
    /// </summary>
    private static ManagedSpaceAccessHandler CreateHandler(
        CountingDecider decider,
        HttpClient? httpClient,
        TimeProvider timeProvider) =>
        new(
            options: new ManagedSpaceAccessHandlerOptions { ManagingApp = s_managingApp },
            decider: decider,
            replayGuard: new InMemoryServiceTokenReplayGuard(timeProvider: timeProvider),
            httpClient: httpClient,
            timeProvider: timeProvider);

    /// <summary>
    /// Creates a resolver that serves the selected issuer's test DID document.
    /// </summary>
    private static TestServer CreateResolver(string issuer, ECDsa key) =>
        TestServerBuilder.CreateServer("plc.directory", async context =>
        {
            Assert.Equal($"/{issuer}", context.Request.Path);
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(DidDocumentFor(issuer, "atproto", key), AtProtoServer.AtProtoJsonSerializerOptions),
                TestContext.Current.CancellationToken);
        });

    /// <summary>
    /// Creates a signed service token with the supplied claims and test clock.
    /// </summary>
    private static string CreateToken(
        ECDsa key,
        TimeProvider time,
        string issuer,
        string audience,
        string method,
        string tokenId)
    {
        JsonObject payload = new()
        {
            ["iss"] = issuer,
            ["aud"] = audience,
            ["lxm"] = method,
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
    /// Encodes bytes as an unpadded base64url segment.
    /// </summary>
    private static string Encode(string value) => Encode(Encoding.UTF8.GetBytes(value));

    /// <summary>
    /// Encodes bytes as an unpadded base64url segment.
    /// </summary>
    private static string Encode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// Creates the fixed clock used to issue and validate the test tokens.
    /// </summary>
    private static FakeTimeProvider CreateClock() =>
        new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// Captures how many authorization decisions the test handler evaluates.
    /// </summary>
    private sealed class CountingDecider(ManagedSpaceAccess access) : IManagedSpaceAccessDecider
    {
        /// <summary>
        /// Gets the number of access checks evaluated.
        /// </summary>
        public int Calls { get; private set; }

        /// <summary>
        /// Gets the last authenticated access check.
        /// </summary>
        public ManagedSpaceAccessCheck? LastCheck { get; private set; }

        /// <summary>
        /// Returns the configured decision and records the call.
        /// </summary>
        /// <param name="check">The authenticated access check.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The configured decision.</returns>
        public ValueTask<ManagedSpaceAccess> CheckAccess(
            ManagedSpaceAccessCheck check,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastCheck = check;
            return ValueTask.FromResult(access);
        }
    }
}
