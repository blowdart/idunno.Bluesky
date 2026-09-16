// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.Bluesky.Embed;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class EmbeddedCardGeneratorHardeningTests
{
    private const string PagePath = "/document/12345";
    private const string ImagePath = "/document/12345/image.png";

    private static readonly byte[] s_pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ADeclaredImageTypeCannotStopTheImageContentBeingSniffed()
    {
        UploadRecord upload = new();

        TestServer testServer = CreateServer(
            upload,
            headMarkup: $"<meta property=\"og:title\" content=\"Test\" />" +
                        $"<meta property=\"og:url\" content=\"{TestServerBuilder.DefaultUri}document/12345\" />" +
                        $"<meta property=\"og:image\" content=\"{TestServerBuilder.DefaultUri}document/12345/image.png\" />" +
                        $"<meta property=\"og:image:type\" content=\"image/png\" />",
            imageHandler: async response =>
            {
                response.ContentType = "image/png";
                await response.WriteAsync("<script>alert(1)</script>");
            });

        EmbeddedExternal? card = await GenerateCard(testServer);

        Assert.NotNull(card);
        Assert.Null(card.External.Thumbnail);
        Assert.Equal(0, upload.Count);
    }

    [Fact]
    public async Task AnImageWhoseDeclaredTypeDisagreesWithItsContentIsUploadedAsItsActualType()
    {
        UploadRecord upload = new();

        TestServer testServer = CreateServer(
            upload,
            headMarkup: $"<meta property=\"og:title\" content=\"Test\" />" +
                        $"<meta property=\"og:url\" content=\"{TestServerBuilder.DefaultUri}document/12345\" />" +
                        $"<meta property=\"og:image\" content=\"{TestServerBuilder.DefaultUri}document/12345/image.png\" />" +
                        $"<meta property=\"og:image:type\" content=\"text/html; charset=utf-8\" />",
            imageHandler: async response =>
            {
                response.ContentType = "text/html";
                await response.Body.WriteAsync(s_pngHeader);
            });

        EmbeddedExternal? card = await GenerateCard(testServer);

        Assert.NotNull(card);
        Assert.NotNull(card.External.Thumbnail);
        Assert.Equal(1, upload.Count);
        Assert.Equal("image/png", upload.ContentType);
    }

    [Theory]
    [InlineData("javascript:alert(document.cookie)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==")]
    [InlineData("file:///etc/passwd")]
    public async Task ACanonicalUrlWhoseSchemeIsNotHttpDoesNotProduceACard(string canonicalUrl)
    {
        TestServer testServer = CreateServer(
            new UploadRecord(),
            headMarkup: $"<meta property=\"og:title\" content=\"Test\" />" +
                        $"<meta property=\"og:url\" content=\"{canonicalUrl}\" />",
            imageHandler: null);

        Assert.Null(await GenerateCard(testServer));
    }

    [Fact]
    public async Task AnImageUriWhoseSchemeIsNotHttpIsNotDownloaded()
    {
        UploadRecord upload = new();

        TestServer testServer = CreateServer(
            upload,
            headMarkup: $"<meta property=\"og:title\" content=\"Test\" />" +
                        $"<meta property=\"og:url\" content=\"{TestServerBuilder.DefaultUri}document/12345\" />" +
                        $"<meta property=\"og:image\" content=\"ftp://example.org/image.png\" />",
            imageHandler: null);

        EmbeddedExternal? card = await GenerateCard(testServer);

        Assert.NotNull(card);
        Assert.Null(card.External.Thumbnail);
        Assert.Equal(0, upload.Count);
    }

    [Fact]
    public async Task APageIsDecodedUsingTheCharacterSetItDeclares()
    {
        // 0xE9 is é in ISO-8859-1, and is not valid UTF-8 on its own.
        byte[] page = Encoding.Latin1.GetBytes(
            "<!DOCTYPE html><html><head><title>Test</title>" +
            "<meta property=\"og:title\" content=\"Caf\u00e9\" />" +
            $"<meta property=\"og:url\" content=\"{TestServerBuilder.DefaultUri}document/12345\" />" +
            "</head><body></body></html>");

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            if (context.Request.Path == PagePath)
            {
                context.Response.ContentType = "text/html; charset=iso-8859-1";
                await context.Response.Body.WriteAsync(page);
            }
        });

        EmbeddedExternal? card = await GenerateCard(testServer);

        Assert.NotNull(card);
        Assert.Equal("Caf\u00e9", card.External.Title);
    }

    [Fact]
    public async Task ADisposedGeneratorWillNotMakeRequests()
    {
        TestServer testServer = CreateServer(
            new UploadRecord(),
            headMarkup: "<meta property=\"og:title\" content=\"Test\" />",
            imageHandler: null);

        using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
        {
            agent.Credentials = CreateCredentials();

            OpenGraphEmbeddedCardGenerator generator = agent.CreateOpenGraphEmbeddedCardGenerator();
            generator.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => generator.Generate(new Uri($"{TestServerBuilder.DefaultUri}document/12345"), TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task WellKnownPublicationResolutionDoesNotReadAWholePageOfResponse()
    {
        const int chunkSize = 4096;
        const int maximumChunks = 512;

        Did did = "did:plc:test";
        Cid cid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";
        RecordKey documentRecordKey = new("3mn4upg7a4z2h");
        AtUri documentUri = new($"at://{did}/site.standard.document/{documentRecordKey}");

        int bytesWritten = 0;

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Host.Host == "plc.directory")
            {
                if (request.Path == $"/{did}")
                {
                    DidDocument didDocument = new(
                        id: $"{did}",
                        context: ["https://www.w3.org/ns/did/v1"],
                        alsoKnownAs: null,
                        verificationMethods: null,
                        services: [new(id: "#atproto_pds", type: "atprotopds", serviceEndpoint: TestServerBuilder.DefaultUri)]);
                    await response.WriteAsJsonAsync(didDocument, _jsonSerializerOptions);
                }

                return;
            }

            if (request.Path == PagePath)
            {
                response.ContentType = "text/html";
                await response.WriteAsync("<!DOCTYPE html><html><head><title>Test</title>");
                await response.WriteAsync("<meta property=\"og:title\" content=\"Test\" />");
                await response.WriteAsync($"<meta property=\"og:url\" content=\"{TestServerBuilder.DefaultUri}document/12345\" />");
                await response.WriteAsync($"<link rel=\"site.standard.document\" href=\"{documentUri}\" />");
                await response.WriteAsync("</head><body></body></html>");
                return;
            }

            if (request.Path == "/xrpc/com.atproto.repo.getRecord")
            {
                response.ContentType = "application/json";
                await response.WriteAsync($"{{\"uri\":\"{documentUri}\",\"cid\":\"{cid}\",\"value\":{{\"test\":true}}}}");
                return;
            }

            if (request.Path == "/.well-known/site.standard.publication")
            {
                // An endless response where a single short AT URI belongs.
                byte[] chunk = new byte[chunkSize];
                Array.Fill(chunk, (byte)'a');

                for (int i = 0; i < maximumChunks && !request.HttpContext.RequestAborted.IsCancellationRequested; i++)
                {
                    await response.Body.WriteAsync(chunk, request.HttpContext.RequestAborted);
                    Interlocked.Add(ref bytesWritten, chunkSize);
                }
            }
        });

        using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
        {
            agent.Credentials = CreateCredentials();

            await agent.CreateStandardSiteEmbeddedCardGenerator()
                .Generate(new Uri($"{TestServerBuilder.DefaultUri}document/12345"), TestContext.Current.CancellationToken);
        }

        // The response is abandoned as soon as the budget is reached, but the server may already have written more into
        // its buffers, so this is a bound well clear of the 1MB page budget rather than of the 4KB well known one.
        Assert.True(
            Volatile.Read(ref bytesWritten) < chunkSize * 128,
            $"The well known document was read with a page sized budget: the server wrote {Volatile.Read(ref bytesWritten)} bytes.");
    }

    private sealed class UploadRecord
    {
        public int Count { get; set; }

        public string? ContentType { get; set; }
    }

    private static AccessCredentials CreateCredentials()
    {
        return new AccessCredentials(
            service: TestServerBuilder.DefaultUri,
            authenticationType: AuthenticationType.UsernamePassword,
            accessJwt: JwtBuilder.CreateJwt("did:plc:test", TestServerBuilder.DefaultUri.ToString()),
            refreshToken: "refreshToken");
    }

    private static TestServer CreateServer(UploadRecord upload, string headMarkup, Func<HttpResponse, Task>? imageHandler)
    {
        return TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Path == PagePath)
            {
                response.ContentType = "text/html";
                await response.WriteAsync($"<!DOCTYPE html><html><head><title>Test</title>{headMarkup}</head><body></body></html>");
                return;
            }

            if (request.Path == ImagePath && imageHandler is not null)
            {
                await imageHandler(response);
                return;
            }

            if (request.Path == "/xrpc/com.atproto.repo.uploadBlob")
            {
                upload.Count++;
                upload.ContentType = request.ContentType;

                response.ContentType = "application/json";
                await response.WriteAsync("{\"blob\":{\"$type\":\"blob\",\"ref\":{\"$link\":\"bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4\"},\"mimeType\":\"image/png\",\"size\":999}}");
                return;
            }
        });
    }

    private static async Task<EmbeddedExternal?> GenerateCard(TestServer testServer)
    {
        using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
        {
            agent.Credentials = CreateCredentials();

            return await agent.CreateOpenGraphEmbeddedCardGenerator()
                .Generate(new Uri($"{TestServerBuilder.DefaultUri}document/12345"), TestContext.Current.CancellationToken);
        }
    }
}
