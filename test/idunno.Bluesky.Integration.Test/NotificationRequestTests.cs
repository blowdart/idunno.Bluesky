// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Notifications;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class NotificationRequestTests
{
    private const string UnreadCountResponse = """{"count":3}""";

    private const string EmptyNotificationsResponse = """{"notifications":[]}""";

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
    public async Task GetNotificationUnreadCountSendsSeenAtAsANamedQueryParameter()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateCapturingServer(
            "/xrpc/app.bsky.notification.getUnreadCount",
            UnreadCountResponse,
            q => queryString = q);

        BlueskyAgent agent = CreateAgent(testServer);

        DateTimeOffset seenAt = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        AtProtoHttpResult<int?> result = await agent.GetNotificationUnreadCount(
            seenAt: seenAt,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Result);

        string expected = Uri.EscapeDataString(seenAt.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
        Assert.Contains($"seenAt={expected}", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetNotificationUnreadCountSendsNoQueryStringWhenSeenAtIsNull()
    {
        string queryString = "not set";

        using TestServer testServer = CreateCapturingServer(
            "/xrpc/app.bsky.notification.getUnreadCount",
            UnreadCountResponse,
            q => queryString = q);

        BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<int?> result = await agent.GetNotificationUnreadCount(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(string.Empty, queryString);
    }

    [Theory]
    [InlineData(NotificationReason.StarterPackJoined, "starterpack-joined")]
    [InlineData(NotificationReason.LikeViaRepost, "like-via-repost")]
    [InlineData(NotificationReason.SubscribedPost, "subscribed-post")]
    [InlineData(NotificationReason.ContactMatch, "contact-match")]
    [InlineData(NotificationReason.Like, "like")]
    public async Task ListNotificationsSendsTheReasonsFilter(NotificationReason reason, string expected)
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateCapturingServer(
            "/xrpc/app.bsky.notification.listNotifications",
            EmptyNotificationsResponse,
            q => queryString = q);

        BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<NotificationCollection> result = await agent.ListNotifications(
            reasons: [reason],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains($"reasons={expected}", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListNotificationsSendsEveryRequestedReason()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateCapturingServer(
            "/xrpc/app.bsky.notification.listNotifications",
            EmptyNotificationsResponse,
            q => queryString = q);

        BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<NotificationCollection> result = await agent.ListNotifications(
            reasons: [NotificationReason.Like, NotificationReason.Reply],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains("reasons=like", queryString, StringComparison.Ordinal);
        Assert.Contains("reasons=reply", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListNotificationsSendsSeenAtEscaped()
    {
        string queryString = string.Empty;

        using TestServer testServer = CreateCapturingServer(
            "/xrpc/app.bsky.notification.listNotifications",
            EmptyNotificationsResponse,
            q => queryString = q);

        BlueskyAgent agent = CreateAgent(testServer);

        DateTimeOffset seenAt = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        AtProtoHttpResult<NotificationCollection> result = await agent.ListNotifications(
            seenAt: seenAt,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);

        string expected = Uri.EscapeDataString(seenAt.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
        Assert.Contains($"seenAt={expected}", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListNotificationsThrowsWhenAReasonIsUnknown()
    {
        using TestServer testServer = CreateCapturingServer(
            "/xrpc/app.bsky.notification.listNotifications",
            EmptyNotificationsResponse,
            _ => { });

        BlueskyAgent agent = CreateAgent(testServer);

        await Assert.ThrowsAsync<ArgumentException>(async () => await agent.ListNotifications(
            reasons: [NotificationReason.Unknown],
            cancellationToken: TestContext.Current.CancellationToken));
    }
}
