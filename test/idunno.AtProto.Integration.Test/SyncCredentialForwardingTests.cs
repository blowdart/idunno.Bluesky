// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http.Headers;

using idunno.AtProto.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class SyncCredentialForwardingTests
{
    private static readonly Did s_did = "did:plc:test";
    private static readonly Uri s_otherService = new("https://relay.example.invalid");

    public static TheoryData<string> Endpoints => ["getHostStatus", "listHosts", "listReposByCollection", "requestCrawl"];

    private static AccessCredentials CreateCredentials(Uri? service = null) => new(
        service: service ?? TestServerBuilder.DefaultUri,
        authenticationType: AuthenticationType.UsernamePassword,
        accessJwt: JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
        refreshToken: "refreshToken");

    private static string ResponseFor(string endpoint) => endpoint switch
    {
        "getHostStatus" => """{"hostname":"pds.example.com","status":"active"}""",
        "listHosts" => """{"hosts":[]}""",
        "listReposByCollection" => """{"repos":[]}""",
        _ => "{}"
    };

    private static TestServer CreateServer(Uri uri, string endpoint, Action<string?> onAuthorization) =>
        TestServerBuilder.CreateServer(uri, async context =>
        {
            onAuthorization(context.Request.Headers.Authorization.Count == 0 ? null : context.Request.Headers.Authorization.ToString());
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(ResponseFor(endpoint));
        });

    private static async Task<bool> CallServer(string endpoint, Uri service, HttpClient httpClient, AccessCredentials? accessCredentials)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        return endpoint switch
        {
            "getHostStatus" => (await AtProtoServer.GetHostStatus("pds.example.com", service, httpClient, accessCredentials, cancellationToken: cancellationToken)).Succeeded,
            "listHosts" => (await AtProtoServer.ListHosts(null, null, service, httpClient, accessCredentials, cancellationToken: cancellationToken)).Succeeded,
            "listReposByCollection" => (await AtProtoServer.ListReposByCollection("app.bsky.feed.post", null, null, service, httpClient, accessCredentials, cancellationToken: cancellationToken)).Succeeded,
            "requestCrawl" => (await AtProtoServer.RequestCrawl("pds.example.com", service, httpClient, accessCredentials, cancellationToken: cancellationToken)).Succeeded,
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint))
        };
    }

    private static async Task<bool> CallAgent(string endpoint, AtProtoAgent agent, Uri? service)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        return endpoint switch
        {
            "getHostStatus" => (await agent.GetHostStatus("pds.example.com", service, cancellationToken)).Succeeded,
            "listHosts" => (await agent.ListHosts(service: service, cancellationToken: cancellationToken)).Succeeded,
            "listReposByCollection" => (await agent.ListReposByCollection("app.bsky.feed.post", service: service, cancellationToken: cancellationToken)).Succeeded,
            "requestCrawl" => (await agent.RequestCrawl("pds.example.com", service, cancellationToken)).Succeeded,
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint))
        };
    }

    private static void AssertBearer(AccessCredentials credentials, string? authorization)
    {
        Assert.NotNull(authorization);
        AuthenticationHeaderValue header = AuthenticationHeaderValue.Parse(authorization);
        Assert.Equal("Bearer", header.Scheme);
        Assert.Equal(credentials.AccessJwt, header.Parameter);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task ServerCallSendsAuthorizationHeaderWhenCredentialsAreSupplied(string endpoint)
    {
        AccessCredentials credentials = CreateCredentials();
        string? authorization = null;
        TestServer testServer = CreateServer(TestServerBuilder.DefaultUri, endpoint, value => authorization = value);

        Assert.True(await CallServer(endpoint, TestServerBuilder.DefaultUri, testServer.CreateClient(), credentials));
        AssertBearer(credentials, authorization);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task ServerCallSendsNoAuthorizationHeaderWhenCredentialsAreNotSupplied(string endpoint)
    {
        string? authorization = "unset";
        TestServer testServer = CreateServer(TestServerBuilder.DefaultUri, endpoint, value => authorization = value);

        Assert.True(await CallServer(endpoint, TestServerBuilder.DefaultUri, testServer.CreateClient(), null));
        Assert.Null(authorization);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task ServerCallThrowsWhenCredentialsAreForADifferentService(string endpoint)
    {
        TestServer testServer = CreateServer(TestServerBuilder.DefaultUri, endpoint, _ => { });

        await Assert.ThrowsAsync<AccessTokenException>(() =>
            CallServer(endpoint, TestServerBuilder.DefaultUri, testServer.CreateClient(), CreateCredentials(s_otherService)));
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AuthenticatedAgentForwardsCredentialsToItsOwnService(string endpoint)
    {
        AccessCredentials credentials = CreateCredentials();
        string? authorization = null;
        TestServer testServer = CreateServer(TestServerBuilder.DefaultUri, endpoint, value => authorization = value);

        using var agent = new AtProtoAgent(TestServerBuilder.DefaultUri, new TestHttpClientFactory(testServer));
        agent.Credentials = credentials;

        Assert.True(await CallAgent(endpoint, agent, null));
        AssertBearer(credentials, authorization);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AuthenticatedAgentDoesNotForwardCredentialsToADifferentService(string endpoint)
    {
        string? authorization = "unset";
        TestServer testServer = CreateServer(s_otherService, endpoint, value => authorization = value);

        using var agent = new AtProtoAgent(TestServerBuilder.DefaultUri, new TestHttpClientFactory(testServer));
        agent.Credentials = CreateCredentials();

        Assert.True(await CallAgent(endpoint, agent, s_otherService));
        Assert.Null(authorization);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task UnauthenticatedAgentDoesNotSendCredentials(string endpoint)
    {
        string? authorization = "unset";
        TestServer testServer = CreateServer(TestServerBuilder.DefaultUri, endpoint, value => authorization = value);

        using var agent = new AtProtoAgent(TestServerBuilder.DefaultUri, new TestHttpClientFactory(testServer));

        Assert.True(await CallAgent(endpoint, agent, null));
        Assert.Null(authorization);
    }
}
