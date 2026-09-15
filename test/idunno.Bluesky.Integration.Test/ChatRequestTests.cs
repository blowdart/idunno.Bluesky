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
    private static readonly Did s_did = "did:plc:test";

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
