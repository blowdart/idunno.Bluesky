// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;

using idunno.AtProto.Authentication;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class TokenResponseCapturingHandlerTests
{
    [Fact]
    public async Task ScopeOfASuccessfulTokenResponseIsCapturedAndTheBodyRemainsReadable()
    {
        const string body = """{"access_token":"token","scope":"atproto transition:generic"}""";
        TokenResponseCapture capture = new();
        using HttpMessageInvoker invoker = new(new TokenResponseCapturingHandler(capture, new StubHandler(() => JsonResponse(new StringContent(body, null, "application/json")))));

        using HttpResponseMessage response = await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "https://authority.test/token"),
            TestContext.Current.CancellationToken);

        Assert.Equal("atproto transition:generic", capture.Scope);
        Assert.Equal(body, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ResponseIsDisposedWhenReadingTheBodyFails()
    {
        ThrowingContent content = new();
        using HttpResponseMessage inner = JsonResponse(content);
        using HttpMessageInvoker invoker = new(new TokenResponseCapturingHandler(new TokenResponseCapture(), new StubHandler(() => inner)));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => invoker.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, "https://authority.test/token"),
                TestContext.Current.CancellationToken));

        Assert.True(content.Disposed);
    }

    private static HttpResponseMessage JsonResponse(HttpContent content)
    {
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    /// <summary>
    /// Returns a canned response from a delegating handler's inner handler.
    /// </summary>
    private sealed class StubHandler(Func<HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory());
    }

    /// <summary>
    /// Content whose body cannot be read, recording whether it was disposed.
    /// </summary>
    private sealed class ThrowingContent : HttpContent
    {
        public bool Disposed { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new IOException("The body could not be read.");

        protected override bool TryComputeLength(out long length)
        {
            length = -1;

            return false;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
