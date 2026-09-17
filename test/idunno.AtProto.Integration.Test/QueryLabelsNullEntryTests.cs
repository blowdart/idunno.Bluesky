// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;
using idunno.AtProto.Labels;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.AtProto.Integration.Test;

/// <summary>
/// Neither <see cref="System.Text.Json.Serialization.JsonRequiredAttribute"/> nor
/// <see cref="System.Text.Json.JsonSerializerOptions.RespectNullableAnnotations"/> apply to the element type of a
/// collection, so a labelling service returning a null entry inside an otherwise well formed collection used to hand
/// that null straight through to the caller. Null entries are now skipped and logged instead.
/// </summary>
[ExcludeFromCodeCoverage]
public class QueryLabelsNullEntryTests
{
    private static readonly Did s_did = "did:plc:test";

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

    [Fact]
    public async Task QueryLabelsSkipsANullLabelRatherThanReturningIt()
    {
        AtProtoHttpResult<PagedReadOnlyCollection<Label>> result = await AtProtoServer.QueryLabels(
            uriPatterns: ["*"],
            sources: null,
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient("""{"labels":[null]}"""),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task QueryLabelsReturnsANonNullLabelAlongsideASkippedNullEntry()
    {
        const string body = """
            {"labels":[null,{"src":"did:plc:test","uri":"at://did:plc:test/app.bsky.feed.post/1","val":"spam","cts":"2024-01-01T00:00:00Z"}]}
            """;

        AtProtoHttpResult<PagedReadOnlyCollection<Label>> result = await AtProtoServer.QueryLabels(
            uriPatterns: ["*"],
            sources: null,
            limit: 25,
            cursor: null,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: CreateCredentials(),
            httpClient: CreateClient(body),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Result);
        Assert.Equal("spam", Assert.Single(result.Result).Value);
    }
}
