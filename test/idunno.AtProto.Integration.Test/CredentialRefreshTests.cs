// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Timers;

using idunno.AtProto.Authentication;
using idunno.AtProto.Authentication.Models;
using idunno.AtProto.Events;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.IdentityModel.Tokens;

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
    public async Task AFailedRefreshOfANearExpiryTokenIssuedAtLoginStillSchedulesARetry()
    {
        RefreshTestServer refreshTestServer = new(this)
        {
            // Short enough that StartTokenRefreshTimer refreshes immediately instead of creating the timer.
            AccessJwtLifetime = TimeSpan.FromSeconds(30),
            FailRefresh = true
        };

        using (AtProtoAgent agent = CreateAgent(refreshTestServer))
        {
            await Login(agent);

            // Login kicks the immediate refresh off in the background, so wait for it to have been attempted and failed.
            while (refreshTestServer.RefreshAttemptCount == 0)
            {
                await Task.Delay(25, TestContext.Current.CancellationToken);
            }

            await Task.Delay(250, TestContext.Current.CancellationToken);

            // The timer does not exist yet on this path, so the retry has nothing to restart unless one is created.
            System.Timers.Timer? timer = GetRefreshTimer(agent);

            Assert.NotNull(timer);
            Assert.True(timer.Enabled);
        }
    }

    [Fact]
    public async Task ARefreshTokenSpentSeveralRefreshesAgoIsStillNotPresentedAgain()
    {
        RefreshTestServer refreshTestServer = new(this);

        using (AtProtoAgent agent = CreateAgent(refreshTestServer))
        {
            await Login(agent);

            AccessCredentials originalCredentials = agent.Credentials!;

            Assert.True(await agent.RefreshCredentials(originalCredentials, TestContext.Current.CancellationToken));
            Assert.Equal(1, refreshTestServer.RefreshCount);

            for (int i = 0; i < 2; i++)
            {
                Assert.True(await agent.RefreshCredentials(TestContext.Current.CancellationToken));
            }

            Assert.Equal(3, refreshTestServer.RefreshCount);

            // Remembering only the most recently spent token would let this stale credential present a spent token.
            Assert.True(await agent.RefreshCredentials(originalCredentials, TestContext.Current.CancellationToken));
            Assert.Equal(3, refreshTestServer.RefreshCount);
        }
    }

    [Fact]
    public async Task LoggingOutForgetsEveryRememberedRefreshToken()
    {
        RefreshTestServer refreshTestServer = new(this);

        using (AtProtoAgent agent = CreateAgent(refreshTestServer))
        {
            await Login(agent);

            Assert.True(await agent.RefreshCredentials(TestContext.Current.CancellationToken));
            Assert.NotEmpty(GetExchangedRefreshTokens(agent));

            await agent.Logout(TestContext.Current.CancellationToken);

            // A spent refresh token must not outlive the session it belonged to.
            Assert.Empty(GetExchangedRefreshTokens(agent));
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

    [Fact]
    public async Task CredentialsReplacedWhilstARefreshIsInFlightAreNotOverwrittenByThatRefresh()
    {
        RefreshTestServer refreshTestServer = new(this) { GateRefresh = true };

        using (AtProtoAgent agent = CreateAgent(refreshTestServer))
        {
            await Login(agent);

            AccessCredentials originalCredentials = agent.Credentials!;

            Task<bool> refresh = Task.Run(
                () => agent.RefreshCredentials(originalCredentials, TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken);

            await refreshTestServer.RefreshEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

            AccessCredentials replacementCredentials = new(
                new Uri($"https://{DomainName}"),
                AuthenticationType.UsernamePassword,
                JwtBuilder.CreateJwt(new Did(ExpectedDid), $"did:web:{DomainName}"),
                "replacementRefreshToken");

            agent.Credentials = replacementCredentials;

            refreshTestServer.ReleaseRefresh.TrySetResult();

            // The refresh started against credentials which have since been replaced, so what it was issued is discarded
            // rather than published over whatever replaced them.
            Assert.False(await refresh);
            Assert.Same(replacementCredentials, agent.Credentials);
        }
    }

    [Fact]
    public async Task ALogoutDoesNotRunWhilstARefreshIsInFlight()
    {
        RefreshTestServer refreshTestServer = new(this) { GateRefresh = true };

        using (AtProtoAgent agent = CreateAgent(refreshTestServer))
        {
            await Login(agent);

            Task<bool> refresh = Task.Run(
                () => agent.RefreshCredentials(agent.Credentials!, TestContext.Current.CancellationToken),
                TestContext.Current.CancellationToken);

            await refreshTestServer.RefreshEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

            Task logout = Task.Run(() => agent.Logout(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

            Task firstToComplete = await Task.WhenAny(logout, Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken));

            // A logout which revoked the tokens whilst a refresh was mid exchange could have the refreshed credentials
            // published over the cleared ones, leaving the agent authenticated against a session the logout ended.
            Assert.NotSame(logout, firstToComplete);

            refreshTestServer.ReleaseRefresh.TrySetResult();

            Assert.True(await refresh);

            await logout;

            Assert.False(agent.IsAuthenticated);
            Assert.Null(agent.Credentials);
        }
    }

    [Fact]
    public async Task AFailedUserInitiatedRefreshRestartsTheRefreshTimerSoTheRefreshIsRetried()
    {
        RefreshTestServer refreshTestServer = new(this);

        using (AtProtoAgent agent = CreateAgent(refreshTestServer))
        {
            await Login(agent);

            refreshTestServer.FailRefresh = true;

            Assert.False(await agent.RefreshCredentials(TestContext.Current.CancellationToken));

            // A refresh started by the caller stops the timer just as a background refresh does, so a failure must not
            // leave it stopped either, otherwise one failed call silently ends background refresh for the agent's lifetime.
            System.Timers.Timer? timer = GetRefreshTimer(agent);

            Assert.NotNull(timer);
            Assert.True(timer.Enabled);
        }
    }

    [Fact]
    public async Task ARefreshWhichThrowsRestartsTheRefreshTimerSoTheRefreshIsRetried()
    {
        RefreshTestServer refreshTestServer = new(this);

        using (AtProtoAgent agent = CreateAgent(refreshTestServer))
        {
            await Login(agent);

            refreshTestServer.IssueUnvalidatableAccessJwtOnRefresh = true;

            await Assert.ThrowsAsync<SecurityTokenValidationException>(
                () => agent.RefreshCredentials(TestContext.Current.CancellationToken));

            System.Timers.Timer? timer = GetRefreshTimer(agent);

            Assert.NotNull(timer);
            Assert.True(timer.Enabled);
        }
    }

    [Fact]
    public async Task ARefreshTokenTheServerHasSpentIsRememberedEvenWhenTheIssuedTokenCannotBeValidated()
    {
        RefreshTestServer refreshTestServer = new(this);

        using (AtProtoAgent agent = CreateAgent(refreshTestServer))
        {
            await Login(agent);

            string spentRefreshToken = agent.Credentials!.RefreshToken;

            refreshTestServer.IssueUnvalidatableAccessJwtOnRefresh = true;

            await Assert.ThrowsAsync<SecurityTokenValidationException>(
                () => agent.RefreshCredentials(TestContext.Current.CancellationToken));

            // The server exchanged the token before the response failed validation. Forgetting that leaves the retry
            // presenting a spent token, which on a server which revokes on reuse ends the session.
            Assert.Contains(spentRefreshToken, GetExchangedRefreshTokens(agent));
        }
    }

    [Fact]
    public async Task ARefreshWhichTheServerRejectsDoesNotRememberTheRefreshToken()
    {
        RefreshTestServer refreshTestServer = new(this);

        using (AtProtoAgent agent = CreateAgent(refreshTestServer))
        {
            await Login(agent);

            string refreshToken = agent.Credentials!.RefreshToken;

            refreshTestServer.FailRefresh = true;

            Assert.False(await agent.RefreshCredentials(TestContext.Current.CancellationToken));

            // The server never exchanged this token, so it must remain usable.
            Assert.DoesNotContain(refreshToken, GetExchangedRefreshTokens(agent));
        }
    }

    [Fact]
    public async Task ARefreshTokenSpentByAnExchangeWhichNeverCompletedIsNotReportedAsASuccessfulRefresh()
    {
        RefreshTestServer refreshTestServer = new(this);

        using (AtProtoAgent agent = CreateAgent(refreshTestServer))
        {
            await Login(agent);

            refreshTestServer.IssueUnvalidatableAccessJwtOnRefresh = true;

            await Assert.ThrowsAsync<SecurityTokenValidationException>(
                () => agent.RefreshCredentials(TestContext.Current.CancellationToken));

            refreshTestServer.IssueUnvalidatableAccessJwtOnRefresh = false;

            // The token is remembered before the exchange completes, so that a failure part way through cannot let a retry
            // re-present it. Treating that as a completed refresh tells the caller the agent holds fresh credentials when
            // it is still holding the ones which could not be refreshed.
            Assert.False(await agent.RefreshCredentials(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task ABackgroundRefreshOfATokenSpentByAnExchangeWhichNeverCompletedKeepsRetrying()
    {
        RefreshTestServer refreshTestServer = new(this);

        using (AtProtoAgent agent = CreateAgent(refreshTestServer))
        {
            await Login(agent);

            refreshTestServer.IssueUnvalidatableAccessJwtOnRefresh = true;

            await Assert.ThrowsAsync<SecurityTokenValidationException>(
                () => agent.RefreshCredentials(TestContext.Current.CancellationToken));

            StopRefreshTimer(agent);
            Assert.False(GetRefreshTimer(agent)!.Enabled);

            await InvokeBackgroundRefresh(agent);

            // Reporting the remembered token as a completed refresh leaves the timer stopped, so the agent sits on
            // credentials it never refreshed until they expire.
            Assert.True(GetRefreshTimer(agent)!.Enabled);
        }
    }

    [Fact]
    public async Task ARefreshWhoseIssuedAccessTokenCannotBeValidatedRaisesTokenRefreshFailed()
    {
        RefreshTestServer refreshTestServer = new(this);

        using (AtProtoAgent agent = CreateAgent(refreshTestServer))
        {
            await Login(agent);

            TokenRefreshFailedEventArgs? failure = null;
            agent.TokenRefreshFailed += (sender, e) => failure = e;

            refreshTestServer.IssueUnvalidatableAccessJwtOnRefresh = true;

            await Assert.ThrowsAsync<SecurityTokenValidationException>(
                () => agent.RefreshCredentials(TestContext.Current.CancellationToken));

            // Without this the application has no signal that the session is over, and no chance to authenticate again.
            Assert.NotNull(failure);
            Assert.Equal(ExpectedDid, failure.Did.ToString());
            Assert.Null(failure.StatusCode);
        }
    }

    [Fact]
    public async Task ATokenRefreshFailedHandlerWhichRefreshesTheAgentDoesNotDeadlockTheRefresh()
    {
        RefreshTestServer refreshTestServer = new(this);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (AtProtoAgent agent = CreateAgent(refreshTestServer))
        {
            await Login(agent);

            refreshTestServer.FailRefresh = true;

            bool reentered = false;

            agent.TokenRefreshFailed += (sender, e) =>
            {
                if (!reentered)
                {
                    reentered = true;

                    // The handler runs on the thread which raised the event. Raising whilst the refresh semaphore is held
                    // leaves this waiting on a semaphore only the thread it has blocked can release.
                    agent.RefreshCredentials(cancellationToken).GetAwaiter().GetResult();
                }
            };

            Task<bool> refresh = agent.RefreshCredentials(cancellationToken);

            Assert.Same(refresh, await Task.WhenAny(refresh, Task.Delay(TimeSpan.FromSeconds(30), cancellationToken)));
            Assert.False(await refresh);
            Assert.True(reentered);
        }
    }

    [Fact]
    public async Task AnAccessTokenWhichIsAlreadyCloseToExpiryIsRefreshedThroughTheTimerRatherThanInline()
    {
        RefreshTestServer refreshTestServer = new(this)
        {
            // Short enough that the refresh timer start refreshes immediately rather than waiting.
            AccessJwtLifetime = TimeSpan.FromSeconds(30)
        };

        using (AtProtoAgent agent = CreateAgent(refreshTestServer))
        {
            await Login(agent);

            // Refreshing inline recurses, because the refresh starts the timer whilst it still holds the refresh
            // semaphore, and a server issuing tokens this short lived never lets that chain end.
            System.Timers.Timer? timer = GetRefreshTimer(agent);

            Assert.NotNull(timer);
            Assert.True(timer.Enabled);
            Assert.Equal(TimeSpan.FromSeconds(1).TotalMilliseconds, timer.Interval);
        }
    }

    [Fact]
    public async Task SettingCredentialsOnADisposedAgentThrows()
    {
        RefreshTestServer refreshTestServer = new(this);

        AtProtoAgent agent = CreateAgent(refreshTestServer);
        AccessCredentials credentials;

        using (agent)
        {
            await Login(agent);

            credentials = agent.Credentials!;
        }

        // Silently discarding the credential leaves the caller believing the agent has been given one.
        Assert.Throws<ObjectDisposedException>(() => agent.Credentials = credentials);
    }

    private static void StopRefreshTimer(AtProtoAgent agent)
    {
        typeof(AtProtoAgent)
            .GetMethod("StopTokenRefreshTimer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(agent, [false]);
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

    private static IEnumerable<string> GetExchangedRefreshTokens(AtProtoAgent agent)
    {
        return (Queue<string>)typeof(AtProtoAgent)
            .GetField("_exchangedRefreshTokens", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(agent)!;
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
        private int _refreshAttemptCount;
        private int _tokenSerialNumber;

        internal RefreshTestServer(CredentialRefreshTests test)
        {
            TestServer = TestServerBuilder.CreateServer(DomainName, context => Handle(test, context));
        }

        internal TestServer TestServer { get; }

        internal bool FailRefresh { get; set; }

        internal bool IssueUnvalidatableAccessJwtOnRefresh { get; set; }

        internal bool GateRefresh { get; set; }

        /// <summary>
        /// Completed once a refresh has reached the server and is waiting on <see cref="ReleaseRefresh"/>.
        /// </summary>
        internal TaskCompletionSource RefreshEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Completing this lets a gated refresh finish, so a test can do work whilst the refresh is in flight.
        /// </summary>
        internal TaskCompletionSource ReleaseRefresh { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TimeSpan AccessJwtLifetime { get; set; } = TimeSpan.FromMinutes(15);

        internal int RefreshCount => Volatile.Read(ref _refreshCount);

        internal int RefreshAttemptCount => Volatile.Read(ref _refreshAttemptCount);

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
                if (GateRefresh)
                {
                    RefreshEntered.TrySetResult();
                    await ReleaseRefresh.Task.ConfigureAwait(false);
                }

                if (FailRefresh)
                {
                    Interlocked.Increment(ref _refreshAttemptCount);
                    response.StatusCode = 400;
                    return;
                }

                Interlocked.Increment(ref _refreshAttemptCount);
                Interlocked.Increment(ref _refreshCount);

                response.StatusCode = 200;

                await response.WriteAsJsonAsync(
                    new RefreshSessionResponse(
                        accessJwt: IssueUnvalidatableAccessJwtOnRefresh ? CreateUnvalidatableAccessJwt() : CreateAccessJwt(),
                        refreshJwt: NextRefreshToken(),
                        handle: new Handle(DomainName),
                        did: new Did(ExpectedDid),
                        didDoc: null,
                        active: true,
                        status: null),
                    test._jsonSerializerOptions);
            }
            else if (request.Path == "/xrpc/com.atproto.server.deleteSession" && request.Method == HttpMethod.Post.Method)
            {
                response.StatusCode = 200;
                await response.WriteAsJsonAsync(new EmptyResponse(), test._jsonSerializerOptions);
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

        private string CreateAccessJwt() => JwtBuilder.CreateJwt(new Did(ExpectedDid), $"did:web:{DomainName}", expiresIn: AccessJwtLifetime);

        /// <summary>
        /// Creates an access token whose audience is not the service it was requested from, so validation of it fails.
        /// </summary>
        private string CreateUnvalidatableAccessJwt() => JwtBuilder.CreateJwt(new Did(ExpectedDid), "did:web:elsewhere.invalid", expiresIn: AccessJwtLifetime);

        private string NextRefreshToken() => $"refreshToken{Interlocked.Increment(ref _tokenSerialNumber)}";
    }
}
