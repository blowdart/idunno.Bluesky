// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Chat;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class ChatValidationTests
{
    private static readonly Uri s_service = new("https://test.internal");

    private static AccessCredentials CreateCredentials()
    {
        return new AccessCredentials(
            service: s_service,
            authenticationType: AuthenticationType.UsernamePassword,
            accessJwt: JwtBuilder.CreateJwt(new Did("did:plc:test"), s_service.ToString()),
            refreshToken: "refreshToken");
    }

    [Fact]
    public void MessageInputThrowsWhenTextExceedsTheMaximumNumberOfGraphemes()
    {
        string text = new('\u00e9', Maximum.MessageLengthInGraphemes + 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => new MessageInput(text));
    }

    [Fact]
    public void MessageInputAcceptsTextAtTheMaximumNumberOfGraphemes()
    {
        string text = new('\u00e9', Maximum.MessageLengthInGraphemes);

        MessageInput message = new(text);

        Assert.Equal(text, message.Text);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task ListConversationRequestsThrowsWhenTheLimitIsOutOfRange(int limit)
    {
        using HttpClient httpClient = new();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => BlueskyServer.ListConversationRequests(
            limit: limit,
            cursor: null,
            service: s_service,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task ListJoinGroupRequestsThrowsWhenTheLimitIsOutOfRange(int limit)
    {
        using HttpClient httpClient = new();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => BlueskyServer.ListJoinGroupRequests(
            conversationId: "convoId",
            limit: limit,
            cursor: null,
            service: s_service,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EditGroupThrowsWhenTheNameExceedsTheMaximumNumberOfCharacters()
    {
        using HttpClient httpClient = new();

        // A ZWJ family emoji is a single grapheme but eleven UTF-16 characters, so this name stays
        // within the grapheme limit whilst exceeding the character limit.
        string name = string.Concat(Enumerable.Repeat("\U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466", Maximum.GroupNameLengthInGraphemes));

        Assert.True(name.Length > Maximum.GroupNameLengthInCharacters);
        Assert.True(name.GetGraphemeLength() <= Maximum.GroupNameLengthInGraphemes);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => BlueskyServer.EditGroup(
            conversationId: "convoId",
            name: name,
            service: s_service,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EditGroupThrowsWhenTheNameExceedsTheMaximumNumberOfGraphemes()
    {
        using HttpClient httpClient = new();
        string name = new('\u00e9', Maximum.GroupNameLengthInGraphemes + 1);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => BlueskyServer.EditGroup(
            conversationId: "convoId",
            name: name,
            service: s_service,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateGroupAllowsMoreMembersThanTheOldRequestLimit()
    {
        // The request object used to cap the member count at 49, which was lower than the limit both the
        // agent and the server validated against, so a valid call could still throw from inside the
        // internal request type. Anything up to Maximum.GroupMembers should now reach the HTTP layer.
        using HttpClient httpClient = new();
        List<Did> members = [.. Enumerable.Range(0, 60).Select(i => new Did($"did:plc:member{i}"))];

        Exception? exception = await Xunit.Record.ExceptionAsync(() => BlueskyServer.CreateGroup(
            members: members,
            name: "name",
            service: new Uri("https://localhost:1"),
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.IsNotType<ArgumentOutOfRangeException>(exception);
    }

    [Fact]
    public async Task CreateGroupThrowsWhenMembersExceedsTheMaximum()
    {
        using HttpClient httpClient = new();
        List<Did> members = [.. Enumerable.Range(0, Maximum.GroupMembers + 1).Select(i => new Did($"did:plc:member{i}"))];

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => BlueskyServer.CreateGroup(
            members: members,
            name: "name",
            service: s_service,
            accessCredentials: CreateCredentials(),
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    public static TheoryData<string, Func<Uri, AccessCredentials, HttpClient, CancellationToken, Task>> GroupServerCalls()
    {
        return new TheoryData<string, Func<Uri, AccessCredentials, HttpClient, CancellationToken, Task>>
        {
            {
                nameof(BlueskyServer.AddMembersToGroup),
                (service, credentials, httpClient, cancellationToken) => BlueskyServer.AddMembersToGroup(
                    conversationId: "convoId", members: [new Did("did:plc:member")], service: service, accessCredentials: credentials, httpClient: httpClient, cancellationToken: cancellationToken)
            },
            {
                nameof(BlueskyServer.ApproveJoinGroupRequest),
                (service, credentials, httpClient, cancellationToken) => BlueskyServer.ApproveJoinGroupRequest(
                    conversationId: "convoId", member: new Did("did:plc:member"), service: service, accessCredentials: credentials, httpClient: httpClient, cancellationToken: cancellationToken)
            },
            {
                nameof(BlueskyServer.CreateGroup),
                (service, credentials, httpClient, cancellationToken) => BlueskyServer.CreateGroup(
                    members: [new Did("did:plc:member")], name: "name", service: service, accessCredentials: credentials, httpClient: httpClient, cancellationToken: cancellationToken)
            },
            {
                nameof(BlueskyServer.CreateJoinGroupLink),
                (service, credentials, httpClient, cancellationToken) => BlueskyServer.CreateJoinGroupLink(
                    conversationId: "convoId", requireApproval: false, joinRule: Chat.Group.JoinRule.Anyone, service: service, accessCredentials: credentials, httpClient: httpClient, cancellationToken: cancellationToken)
            },
            {
                nameof(BlueskyServer.DisableJoinGroupLink),
                (service, credentials, httpClient, cancellationToken) => BlueskyServer.DisableJoinGroupLink(
                    conversationId: "convoId", service: service, accessCredentials: credentials, httpClient: httpClient, cancellationToken: cancellationToken)
            },
            {
                nameof(BlueskyServer.EditGroup),
                (service, credentials, httpClient, cancellationToken) => BlueskyServer.EditGroup(
                    conversationId: "convoId", name: "name", service: service, accessCredentials: credentials, httpClient: httpClient, cancellationToken: cancellationToken)
            },
            {
                nameof(BlueskyServer.EnableJoinGroupLink),
                (service, credentials, httpClient, cancellationToken) => BlueskyServer.EnableJoinGroupLink(
                    conversationId: "convoId", service: service, accessCredentials: credentials, httpClient: httpClient, cancellationToken: cancellationToken)
            },
            {
                nameof(BlueskyServer.GetJoinGroupLinkPreviews),
                (service, credentials, httpClient, cancellationToken) => BlueskyServer.GetJoinGroupLinkPreviews(
                    codes: ["code"], service: service, accessCredentials: credentials, httpClient: httpClient, cancellationToken: cancellationToken)
            },
            {
                nameof(BlueskyServer.ListJoinGroupRequests),
                (service, credentials, httpClient, cancellationToken) => BlueskyServer.ListJoinGroupRequests(
                    conversationId: "convoId", limit: null, cursor: null, service: service, accessCredentials: credentials, httpClient: httpClient, cancellationToken: cancellationToken)
            },
            {
                nameof(BlueskyServer.ListMutualGroups),
                (service, credentials, httpClient, cancellationToken) => BlueskyServer.ListMutualGroups(
                    subject: new Did("did:plc:subject"), limit: null, cursor: null, service: service, accessCredentials: credentials, httpClient: httpClient, cancellationToken: cancellationToken)
            },
            {
                nameof(BlueskyServer.RejectJoinGroupRequest),
                (service, credentials, httpClient, cancellationToken) => BlueskyServer.RejectJoinGroupRequest(
                    conversationId: "convoId", member: new Did("did:plc:member"), service: service, accessCredentials: credentials, httpClient: httpClient, cancellationToken: cancellationToken)
            },
            {
                nameof(BlueskyServer.RemoveGroupMembers),
                (service, credentials, httpClient, cancellationToken) => BlueskyServer.RemoveGroupMembers(
                    conversationId: "convoId", members: [new Did("did:plc:member")], service: service, accessCredentials: credentials, httpClient: httpClient, cancellationToken: cancellationToken)
            },
            {
                nameof(BlueskyServer.RequestJoinGroup),
                (service, credentials, httpClient, cancellationToken) => BlueskyServer.RequestJoinGroup(
                    code: "code", service: service, accessCredentials: credentials, httpClient: httpClient, cancellationToken: cancellationToken)
            },
            {
                nameof(BlueskyServer.UpdateJoinGroupRequestsRead),
                (service, credentials, httpClient, cancellationToken) => BlueskyServer.UpdateJoinGroupRequestsRead(
                    conversationId: "convoId", service: service, accessCredentials: credentials, httpClient: httpClient, cancellationToken: cancellationToken)
            },
            {
                nameof(BlueskyServer.WithdrawJoinGroupRequest),
                (service, credentials, httpClient, cancellationToken) => BlueskyServer.WithdrawJoinGroupRequest(
                    conversationId: "convoId", service: service, accessCredentials: credentials, httpClient: httpClient, cancellationToken: cancellationToken)
            },
        };
    }

    [Theory]
    [MemberData(nameof(GroupServerCalls))]
    public async Task GroupServerMethodsThrowWhenTheServiceIsNull(string name, Func<Uri, AccessCredentials, HttpClient, CancellationToken, Task> call)
    {
        using HttpClient httpClient = new();

        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => call(null!, CreateCredentials(), httpClient, TestContext.Current.CancellationToken));

        Assert.Equal("service", exception.ParamName);
        Assert.NotEmpty(name);
    }

    [Theory]
    [MemberData(nameof(GroupServerCalls))]
    public async Task GroupServerMethodsThrowWhenTheCredentialsAreNull(string name, Func<Uri, AccessCredentials, HttpClient, CancellationToken, Task> call)
    {
        using HttpClient httpClient = new();

        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => call(s_service, null!, httpClient, TestContext.Current.CancellationToken));

        Assert.Equal("accessCredentials", exception.ParamName);
        Assert.NotEmpty(name);
    }

    [Theory]
    [MemberData(nameof(GroupServerCalls))]
    public async Task GroupServerMethodsThrowWhenTheHttpClientIsNull(string name, Func<Uri, AccessCredentials, HttpClient, CancellationToken, Task> call)
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => call(s_service, CreateCredentials(), null!, TestContext.Current.CancellationToken));

        Assert.Equal("httpClient", exception.ParamName);
        Assert.NotEmpty(name);
    }

    [Fact]
    public async Task GetJoinGroupLinkPreviewsThrowsWhenTheAgentIsNotAuthenticated()
    {
        using BlueskyAgent agent = new();

        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => agent.GetJoinGroupLinkPreviews(["code"], TestContext.Current.CancellationToken));
    }
}
