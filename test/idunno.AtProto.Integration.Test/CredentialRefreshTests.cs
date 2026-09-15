// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Timers;

using idunno.AtProto.Authentication;
using idunno.AtProto.Authentication.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class CredentialRefreshTests
{
    private const string DomainName = "test.invalid";
    private const string ExpectedDid = "did:plc:ec72yg6n2sydzjvtovvdlxrk";

    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public CredentialRefreshTests()
    {
        _jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        _jsonSerializerOptions.TypeInfoResolverChain.Insert(0, SourceGenerationContext.Default);
        _jsonSerializerOptions.TypeInfoResolverChain.Insert(0, AtProto.SourceGenerationContext.Default);
    }

    [Fact]
    public async Task TheRefreshTimerElapsedHandlerIsOnlyEverSubscribedOnce()
    {
        RefreshTestServer refreshTestServer = new(this);

        using (AtProtoAgent agent = CreateAgent(refreshTestServer))
        {
            await Login(agent);

            Assert.Equal(1, CountElapsedSubscribers(agent));

            for (int i = 0; i < 4; i++)
            {
                Assert.True(await agent.RefreshCredentials(TestContext.Current.CancellationToken));

                // Subscribing on every start would double the subscriber count on every tick, and every subscriber
                // would race to spend the same single use refresh token.
                Assert.Equal(1, CountElapsedSubscribers(agent));
            }
        }
    }

    [Fact]
    public async Task ASuccessfulRefreshLeavesTheRefreshTimerRunning()
    {
        RefreshTestServer refreshTestServer = new(this);

        using (AtProtoAgent agent = CreateAgent(refreshTestServer))
        {
            await Login(agent);

            Assert.True(await agent.RefreshCredentials(TestContext.Current.CancellationToken));

            Assert.True(GetRefreshTimer(agent)!.Enabled);
        }
    }

    [Fact]
    public async Task AFailedBackgroundRefreshRestartsTheRefreshTimerSoTheRefreshIsRetried()
    {
        RefreshTestServer refreshTestServer = new(this);

        using (AtProtoAgent agent = CreateAgent(refreshTestServer))
        {
            await Login(agent);

            refreshTestServer.FailRefresh = true;

            await InvokeBackgroundRefresh(agent);

            // A single transient failure must not silently end background refresh for the lifetime of the agent.
            System.Timers.Timer? timer = GetRefreshTimer(agent);

            Assert.NotNull(timer);
            Assert.True(timer.Enabled);
        }
    }

    [Fact]
    public async Task ARefreshTokenWhichHasAlreadyBeenExchangedIsNotPresentedAgain()
    {
        RefreshTestServer refreshTestServer = new(this);

        using (AtProtoAgent agent = CreateAgent(refreshTestServer))
        {
            await Login(agent);

            AccessCredentials originalCredentials = agent.Credentials!;

            Assert.True(await agent.RefreshCredentials(originalCredentials, TestContext.Current.CancellationToken));
            Assert.Equal(1, refreshTestServer.RefreshCount);

            // Refresh tokens are single use, so a caller which lost the race to refresh must not present the spent token.
            Assert.True(await agent.RefreshCredentials(originalCredentials, TestContext.Current.CancellationToken));
            Assert.Equal(1, refreshTestServer.RefreshCount);
        }
    }

    [Fact]
    public async Task ConcurrentRefreshesOnlyExchangeTheRefreshTokenOnce()
    {
        RefreshTestServer refreshTestServer = new(this);

        using (AtProtoAgent agent = CreateAgent(refreshTestServer))
        {
            await Login(agent);

            AccessCredentials originalCredentials = agent.Credentials!;

            Task<bool>[] refreshes =
            [
                .. Enumerable.Range(0, 8).Select(_ => Task.Run(
                    () => agent.RefreshCredentials(originalCredentials, TestContext.Current.CancellationToken),
                    TestContext.Current.CancellationToken))
            ];

            bool[] results = await Task.WhenAll(refreshes);

            Assert.All(results, Assert.True);
            Assert.Equal(1, refreshTestServer.RefreshCount);
        }
    }

    private static int CountElapsedSubscribers(AtProtoAgent agent)
    {
        System.Timers.Timer? timer = GetRefreshTimer(agent);

        Assert.NotNull(timer);

        ElapsedEventHandler? handler = (ElapsedEventHandler?)typeof(System.Timers.Timer)
            .GetField("_onIntervalElapsed", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(timer);

        return handler is null ? 0 : handler.GetInvocationList().Length;
    }

    private static System.Timers.Timer? GetRefreshTimer(AtProtoAgent agent)
    {
        return (System.Timers.Timer?)typeof(AtProtoAgent)
            .GetField("_credentialRefreshTimer", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(agent);
    }

    private static async Task InvokeBackgroundRefresh(AtProtoAgent agent)
    {
        MethodInfo backgroundRefresh = typeof(AtProtoAgent).GetMethod(
            "BackgroundRefreshCredentials",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)backgroundRefresh.Invoke(agent, null)!;
    }

    private static AtProtoAgent CreateAgent(RefreshTestServer refreshTestServer)
    {
        return new AtProtoAgent(
            new Uri($"https://{DomainName}"),
            new TestHttpClientFactory(refreshTestServer.TestServer),
            new AtProtoAgentOptions()
            {
                PlcDirectoryServer = new Uri($"https://{DomainName}")
            });
    }

    private static async Task Login(AtProtoAgent agent)
    {
        AtProtoHttpResult<bool> loginResult = await agent.Login(
            did: ExpectedDid,
            password: "password",
            authFactorToken: null,
            service: new Uri($"https://{DomainName}"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(loginResult.Succeeded);
        Assert.True(agent.IsAuthenticated);
    }

    /// <summary>
    /// A test server which supports session creation and refresh, counting how often a session is refreshed.
    /// </summary>
    private sealed class RefreshTestServer
    {
        private int _refreshCount;
        private int _tokenSerialNumber;

        internal RefreshTestServer(CredentialRefreshTests test)
        {
            TestServer = TestServerBuilder.CreateServer(DomainName, context => Handle(test, context));
        }

        internal TestServer TestServer { get; }

        internal bool FailRefresh { get; set; }

        internal int RefreshCount => Volatile.Read(ref _refreshCount);

        private async Task Handle(CredentialRefreshTests test, HttpContext context)
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Path == $"/{ExpectedDid}")
            {
                response.StatusCode = 200;
                DidDocument didDocument = new(
                    id: $"did:web:{DomainName}",
                    context: ["https://www.w3.org/ns/did/v1"],
                    alsoKnownAs: null,
                    verificationMethods: null,
                    services: [new(id: "#atproto_pds", type: "atprotopds", serviceEndpoint: new Uri($"https://{DomainName}"))]);
                await response.WriteAsJsonAsync(didDocument, test._jsonSerializerOptions);
            }
            else if (request.Path == "/xrpc/com.atproto.server.createSession" && request.Method == HttpMethod.Post.Method)
            {
                response.StatusCode = 200;

                await response.WriteAsJsonAsync(
                    new CreateSessionResponse(
                        accessJwt: CreateAccessJwt(),
                        refreshJwt: NextRefreshToken(),
                        handle: DomainName,
                        did: ExpectedDid),
                    test._jsonSerializerOptions);
            }
            else if (request.Path == "/xrpc/com.atproto.server.refreshSession" && request.Method == HttpMethod.Post.Method)
            {
                if (FailRefresh)
                {
                    response.StatusCode = 400;
                    return;
                }

                Interlocked.Increment(ref _refreshCount);

                response.StatusCode = 200;

                await response.WriteAsJsonAsync(
                    new RefreshSessionResponse(
                        accessJwt: CreateAccessJwt(),
                        refreshJwt: NextRefreshToken(),
                        handle: new Handle(DomainName),
                        did: new Did(ExpectedDid),
                        didDoc: null,
                        active: true,
                        status: null),
                    test._jsonSerializerOptions);
            }
            else if (request.Path == "/xrpc/com.atproto.server.getSession")
            {
                response.StatusCode = 200;

                await response.WriteAsJsonAsync(
                    new GetSessionResponse(
                        handle: new Handle(DomainName),
                        did: new Did(ExpectedDid),
                        email: null,
                        emailConfirmed: null,
                        emailAuthFactor: null,
                        didDoc: null,
                        active: true,
                        status: null),
                    test._jsonSerializerOptions);
            }
            else
            {
                response.StatusCode = 404;
            }
        }

        private static string CreateAccessJwt() => JwtBuilder.CreateJwt(new Did(ExpectedDid), $"did:web:{DomainName}");

        private string NextRefreshToken() => $"refreshToken{Interlocked.Increment(ref _tokenSerialNumber)}";
    }
}
