// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Drafts;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class DraftRequestTests
{
    private const string EmptyDraftsResponse = """{"drafts":[]}""";

    private static readonly Did s_did = new("did:plc:hfgp6pj3akhqxntgqwramlbg");

    private static AccessCredentials CreateCredentials() => new(
        service: TestServerBuilder.DefaultUri,
        authenticationType: AuthenticationType.UsernamePassword,
        accessJwt: JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
        refreshToken: "refreshToken");

    private static BlueskyAgent CreateAgent(TestServer testServer) =>
        new(new TestHttpClientFactory(testServer)) { Credentials = CreateCredentials(), Service = TestServerBuilder.DefaultUri };

    [Theory]
    [InlineData("cursor&with=ampersand")]
    [InlineData("cursor%with=percent")]
    [InlineData("cursor/with?reserved#characters")]
    public async Task GetDraftsEscapesTheCursor(string cursor)
    {
        string queryString = string.Empty;

        using TestServer testServer = TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                if (context.Request.Path == "/xrpc/app.bsky.draft.getDrafts")
                {
                    queryString = context.Request.QueryString.Value ?? string.Empty;

                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(EmptyDraftsResponse, TestContext.Current.CancellationToken);
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                }
            });

        BlueskyAgent agent = CreateAgent(testServer);

        AtProtoHttpResult<PagedViewReadOnlyCollection<DraftView>> result = await agent.GetDrafts(
            cursor: cursor,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains($"cursor={Uri.EscapeDataString(cursor)}", queryString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateDraftReportsSuccessWhenTheServerReturnsNoBody()
    {
        using TestServer testServer = TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            context =>
            {
                if (context.Request.Path == "/xrpc/app.bsky.draft.updateDraft")
                {
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                }

                return Task.CompletedTask;
            });

        BlueskyAgent agent = CreateAgent(testServer);

        DraftWithId draftWithId = new(new Draft(new DraftPost("Updated text"), deviceId: null, deviceName: null));

        AtProtoHttpResult<EmptyResponse> result = await agent.UpdateDraft(
            draftWithId,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task DeleteDraftSendsTheIdentifierInTheBodyAndNotTheQueryString()
    {
        string queryString = string.Empty;
        string body = string.Empty;

        using TestServer testServer = TestServerBuilder.CreateServer(
            TestServerBuilder.DefaultUri,
            async context =>
            {
                if (context.Request.Path == "/xrpc/app.bsky.draft.deleteDraft")
                {
                    queryString = context.Request.QueryString.Value ?? string.Empty;

                    using StreamReader reader = new(context.Request.Body);
                    body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                }
            });

        BlueskyAgent agent = CreateAgent(testServer);

        TimestampIdentifier draftId = TimestampIdentifier.Next();

        AtProtoHttpResult<EmptyResponse> result = await agent.DeleteDraft(
            draftId,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Empty(queryString);
        Assert.Contains(draftId.ToString(), body, StringComparison.Ordinal);
    }
}
