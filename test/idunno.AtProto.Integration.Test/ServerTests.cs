// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Server;
using idunno.AtProto.Sync;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.AtProto.Integration.Test;

public class ServerTests
{
    [Fact]
    public async Task DirectGetRepoStreamsCarResponse()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();
        Did repo = new("did:plc:abcdefghijklmnopqrstuvwx");
        byte[] carContent = [0x43, 0x41, 0x52];
        bool receivedExpectedAcceptHeader = false;

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            Assert.Equal("/xrpc/com.atproto.sync.getRepo", context.Request.Path);
            Assert.Equal(repo.Value, context.Request.Query["did"]);
            receivedExpectedAcceptHeader = context.Request.Headers.Accept.Any(
                value => value?.StartsWith("application/vnd.ipld.car", StringComparison.OrdinalIgnoreCase) == true);
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.Body.WriteAsync(carContent, TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<Stream> result = await AtProtoServer.GetRepo(
            repo,
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(receivedExpectedAcceptHeader);
        await using Stream stream = result.Result;
        using MemoryStream content = new();
        await stream.CopyToAsync(content, TestContext.Current.CancellationToken);
        Assert.Equal(carContent, content.ToArray());
    }

    [Fact]
    public async Task DirectGetRepoReturnsAtProtoErrorDetails()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();
        Did repo = new("did:plc:abcdefghijklmnopqrstuvwx");

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(
                new { error = "RepoNotFound", message = "Repository not found." },
                TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<Stream> result = await AtProtoServer.GetRepo(
            repo,
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal("RepoNotFound", result.AtErrorDetail?.Error);
        Assert.Equal("Repository not found.", result.AtErrorDetail?.Message);
    }

    [Fact]
    public async Task DirectGetBlobStreamsBlobResponse()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();
        Did repo = new("did:plc:abcdefghijklmnopqrstuvwx");
        Cid cid = new("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a");
        byte[] blobContent = [0x89, 0x50, 0x4E, 0x47];

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            Assert.Equal("/xrpc/com.atproto.sync.getBlob", context.Request.Path);
            Assert.Equal(repo.Value, context.Request.Query["did"]);
            Assert.Equal(cid.ToString(), context.Request.Query["cid"]);
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "image/png";
            context.Response.ContentLength = blobContent.Length;
            await context.Response.Body.WriteAsync(blobContent, TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<BlobContent> result = await AtProtoServer.GetBlob(
            repo,
            cid,
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        await using BlobContent blob = result.Result;
        Assert.Equal(cid, blob.Cid);
        Assert.Equal("image/png", blob.ContentType?.MediaType);
        Assert.Equal(blobContent.Length, blob.ContentLength);
        using MemoryStream content = new();
        await blob.Content.CopyToAsync(content, TestContext.Current.CancellationToken);
        Assert.Equal(blobContent, content.ToArray());
    }

    [Fact]
    public async Task DirectGetBlobReturnsAtProtoErrorDetails()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();
        Did repo = new("did:plc:abcdefghijklmnopqrstuvwx");
        Cid cid = new("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a");

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new { error = "BlobNotFound", message = "Blob not found." },
                TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<BlobContent> result = await AtProtoServer.GetBlob(
            repo,
            cid,
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("BlobNotFound", result.AtErrorDetail?.Error);
        Assert.Equal("Blob not found.", result.AtErrorDetail?.Message);
    }

    [Fact]
    public async Task DirectGetBlocksSendsAllCidsAndStreamsCarResponse()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();
        Did repo = new("did:plc:abcdefghijklmnopqrstuvwx");
        Cid[] cids =
        [
            new("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4"),
            new("bafyreih5pxnryqrmcfefnxcpqezzosgozd4n4nw37o6cu52h4m7wefjmmq")
        ];
        byte[] carContent = [0x43, 0x41, 0x52];
        string[]? receivedCids = null;

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            Assert.Equal("/xrpc/com.atproto.sync.getBlocks", context.Request.Path);
            Assert.Equal(repo.Value, context.Request.Query["did"]);
            receivedCids = context.Request.Query["cids"].ToArray()!;
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.Body.WriteAsync(carContent, TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<Stream> result = await AtProtoServer.GetBlocks(
            repo,
            cids,
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(cids.Select(cid => cid.ToString()), receivedCids);
        await using Stream stream = result.Result;
        using MemoryStream content = new();
        await stream.CopyToAsync(content, TestContext.Current.CancellationToken);
        Assert.Equal(carContent, content.ToArray());
    }

    [Fact]
    public async Task DirectGetBlocksReturnsAtProtoErrorDetails()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();
        Did repo = new("did:plc:abcdefghijklmnopqrstuvwx");

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new { error = "BlockNotFound", message = "Block not found." },
                TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<Stream> result = await AtProtoServer.GetBlocks(
            repo,
            [new Cid("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4")],
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("BlockNotFound", result.AtErrorDetail?.Error);
    }

    [Fact]
    public async Task DirectGetBlocksThrowsWhenNoCidsSpecified()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => AtProtoServer.GetBlocks(
            new Did("did:plc:abcdefghijklmnopqrstuvwx"),
            [],
            new Uri("https://example.invalid"),
            new HttpClient(),
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DirectGetSyncRecordSendsParametersAndStreamsCarResponse()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();
        Did repo = new("did:plc:abcdefghijklmnopqrstuvwx");
        Nsid collection = new("app.bsky.feed.post");
        RecordKey rKey = new("3l2k4j5h6g7f8");
        byte[] carContent = [0x43, 0x41, 0x52];

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            Assert.Equal("/xrpc/com.atproto.sync.getRecord", context.Request.Path);
            Assert.Equal(repo.Value, context.Request.Query["did"]);
            Assert.Equal(collection.ToString(), context.Request.Query["collection"]);
            Assert.Equal(rKey.Value, context.Request.Query["rkey"]);
            Assert.Equal("application/vnd.ipld.car", context.Request.Headers.Accept);
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.Body.WriteAsync(carContent, TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<Stream> result = await AtProtoServer.GetSyncRecord(
            repo,
            collection,
            rKey,
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        await using Stream stream = result.Result;
        using MemoryStream content = new();
        await stream.CopyToAsync(content, TestContext.Current.CancellationToken);
        Assert.Equal(carContent, content.ToArray());
    }

    [Theory]
    [InlineData("RecordNotFound")]
    [InlineData("RepoNotFound")]
    [InlineData("RepoTakendown")]
    [InlineData("RepoSuspended")]
    [InlineData("RepoDeactivated")]
    public async Task DirectGetSyncRecordReturnsAtProtoErrorDetails(string error)
    {
        string domainName = TestServerBuilder.CreateRandomHostName();

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new { error, message = "Error." },
                TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<Stream> result = await AtProtoServer.GetSyncRecord(
            new Did("did:plc:abcdefghijklmnopqrstuvwx"),
            new Nsid("app.bsky.feed.post"),
            new RecordKey("3l2k4j5h6g7f8"),
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(error, result.AtErrorDetail?.Error);
    }

    [Theory]
    [InlineData("""{"did":"did:plc:abcdefghijklmnopqrstuvwx","active":true,"rev":"3l2k4j5h6g7f8"}""", true, null, "3l2k4j5h6g7f8")]
    [InlineData("""{"did":"did:plc:abcdefghijklmnopqrstuvwx","active":false,"status":"desynchronized"}""", false, RepoStatus.Desynchronized, null)]
    [InlineData("""{"did":"did:plc:abcdefghijklmnopqrstuvwx","active":false,"status":"takendown"}""", false, RepoStatus.Takendown, null)]
    [InlineData("""{"did":"did:plc:abcdefghijklmnopqrstuvwx","active":false,"status":"suspended"}""", false, RepoStatus.Suspended, null)]
    [InlineData("""{"did":"did:plc:abcdefghijklmnopqrstuvwx","active":false,"status":"deleted"}""", false, RepoStatus.Deleted, null)]
    [InlineData("""{"did":"did:plc:abcdefghijklmnopqrstuvwx","active":false,"status":"deactivated"}""", false, RepoStatus.Deactivated, null)]
    [InlineData("""{"did":"did:plc:abcdefghijklmnopqrstuvwx","active":false,"status":"throttled"}""", false, RepoStatus.Throttled, null)]
    [InlineData("""{"did":"did:plc:abcdefghijklmnopqrstuvwx","active":false,"status":"somethingNew"}""", false, RepoStatus.Unknown, null)]
    [InlineData("""{"did":"did:plc:abcdefghijklmnopqrstuvwx","active":false}""", false, null, null)]
    public async Task DirectGetRepoStatusIsDeserializedCorrectly(string json, bool expectedActive, RepoStatus? expectedStatus, string? expectedRev)
    {
        string domainName = TestServerBuilder.CreateRandomHostName();
        Did repo = new("did:plc:abcdefghijklmnopqrstuvwx");

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            Assert.Equal("/xrpc/com.atproto.sync.getRepoStatus", context.Request.Path);
            Assert.Equal(repo.Value, context.Request.Query["did"]);
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(json, TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<RepoHostingStatus> result = await AtProtoServer.GetRepoStatus(
            repo,
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(repo, result.Result.Did);
        Assert.Equal(expectedActive, result.Result.Active);
        Assert.Equal(expectedStatus, result.Result.Status);
        Assert.Equal(expectedRev, result.Result.Rev);
    }

    [Fact]
    public async Task DirectGetRepoStatusReturnsAtProtoErrorDetails()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new { error = "RepoNotFound", message = "Repo not found." },
                TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<RepoHostingStatus> result = await AtProtoServer.GetRepoStatus(
            new Did("did:plc:abcdefghijklmnopqrstuvwx"),
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("RepoNotFound", result.AtErrorDetail?.Error);
    }

    [Fact]
    public async Task DirectListBlobsSendsParametersAndReturnsCidsAndCursor()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();
        Did repo = new("did:plc:abcdefghijklmnopqrstuvwx");
        TimestampIdentifier since = new("3l2k4j5h6g7f2");
        const string cid1 = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";
        const string cid2 = "bafyreih5pxnryqrmcfefnxcpqezzosgozd4n4nw37o6cu52h4m7wefjmmq";

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            Assert.Equal("/xrpc/com.atproto.sync.listBlobs", context.Request.Path);
            Assert.Equal(repo.Value, context.Request.Query["did"]);
            Assert.Equal(since.Value, context.Request.Query["since"]);
            Assert.Equal("10", context.Request.Query["limit"]);
            Assert.Equal("previous", context.Request.Query["cursor"]);
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                $$"""{"cursor":"next","cids":["{{cid1}}","{{cid2}}"]}""",
                TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<PagedCidCollection> result = await AtProtoServer.ListBlobs(
            repo,
            since,
            10,
            "previous",
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal([cid1, cid2], result.Result.Select(cid => cid.ToString()));
        Assert.Equal("next", result.Result.Cursor);
    }

    [Fact]
    public async Task DirectListBlobsOmitsOptionalParameters()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            Assert.Equal("/xrpc/com.atproto.sync.listBlobs", context.Request.Path);
            Assert.Equal(["did"], context.Request.Query.Keys);
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"cids":[]}""", TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<PagedCidCollection> result = await AtProtoServer.ListBlobs(
            new Did("did:plc:abcdefghijklmnopqrstuvwx"),
            since: null,
            limit: null,
            cursor: null,
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Result);
        Assert.Null(result.Result.Cursor);
    }

    [Theory]
    [InlineData("RepoNotFound")]
    [InlineData("RepoTakendown")]
    [InlineData("RepoSuspended")]
    [InlineData("RepoDeactivated")]
    public async Task DirectListBlobsReturnsAtProtoErrorDetails(string error)
    {
        string domainName = TestServerBuilder.CreateRandomHostName();

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new { error, message = "Error." },
                TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<PagedCidCollection> result = await AtProtoServer.ListBlobs(
            new Did("did:plc:abcdefghijklmnopqrstuvwx"),
            since: null,
            limit: null,
            cursor: null,
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(error, result.AtErrorDetail?.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public async Task DirectListBlobsThrowsWhenLimitIsOutOfRange(int limit)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => AtProtoServer.ListBlobs(
            new Did("did:plc:abcdefghijklmnopqrstuvwx"),
            since: null,
            limit,
            cursor: null,
            new Uri("https://example.invalid"),
            new HttpClient(),
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DirectListHostsSendsParametersAndReturnsHostsAndCursor()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            Assert.Equal("/xrpc/com.atproto.sync.listHosts", context.Request.Path);
            Assert.Equal("10", context.Request.Query["limit"]);
            Assert.Equal("previous", context.Request.Query["cursor"]);
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """
                {
                  "cursor": "next",
                  "hosts": [
                    { "hostname": "pds.example.com", "seq": 42, "accountCount": 7, "status": "active" },
                    { "hostname": "other.example.com", "status": "somethingNew" },
                    { "hostname": "minimal.example.com" }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<PagedReadOnlyCollection<HostDescription>> result = await AtProtoServer.ListHosts(
            10,
            "previous",
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("next", result.Result.Cursor);
        Assert.Equal(3, result.Result.Count);

        Assert.Equal("pds.example.com", result.Result[0].Hostname);
        Assert.Equal(42, result.Result[0].Seq);
        Assert.Equal(7, result.Result[0].AccountCount);
        Assert.Equal(HostStatus.Active, result.Result[0].Status);

        Assert.Equal("other.example.com", result.Result[1].Hostname);
        Assert.Equal(HostStatus.Unknown, result.Result[1].Status);

        Assert.Equal("minimal.example.com", result.Result[2].Hostname);
        Assert.Null(result.Result[2].Seq);
        Assert.Null(result.Result[2].AccountCount);
        Assert.Null(result.Result[2].Status);
    }

    [Fact]
    public async Task DirectListHostsOmitsOptionalParameters()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            Assert.Equal("/xrpc/com.atproto.sync.listHosts", context.Request.Path);
            Assert.False(context.Request.QueryString.HasValue);
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"hosts":[]}""", TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<PagedReadOnlyCollection<HostDescription>> result = await AtProtoServer.ListHosts(
            limit: null,
            cursor: null,
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Result);
        Assert.Null(result.Result.Cursor);
    }

    [Fact]
    public async Task DirectListHostsSendsCursorWithoutLimit()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            Assert.Equal("?cursor=abc%2Fdef", context.Request.QueryString.Value);
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"hosts":[]}""", TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<PagedReadOnlyCollection<HostDescription>> result = await AtProtoServer.ListHosts(
            limit: null,
            cursor: "abc/def",
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public async Task DirectListHostsThrowsWhenLimitIsOutOfRange(int limit)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => AtProtoServer.ListHosts(
            limit,
            cursor: null,
            new Uri("https://example.invalid"),
            new HttpClient(),
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DirectListReposSendsParametersAndReturnsReposAndCursor()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();
        const string head = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            Assert.Equal("/xrpc/com.atproto.sync.listRepos", context.Request.Path);
            Assert.Equal("10", context.Request.Query["limit"]);
            Assert.Equal("previous", context.Request.Query["cursor"]);
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                $$"""
                {
                  "cursor": "next",
                  "repos": [
                    { "did": "did:plc:abcdefghijklmnopqrstuvwx", "head": "{{head}}", "rev": "3l2k4j5h6g7f8", "active": true },
                    { "did": "did:plc:bcdefghijklmnopqrstuvwxy", "head": "{{head}}", "rev": "3l2k4j5h6g7f9", "active": false, "status": "desynchronized" },
                    { "did": "did:plc:cdefghijklmnopqrstuvwxyz", "head": "{{head}}", "rev": "3l2k4j5h6g7fa", "active": false, "status": "somethingNew" },
                    { "did": "did:plc:defghijklmnopqrstuvwxyza", "head": "{{head}}", "rev": "3l2k4j5h6g7fb" }
                  ]
                }
                """,
                TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<PagedReadOnlyCollection<HostedRepository>> result = await AtProtoServer.ListRepos(
            10,
            "previous",
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("next", result.Result.Cursor);
        Assert.Equal(4, result.Result.Count);

        Assert.Equal(new Did("did:plc:abcdefghijklmnopqrstuvwx"), result.Result[0].Did);
        Assert.Equal(head, result.Result[0].Head.ToString());
        Assert.Equal("3l2k4j5h6g7f8", result.Result[0].Rev);
        Assert.True(result.Result[0].Active);
        Assert.Null(result.Result[0].Status);

        Assert.False(result.Result[1].Active);
        Assert.Equal(RepoStatus.Desynchronized, result.Result[1].Status);

        Assert.Equal(RepoStatus.Unknown, result.Result[2].Status);

        Assert.Null(result.Result[3].Active);
        Assert.Null(result.Result[3].Status);
    }

    [Fact]
    public async Task DirectListReposOmitsOptionalParameters()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            Assert.Equal("/xrpc/com.atproto.sync.listRepos", context.Request.Path);
            Assert.False(context.Request.QueryString.HasValue);
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"repos":[]}""", TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<PagedReadOnlyCollection<HostedRepository>> result = await AtProtoServer.ListRepos(
            limit: null,
            cursor: null,
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Result);
        Assert.Null(result.Result.Cursor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public async Task DirectListReposThrowsWhenLimitIsOutOfRange(int limit)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => AtProtoServer.ListRepos(
            limit,
            cursor: null,
            new Uri("https://example.invalid"),
            new HttpClient(),
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DirectListReposByCollectionSendsParametersAndReturnsDidsAndCursor()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();
        Nsid collection = new("app.bsky.feed.post");

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            Assert.Equal("/xrpc/com.atproto.sync.listReposByCollection", context.Request.Path);
            Assert.Equal(collection.ToString(), context.Request.Query["collection"]);
            Assert.Equal("2000", context.Request.Query["limit"]);
            Assert.Equal("previous", context.Request.Query["cursor"]);
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"cursor":"next","repos":[{"did":"did:plc:abcdefghijklmnopqrstuvwx"},{"did":"did:web:example.com"}]}""",
                TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<PagedDidCollection> result = await AtProtoServer.ListReposByCollection(
            collection,
            2000,
            "previous",
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("next", result.Result.Cursor);
        Assert.Equal([new Did("did:plc:abcdefghijklmnopqrstuvwx"), new Did("did:web:example.com")], result.Result);
    }

    [Fact]
    public async Task DirectListReposByCollectionOmitsOptionalParameters()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            Assert.Equal("/xrpc/com.atproto.sync.listReposByCollection", context.Request.Path);
            Assert.Equal(["collection"], context.Request.Query.Keys);
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"repos":[]}""", TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<PagedDidCollection> result = await AtProtoServer.ListReposByCollection(
            new Nsid("app.bsky.feed.post"),
            limit: null,
            cursor: null,
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Result);
        Assert.Null(result.Result.Cursor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2001)]
    public async Task DirectListReposByCollectionThrowsWhenLimitIsOutOfRange(int limit)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => AtProtoServer.ListReposByCollection(
            new Nsid("app.bsky.feed.post"),
            limit,
            cursor: null,
            new Uri("https://example.invalid"),
            new HttpClient(),
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DirectRequestCrawlPostsHostname()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();
        string? receivedHostname = null;

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            Assert.Equal(HttpMethods.Post, context.Request.Method);
            Assert.Equal("/xrpc/com.atproto.sync.requestCrawl", context.Request.Path);
            using System.Text.Json.JsonDocument body = await System.Text.Json.JsonDocument.ParseAsync(context.Request.Body, cancellationToken: TestContext.Current.CancellationToken);
            receivedHostname = body.RootElement.GetProperty("hostname").GetString();
            context.Response.StatusCode = StatusCodes.Status200OK;
        });

        AtProtoHttpResult<EmptyResponse> result = await AtProtoServer.RequestCrawl(
            "pds.example.com",
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("pds.example.com", receivedHostname);
    }

    [Fact]
    public async Task DirectRequestCrawlReturnsAtProtoErrorDetails()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new { error = "HostBanned", message = "Host is banned." },
                TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<EmptyResponse> result = await AtProtoServer.RequestCrawl(
            "pds.example.com",
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("HostBanned", result.AtErrorDetail?.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task DirectRequestCrawlThrowsWhenHostnameIsEmpty(string hostname)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => AtProtoServer.RequestCrawl(
            hostname,
            new Uri("https://example.invalid"),
            new HttpClient(),
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("active", HostStatus.Active)]
    [InlineData("idle", HostStatus.Idle)]
    [InlineData("offline", HostStatus.Offline)]
    [InlineData("throttled", HostStatus.Throttled)]
    [InlineData("banned", HostStatus.Banned)]
    [InlineData("somethingNew", HostStatus.Unknown)]
    public async Task DirectGetHostStatusIsDeserializedCorrectly(string status, HostStatus expectedStatus)
    {
        string domainName = TestServerBuilder.CreateRandomHostName();
        const string hostname = "pds.example.com";

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            Assert.Equal("/xrpc/com.atproto.sync.getHostStatus", context.Request.Path);
            Assert.Equal(hostname, context.Request.Query["hostname"]);
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                $$"""{"hostname":"{{hostname}}","seq":1234,"accountCount":56,"status":"{{status}}"}""",
                TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<HostDescription> result = await AtProtoServer.GetHostStatus(
            hostname,
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(hostname, result.Result.Hostname);
        Assert.Equal(1234, result.Result.Seq);
        Assert.Equal(56, result.Result.AccountCount);
        Assert.Equal(expectedStatus, result.Result.Status);
    }

    [Fact]
    public async Task AgentGetHostStatusDeserializesMinimalResponse()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();
        const string hostname = "pds.example.com";

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($$"""{"hostname":"{{hostname}}"}""", TestContext.Current.CancellationToken);
        });

        using var agent = new AtProtoAgent(new Uri($"https://{domainName}"), new TestHttpClientFactory(testServer));
        AtProtoHttpResult<HostDescription> result = await agent.GetHostStatus(hostname, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(hostname, result.Result.Hostname);
        Assert.Null(result.Result.Seq);
        Assert.Null(result.Result.AccountCount);
        Assert.Null(result.Result.Status);
    }

    [Fact]
    public async Task DirectGetHostStatusReturnsHostNotFound()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new { error = "HostNotFound", message = "Host not found." },
                TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<HostDescription> result = await AtProtoServer.GetHostStatus(
            "unknown.example.com",
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("HostNotFound", result.AtErrorDetail?.Error);
    }

    [Fact]
    public async Task DirectGetLatestCommitIsDeserializedCorrectly()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();
        Did repo = new("did:plc:abcdefghijklmnopqrstuvwx");
        const string cid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";
        const string rev = "3lp33zwqh6e2v";

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            Assert.Equal("/xrpc/com.atproto.sync.getLatestCommit", context.Request.Path);
            Assert.Equal(repo.Value, context.Request.Query["did"]);
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($$"""{"cid":"{{cid}}","rev":"{{rev}}"}""", TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<idunno.AtProto.Repo.Commit> result = await AtProtoServer.GetLatestCommit(
            repo,
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(new Cid(cid), result.Result.Cid);
        Assert.Equal(rev, result.Result.Rev);
    }

    [Theory]
    [InlineData("RepoNotFound")]
    [InlineData("RepoTakendown")]
    [InlineData("RepoSuspended")]
    [InlineData("RepoDeactivated")]
    public async Task DirectGetLatestCommitReturnsErrorDetails(string error)
    {
        string domainName = TestServerBuilder.CreateRandomHostName();

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error, message = "Failed." }, TestContext.Current.CancellationToken);
        });

        AtProtoHttpResult<idunno.AtProto.Repo.Commit> result = await AtProtoServer.GetLatestCommit(
            new Did("did:plc:abcdefghijklmnopqrstuvwx"),
            new Uri($"https://{domainName}"),
            testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(error, result.AtErrorDetail?.Error);
    }

    [Fact]
    public async Task DirectDescribeServerIsDeserializedCorrectly()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Path == "/xrpc/com.atproto.server.describeServer")
            {
                response.StatusCode = 200;
                var serverDescription = new ServerDescription(
                    did: $"did:web:{domainName}",
                    contact: new Contact($"test@{domainName}"),
                    links: new Links
                    {
                        PrivacyPolicy = new Uri($"https://{domainName}/privacy"),
                        TermsOfService = new Uri($"https://{domainName}/terms")
                    },
                    availableUserDomains: [domainName],
                    inviteCodeRequired: false,
                    phoneVerificationRequired: false,
                    blobUploadLimit: 10000000);
                await response.WriteAsJsonAsync(serverDescription);
            }
        });

        AtProtoHttpResult<ServerDescription> response = await AtProtoServer.DescribeServer(
            service: new Uri($"https://{domainName}"),
            httpClient: testServer.CreateClient(),
            loggerFactory: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.Succeeded);

        Assert.Equal($"did:web:{domainName}", response.Result.Did);
        Assert.NotNull(response.Result.Contact);
        Assert.Equal($"test@{domainName}", response.Result.Contact.Email);

        Assert.False(response.Result.InviteCodeRequired);
        Assert.False(response.Result.PhoneVerificationRequired);
        Assert.Equal(10000000, response.Result.BlobUploadLimit);

        Assert.Single(response.Result.AvailableUserDomains);
        Assert.Equal(response.Result.AvailableUserDomains[0], domainName);

        Assert.NotNull(response.Result.Links);
        Assert.Equal(new Uri($"https://{domainName}/privacy"), response.Result.Links.PrivacyPolicy);
        Assert.Equal(new Uri($"https://{domainName}/terms"), response.Result.Links.TermsOfService);
    }

    [Fact]
    public async Task AgentDescribeServerIsDeserializedCorrectly()
    {
        string domainName = TestServerBuilder.CreateRandomHostName();

        TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Path == "/xrpc/com.atproto.server.describeServer")
            {
                response.StatusCode = 200;
                var serverDescription = new ServerDescription(
                    did: $"did:web:{domainName}",
                    contact: new Contact($"test@{domainName}"),
                    links: new Links
                    {
                        PrivacyPolicy = new Uri($"https://{domainName}/privacy"),
                        TermsOfService = new Uri($"https://{domainName}/terms")
                    },
                    availableUserDomains: [domainName],
                    inviteCodeRequired: false,
                    phoneVerificationRequired: false,
                    blobUploadLimit: 10000000);
                await response.WriteAsJsonAsync(serverDescription);
            }
        });

        using (var agent = new AtProtoAgent(new Uri($"https://{domainName}"), new TestHttpClientFactory(testServer)))
        {
            AtProtoHttpResult<ServerDescription> response = await agent.DescribeServer(new Uri($"https://{domainName}"), TestContext.Current.CancellationToken);

            Assert.True(response.Succeeded);

            Assert.Equal($"did:web:{domainName}", response.Result.Did);
            Assert.NotNull(response.Result.Contact);
            Assert.Equal($"test@{domainName}", response.Result.Contact.Email);

            Assert.False(response.Result.InviteCodeRequired);
            Assert.False(response.Result.PhoneVerificationRequired);
            Assert.Equal(10000000, response.Result.BlobUploadLimit);

            Assert.Single(response.Result.AvailableUserDomains);
            Assert.Equal(response.Result.AvailableUserDomains[0], domainName);

            Assert.NotNull(response.Result.Links);
            Assert.Equal(new Uri($"https://{domainName}/privacy"), response.Result.Links.PrivacyPolicy);
            Assert.Equal(new Uri($"https://{domainName}/terms"), response.Result.Links.TermsOfService);
        }
    }
}