// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.Bluesky.Graph;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

/// <summary>
/// Covers the paging and null entry filtering behaviour of the graph APIs.
/// </summary>
/// <remarks>
/// <para><see cref="BlueskyAgent.DeleteFromList(AtUri, Did, CancellationToken)"/> and its handle overload search a list
/// for their subject a page at a time. The loop which did so never carried the cursor of the previous page forward and
/// never re-read, so a list whose first page did not contain the subject and which reported a cursor span forever.</para>
/// </remarks>
[ExcludeFromCodeCoverage]
public class GraphPagingAndFilteringTests
{
    private const string ListView = """
        {
            "uri": "at://did:plc:test/app.bsky.graph.list/1",
            "cid": "bafyreib2rxk3rh6kzwq6y7ug4eqhfhpqaqzqvuflstfpvgkzjt7b5yfkzy",
            "creator": { "did": "did:plc:test", "handle": "test.invalid" },
            "name": "Test",
            "purpose": "app.bsky.graph.defs#curatelist"
        }
        """;

    private const string CommitResponse = """
        {"commit":{"cid":"bafyreib2rxk3rh6kzwq6y7ug4eqhfhpqaqzqvuflstfpvgkzjt7b5yfkzy","rev":"1"}}
        """;

    private const string RecordCid = "bafyreic6ymjhqzqvtnqgcx7fcnmhdmxlhfqpvbkhkpkzy7yqvq7zqvq7za";

    private const string PutRecordResponse = """
        {"uri":"at://did:plc:test/app.bsky.graph.list/1","cid":"bafyreib2rxk3rh6kzwq6y7ug4eqhfhpqaqzqvuflstfpvgkzjt7b5yfkzy"}
        """;

    private static readonly Did s_did = "did:plc:test";

    private static readonly AtUri s_list = new("at://did:plc:test/app.bsky.graph.list/1");

    private static AccessCredentials CreateCredentials() => new(
        service: TestServerBuilder.DefaultUri,
        authenticationType: AuthenticationType.UsernamePassword,
        accessJwt: JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
        refreshToken: "refreshToken");

