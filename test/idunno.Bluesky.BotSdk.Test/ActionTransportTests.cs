// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.Bluesky;
using idunno.Bluesky.BotSdk.DataProviders;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace idunno.Bluesky.BotSdk.Test;

/// <summary>Verifies actual SDK requests and generated response decoding against an in-memory HTTP server.</summary>
public sealed class ActionTransportTests
{
    /// <summary>Sends the original key and complete payload for each repository action and reads it back without reflection.</summary>
    /// <param name="kind">The repository action kind.</param>
    [Theory]
    [InlineData("reply")]
    [InlineData("like")]
    [InlineData("repost")]
    [InlineData("list-item")]
    public async Task PreservesRepositoryPayloadAndKey(string kind)
    {
        BotAction action = Create(kind);
        string uri = $"at://{TestRecords.BotDid}/{ActionRecords.Collection(kind)}/{action.RecordKey}";
        using Transport transport = new(async request =>
        {
            if (request.RequestUri!.Host == "plc.directory")
            {
                return Json(new
                {
                    id = TestRecords.BotDid,
                    service = new[]
                    {
                        new
                        {
                            id = "#atproto_pds",
                            type = "AtprotoPersonalDataServer",
                            serviceEndpoint = "https://pds.example.test"
                        }
                    }
                });
            }
            if (request.Method == HttpMethod.Post)
            {
                using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                Assert.Equal(action.RecordKey, body.RootElement.GetProperty("rkey").GetString());
                Assert.Equal(ActionRecords.Collection(kind), body.RootElement.GetProperty("collection").GetString());
                using JsonDocument expected = JsonDocument.Parse(action.RecordJson);
                Assert.True(JsonElement.DeepEquals(expected.RootElement, body.RootElement.GetProperty("record")));

                return Json(new { uri, cid = TestRecords.CidValue });
            }
            Assert.Contains("rkey=" + action.RecordKey, request.RequestUri!.Query);
            using JsonDocument payload = JsonDocument.Parse(action.RecordJson);

            return Json(new { uri, cid = TestRecords.CidValue, value = payload.RootElement });
        });
        await transport.AuthenticateAsync();
        Assert.True(transport.Remote.Agent.IsAuthenticated);
        Assert.Equal(TestRecords.BotDid, transport.Remote.Agent.Did!.ToString());
        BotActionResult created = await transport.Remote.CreateRecordAsync(action, CancellationToken.None);
        Assert.True(created.Succeeded);
        Assert.Equal(uri.ToString(), created.RemoteReference);
        string? found = await transport.Remote.FindRecordAsync(action, CancellationToken.None);
        using JsonDocument actual = JsonDocument.Parse(found!);
        using JsonDocument original = JsonDocument.Parse(action.RecordJson);
        Assert.True(JsonElement.DeepEquals(original.RootElement, actual.RootElement));
    }

    /// <summary>Finds a bookmark on a later page even when its post is no longer available in AppView.</summary>
    [Fact]
    public async Task ScansBookmarkPagesIncludingUnavailablePosts()
    {
        BotAction action = Create("bookmark");
        int requests = 0;
        using Transport transport = new(request =>
        {
            requests++;
            bool later = request.RequestUri!.Query.Contains("cursor=next", StringComparison.Ordinal);
            object[] bookmarks = later
                ? [new
                {
                    subject = new { uri = action.SourceUri, cid = TestRecords.CidValue },
                    item = new Dictionary<string, object>
                    {
                        ["$type"] = "app.bsky.feed.defs#notFoundPost",
                        ["uri"] = action.SourceUri,
                        ["notFound"] = true
                    }
                }]
                : [];

            return Task.FromResult(Json(new { bookmarks, cursor = later ? null : "next" }));
        });
        await transport.AuthenticateAsync();
        Assert.True(await transport.Remote.HasBookmarkAsync(action.SourceUri, CancellationToken.None));
        Assert.Equal(2, requests);
    }

