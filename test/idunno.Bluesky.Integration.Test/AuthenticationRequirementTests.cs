// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.AtProto.Server;
using idunno.Bluesky.Feed.Gates;
using idunno.Bluesky.Video;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

#pragma warning disable BSKYUnspecced

[ExcludeFromCodeCoverage]
public class AuthenticationRequirementTests
{
    private static readonly Did s_did = new("did:plc:hfgp6pj3akhqxntgqwramlbg");

    private static readonly AtUri s_otherPost = new("at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3lcf6ry7xy22x");

    private static AccessCredentials CreateCredentials() => new(
        service: TestServerBuilder.DefaultUri,
        authenticationType: AuthenticationType.UsernamePassword,
        accessJwt: JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
        refreshToken: "refreshToken");

    private sealed record CapturedRequest(string Host, string QueryString, bool HadAuthorization);

    private static TestServer CreateCapturingServer(string path, string responseBody, Action<CapturedRequest> capture) =>
        TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                if (context.Request.Path == path)
                {
                    capture(new CapturedRequest(
                        context.Request.Host.Host,
                        context.Request.QueryString.Value ?? string.Empty,
                        context.Request.Headers.Authorization.Count != 0));

                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(responseBody, TestContext.Current.CancellationToken);
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                }
            });

    [Theory]
    [InlineData("app.bsky.graph.searchStarterPacks", """{"starterPacks":[]}""", "public.api.bsky.app")]
    [InlineData("app.bsky.graph.searchStarterPacksV2", """{"starterPacks":[]}""", "public.api.bsky.app")]
    [InlineData("app.bsky.unspecced.getPostThreadV2", """{"thread":[],"hasOtherReplies":false}""", "api.bsky.app")]
    [InlineData("app.bsky.labeler.getServices", """{"views":[]}""", "public.api.bsky.app")]
    [InlineData("app.bsky.actor.getSuggestions", """{"actors":[]}""", "api.bsky.app")]
    [InlineData("app.bsky.feed.searchPostsV2", """{"posts":[]}""", "api.bsky.app")]
    public async Task PublicEndpointsCanBeCalledWithoutAuthentication(string nsid, string responseBody, string expectedHost)
    {
        CapturedRequest? captured = null;

        using TestServer testServer = CreateCapturingServer($"/xrpc/{nsid}", responseBody, r => captured = r);
        using BlueskyAgent agent = new(new TestHttpClientFactory(testServer));

        Assert.False(agent.IsAuthenticated);

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        bool succeeded = nsid switch
        {
            "app.bsky.graph.searchStarterPacks" => (await agent.SearchStarterPacks("news", cancellationToken: cancellationToken)).Succeeded,
            "app.bsky.graph.searchStarterPacksV2" => (await agent.SearchStarterPacksV2("news", cancellationToken: cancellationToken)).Succeeded,
            "app.bsky.unspecced.getPostThreadV2" => (await agent.GetPostThreadV2(s_otherPost, cancellationToken: cancellationToken)).Succeeded,
            "app.bsky.labeler.getServices" => (await agent.GetLabelerServices([new Did("did:plc:ar7c4by46qjdydhdevvrndac")], cancellationToken: cancellationToken)).Succeeded,
            "app.bsky.actor.getSuggestions" => (await agent.GetSuggestions(cancellationToken: cancellationToken)).Succeeded,
            "app.bsky.feed.searchPostsV2" => (await agent.SearchPostsV2(query: "bluesky", cancellationToken: cancellationToken)).Succeeded,
            _ => throw new ArgumentOutOfRangeException(nameof(nsid))
        };

        Assert.True(succeeded);
        Assert.NotNull(captured);
        Assert.Equal(expectedHost, captured.Host);
        Assert.False(captured.HadAuthorization);
    }

    [Fact]
    public async Task SearchPostsV2UsesTheAppViewWhenAnUnauthenticatedAgentsServiceIsAPds()
    {
        CapturedRequest? captured = null;

        using TestServer testServer = CreateCapturingServer("/xrpc/app.bsky.feed.searchPostsV2", """{"posts":[]}""", r => captured = r);
        using BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Service = new Uri("https://former.pds.test/") };

        Assert.False(agent.IsAuthenticated);

        AtProtoHttpResult<Feed.SearchV2Results> result = await agent.SearchPostsV2(
            query: "bluesky",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(captured);
        Assert.Equal("api.bsky.app", captured.Host);
        Assert.False(captured.HadAuthorization);
    }

    [Fact]
    public async Task GetLabelerServicesDoesNotImposeAnUpperBoundOnTheDidsItIsSupplied()
    {
        // app.bsky.labeler.getServices declares no maximum on dids and the service applies none, so a large collection must not be rejected.
        List<Did> dids = [.. Enumerable.Range(0, 100).Select(i => new Did($"did:plc:ar7c4by46qjdydhdevvrndac{i}"))];

        CapturedRequest? captured = null;

        using TestServer testServer = CreateCapturingServer("/xrpc/app.bsky.labeler.getServices", """{"views":[]}""", r => captured = r);
        using BlueskyAgent agent = new(new TestHttpClientFactory(testServer));

        AtProtoHttpResult<ICollection<Labeler.LabelerView>> result = await agent.GetLabelerServices(dids, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(captured);
        Assert.Equal(dids.Count, captured.QueryString.Split("dids=", StringSplitOptions.None).Length - 1);
    }

    [Theory]
    [InlineData("app.bsky.feed.threadgate", true)]
    [InlineData("app.bsky.feed.threadgate", false)]
    [InlineData("app.bsky.feed.postgate", true)]
    [InlineData("app.bsky.feed.postgate", false)]
    public async Task GateRecordsAreReadFromThePostAuthorsRepositoryOnTheirPds(string gateCollection, bool authenticated)
    {
        const string authorPdsHost = "author.pds.test";
        string response =
            $$"""
            {
              "uri": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/{{gateCollection}}/3lcf6ry7xy22x",
              "cid": "bafyreihbfkwjqz3hfyvnlwvbxbfqvwzqkqcfcjhbgvqjqvqmgqgqjqgqgq",
              "value": {
                "$type": "{{gateCollection}}",
                "post": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3lcf6ry7xy22x",
                "createdAt": "2024-12-01T00:00:00Z"
              }
            }
            """;

        CapturedRequest? captured = null;

        using TestServer testServer = TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse httpResponse = context.Response;

                if (request.Host.Host == "plc.directory" && request.Path == $"/{s_otherPost.Repo}")
                {
                    DidDocument didDocument = new(
                        id: s_otherPost.Repo.ToString(),
                        context: ["https://www.w3.org/ns/did/v1"],
                        alsoKnownAs: null,
                        verificationMethods: null,
                        services: [new(id: "#atproto_pds", type: "AtprotoPersonalDataServer", serviceEndpoint: new Uri($"https://{authorPdsHost}/"))]);
                    await httpResponse.WriteAsJsonAsync(didDocument, new JsonSerializerOptions(JsonSerializerDefaults.Web), TestContext.Current.CancellationToken);
                }
                else if (request.Path == "/xrpc/com.atproto.repo.getRecord")
                {
                    captured = new CapturedRequest(request.Host.Host, request.QueryString.Value ?? string.Empty, request.Headers.Authorization.Count != 0);

                    httpResponse.StatusCode = (int)HttpStatusCode.OK;
                    httpResponse.ContentType = "application/json";
                    await httpResponse.WriteAsync(response, TestContext.Current.CancellationToken);
                }
                else
                {
                    httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                }
            });

        using BlueskyAgent agent = authenticated
            ? new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri }
            : new(new TestHttpClientFactory(testServer));

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        bool succeeded = gateCollection == CollectionNsid.ThreadGate
            ? (await agent.GetThreadGate(s_otherPost, cancellationToken)).Succeeded
            : (await agent.GetPostGate(s_otherPost, cancellationToken)).Succeeded;

        Assert.True(succeeded);
        Assert.NotNull(captured);
        Assert.Equal(authorPdsHost, captured.Host);
        Assert.False(captured.HadAuthorization);
        Assert.Contains($"repo={Uri.EscapeDataString(s_otherPost.Repo.ToString())}", captured.QueryString, StringComparison.Ordinal);
        Assert.Contains($"collection={Uri.EscapeDataString(gateCollection)}", captured.QueryString, StringComparison.Ordinal);
        Assert.Contains("rkey=3lcf6ry7xy22x", captured.QueryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetUploadStatusSendsAServiceAuthTokenToTheVideoService()
    {
        string serviceAuthToken = JwtBuilder.CreateJwt(
            did: null,
            issuer: s_did.ToString(),
            audience: "did:web:pds.test.internal",
            lxm: "com.atproto.repo.uploadBlob");

        string? serviceAuthLxm = null;
        string? videoAuthorization = null;

        using TestServer testServer = TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Host.Host == TestServerBuilder.DefaultUri.Host && request.Path == "/xrpc/com.atproto.server.describeServer")
                {
                    response.StatusCode = (int)HttpStatusCode.OK;
                    await response.WriteAsJsonAsync(
                        new ServerDescription(
                            did: "did:web:pds.test.internal",
                            contact: new Contact($"test@{request.Host.Host}"),
                            links: new Links(),
                            availableUserDomains: [request.Host.Host],
                            inviteCodeRequired: false,
                            phoneVerificationRequired: false,
                            blobUploadLimit: 10000000),
                        TestContext.Current.CancellationToken);
                }
                else if (request.Host.Host == TestServerBuilder.DefaultUri.Host && request.Path == "/xrpc/com.atproto.server.getServiceAuth")
                {
                    serviceAuthLxm = request.Query["lxm"].ToString();

                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.ContentType = "application/json";
                    await response.WriteAsync($$"""{"token":"{{serviceAuthToken}}"}""", TestContext.Current.CancellationToken);
                }
                else if (request.Host.Host == "video.bsky.app" && request.Path == "/xrpc/app.bsky.video.getUploadStatus")
                {
                    videoAuthorization = request.Headers.Authorization.ToString();

                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.ContentType = "application/json";
                    await response.WriteAsync(
                        """{"jobId":"jobId","partSizeBytes":5242880,"partCount":3,"receivedParts":[1],"expiresAt":"2026-08-18T14:58:45Z","state":"created"}""",
                        TestContext.Current.CancellationToken);
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                }
            });

        using BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        AtProtoHttpResult<UploadStatus> result = await agent.GetUploadStatus("jobId", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, $"Status {result.StatusCode}");
        Assert.Equal("com.atproto.repo.uploadBlob", serviceAuthLxm);
        Assert.NotNull(videoAuthorization);
        Assert.EndsWith(serviceAuthToken, videoAuthorization, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetUploadStatusThrowsWhenTheAgentIsNotAuthenticated()
    {
        using BlueskyAgent agent = new();

        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => agent.GetUploadStatus("jobId", TestContext.Current.CancellationToken));
    }
}
