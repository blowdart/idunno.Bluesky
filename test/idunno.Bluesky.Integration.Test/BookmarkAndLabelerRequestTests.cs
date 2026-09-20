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
}