    /// <summary>Refuses to report bookmark absence when a later page fails or repeats its cursor.</summary>
    /// <param name="repeatCursor">A value indicating whether the server repeats a cursor rather than failing.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncompleteBookmarkScanCannotAuthorizeWrite(bool repeatCursor)
    {
        int requests = 0;
        using Transport transport = new(_ =>
        {
            requests++;
            if (requests > 1 && !repeatCursor)
            {
                return Task.FromResult(Json(new { error = "InternalServerError" }, HttpStatusCode.ServiceUnavailable));
            }

            return Task.FromResult(Json(new { bookmarks = Array.Empty<object>(), cursor = "next" }));
        });
        await transport.AuthenticateAsync();
        if (repeatCursor)
        {
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                transport.Remote.HasBookmarkAsync(TestRecords.Work().SourceUri, CancellationToken.None));
        }
        else
        {
            await Assert.ThrowsAsync<RemoteFailureException>(() =>
                transport.Remote.HasBookmarkAsync(TestRecords.Work().SourceUri, CancellationToken.None));
        }
    }

    /// <summary>Checks unsuccessful bookmark creation responses instead of assuming an empty result means success.</summary>
    [Fact]
    public async Task BookmarkCreateChecksServerResult()
    {
        using Transport transport = new(_ =>
            Task.FromResult(Json(new { error = "InvalidRequest", message = "rejected" }, HttpStatusCode.BadRequest)));
        await transport.AuthenticateAsync();
        await Assert.ThrowsAsync<RemoteFailureException>(() =>
            transport.Remote.CreateBookmarkAsync(Source(), CancellationToken.None));
    }

    /// <summary>Finds manual author membership on a later page of an owned curation list.</summary>
    [Fact]
    public async Task ScansListMembershipPages()
    {
        AtUri list = new($"at://{TestRecords.BotDid}/app.bsky.graph.list/example");
        Did author = new(TestRecords.Work().SourceDid);
        string membership = $"at://{TestRecords.BotDid}/app.bsky.graph.listitem/manual";
        int requests = 0;
        using Transport transport = new(request =>
        {
            requests++;
            bool later = request.RequestUri!.Query.Contains("cursor=next", StringComparison.Ordinal);
            object[] items = later
                ? [new { uri = membership, subject = new { did = author.ToString(), handle = "author.example.test" } }]
                : [];

            return Task.FromResult(ListPage(list, items, later ? null : "next"));
        });
        await transport.AuthenticateAsync();
        Assert.Equal(membership, await transport.Remote.FindListMemberAsync(list, author, CancellationToken.None));
        Assert.Equal(2, requests);
    }

    /// <summary>Refuses to infer absent author membership after a failed page or a pagination loop.</summary>
    /// <param name="repeatCursor">Whether a later page repeats its cursor instead of failing.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncompleteListScanCannotAuthorizeWrite(bool repeatCursor)
    {
        AtUri list = new($"at://{TestRecords.BotDid}/app.bsky.graph.list/example");
        int requests = 0;
        using Transport transport = new(_ =>
        {
            requests++;

            return Task.FromResult(requests > 1 && !repeatCursor
                ? Json(new { error = "InternalServerError" }, HttpStatusCode.ServiceUnavailable)
                : ListPage(list, [], "next"));
        });
        await transport.AuthenticateAsync();
        if (repeatCursor)
        {
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                transport.Remote.FindListMemberAsync(list, new Did(TestRecords.Work().SourceDid), CancellationToken.None));
        }
        else
        {
            await Assert.ThrowsAsync<RemoteFailureException>(() =>
                transport.Remote.FindListMemberAsync(list, new Did(TestRecords.Work().SourceDid), CancellationToken.None));
        }
        Assert.Equal(2, requests);
    }

    /// <summary>Rejects another account's list before making any HTTP request.</summary>
    [Fact]
    public async Task RejectsUnownedAuthorList()
    {
        using Transport transport = new(_ => throw new InvalidOperationException("An unowned list must not be requested."));
        await transport.AuthenticateAsync();
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.Remote.FindListMemberAsync(
                new AtUri($"at://{TestRecords.Work().SourceDid}/app.bsky.graph.list/example"),
                new Did(TestRecords.Work().SourceDid),
                CancellationToken.None));
        Assert.Contains("owned by the bot", error.Message);
    }

    /// <summary>Rejects moderation and reference lists rather than changing account moderation behavior.</summary>
    /// <param name="purpose">The non-curation list purpose returned by AppView.</param>
    [Theory]
    [InlineData("app.bsky.graph.defs#modlist")]
    [InlineData("app.bsky.graph.defs#referencelist")]
    public async Task RejectsNonCurationAuthorList(string purpose)
    {
        AtUri list = new($"at://{TestRecords.BotDid}/app.bsky.graph.list/example");
        using Transport transport = new(_ => Task.FromResult(ListPage(list, [], null, purpose)));
        await transport.AuthenticateAsync();
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.Remote.FindListMemberAsync(list, new Did(TestRecords.Work().SourceDid), CancellationToken.None));
        Assert.Contains("curation list", error.Message);
    }

    /// <summary>Produces a realistic list response for generated SDK decoding without real network access.</summary>
    /// <param name="list">The requested list URI.</param>
    /// <param name="items">The memberships returned on this page.</param>
    /// <param name="cursor">The continuation cursor, if another page exists.</param>
    /// <param name="purpose">The list purpose, defaulting to curation.</param>
    /// <returns>The synthetic successful HTTP response.</returns>
    private static HttpResponseMessage ListPage(
        AtUri list,
        object[] items,
        string? cursor,
        string purpose = "app.bsky.graph.defs#curatelist")
    {
        Dictionary<string, object> page = new()
        {
            ["list"] = new
            {
                uri = list.ToString(),
                cid = TestRecords.CidValue,
                creator = new { did = TestRecords.BotDid, handle = "bot.example.test" },
                name = "Example",
                purpose,
                indexedAt = TestRecords.Start
            },
            ["items"] = items
        };
        if (cursor is not null)
        {
            page["cursor"] = cursor;
        }

        return Json(page);
    }

    /// <summary>Builds complete saved intent for a built-in transport test.</summary>
    /// <param name="kind">The built-in action kind.</param>
    /// <returns>The stable test action.</returns>
    private static BotAction Create(string kind) => kind switch
    {
        "reply" => TestRecords.Work(),
        "like" => BotActions.Like(Source(), 11, TestRecords.Start),
        "repost" => BotActions.Repost(Source(), 11, TestRecords.Start),
        "bookmark" => BotActions.Bookmark(Source(), 11),
        "list-item" => BotActions.AddAuthorToList(
            Source(),
            11,
            new AtUri($"at://{TestRecords.BotDid}/app.bsky.graph.list/example"),
            TestRecords.Start),
        _ => throw new ArgumentException("Unknown test kind.", nameof(kind))
    };

    /// <summary>Constructs the source's exact URI and CID without fetching a real post.</summary>
    /// <returns>The synthetic source reference.</returns>
    private static StrongReference Source() => new(
        TestRecords.Work().SourceUri,
        new Cid(TestRecords.CidValue));

    /// <summary>Encodes a synthetic SDK server response using test-only reflection serialization.</summary>
    /// <param name="value">The synthetic response body.</param>
    /// <param name="status">The HTTP result status.</param>
    /// <returns>The in-memory response owned by the caller.</returns>
    private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
        };

    /// <summary>Owns an authenticated SDK agent with synthetic credentials and an in-memory HTTP transport.</summary>
    private sealed class Transport : IDisposable
    {
        private readonly Handler _handler;
        private readonly BlueskySession _session;
        public BlueskyActionRemote Remote { get; }

        /// <summary>Initializes a new instance of the transport fixture without opening storage or network connections.</summary>
        /// <param name="send">The synthetic server response callback.</param>
        public Transport(Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
        {
            _handler = new Handler(send);
            BotOptions options = TestRecords.Options();
            BlueskyAgent agent = new(new ClientFactory(_handler), new BlueskyAgentOptions
            {
                EnableBackgroundTokenRefresh = false
            });
            _session = new BlueskySession(
                agent,
                new SqliteDatabaseProvider(
                    "Data Source=unused-transport-test.db",
                    options,
                    TimeProvider.System,
                    NullLogger<SqliteDatabaseProvider>.Instance),
                new CredentialVault(options),
                options,
                NullLogger<BlueskySession>.Instance);
            Remote = new BlueskyActionRemote(
                _session,
                options,
                new ClientFactory(_handler));
        }

        public async Task AuthenticateAsync()
        {
            static string Segment(object value) => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(value))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            string header = Segment(new { alg = "HS256", typ = "JWT" });
            string payload = Segment(new
            {
                sub = TestRecords.BotDid,
                exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
                scope = "com.atproto.access"
            });
            AccessCredentials credentials = new(
                new Uri("https://pds.example.test"),
                AuthenticationType.UsernamePassword,
                $"{header}.{payload}.dGVzdA",
                "synthetic-refresh-token");
            await _session.Agent.Login(credentials);
        }

        public void Dispose()
        {
            _session.Dispose();
            _handler.Dispose();
        }
    }

    /// <summary>Routes HTTP requests to the test callback rather than the network.</summary>
    /// <param name="send">The in-memory server response callback.</param>
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            send(request);
    }

    /// <summary>Shares the fixture-owned transport across SDK clients without disposing it prematurely.</summary>
    /// <param name="handler">The fixture-owned HTTP handler.</param>
    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
