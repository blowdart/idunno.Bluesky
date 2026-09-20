// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Bookmarks;
using idunno.Bluesky.Labeler;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class BookmarkAndLabelerRequestTests
{
    private const string EmptyBookmarksResponse = """{"bookmarks":[]}""";

    private const string EmptyServicesResponse = """{"views":[]}""";

    private static readonly Did s_did = new("did:plc:hfgp6pj3akhqxntgqwramlbg");

    private static AccessCredentials CreateCredentials() => new(
        service: TestServerBuilder.DefaultUri,
        authenticationType: AuthenticationType.UsernamePassword,
        accessJwt: JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
        refreshToken: "refreshToken");

    private static BlueskyAgent CreateAgent(TestServer testServer) =>
        new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

    private static TestServer CreateCapturingServer(string path, string responseBody, Action<string> captureQueryString) =>
        TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                if (context.Request.Path == path)
                {
                    captureQueryString(context.Request.QueryString.Value ?? string.Empty);

                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(responseBody, TestContext.Current.CancellationToken);
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                }
            });

    [Fact]
    public async Task GetBookmarksDoesNotSendACursorOrALimitWhenNeitherIsSpecified()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateCapturingServer(
            "/xrpc/app.bsky.bookmark.getBookmarks",
            EmptyBookmarksResponse,
            q => queryString = q);

        BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<PagedViewReadOnlyCollection<BookmarkView>> result = await agent.GetBookmarks(
            limit: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.DoesNotContain("cursor", queryString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("limit", queryString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetBookmarksSendsTheCursorAndLimitItIsGiven()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateCapturingServer(
            "/xrpc/app.bsky.bookmark.getBookmarks",
            EmptyBookmarksResponse,
            q => queryString = q);

        BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<PagedViewReadOnlyCollection<BookmarkView>> result = await agent.GetBookmarks(
            limit: 25,
            cursor: "a cursor",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains("limit=25", queryString, StringComparison.Ordinal);
        Assert.Contains($"cursor={Uri.EscapeDataString("a cursor")}", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLabelerServicesOnlyEnumeratesTheDidsItIsGivenOnce()
    {
        int enumerations = 0;

        string queryString = string.Empty;

        using TestServer testServer = CreateCapturingServer(
            "/xrpc/app.bsky.labeler.getServices",
            EmptyServicesResponse,
            q => queryString = q);

        BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<ICollection<LabelerView>> result = await agent.GetLabelerServices(
            CountingDids(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains("dids=", queryString, StringComparison.Ordinal);
        Assert.Equal(1, enumerations);

        IEnumerable<Did> CountingDids()
        {
            enumerations++;
            yield return new Did("did:plc:ar7c4by46qjdydhdevvrndac");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThePreferencesDrivenLabelerServiceCallsWorkWhenTheActorSubscribesToALargeNumberOfLabelers(bool detailed)
    {
        // app.bsky.labeler.getServices declares no maximum on dids. The number of labelers an actor subscribes to is
        // remote account state rather than a caller argument, so these overloads must not reject it.
        const int subscribedLabelerCount = 30;

        List<Did> subscribedLabelers = [.. Enumerable.Range(0, subscribedLabelerCount).Select(i => new Did($"did:plc:ar7c4by46qjdydhdevvrndac{i}"))];

        string preferencesResponse =
            $$"""
            {
                "preferences": [
                    {
                        "$type": "app.bsky.actor.defs#labelersPref",
                        "labelers": [{{string.Join(",", subscribedLabelers.Select(did => $$"""{"did":"{{did}}"}"""))}}]
                    }
                ]
            }
            """;

        string queryString = string.Empty;

        using TestServer testServer = TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                switch (context.Request.Path)
                {
                    case "/xrpc/app.bsky.actor.getPreferences":
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(preferencesResponse, TestContext.Current.CancellationToken);
                        break;

                    case "/xrpc/app.bsky.labeler.getServices":
                        queryString = context.Request.QueryString.Value ?? string.Empty;
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(EmptyServicesResponse, TestContext.Current.CancellationToken);
                        break;

                    default:
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        break;
                }
            });

        BlueskyAgent agent = CreateAgent(testServer);

        bool succeeded = detailed
            ? (await agent.GetUserSubscribedLabelerServices(TestContext.Current.CancellationToken)).Succeeded
            : (await agent.GetLabelerServices(cancellationToken: TestContext.Current.CancellationToken)).Succeeded;

        Assert.True(succeeded);

        // Preferences prepends the Bluesky moderation labeler, so one more Did goes out than the actor subscribes to.
        Assert.Equal(subscribedLabelerCount + 1, queryString.Split("dids=", StringSplitOptions.None).Length - 1);
    }
}
