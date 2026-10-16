// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;

using idunno.AtProto;
using idunno.AtProto.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class ActorQueryStringTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(" ", true)]
    public async Task ActorSearchPreservesEmptyQueriesAndOmitsNullQueries(string? query, bool typeahead)
    {
        string queryString = string.Empty;
        string endpoint = typeahead ? "/xrpc/app.bsky.actor.searchActorsTypeahead" : "/xrpc/app.bsky.actor.searchActors";
        using TestServer testServer = CreateActorServer(endpoint, q => queryString = q);
        using BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        bool succeeded = typeahead
            ? (await agent.SearchActorsTypeahead(query, cancellationToken: TestContext.Current.CancellationToken)).Succeeded
            : (await agent.SearchActors(query, cancellationToken: TestContext.Current.CancellationToken)).Succeeded;

        Assert.True(succeeded);
        if (query is null)
        {
            Assert.DoesNotContain("q=", queryString, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains($"&q={Uri.EscapeDataString(query)}", queryString, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ServerActorSearchCanOmitTheQuery(bool typeahead)
    {
        string queryString = string.Empty;
        string endpoint = typeahead ? "/xrpc/app.bsky.actor.searchActorsTypeahead" : "/xrpc/app.bsky.actor.searchActors";
        using TestServer testServer = CreateActorServer(endpoint, q => queryString = q);
        using HttpClient client = testServer.CreateClient();

        bool succeeded = typeahead
            ? (await BlueskyServer.SearchActorsTypeahead(TestServerBuilder.DefaultUri, null, client)).Succeeded
            : (await BlueskyServer.SearchActors(TestServerBuilder.DefaultUri, null, client)).Succeeded;

        Assert.True(succeeded);
        Assert.DoesNotContain("q=", queryString, StringComparison.Ordinal);
    }

    private const string EmptyActorsResponse = """{"actors":[]}""";

    private static readonly Did s_did = new("did:plc:hfgp6pj3akhqxntgqwramlbg");

    private static AccessCredentials CreateCredentials() => new(
        service: TestServerBuilder.DefaultUri,
        authenticationType: AuthenticationType.UsernamePassword,
        accessJwt: JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
        refreshToken: "refreshToken");

    private static TestServer CreateActorServer(string path, Action<string> captureQueryString)
    {
        return TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                if (context.Request.Path == path)
                {
                    captureQueryString(context.Request.QueryString.Value ?? string.Empty);

                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(EmptyActorsResponse, TestContext.Current.CancellationToken);
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                }
            });
    }

    [Fact]
    public async Task GetSuggestionsSendsTheLimitItDocumentsAndNoEmptyCursor()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateActorServer("/xrpc/app.bsky.actor.getSuggestions", q => queryString = q);
        BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        await agent.GetSuggestions(limit: null, cursor: null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("limit=50", queryString, StringComparison.Ordinal);
        Assert.DoesNotContain("cursor=", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchActorsSendsTheLimitItDocumentsAndNoEmptyCursor()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateActorServer("/xrpc/app.bsky.actor.searchActors", q => queryString = q);
        BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        await agent.SearchActors("test", limit: null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("limit=25", queryString, StringComparison.Ordinal);
        Assert.DoesNotContain("cursor=", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchActorsTypeaheadSendsTheLimitItDocuments()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateActorServer("/xrpc/app.bsky.actor.searchActorsTypeahead", q => queryString = q);
        BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        await agent.SearchActorsTypeahead("test", limit: null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("limit=10", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchActorsSendsTheCursorWhenOneIsSupplied()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateActorServer("/xrpc/app.bsky.actor.searchActors", q => queryString = q);
        BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        await agent.SearchActors("test", cursor: "cursorValue", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("cursor=cursorValue", queryString, StringComparison.Ordinal);
    }
}
