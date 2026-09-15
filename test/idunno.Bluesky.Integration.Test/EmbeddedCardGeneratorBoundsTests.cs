// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Embed;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class EmbeddedCardGeneratorBoundsTests
{
    private const int ChunkSize = 64 * 1024;
    private const int MaximumChunks = 512;

    [Fact]
    public async Task CardGenerationDoesNotBufferAnOverLongPageIntoMemory()
    {
        int bytesWritten = 0;

        TestServer testServer = CreateServer(
            pageHandler: async response =>
            {
                response.ContentType = "text/html";

                // A well formed head, then an endless body. Everything the card needs has already been sent.
                await response.WriteAsync("<!DOCTYPE html><html><head><title>Test</title>");
                await response.WriteAsync("<meta property=\"og:title\" content=\"Test\" />");
                await response.WriteAsync($"<meta property=\"og:url\" content=\"{TestServerBuilder.DefaultUri}document/12345\" />");
                await response.WriteAsync("</head><body>");

                await WriteUntilAborted(response, () => Interlocked.Add(ref bytesWritten, ChunkSize));
            },
            imageHandler: null);

        EmbeddedExternal? card = await GenerateCard(testServer);

        Assert.NotNull(card);
        Assert.Equal("Test", card.External.Title);

        Assert.True(
            Volatile.Read(ref bytesWritten) < ChunkSize * MaximumChunks,
            $"The whole page was read into memory: the server wrote {Volatile.Read(ref bytesWritten)} bytes of body.");
    }

    [Fact]
    public async Task CardGenerationDoesNotBufferAnOverLongThumbnailIntoMemory()
    {
        int bytesWritten = 0;

        TestServer testServer = CreateServer(
            pageHandler: async response =>
            {
                response.ContentType = "text/html";
                await response.WriteAsync("<!DOCTYPE html><html><head><title>Test</title>");
                await response.WriteAsync("<meta property=\"og:title\" content=\"Test\" />");
                await response.WriteAsync($"<meta property=\"og:url\" content=\"{TestServerBuilder.DefaultUri}document/12345\" />");
                await response.WriteAsync($"<meta property=\"og:image\" content=\"{TestServerBuilder.DefaultUri}document/12345/image.png\" />");
                await response.WriteAsync("</head><body></body></html>");
            },
            imageHandler: async response =>
            {
                response.ContentType = "image/png";

                // A valid PNG signature so the type sniff passes, then an endless body. Content-Length is deliberately
                // omitted so the size cannot be rejected up front.
                await response.Body.WriteAsync(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A });
                Interlocked.Add(ref bytesWritten, 6);

                await WriteUntilAborted(response, () => Interlocked.Add(ref bytesWritten, ChunkSize));
            });

        EmbeddedExternal? card = await GenerateCard(testServer);

        Assert.NotNull(card);

        // The default maxDownloadSize is 2,000,000 bytes, so the download must have been abandoned well short of everything
        // the server was prepared to send.
        Assert.True(
            Volatile.Read(ref bytesWritten) < ChunkSize * MaximumChunks,
            $"The whole image was read into memory: the server wrote {Volatile.Read(ref bytesWritten)} bytes.");
    }

    private static async Task WriteUntilAborted(HttpResponse response, Action onChunkWritten)
    {
        byte[] chunk = new byte[ChunkSize];
        Array.Fill(chunk, (byte)'a');

        for (int i = 0; i < MaximumChunks && !response.HttpContext.RequestAborted.IsCancellationRequested; i++)
        {
            await response.Body.WriteAsync(chunk, response.HttpContext.RequestAborted);
            onChunkWritten();
        }
    }

    private static TestServer CreateServer(Func<HttpResponse, Task> pageHandler, Func<HttpResponse, Task>? imageHandler)
    {
        return TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Path == "/document/12345")
            {
                await pageHandler(response);
                return;
            }

            if (request.Path == "/document/12345/image.png" && imageHandler is not null)
            {
                await imageHandler(response);
                return;
            }

            if (request.Path == "/xrpc/com.atproto.repo.uploadBlob")
            {
                response.ContentType = "application/json";
                await response.WriteAsync("{\"blob\":{\"$type\":\"blob\",\"ref\":{\"$link\":\"bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4\"},\"mimeType\":\"image/png\",\"size\":999}}");
                return;
            }
        });
    }

    private static async Task<EmbeddedExternal?> GenerateCard(TestServer testServer)
    {
        AccessCredentials credentials = new(
            service: TestServerBuilder.DefaultUri,
            authenticationType: AuthenticationType.UsernamePassword,
            accessJwt: JwtBuilder.CreateJwt("did:plc:test", TestServerBuilder.DefaultUri.ToString()),
            refreshToken: "refreshToken");

        using (var agent = new BlueskyAgent(new TestHttpClientFactory(testServer)))
        {
            agent.Credentials = credentials;

            return await agent.CreateOpenGraphEmbeddedCardGenerator()
                .Generate(new Uri($"{TestServerBuilder.DefaultUri}document/12345"), TestContext.Current.CancellationToken);
        }
    }
}