    private static HttpClient CreateClient(string body) =>
        TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(body);
        }).CreateClient();

    /// <summary>
    /// Creates a server which serves a two page list. The subject on the second page is only reachable by following
    /// the cursor returned with the first.
    /// </summary>
    private static TestServer CreatePagedListServer(List<string?> cursorsSeen, List<string> deleteBodies, bool secondPageIsEmpty = false)
    {
        string firstPage = $$"""
            {
                "list": {{ListView}},
                "items": [ { "uri": "at://did:plc:test/app.bsky.graph.listitem/1",
                             "subject": { "did": "did:plc:pageone", "handle": "pageone.invalid" } } ],
                "cursor": "page2"
            }
            """;

        string secondPageItems = secondPageIsEmpty
            ? "[]"
            : """
              [ { "uri": "at://did:plc:test/app.bsky.graph.listitem/2",
                  "subject": { "did": "did:plc:pagetwo", "handle": "pagetwo.invalid" } } ]
              """;

        string secondPage = $$"""
            {
                "list": {{ListView}},
                "items": {{secondPageItems}}
            }
            """;

        return TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";

            if (context.Request.Path == "/xrpc/app.bsky.graph.getList")
            {
                string? cursor = context.Request.Query["cursor"];
                cursorsSeen.Add(string.IsNullOrEmpty(cursor) ? null : cursor);

                await context.Response.WriteAsync(cursor == "page2" ? secondPage : firstPage);
            }
            else if (context.Request.Path == "/xrpc/com.atproto.repo.deleteRecord")
            {
                using StreamReader reader = new(context.Request.Body);
                deleteBodies.Add(await reader.ReadToEndAsync(TestContext.Current.CancellationToken));

                await context.Response.WriteAsync(CommitResponse);
            }
            else
            {
                context.Response.StatusCode = 404;
            }
        });
    }

    private static BlueskyAgent CreateAgent(TestServer testServer)
    {
        BlueskyAgent agent = new(new TestHttpClientFactory(testServer))
        {
            Credentials = CreateCredentials(),
            Service = TestServerBuilder.DefaultUri
        };

        return agent;
    }

    [Fact(Timeout = 60000)]
    public async Task DeleteFromListFollowsTheCursorToFindASubjectWhichIsNotOnTheFirstPage()
    {
        List<string?> cursorsSeen = [];
        List<string> deleteBodies = [];

        using TestServer testServer = CreatePagedListServer(cursorsSeen, deleteBodies);
        using BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<DeleteResult> result = await agent.DeleteFromList(
            s_list,
            new Did("did:plc:pagetwo"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal([null, "page2"], cursorsSeen);
        Assert.Contains("app.bsky.graph.listitem", Assert.Single(deleteBodies), StringComparison.Ordinal);
        Assert.Contains("\"2\"", Assert.Single(deleteBodies), StringComparison.Ordinal);
    }

    [Fact(Timeout = 60000)]
    public async Task DeleteFromListByHandleFollowsTheCursorToFindASubjectWhichIsNotOnTheFirstPage()
    {
        List<string?> cursorsSeen = [];
        List<string> deleteBodies = [];

        using TestServer testServer = CreatePagedListServer(cursorsSeen, deleteBodies);
        using BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<DeleteResult> result = await agent.DeleteFromList(
            s_list,
            new Handle("pagetwo.invalid"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal([null, "page2"], cursorsSeen);
        Assert.Single(deleteBodies);
    }

    [Fact(Timeout = 60000)]
    public async Task DeleteFromListStopsPagingAndReportsNotFoundWhenNoPageContainsTheSubject()
    {
        List<string?> cursorsSeen = [];
        List<string> deleteBodies = [];

        using TestServer testServer = CreatePagedListServer(cursorsSeen, deleteBodies);
        using BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<DeleteResult> result = await agent.DeleteFromList(
            s_list,
            new Did("did:plc:absent"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal([null, "page2"], cursorsSeen);
        Assert.Empty(deleteBodies);
    }

    [Fact(Timeout = 60000)]
    public async Task DeleteFromListReportsADiagnosableNotFoundForAnEmptyList()
    {
        List<string?> cursorsSeen = [];
        List<string> deleteBodies = [];

        using TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            cursorsSeen.Add(context.Request.Query["cursor"]);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($$"""{"list":{{ListView}},"items":[]}""");
        });

        using BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<DeleteResult> result = await agent.DeleteFromList(
            s_list,
            new Did("did:plc:absent"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.NotNull(result.AtErrorDetail);
        Assert.Equal("NotFound", result.AtErrorDetail.Error);
        Assert.Empty(deleteBodies);
    }

    [Fact]
    public async Task GetListDoesNotRequireCredentials()
    {
        AtProtoHttpResult<ListViewWithItems> result = await BlueskyServer.GetList(
            list: s_list,
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: null,
            httpClient: CreateClient($$"""{"list":{{ListView}},"items":[]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
    }

    [Fact]
    public async Task GetRelationshipsSkipsANullRelationshipRatherThanReturningIt()
    {
        AtProtoHttpResult<RelationshipMap> result = await BlueskyServer.GetRelationships(
            actor: s_did,
            others: [new Did("did:plc:other")],
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"actor":"did:plc:test","relationships":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result.Relationships);
    }

    [Fact]
    public async Task GetSuggestedFollowsByActorSkipsANullSuggestionRatherThanReturningIt()
    {
        AtProtoHttpResult<SuggestedActors> result = await BlueskyServer.GetSuggestedFollowsByActor(
            actor: s_did,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"suggestions":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result.Suggestions);
    }

    [Fact]
    public async Task SearchStarterPacksV2SkipsANullStarterPackRatherThanReturningIt()
    {
        AtProtoHttpResult<SearchStarterPacksV2Result> result = await BlueskyServer.SearchStarterPacksV2(
            q: "test",
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"starterPacks":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    /// <summary>
    /// Creates a server which records the body of every putRecord request it receives.
    /// </summary>
    private static TestServer CreatePutRecordServer(List<string> putBodies) =>
        TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            using StreamReader reader = new(context.Request.Body);
            putBodies.Add(await reader.ReadToEndAsync(TestContext.Current.CancellationToken));

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(PutRecordResponse);
        });

    [Fact]
    public async Task UpdateListFromARepositoryRecordSendsTheRecordCidAsASwapRecord()
    {
        List<string> putBodies = [];

        using TestServer testServer = CreatePutRecordServer(putBodies);
        using BlueskyAgent agent = CreateAgent(testServer);

        AtProtoRepositoryRecord<Graph.List> record = new(
            s_list,
            new Cid(RecordCid),
            new Graph.List("name", ListPurpose.CurateList, description: null));

        AtProtoHttpResult<PutRecordResult> result = await agent.UpdateList(record, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains(RecordCid, Assert.Single(putBodies), StringComparison.Ordinal);
        Assert.Contains("swapRecord", Assert.Single(putBodies), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateListFromALooseRecordDoesNotSendASwapRecord()
    {
        List<string> putBodies = [];

        using TestServer testServer = CreatePutRecordServer(putBodies);
        using BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<PutRecordResult> result = await agent.UpdateList(
            s_list,
            new Graph.List("name", ListPurpose.CurateList, description: null),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.DoesNotContain("swapRecord", Assert.Single(putBodies), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetRelationshipsRejectsANullEntryInOthersRatherThanThrowingNullReference()
    {
        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await BlueskyServer.GetRelationships(
                actor: s_did,
                others: [null!],
                service: TestServerBuilder.DefaultUri,
                accessCredentials: CreateCredentials(),
                httpClient: CreateClient("{}"),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("others", exception.ParamName);
    }
}
