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
