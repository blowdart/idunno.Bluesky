// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto.Authentication;
using idunno.Bluesky.Embed;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class OpenGraphMetadataParsingTests
{
    private const string PagePath = "/document/12345";

    [Theory]
    [InlineData("<meta property=\"og:title\" content=\"Title\" />")]
    [InlineData("<meta content=\"Title\" property=\"og:title\" />")]
    [InlineData("<META PROPERTY=\"OG:TITLE\" CONTENT=\"Title\">")]
    [InlineData("<meta property='og:title' content='Title'>")]
    [InlineData("<meta property=og:title content=Title>")]
    [InlineData("<meta\n    property=\"og:title\"\n    content=\"Title\"\n/>")]
    public async Task TitleIsExtractedRegardlessOfAttributeOrderCaseOrQuoting(string titleMarkup)
    {
        EmbeddedExternal? card = await GenerateCard(titleMarkup);

        Assert.NotNull(card);
        Assert.Equal("Title", card.External.Title);
    }

    [Theory]
    [InlineData("Caf&eacute; &amp; Bar", "Caf\u00e9 & Bar")]
    [InlineData("&lt;script&gt;", "<script>")]
    [InlineData("&#65;&#66;&#67;", "ABC")]
    public async Task HtmlEntitiesInMetadataAreDecoded(string encoded, string expected)
    {
        EmbeddedExternal? card = await GenerateCard($"<meta property=\"og:title\" content=\"{encoded}\" />");

        Assert.NotNull(card);
        Assert.Equal(expected, card.External.Title);
    }

    private static async Task<EmbeddedExternal?> GenerateCard(string titleMarkup)
    {
        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            if (context.Request.Path == PagePath)
            {
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync("<!DOCTYPE html><html><head><title>Fallback</title>");
                await context.Response.WriteAsync(titleMarkup);
                await context.Response.WriteAsync($"<meta property=\"og:url\" content=\"{TestServerBuilder.DefaultUri}document/12345\" />");
                await context.Response.WriteAsync("</head><body></body></html>");
            }
        });

        using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
        {
            agent.Credentials = new AccessCredentials(
                service: TestServerBuilder.DefaultUri,
                authenticationType: AuthenticationType.UsernamePassword,
                accessJwt: JwtBuilder.CreateJwt("did:plc:test", TestServerBuilder.DefaultUri.ToString()),
                refreshToken: "refreshToken");

            return await agent.CreateOpenGraphEmbeddedCardGenerator()
                .Generate(new Uri($"{TestServerBuilder.DefaultUri}document/12345"), TestContext.Current.CancellationToken);
        }
    }
}
