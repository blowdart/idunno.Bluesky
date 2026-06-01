// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Embed;

namespace idunno.Bluesky.Integration.Test;

public class OpenGraphEmbeddedCardGeneratorTests
{
    [Fact]
    public async Task OpenGraphFullMetadataCreatesCorrectCard()
    {
        Did expectedDid = "did:plc:test";

        Cid expectedBlobCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

        const string expectedCardUrl = "https://example.com/test-page";
        const string expectedCardTitle = "Test Document";
        const string expectedCardDescription = "This is a test document for Open Graph metadata.";

        AccessCredentials expectedCredentials = new(
                service: TestServerBuilder.DefaultUri,
                authenticationType: AuthenticationType.UsernamePassword,
                accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                refreshToken: "refreshToken");

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Host.Host == TestServerBuilder.DefaultUri.Host)
            {
                if (request.Path == "/document/12345")
                {
                    await response.WriteAsync(@"<!DOCTYPE html>");
                    await response.WriteAsync(@"<html>");
                    await response.WriteAsync(@"<head>");
                    await response.WriteAsync($@"<title>{expectedCardTitle}</title>");
                    await response.WriteAsync($@"<meta property=""og:title"" content=""{expectedCardTitle}"" />");
                    await response.WriteAsync($@"<meta property=""og:description"" content=""{expectedCardDescription}"" />");
                    await response.WriteAsync($@"<meta property=""og:url"" content=""{expectedCardUrl}"" />");
                    await response.WriteAsync($@"<meta property=""og:image"" content=""{TestServerBuilder.DefaultUri}document/12345/image.png"" />");
                    await response.WriteAsync(@"</head>");
                    await response.WriteAsync(@"<body>");
                    await response.WriteAsync($@"<h1>{expectedCardTitle}</h1>");
                    await response.WriteAsync(@"</body>");
                    await response.WriteAsync(@"</html>");
                    return;
                }

                if (request.Path == "/document/12345/image.png")
                {
                    await response.SendFileAsync("image.png");
                    return;
                }

                if (request.Path == "/xrpc/com.atproto.repo.uploadBlob")
                {
                    string json = "{\"blob\":{\"$type\":\"blob\",\"ref\":{\"$link\":\"bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4\"},\"mimeType\":\"image/png\",\"size\":999}}";

                    response.ContentType = "application/json";
                    await response.WriteAsync(json);
                    return;
                }
            }
        });

        using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
        {
            agent.Credentials = expectedCredentials;

            Uri uri = new ($"{TestServerBuilder.DefaultUri}document/12345");
            OpenGraphEmbeddedCardGenerator openGraphCardGenerator = agent.CreateOpenGraphEmbeddedCardGenerator();

            EmbeddedExternal? card = await openGraphCardGenerator.Generate(uri, TestContext.Current.CancellationToken);

            Assert.NotNull(card);

            Assert.Equal(expectedCardUrl, card.External.Uri.ToString());
            Assert.Equal(expectedCardTitle, card.External.Title);
            Assert.Equal(expectedCardDescription, card.External.Description);
            Assert.Equal(expectedBlobCid, card.External.Thumbnail!.Reference!.Link);
            Assert.Equal("image/png", card.External.Thumbnail.MimeType);
            Assert.Equal(999, card.External.Thumbnail.Size);
        }
    }

    [Fact]
    public async Task OpenGraphMetadataWithNoDescriptionCreatesCorrectCard()
    {
        Did expectedDid = "did:plc:test";

        Cid expectedBlobCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

        const string expectedCardUrl = "https://example.com/test-page";
        const string expectedCardTitle = "Test Document";

        AccessCredentials expectedCredentials = new(
                service: TestServerBuilder.DefaultUri,
                authenticationType: AuthenticationType.UsernamePassword,
                accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                refreshToken: "refreshToken");

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Host.Host == TestServerBuilder.DefaultUri.Host)
            {
                if (request.Path == "/document/12345")
                {
                    await response.WriteAsync(@"<!DOCTYPE html>");
                    await response.WriteAsync(@"<html>");
                    await response.WriteAsync(@"<head>");
                    await response.WriteAsync($@"<title>{expectedCardTitle}</title>");
                    await response.WriteAsync($@"<meta property=""og:title"" content=""{expectedCardTitle}"" />");
                    await response.WriteAsync($@"<meta property=""og:url"" content=""{expectedCardUrl}"" />");
                    await response.WriteAsync($@"<meta property=""og:image"" content=""{TestServerBuilder.DefaultUri}document/12345/image.png"" />");
                    await response.WriteAsync(@"</head>");
                    await response.WriteAsync(@"<body>");
                    await response.WriteAsync($@"<h1>{expectedCardTitle}</h1>");
                    await response.WriteAsync(@"</body>");
                    await response.WriteAsync(@"</html>");
                    return;
                }

                if (request.Path == "/document/12345/image.png")
                {
                    await response.SendFileAsync("image.png");
                    return;
                }

                if (request.Path == "/xrpc/com.atproto.repo.uploadBlob")
                {
                    string json = "{\"blob\":{\"$type\":\"blob\",\"ref\":{\"$link\":\"bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4\"},\"mimeType\":\"image/png\",\"size\":999}}";

                    response.ContentType = "application/json";
                    await response.WriteAsync(json);
                    return;
                }
            }
        });

        using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
        {
            agent.Credentials = expectedCredentials;

            Uri uri = new($"{TestServerBuilder.DefaultUri}document/12345");
            OpenGraphEmbeddedCardGenerator openGraphCardGenerator = agent.CreateOpenGraphEmbeddedCardGenerator();

            EmbeddedExternal? card = await openGraphCardGenerator.Generate(uri, TestContext.Current.CancellationToken);

            Assert.NotNull(card);

            Assert.Equal(expectedCardUrl, card.External.Uri.ToString());
            Assert.Equal(expectedCardTitle, card.External.Title);
            Assert.Equal(string.Empty, card.External.Description);
            Assert.Equal(expectedBlobCid, card.External.Thumbnail!.Reference!.Link);
            Assert.Equal("image/png", card.External.Thumbnail.MimeType);
            Assert.Equal(999, card.External.Thumbnail.Size);
        }
    }

    [Fact]
    public async Task OpenGraphNoImageCreatesCorrectCard()
    {
        Did expectedDid = "did:plc:test";

        const string expectedCardUrl = "https://example.com/test-page";
        const string expectedCardTitle = "Test Document";
        const string expectedCardDescription = "This is a test document for Open Graph metadata.";

        AccessCredentials expectedCredentials = new(
                service: TestServerBuilder.DefaultUri,
                authenticationType: AuthenticationType.UsernamePassword,
                accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                refreshToken: "refreshToken");

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Host.Host == TestServerBuilder.DefaultUri.Host)
            {
                if (request.Path == "/document/12345")
                {
                    await response.WriteAsync(@"<!DOCTYPE html>");
                    await response.WriteAsync(@"<html>");
                    await response.WriteAsync(@"<head>");
                    await response.WriteAsync($@"<title>{expectedCardTitle}</title>");
                    await response.WriteAsync($@"<meta property=""og:title"" content=""{expectedCardTitle}"" />");
                    await response.WriteAsync($@"<meta property=""og:description"" content=""{expectedCardDescription}"" />");
                    await response.WriteAsync($@"<meta property=""og:url"" content=""{expectedCardUrl}"" />");
                    await response.WriteAsync(@"</head>");
                    await response.WriteAsync(@"<body>");
                    await response.WriteAsync($@"<h1>{expectedCardTitle}</h1>");
                    await response.WriteAsync(@"</body>");
                    await response.WriteAsync(@"</html>");
                    return;
                }
            }
        });

        using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
        {
            agent.Credentials = expectedCredentials;

            Uri uri = new($"{TestServerBuilder.DefaultUri}document/12345");
            OpenGraphEmbeddedCardGenerator openGraphCardGenerator = agent.CreateOpenGraphEmbeddedCardGenerator();

            EmbeddedExternal? card = await openGraphCardGenerator.Generate(uri, TestContext.Current.CancellationToken);

            Assert.NotNull(card);

            Assert.Equal(expectedCardUrl, card.External.Uri.ToString());
            Assert.Equal(expectedCardTitle, card.External.Title);
            Assert.Equal(expectedCardDescription, card.External.Description);
            Assert.Null(card.External.Thumbnail);
        }
    }

    [Fact]
    public async Task OpenGraphFullMetadataWithDuplicatesPicksTheFirstFromDuplicatesAndCreatesCorrectCard()
    {
        Did expectedDid = "did:plc:test";

        Cid expectedBlobCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

        const string expectedCardUrl = "https://example.com/test-page";
        const string expectedCardTitle = "Test Document";
        const string expectedCardDescription = "This is a test document for Open Graph metadata.";

        AccessCredentials expectedCredentials = new(
                service: TestServerBuilder.DefaultUri,
                authenticationType: AuthenticationType.UsernamePassword,
                accessJwt: JwtBuilder.CreateJwt(expectedDid, TestServerBuilder.DefaultUri.ToString()),
                refreshToken: "refreshToken");

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Host.Host == TestServerBuilder.DefaultUri.Host)
            {
                if (request.Path == "/document/12345")
                {
                    await response.WriteAsync(@"<!DOCTYPE html>");
                    await response.WriteAsync(@"<html>");
                    await response.WriteAsync(@"<head>");
                    await response.WriteAsync($@"<title>{expectedCardTitle}</title>");
                    await response.WriteAsync($@"<meta property=""og:title"" content=""{expectedCardTitle}"" />");
                    await response.WriteAsync($@"<meta property=""og:title"" content=""WRONG"" />");
                    await response.WriteAsync($@"<meta property=""og:description"" content=""{expectedCardDescription}"" />");
                    await response.WriteAsync($@"<meta property=""og:description"" content=""WRONG"" />");
                    await response.WriteAsync($@"<meta property=""og:url"" content=""{expectedCardUrl}"" />");
                    await response.WriteAsync($@"<meta property=""og:url"" content=""WRONG"" />");
                    await response.WriteAsync($@"<meta property=""og:image"" content=""{TestServerBuilder.DefaultUri}document/12345/image.png"" />");
                    await response.WriteAsync(@"</head>");
                    await response.WriteAsync(@"<body>");
                    await response.WriteAsync($@"<h1>{expectedCardTitle}</h1>");
                    await response.WriteAsync(@"</body>");
                    await response.WriteAsync(@"</html>");
                    return;
                }

                if (request.Path == "/document/12345/image.png")
                {
                    await response.SendFileAsync("image.png");
                    return;
                }

                if (request.Path == "/xrpc/com.atproto.repo.uploadBlob")
                {
                    string json = "{\"blob\":{\"$type\":\"blob\",\"ref\":{\"$link\":\"bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4\"},\"mimeType\":\"image/png\",\"size\":999}}";

                    response.ContentType = "application/json";
                    await response.WriteAsync(json);
                    return;
                }
            }
        });

        using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
        {
            agent.Credentials = expectedCredentials;

            Uri uri = new($"{TestServerBuilder.DefaultUri}document/12345");
            OpenGraphEmbeddedCardGenerator openGraphCardGenerator = agent.CreateOpenGraphEmbeddedCardGenerator();

            EmbeddedExternal? card = await openGraphCardGenerator.Generate(uri, TestContext.Current.CancellationToken);

            Assert.NotNull(card);

            Assert.Equal(expectedCardUrl, card.External.Uri.ToString());
            Assert.Equal(expectedCardTitle, card.External.Title);
            Assert.Equal(expectedCardDescription, card.External.Description);
            Assert.Equal(expectedBlobCid, card.External.Thumbnail!.Reference!.Link);
            Assert.Equal("image/png", card.External.Thumbnail.MimeType);
            Assert.Equal(999, card.External.Thumbnail.Size);
        }
    }
}
