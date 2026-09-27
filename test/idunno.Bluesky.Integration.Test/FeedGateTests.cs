// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.Bluesky;
using idunno.Bluesky.Feed.Gates;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class FeedGateTests
{
    private static readonly Did s_did = new("did:plc:hfgp6pj3akhqxntgqwramlbg");

    private static readonly AtUri s_ownPost = new("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lcf6ry7xy22x");

    private static readonly AtUri s_otherPost = new("at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3lcf6ry7xy22x");

    private const string PutRecordResponse = """{"uri":"at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.threadgate/3lcf6ry7xy22x","cid":"bafyreihbfkwjqz3hfyvnlwvbxbfqvwzqkqcfcjhbgvqjqvqmgqgqjqgqgq"}""";

    private static AccessCredentials CreateCredentials() => new(
        service: TestServerBuilder.DefaultUri,
        authenticationType: AuthenticationType.UsernamePassword,
        accessJwt: JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
        refreshToken: "refreshToken");

    private static TestServer CreatePutRecordServer(Action<string> captureBody)
    {
        return TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                if (context.Request.Path == "/xrpc/com.atproto.repo.putRecord")
                {
                    using StreamReader reader = new(context.Request.Body);
                    captureBody(await reader.ReadToEndAsync(TestContext.Current.CancellationToken));

                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(PutRecordResponse, TestContext.Current.CancellationToken);
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                }
            });
    }

    [Fact]
    public async Task UpdateThreadGateSendsSwapRecordWhenOneIsSupplied()
    {
        string body = string.Empty;

        using TestServer testServer = CreatePutRecordServer(b => body = b);
        BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        Cid swapRecord = new("bafyreihbfkwjqz3hfyvnlwvbxbfqvwzqkqcfcjhbgvqjqvqmgqgqjqgqgq");

        await agent.UpdateThreadGate(new ThreadGate(s_ownPost), swapRecord, TestContext.Current.CancellationToken);

        Assert.Contains("swapRecord", body, StringComparison.Ordinal);
        Assert.Contains(swapRecord.Value, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdatePostGateSendsSwapRecordWhenOneIsSupplied()
    {
        string body = string.Empty;

        using TestServer testServer = CreatePutRecordServer(b => body = b);
        BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        Cid swapRecord = new("bafyreihbfkwjqz3hfyvnlwvbxbfqvwzqkqcfcjhbgvqjqvqmgqgqjqgqgq");

        await agent.UpdatePostGate(new PostGate(s_ownPost, null), swapRecord, TestContext.Current.CancellationToken);

        Assert.Contains("swapRecord", body, StringComparison.Ordinal);
        Assert.Contains(swapRecord.Value, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateThreadGateDoesNotSendSwapRecordWhenNoneIsSupplied()
    {
        string body = string.Empty;

        using TestServer testServer = CreatePutRecordServer(b => body = b);
        BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        await agent.UpdateThreadGate(new ThreadGate(s_ownPost), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("swapRecord", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddPostGateRejectsAPostOwnedByAnotherAccount()
    {
        using TestServer testServer = CreatePutRecordServer(_ => { });
        BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        ArgumentException ex = await Assert.ThrowsAsync<ArgumentException>(
            async () => await agent.AddPostGate(new PostGate(s_otherPost, null), TestContext.Current.CancellationToken));

        Assert.Contains("not created by the current user", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetThreadGateRecordReturnsTheRecordCid()
    {
        const string getRecordResponse = """
            {
              "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.threadgate/3lcf6ry7xy22x",
              "cid": "bafyreihbfkwjqz3hfyvnlwvbxbfqvwzqkqcfcjhbgvqjqvqmgqgqjqgqgq",
              "value": {
                "$type": "app.bsky.feed.threadgate",
                "post": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lcf6ry7xy22x",
                "createdAt": "2024-12-01T00:00:00Z"
              }
            }
            """;

        using TestServer testServer = TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                if (context.Request.Path == "/xrpc/com.atproto.repo.getRecord")
                {
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(getRecordResponse, TestContext.Current.CancellationToken);
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                }
            });

        BlueskyAgent agent = new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        AtProtoHttpResult<AtProtoRepositoryRecord<ThreadGate>> result =
            await agent.GetThreadGateRecord(s_ownPost, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("bafyreihbfkwjqz3hfyvnlwvbxbfqvwzqkqcfcjhbgvqjqvqmgqgqjqgqgq", result.Result.Cid.Value);
    }
}
