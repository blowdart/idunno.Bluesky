// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

using idunno.AtProto.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.IdentityModel.Tokens;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class OAuthCredentialRefreshTests
{
    private const string DomainName = "oauth.test.internal";
    private const string ServerDid = $"did:web:{DomainName}";
    private const string Authority = $"https://{DomainName}/";
    private const string AccountDid = "did:plc:oauthaccount";
    private const string OtherDid = "did:plc:someoneelse";

    /// <summary>
    /// A public signing key for the test authorization server's discovery document. Duende will not load a discovery
    /// document which does not offer a key set.
    /// </summary>
    private static readonly string s_signingKeyJson = CreatePublicSigningKeyJson();

    private static string CreatePublicSigningKeyJson()
    {
        using RSA rsa = RSA.Create(2048);

        RSAParameters parameters = rsa.ExportParameters(false);

        string modulus = Base64UrlEncoder.Encode(parameters.Modulus!);
        string exponent = Base64UrlEncoder.Encode(parameters.Exponent!);

        return $$"""{"kty":"RSA","use":"sig","alg":"RS256","kid":"test","n":"{{modulus}}","e":"{{exponent}}"}""";
    }

    [Fact]
    public async Task ARefreshWhichIssuesATokenForTheSameAccountSucceeds()
    {
        OAuthTestServer server = new();
        using AtProtoAgent agent = CreateAgent(server);

        await Login(agent);

        Assert.True(await agent.RefreshCredentials(TestContext.Current.CancellationToken));

        Assert.NotNull(agent.Credentials);
        Assert.Equal(new Did(AccountDid), agent.Credentials.Did);
        Assert.Equal(server.LastIssuedRefreshToken, agent.Credentials.RefreshToken);
    }

    [Fact]
    public async Task ANonceUpdateDuringRefreshDoesNotDiscardTheRefreshedCredentials()
    {
        OAuthTestServer server = new() { GateRefresh = true };
        using OAuthTestAgent agent = (OAuthTestAgent)CreateAgent(server);

        await Login(agent);

        DPoPAccessCredentials originalCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);
        Task<bool> refresh = agent.RefreshCredentials(TestContext.Current.CancellationToken);

        await server.RefreshEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

        originalCredentials.DPoPNonce = "rotatedNonce";
        await agent.NotifyCredentialsUpdated(originalCredentials, TestContext.Current.CancellationToken);

        server.ReleaseRefresh.TrySetResult();

        Assert.True(await refresh);
        DPoPAccessCredentials refreshedCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);
        Assert.NotSame(originalCredentials, refreshedCredentials);
        Assert.Equal("rotatedNonce", refreshedCredentials.DPoPNonce);
    }

    [Fact]
    public async Task AStaleNonceUpdateAfterRefreshDoesNotReplaceTheRefreshedCredentials()
    {
        OAuthTestServer server = new();
        using OAuthTestAgent agent = (OAuthTestAgent)CreateAgent(server);

        await Login(agent);

        DPoPAccessCredentials originalCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);
        Assert.True(await agent.RefreshCredentials(TestContext.Current.CancellationToken));

        DPoPAccessCredentials refreshedCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);
        int credentialsUpdatedCount = 0;
        agent.CredentialsUpdated += (_, _) => credentialsUpdatedCount++;

        originalCredentials.DPoPNonce = "staleNonce";
        await agent.NotifyCredentialsUpdated(originalCredentials, TestContext.Current.CancellationToken);

        Assert.Same(refreshedCredentials, agent.Credentials);
        Assert.Equal(0, credentialsUpdatedCount);
    }

    [Fact]
    public async Task ASuspendedNonceNotificationCannotPersistCredentialsSupersededByARefresh()
    {
        OAuthTestServer server = new() { GateRefresh = true };
        using OAuthTestAgent agent = (OAuthTestAgent)CreateAgent(server);

        await Login(agent);

        DPoPAccessCredentials originalCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);

        TaskCompletionSource releaseFirstNotification = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource firstNotificationEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        AccessCredentials? lastPersistedCredentials = null;
        int notificationCount = 0;

        agent.CredentialsUpdatedAsync = async (e, _) =>
        {
            if (Interlocked.Increment(ref notificationCount) == 1)
            {
                firstNotificationEntered.TrySetResult();
                await releaseFirstNotification.Task;
            }

            lastPersistedCredentials = e.AccessCredentials;
        };

        originalCredentials.DPoPNonce = "rotatedNonce";
        Task notification = agent.NotifyCredentialsUpdated(originalCredentials, TestContext.Current.CancellationToken);

        await firstNotificationEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

        Task<bool> refresh = agent.RefreshCredentials(TestContext.Current.CancellationToken);

        await server.RefreshEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
        server.ReleaseRefresh.TrySetResult();

        // Give the refresh time to publish its credentials and reach the point where it notifies handlers, so the
        // notification for the superseded credentials is the one which completes last.
        await Task.Delay(500, TestContext.Current.CancellationToken);

        releaseFirstNotification.TrySetResult();

        await notification;
        Assert.True(await refresh);

        // The refreshed credentials must be the last ones a handler was given, otherwise a handler which persists
        // them would leave a spent refresh token in durable storage.
        Assert.Equal(2, notificationCount);
        Assert.Same(agent.Credentials, lastPersistedCredentials);
    }

    [Fact]
    public async Task ACancelledRefreshStillNotifiesCredentialsItHasAlreadyCommitted()
    {
        OAuthTestServer server = new();
        using OAuthTestAgent agent = (OAuthTestAgent)CreateAgent(server);

        await Login(agent);

        DPoPAccessCredentials originalCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);

        TaskCompletionSource releaseFirstNotification = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource firstNotificationEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        AccessCredentials? lastPersistedCredentials = null;
        int notificationCount = 0;

        agent.CredentialsUpdatedAsync = async (e, _) =>
        {
            if (Interlocked.Increment(ref notificationCount) == 1)
            {
                firstNotificationEntered.TrySetResult();
                await releaseFirstNotification.Task;
            }

            lastPersistedCredentials = e.AccessCredentials;
        };

        originalCredentials.DPoPNonce = "rotatedNonce";
        Task notification = agent.NotifyCredentialsUpdated(originalCredentials, TestContext.Current.CancellationToken);

        await firstNotificationEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

        using CancellationTokenSource refreshCancellation = new();
        Task<bool> refresh = agent.RefreshCredentials(refreshCancellation.Token);

        // Wait until the refresh has committed its credentials, then cancel the caller. The notification for
        // credentials the agent is already using must still be raised, otherwise the suspended handler would be the
        // last to write and would persist the refresh token the server has spent.
        while (ReferenceEquals(agent.Credentials, originalCredentials))
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        await refreshCancellation.CancelAsync();

        releaseFirstNotification.TrySetResult();

        await notification;
        Assert.True(await refresh);

        Assert.Equal(2, notificationCount);
        Assert.Same(agent.Credentials, lastPersistedCredentials);
    }

    [Fact]
    public async Task AHandlerWhichRefreshesBeforePersistingIsGivenTheRefreshedCredentialsLast()
    {
        OAuthTestServer server = new();
        using OAuthTestAgent agent = (OAuthTestAgent)CreateAgent(server);

        await Login(agent);

        DPoPAccessCredentials originalCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);

        List<AccessCredentials> persisted = [];
        bool refreshed = false;

        agent.CredentialsUpdatedAsync = async (e, cancellationToken) =>
        {
            // A handler which calls back into the agent before it persists what it was given. The refresh replaces the
            // credentials this handler is holding, so its notification has to be the last one raised.
            if (!refreshed)
            {
                refreshed = true;
                Assert.True(await agent.RefreshCredentials(cancellationToken));
            }

            persisted.Add(e.AccessCredentials);
        };

        originalCredentials.DPoPNonce = "rotatedNonce";
        await agent.NotifyCredentialsUpdated(originalCredentials, TestContext.Current.CancellationToken);

        Assert.Equal(2, persisted.Count);
        Assert.Same(originalCredentials, persisted[0]);
        Assert.Same(agent.Credentials, persisted[1]);
    }

    [Fact]
    public async Task AHandlerWhichRefreshesAndThenThrowsStillHasTheRefreshedCredentialsNotified()
    {
        OAuthTestServer server = new();
        using OAuthTestAgent agent = (OAuthTestAgent)CreateAgent(server);

        await Login(agent);

        DPoPAccessCredentials originalCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);

        List<AccessCredentials> persisted = [];
        bool refreshed = false;

        agent.CredentialsUpdatedAsync = async (e, cancellationToken) =>
        {
            // A handler which spends the refresh token by calling back into the agent, and then fails. The credentials
            // the refresh issued must still reach a handler, otherwise nothing can persist them.
            if (!refreshed)
            {
                refreshed = true;
                Assert.True(await agent.RefreshCredentials(cancellationToken));

                throw new InvalidOperationException("Persistence failed.");
            }

            persisted.Add(e.AccessCredentials);
        };

        originalCredentials.DPoPNonce = "rotatedNonce";

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => agent.NotifyCredentialsUpdated(originalCredentials, TestContext.Current.CancellationToken));

        AccessCredentials refreshedCredentials = Assert.IsType<DPoPAccessCredentials>(agent.Credentials);

        Assert.Same(refreshedCredentials, Assert.Single(persisted));
    }

    [Fact]
    public async Task ARefreshWhichIssuesATokenForADifferentAccountIsRejected()
    {
        OAuthTestServer server = new() { IssuedDid = new Did(OtherDid) };
        using AtProtoAgent agent = CreateAgent(server);

        await Login(agent);

        AccessCredentials credentialsBeforeRefresh = agent.Credentials!;

        await Assert.ThrowsAsync<SecurityTokenValidationException>(
            () => agent.RefreshCredentials(TestContext.Current.CancellationToken));

        // The credentials the agent presents must still be the ones it was authenticated with, rather than the ones
        // issued for another actor.
        Assert.Same(credentialsBeforeRefresh, agent.Credentials);
        Assert.Equal(new Did(AccountDid), agent.Credentials!.Did);

        bool unauthenticatedEventRaised = false;
        agent.Unauthenticated += (_, _) => unauthenticatedEventRaised = true;

        Assert.False(await agent.RefreshCredentials(TestContext.Current.CancellationToken));
        Assert.Null(agent.Credentials);
        Assert.True(unauthenticatedEventRaised);
    }

    [Fact]
    public async Task ARefreshOfASuppliedCredentialWhichIssuesATokenForADifferentAccountIsRejected()
    {
        OAuthTestServer server = new() { IssuedDid = new Did(OtherDid) };
        using AtProtoAgent agent = CreateAgent(server);

        DPoPAccessCredentials credentials = CreateCredentials();

        await Assert.ThrowsAsync<SecurityTokenValidationException>(
            () => agent.RefreshCredentials(credentials, TestContext.Current.CancellationToken));

        Assert.Null(agent.Credentials);
    }

    private static DPoPAccessCredentials CreateCredentials() =>
        new(service: new Uri($"https://{DomainName}"),
            accessJwt: CreateAccessJwt(new Did(AccountDid)),
            refreshToken: "initialRefreshToken",
            dPoPProofKey: JwtBuilder.CreateProofKey(),
            dPoPNonce: "nonce");

    private static string CreateAccessJwt(Did did) =>
        JwtBuilder.CreateJwt(did, issuer: Authority, audience: ServerDid, scope: "atproto");

    private static async Task Login(AtProtoAgent agent)
    {
        Assert.True(await agent.Login(CreateCredentials(), TestContext.Current.CancellationToken));
        Assert.True(agent.IsAuthenticated);
    }

    private static AtProtoAgent CreateAgent(OAuthTestServer server) =>
        new OAuthTestAgent(server)
        {
            Options = new AtProtoAgentOptions
            {
                OAuthOptions = new OAuthOptions("https://client.test/clientMetadata.json")
            }
        };

    /// <summary>
    /// An agent whose OAuth exchanges are routed at the in memory test server rather than the network.
    /// </summary>
    private sealed class OAuthTestAgent(OAuthTestServer server) : AtProtoAgent(
        new Uri($"https://{DomainName}"),
        new TestHttpClientFactory(server.TestServer),
        new AtProtoAgentOptions
        {
            PlcDirectoryServer = new Uri($"https://{DomainName}"),
            OAuthOptions = new OAuthOptions("https://client.test/clientMetadata.json")
        })
    {
        private readonly OAuthTestServer _server = server;

        internal Task NotifyCredentialsUpdated(AtProtoCredential credentials, CancellationToken cancellationToken) =>
            InternalOnCredentialsUpdatedCallBack(credentials, cancellationToken);

        public override OAuthClient CreateOAuthClient() =>
            new(httpClientConfigurator: httpClient => httpClient,
                innerHandlerFactory: _server.TestServer.CreateHandler,
                loggerFactory: null,
                options: Options?.OAuthOptions);
    }

    /// <summary>
    /// A test server which serves authorization server discovery, a token endpoint and enough of a personal data
    /// server for an OAuth credential refresh to complete.
    /// </summary>
    private sealed class OAuthTestServer
    {
        private int _tokenSerialNumber;

        internal bool GateRefresh { get; set; }

        internal OAuthTestServer()
        {
            TestServer = TestServerBuilder.CreateServer(DomainName, Handle);
        }

        internal TestServer TestServer { get; }

        internal TaskCompletionSource RefreshEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource ReleaseRefresh { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Gets or sets the DID the token endpoint issues tokens for. Defaults to the account being refreshed.
        /// </summary>
        internal Did IssuedDid { get; set; } = new Did(AccountDid);

        internal string? LastIssuedRefreshToken { get; private set; }

        private async Task Handle(HttpContext context)
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            switch (request.Path)
            {
                case "/.well-known/oauth-protected-resource":
                    response.ContentType = "application/json";
                    await response.WriteAsync($$"""{"authorization_servers":["{{Authority}}"]}""");
                    return;

                case "/.well-known/oauth-authorization-server":
                    response.ContentType = "application/json";
                    await response.WriteAsync($$"""
                        {
                          "issuer": "https://{{DomainName}}",
                          "authorization_endpoint": "https://{{DomainName}}/authorize",
                          "token_endpoint": "https://{{DomainName}}/token",
                          "jwks_uri": "https://{{DomainName}}/jwks",
                          "response_types_supported": [ "code" ],
                          "grant_types_supported": [ "authorization_code", "refresh_token" ]
                        }
                        """);
                    return;

                case "/jwks":
                    response.ContentType = "application/json";
                    await response.WriteAsync($$"""{"keys":[{{s_signingKeyJson}}]}""");
                    return;

                case "/token" when request.Method == HttpMethod.Post.Method:
                    if (GateRefresh)
                    {
                        RefreshEntered.TrySetResult();
                        await ReleaseRefresh.Task.ConfigureAwait(false);
                    }

                    LastIssuedRefreshToken = $"refreshToken{Interlocked.Increment(ref _tokenSerialNumber)}";

                    response.ContentType = "application/json";
                    await response.WriteAsync($$"""
                        {
                          "access_token": "{{CreateAccessJwt(IssuedDid)}}",
                          "token_type": "DPoP",
                          "expires_in": 900,
                          "refresh_token": "{{LastIssuedRefreshToken}}",
                          "scope": "atproto"
                        }
                        """);
                    return;

                case "/xrpc/com.atproto.server.describeServer":
                    response.ContentType = "application/json";
                    await response.WriteAsync($$"""{"did":"{{ServerDid}}","availableUserDomains":[".{{DomainName}}"]}""");
                    return;

                default:
                    response.StatusCode = StatusCodes.Status404NotFound;
                    return;
            }
        }
    }
}
