// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class ChatRequestTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetConversationPreservesUnconstrainedIdentifiers(string id)
    {
        bool received = false;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Assert.Equal("/xrpc/chat.bsky.convo.getConvo", context.Request.Path);
            Assert.Equal(id, context.Request.Query["convoId"]);
            received = true;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"convo":{"id":"","rev":"","members":[],"muted":false,"unreadCount":0}}""", TestContext.Current.CancellationToken);
        });
        using BlueskyAgent agent = new(new TestHttpClientFactory(server)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        AtProtoHttpResult<Chat.ConversationView> result = await agent.GetConversation(id, TestContext.Current.CancellationToken);

        Assert.True(received);
        Assert.True(result.Succeeded);
        Assert.Equal(string.Empty, result.Result.Id);
        Assert.Equal(string.Empty, result.Result.Revision);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task DeleteMessagePreservesEmptyConversationAndMessageIdentifiers(string id)
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Assert.Equal("/xrpc/chat.bsky.convo.deleteMessageForSelf", context.Request.Path);
            using JsonDocument request = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(id, request.RootElement.GetProperty("convoId").GetString());
            Assert.Equal(id, request.RootElement.GetProperty("messageId").GetString());
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"id":"","rev":"","sender":{"did":"did:plc:test"},"sentAt":"2026-10-12T00:00:00Z"}""", TestContext.Current.CancellationToken);
        });
        using BlueskyAgent agent = new(new TestHttpClientFactory(server)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        AtProtoHttpResult<Chat.DeletedMessageView> result = await agent.DeleteMessageForSelf(id, id, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(string.Empty, result.Result.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ListConversationsPreservesEmptyAndWhitespaceOpenStringFilters(string value)
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Assert.Equal("/xrpc/chat.bsky.convo.listConvos", context.Request.Path);
            foreach (string name in s_queryParameterNames)
            {
                Assert.True(context.Request.Query.ContainsKey(name));
                Assert.Equal(value, context.Request.Query[name]);
            }
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"convos":[]}""", TestContext.Current.CancellationToken);
        });
        using HttpClient client = server.CreateClient();

        AtProtoHttpResult<Chat.Conversations> result = await BlueskyServer.ListConversations(
            null, value, value, value, value, value, TestServerBuilder.DefaultUri, CreateCredentials(), client,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task GetConversationDoesNotExposeNullMembers()
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"convo":{"id":"","rev":"","members":[null],"muted":false,"unreadCount":0}}""", TestContext.Current.CancellationToken);
        });
        using BlueskyAgent agent = new(new TestHttpClientFactory(server)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

        await Assert.ThrowsAsync<ArgumentException>(() => agent.GetConversation("", TestContext.Current.CancellationToken));
    }

    private static readonly Did s_did = "did:plc:test";
    private static readonly string[] s_queryParameterNames = ["cursor", "readState", "status", "kind", "lockStatus"];

    private static AccessCredentials CreateCredentials()
    {
        return new AccessCredentials(
            service: TestServerBuilder.DefaultUri,
            authenticationType: AuthenticationType.UsernamePassword,
            accessJwt: JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
            refreshToken: "refreshToken");
    }

    [Fact]
    public async Task ListConversationRequestsSendsTheCursorToTheServer()
    {
        string? actualQueryString = null;

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Path == "/xrpc/chat.bsky.convo.listConvoRequests")
            {
                actualQueryString = request.QueryString.Value;
                response.StatusCode = 200;
                response.ContentType = "application/json";
                await response.WriteAsync("""{"requests":[]}""");
                return;
            }

            response.StatusCode = 404;
        });

        AtProtoHttpResult<PagedViewReadOnlyCollection<Chat.ConversationViewBase>> result =
            await BlueskyServer.ListConversationRequests(
                limit: 25,
                cursor: "page two",
                service: TestServerBuilder.DefaultUri,
                accessCredentials: CreateCredentials(),
                httpClient: testServer.CreateClient(),
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(actualQueryString);
        Assert.Contains("cursor=page%20two", actualQueryString, StringComparison.Ordinal);
        Assert.Contains("limit=25", actualQueryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListConversationRequestsDoesNotSendAnEmptyQueryStringWhenNoParametersAreSpecified()
    {
        string? actualQueryString = null;

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Path == "/xrpc/chat.bsky.convo.listConvoRequests")
            {
                actualQueryString = request.QueryString.Value;
                response.StatusCode = 200;
                response.ContentType = "application/json";
                await response.WriteAsync("""{"requests":[]}""");
                return;
            }

            response.StatusCode = 404;
        });

        AtProtoHttpResult<PagedViewReadOnlyCollection<Chat.ConversationViewBase>> result =
            await BlueskyServer.ListConversationRequests(
                limit: null,
                cursor: null,
                service: TestServerBuilder.DefaultUri,
                accessCredentials: CreateCredentials(),
                httpClient: testServer.CreateClient(),
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(string.IsNullOrEmpty(actualQueryString));
    }

    [Fact]
    public async Task ListJoinGroupRequestsSendsTheConversationIdLimitAndCursorToTheServer()
    {
        string? actualQueryString = null;

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Path == "/xrpc/chat.bsky.group.listJoinRequests")
            {
                actualQueryString = request.QueryString.Value;
                response.StatusCode = 200;
                response.ContentType = "application/json";
                await response.WriteAsync("""{"requests":[]}""");
                return;
            }

            response.StatusCode = 404;
        });

        AtProtoHttpResult<PagedViewReadOnlyCollection<Chat.Group.JoinRequestView>> result =
            await BlueskyServer.ListJoinGroupRequests(
                conversationId: "convo id",
                limit: 10,
                cursor: "cursor value",
                service: TestServerBuilder.DefaultUri,
                accessCredentials: CreateCredentials(),
                httpClient: testServer.CreateClient(),
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(actualQueryString);
        Assert.Contains("convoId=convo%20id", actualQueryString, StringComparison.Ordinal);
        Assert.Contains("cursor=cursor%20value", actualQueryString, StringComparison.Ordinal);
        Assert.Contains("limit=10", actualQueryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListJoinGroupRequestsDeserializesTheJoinRequestsReturnedByTheServer()
    {
        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Path == "/xrpc/chat.bsky.group.listJoinRequests")
            {
                response.StatusCode = 200;
                response.ContentType = "application/json";
                await response.WriteAsync(
                    """
                    {
                      "cursor": "next",
                      "requests": [
                        {
                          "convoId": "3kxyz",
                          "requestedBy": {
                            "did": "did:plc:g6ylltenitt4tp27bpwalh7b",
                            "handle": "example.test.internal"
                          },
                          "requestedAt": "2025-05-08T00:20:44.859Z"
                        }
                      ]
                    }
                    """);
                return;
            }

            response.StatusCode = 404;
        });

        AtProtoHttpResult<PagedViewReadOnlyCollection<Chat.Group.JoinRequestView>> result =
            await BlueskyServer.ListJoinGroupRequests(
                conversationId: "3kxyz",
                limit: null,
                cursor: null,
                service: TestServerBuilder.DefaultUri,
                accessCredentials: CreateCredentials(),
                httpClient: testServer.CreateClient(),
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("next", result.Result.Cursor);

        Chat.Group.JoinRequestView joinRequest = Assert.Single(result.Result);
        Assert.Equal("3kxyz", joinRequest.ConversationId);
        Assert.Equal("did:plc:g6ylltenitt4tp27bpwalh7b", joinRequest.RequestedBy.Did.ToString());
    }

    [Fact]
    public async Task AddMembersToGroupOnlyEnumeratesTheSuppliedMembersOnce()
    {
        string? actualRequestBody = null;

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Path == "/xrpc/chat.bsky.group.addMembers")
            {
                using StreamReader reader = new(request.Body);
                actualRequestBody = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
            }

            response.StatusCode = 400;
            response.ContentType = "application/json";
            await response.WriteAsync("""{"error":"InvalidRequest","message":"test"}""");
        });

        int enumerationCount = 0;

        IEnumerable<Did> SingleUseMembers()
        {
            enumerationCount++;

            if (enumerationCount > 1)
            {
                throw new InvalidOperationException("The member sequence was enumerated more than once.");
            }

            yield return new Did("did:plc:first");
            yield return new Did("did:plc:second");
        }

        await BlueskyServer.AddMembersToGroup(
            conversationId: "convoId",
            members: SingleUseMembers(),
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, enumerationCount);
        Assert.NotNull(actualRequestBody);

        using JsonDocument document = JsonDocument.Parse(actualRequestBody);
        JsonElement members = document.RootElement.GetProperty("members");
        Assert.Equal(2, members.GetArrayLength());
    }

    [Fact]
    public async Task ListConversationsReturnsTheCursorSentByTheServer()
    {
        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Path == "/xrpc/chat.bsky.convo.listConvos")
            {
                response.StatusCode = 200;
                response.ContentType = "application/json";
                await response.WriteAsync("""{"cursor":"page two","convos":[]}""");
                return;
            }

            response.StatusCode = 404;
        });

        AtProtoHttpResult<Chat.Conversations> result =
            await BlueskyServer.ListConversations(
                limit: null,
                cursor: null,
                readState: null,
                status: null,
                kind: null,
                lockStatus: null,
                service: TestServerBuilder.DefaultUri,
                accessCredentials: CreateCredentials(),
                httpClient: testServer.CreateClient(),
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("page two", result.Result.Cursor);
    }

    [Fact]
    public async Task ListConversationsSendsEveryFilterToTheServer()
    {
        string? actualQueryString = null;

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Path == "/xrpc/chat.bsky.convo.listConvos")
            {
                actualQueryString = request.QueryString.Value;
                response.StatusCode = 200;
                response.ContentType = "application/json";
                await response.WriteAsync("""{"convos":[]}""");
                return;
            }

            response.StatusCode = 404;
        });

        AtProtoHttpResult<Chat.Conversations> result =
            await BlueskyServer.ListConversations(
                limit: 25,
                cursor: "page two",
                readState: Chat.ConversationReadState.Unread,
                status: Chat.ConversationStatus.Accepted,
                kind: Chat.ConversationKind.Direct,
                lockStatus: Chat.ConversationLockStatus.LockedPermanently,
                service: TestServerBuilder.DefaultUri,
                accessCredentials: CreateCredentials(),
                httpClient: testServer.CreateClient(),
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(actualQueryString);
        Assert.Contains("limit=25", actualQueryString, StringComparison.Ordinal);
        Assert.Contains("cursor=page%20two", actualQueryString, StringComparison.Ordinal);
        Assert.Contains("readState=unread", actualQueryString, StringComparison.Ordinal);
        Assert.Contains("status=accepted", actualQueryString, StringComparison.Ordinal);
        Assert.Contains("kind=direct", actualQueryString, StringComparison.Ordinal);
        Assert.Contains("lockStatus=locked-permanently", actualQueryString, StringComparison.Ordinal);
        Assert.DoesNotContain("?&", actualQueryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListConversationsDoesNotSendAnEmptyQueryStringWhenNoParametersAreSpecified()
    {
        string? actualQueryString = null;

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Path == "/xrpc/chat.bsky.convo.listConvos")
            {
                actualQueryString = request.QueryString.Value;
                response.StatusCode = 200;
                response.ContentType = "application/json";
                await response.WriteAsync("""{"convos":[]}""");
                return;
            }

            response.StatusCode = 404;
        });

        AtProtoHttpResult<Chat.Conversations> result =
            await BlueskyServer.ListConversations(
                limit: null,
                cursor: null,
                readState: null,
                status: null,
                kind: null,
                lockStatus: null,
                service: TestServerBuilder.DefaultUri,
                accessCredentials: CreateCredentials(),
                httpClient: testServer.CreateClient(),
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(string.IsNullOrEmpty(actualQueryString));
    }
}
