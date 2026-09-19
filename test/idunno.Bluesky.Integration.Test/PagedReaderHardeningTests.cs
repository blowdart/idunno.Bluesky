// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Notifications;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

/// <summary>
/// A 200 response whose body cannot be deserialized used to either throw out of the client or, because the failure
/// branch returned an empty collection alongside the original OK status code, be reported as a successful empty page.
/// Both now surface as a failed result with a null <see cref="AtProtoHttpResult{TResult}.Result"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public class PagedReaderHardeningTests
{
    private static readonly Did s_did = "did:plc:test";

    private static AccessCredentials CreateCredentials() => new(
        service: TestServerBuilder.DefaultUri,
        authenticationType: AuthenticationType.UsernamePassword,
        accessJwt: JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
        refreshToken: "refreshToken");

    private static TestServer CreateServer(string body) =>
        TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(body);
        });

    private static Task<AtProtoHttpResult<NotificationCollection>> ListNotifications(TestServer testServer) =>
        BlueskyServer.ListNotifications(
            limit: 25,
            cursor: null,
            seenAt: null,
            reasons: null,
            subscribedLabelers: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: testServer.CreateClient(),
            cancellationToken: TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("""{"seenAt":"2024-01-01T00:00:00+00:00"}""")]
    [InlineData("""{"notifications":null,"seenAt":"2024-01-01T00:00:00+00:00"}""")]
    public async Task ListNotificationsReportsAMalformedPageAsAFailureRatherThanAnEmptyPage(string body)    {
        AtProtoHttpResult<NotificationCollection> result = await ListNotifications(CreateServer(body));

        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task ListNotificationsSkipsANullNotificationRatherThanThrowing()
    {
        AtProtoHttpResult<NotificationCollection> result = await ListNotifications(
            CreateServer("""{"notifications":[null],"seenAt":"2024-01-01T00:00:00+00:00"}"""));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task ListNotificationsSucceedsForAWellFormedEmptyPage()
    {
        AtProtoHttpResult<NotificationCollection> result =
            await ListNotifications(CreateServer("""{"notifications":[],"seenAt":"2024-01-01T00:00:00+00:00"}"""));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }
}
