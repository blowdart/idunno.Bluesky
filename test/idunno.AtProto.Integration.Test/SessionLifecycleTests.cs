// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;

using idunno.AtProto.Authentication;
using idunno.AtProto.Authentication.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.IdentityModel.Tokens;

namespace idunno.AtProto.Integration.Test;

/// <summary>
/// Covers the credential and background refresh state an agent is left in when a login or a logout does not succeed.
/// </summary>
/// <remarks>
/// <para>
///   Both stop the refresh timer before they start, so a failure which leaves the credentials in place has to start it
///   again. Leaving it stopped ends background refresh for the lifetime of the agent, and the session the failure left
///   behind then expires with nothing to renew it.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public class SessionLifecycleTests
{
    private const string DomainName = "test.invalid";
    private const string ExpectedDid = "did:plc:ec72yg6n2sydzjvtovvdlxrk";

    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public SessionLifecycleTests()
    {
        _jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        _jsonSerializerOptions.TypeInfoResolverChain.Insert(0, SourceGenerationContext.Default);
        _jsonSerializerOptions.TypeInfoResolverChain.Insert(0, AtProto.SourceGenerationContext.Default);
    }

    [Fact]
    public async Task ALoginWhichCannotResolveAPdsLeavesBackgroundRefreshRunningForTheSessionItDidNotReplace()
    {
        LifecycleTestServer testServer = new(this);

        using (AtProtoAgent agent = CreateAgent(testServer))
        {
            await Login(agent);

            AccessCredentials originalCredentials = agent.Credentials!;

            testServer.FailDidDocumentResolution = true;

            AtProtoHttpResult<bool> secondLogin = await agent.Login(
                did: ExpectedDid,
                password: "password",
                authFactorToken: null,
                service: null,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(secondLogin.Succeeded);

            // The failure never reached the server, so the session the agent was already holding is untouched and still
            // needs refreshing.
            Assert.Same(originalCredentials, agent.Credentials);
            Assert.True(GetRefreshTimer(agent)!.Enabled);
        }
    }

    [Fact]
    public async Task ALoginWhoseIssuedAccessTokenCannotBeValidatedLeavesBackgroundRefreshRunningForTheSessionItDidNotReplace()
    {
        LifecycleTestServer testServer = new(this);

        using (AtProtoAgent agent = CreateAgent(testServer))
        {
            await Login(agent);

            AccessCredentials originalCredentials = agent.Credentials!;

            testServer.IssueUnvalidatableAccessJwtOnCreateSession = true;

            await Assert.ThrowsAsync<SecurityTokenValidationException>(
                () => agent.Login(
                    did: ExpectedDid,
                    password: "password",
                    authFactorToken: null,
                    service: new Uri($"https://{DomainName}"),
                    cancellationToken: TestContext.Current.CancellationToken));

            Assert.Same(originalCredentials, agent.Credentials);
            Assert.True(GetRefreshTimer(agent)!.Enabled);
        }
    }

    [Fact]
    public async Task ALoginTheServerRejectsClearsTheCredentialsAndLeavesTheRefreshTimerStopped()
    {
        LifecycleTestServer testServer = new(this);

        using (AtProtoAgent agent = CreateAgent(testServer))
        {
            await Login(agent);

            testServer.FailCreateSession = true;

            AtProtoHttpResult<bool> secondLogin = await agent.Login(
                did: ExpectedDid,
                password: "password",
                authFactorToken: null,
                service: new Uri($"https://{DomainName}"),
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(secondLogin.Succeeded);

            // This failure discards the credentials, so there is nothing left to refresh and the timer must stay stopped
            // rather than ticking against an agent which is no longer authenticated.
            Assert.Null(agent.Credentials);
            Assert.False(GetRefreshTimer(agent)!.Enabled);
        }
    }

    [Fact]
    public async Task ALogoutWhichThrowsLeavesBackgroundRefreshRunningForTheSessionItDidNotEnd()
    {
        LifecycleTestServer testServer = new(this) { ThrowOnDeleteSession = true };

        using (AtProtoAgent agent = CreateAgent(testServer))
        {
            await Login(agent);

            AccessCredentials originalCredentials = agent.Credentials!;

            await Assert.ThrowsAnyAsync<Exception>(() => agent.Logout(TestContext.Current.CancellationToken));

            // The session was never deleted, so the agent is still authenticated and still needs to refresh.
            Assert.Same(originalCredentials, agent.Credentials);
            Assert.True(GetRefreshTimer(agent)!.Enabled);
        }
    }

    [Fact]
    public async Task AnOAuthLogoutWhoseRefreshTokenRevocationFailsDoesNotLeaveTheAgentReportingItselfAsAuthenticated()
    {
        LifecycleTestServer testServer = new(this) { RevocationStatusCode = StatusCodes.Status400BadRequest };

        using (AtProtoAgent agent = CreateAgent(testServer))
        {
            GiveAgentOAuthCredentials(agent);

            await Assert.ThrowsAsync<LogoutException>(() => agent.Logout(TestContext.Current.CancellationToken));

            // A logout which leaves the credentials in place leaves an agent which says it is signed in, and which goes
            // on refreshing a session the caller asked it to end.
            Assert.Null(agent.Credentials);
            Assert.False(agent.IsAuthenticated);
            Assert.False(GetRefreshTimer(agent)!.Enabled);
        }
    }

    [Fact]
    public async Task AnOAuthLogoutWhoseAccessTokenRevocationFailsDoesNotLeaveTheAgentReportingItselfAsAuthenticated()
    {
        LifecycleTestServer testServer = new(this) { FailAccessTokenRevocationOnly = true };

        using (AtProtoAgent agent = CreateAgent(testServer))
        {
            GiveAgentOAuthCredentials(agent);

            await Assert.ThrowsAsync<LogoutException>(() => agent.Logout(TestContext.Current.CancellationToken));

            // The refresh token was revoked before this failed, so the session is over whatever happened to the access
            // token. Keeping the credentials leaves the agent authenticated against a session which no longer exists.
            Assert.Equal(["refresh_token", "access_token"], testServer.RevokedTokenTypeHints);
            Assert.Null(agent.Credentials);
            Assert.False(agent.IsAuthenticated);
        }
    }

    [Fact]
    public async Task DisposingAnAgentClearsTheCredentialsItIsHolding()
    {
        LifecycleTestServer testServer = new(this);

        AtProtoAgent agent = CreateAgent(testServer);

        using (agent)
        {
            await Login(agent);

            Assert.NotNull(GetCredentialsField(agent));
        }

        // Disposal already forgets spent refresh tokens. Leaving the live access token and refresh token in place keeps
        // them reachable for as long as anything holds a reference to the disposed agent.
        Assert.Null(GetCredentialsField(agent));
    }

    private static void GiveAgentOAuthCredentials(AtProtoAgent agent)
    {
        agent.Credentials = new DPoPAccessCredentials(
            service: new Uri($"https://{DomainName}"),
            accessJwt: JwtBuilder.CreateJwt(new Did(ExpectedDid), $"did:web:{DomainName}"),
            refreshToken: "refreshToken",
            dPoPProofKey: JwtBuilder.CreateProofKey(),
            dPoPNonce: "nonce");

        StartRefreshTimer(agent);

        Assert.True(GetRefreshTimer(agent)!.Enabled);
    }

    private static void StartRefreshTimer(AtProtoAgent agent)
    {
        typeof(AtProtoAgent)
            .GetMethod("StartTokenRefreshTimer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(agent, null);
    }

    private static System.Timers.Timer? GetRefreshTimer(AtProtoAgent agent)
    {
        return (System.Timers.Timer?)typeof(AtProtoAgent)
            .GetField("_credentialRefreshTimer", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(agent);
    }

    private static AccessCredentials? GetCredentialsField(AtProtoAgent agent)
    {
        return (AccessCredentials?)typeof(AtProtoAgent)
            .GetField("_credentials", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(agent);
    }

    private static AtProtoAgent CreateAgent(LifecycleTestServer testServer)
    {
        return new AtProtoAgent(
            new Uri($"https://{DomainName}"),
            new TestHttpClientFactory(testServer.TestServer),
            new AtProtoAgentOptions()
            {
                PlcDirectoryServer = new Uri($"https://{DomainName}"),
                OAuthOptions = new OAuthOptions("http://localhost")
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
        Assert.True(GetRefreshTimer(agent)!.Enabled);
    }

    /// <summary>
    /// A test server which supports session creation and deletion, and the three round trips an OAuth logout makes.
    /// </summary>
    private sealed class LifecycleTestServer
    {
        private const string RevocationPath = "/oauth/revoke";

        private readonly List<string> _revokedTokenTypeHints = [];
        private readonly object _lock = new();

        private int _tokenSerialNumber;

        internal LifecycleTestServer(SessionLifecycleTests test)
        {
            TestServer = TestServerBuilder.CreateServer(DomainName, context => Handle(test, context));
        }

        internal TestServer TestServer { get; }

        internal bool FailCreateSession { get; set; }

        internal bool FailDidDocumentResolution { get; set; }

        internal bool ThrowOnDeleteSession { get; set; }

        internal bool IssueUnvalidatableAccessJwtOnCreateSession { get; set; }

        internal int RevocationStatusCode { get; set; } = StatusCodes.Status200OK;

        /// <summary>
        /// Whether only the second revocation, the one for the access token, should fail.
        /// </summary>
        internal bool FailAccessTokenRevocationOnly { get; set; }

        internal IReadOnlyList<string> RevokedTokenTypeHints
        {
            get
            {
                lock (_lock)
                {
                    return [.. _revokedTokenTypeHints];
                }
            }
        }

        private async Task Handle(SessionLifecycleTests test, HttpContext context)
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Path == $"/{ExpectedDid}")
            {
                if (FailDidDocumentResolution)
                {
                    response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

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
                if (FailCreateSession)
                {
                    response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                response.StatusCode = 200;

                await response.WriteAsJsonAsync(
                    new CreateSessionResponse(
                        accessJwt: IssueUnvalidatableAccessJwtOnCreateSession ? CreateUnvalidatableAccessJwt() : CreateAccessJwt(),
                        refreshJwt: NextRefreshToken(),
                        handle: DomainName,
                        did: ExpectedDid),
                    test._jsonSerializerOptions);
            }
            else if (request.Path == "/xrpc/com.atproto.server.deleteSession" && request.Method == HttpMethod.Post.Method)
            {
                if (ThrowOnDeleteSession)
                {
                    throw new InvalidOperationException("The connection to the server failed.");
                }

                response.StatusCode = 200;
                await response.WriteAsJsonAsync(new EmptyResponse(), test._jsonSerializerOptions);
            }
            else if (request.Path == "/.well-known/oauth-protected-resource")
            {
                response.ContentType = "application/json";
                await response.WriteAsync($$"""{"authorization_servers":["https://{{DomainName}}/"]}""");
            }
            else if (request.Path == "/.well-known/oauth-authorization-server")
            {
                response.ContentType = "application/json";
                await response.WriteAsync($$"""{"revocation_endpoint":"https://{{DomainName}}{{RevocationPath}}"}""");
            }
            else if (request.Path == RevocationPath)
            {
                IFormCollection form = await request.ReadFormAsync();
                string tokenTypeHint = form["token_type_hint"].ToString();

                lock (_lock)
                {
                    _revokedTokenTypeHints.Add(tokenTypeHint);
                }

                if (FailAccessTokenRevocationOnly)
                {
                    response.StatusCode = tokenTypeHint == "access_token" ? StatusCodes.Status400BadRequest : StatusCodes.Status200OK;
                }
                else
                {
                    response.StatusCode = RevocationStatusCode;
                }
            }
            else
            {
                response.StatusCode = 404;
            }
        }

        private static string CreateAccessJwt() => JwtBuilder.CreateJwt(new Did(ExpectedDid), $"did:web:{DomainName}");

        /// <summary>
        /// Creates an access token whose audience is not the service it was requested from, so validation of it fails.
        /// </summary>
        private static string CreateUnvalidatableAccessJwt() => JwtBuilder.CreateJwt(new Did(ExpectedDid), "did:web:elsewhere.invalid");

        private string NextRefreshToken() => $"refreshToken{Interlocked.Increment(ref _tokenSerialNumber)}";
    }
}
