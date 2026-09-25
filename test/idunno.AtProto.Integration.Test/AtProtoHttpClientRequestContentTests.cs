// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

using Duende.IdentityModel.OidcClient.DPoP;

using idunno.AtProto.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class AtProtoHttpClientRequestContentTests
{
    /// <summary>
    /// An <see cref="HttpContent"/> which records how often it is serialized and disposed.
    /// </summary>
    private sealed class TrackingContent : HttpContent
    {
        private bool _disposed;

        public int DisposeCount { get; private set; }

        public int SerializeCount { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            SerializeCount++;

            byte[] payload = Encoding.UTF8.GetBytes("{}");

            return stream.WriteAsync(payload, 0, payload.Length);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 2;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            DisposeCount++;
            _disposed = true;

            base.Dispose(disposing);
        }
    }

    [Fact]
    public async Task CallerSuppliedContentIsNotDisposedByTheClient()
    {
        using TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"testValue":"test"}""");
        });

        using TrackingContent content = new();
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        AtProtoHttpClient<TestRecord> client = new();
        using HttpClient httpClient = testServer.CreateClient();

        AtProtoHttpResult<TestRecord> result = await client.Post(
            service: TestServerBuilder.DefaultUri,
            endpoint: "/xrpc/test.idunno.content",
            record: content,
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(1, content.SerializeCount);
        Assert.Equal(0, content.DisposeCount);
    }

    [Fact]
    public async Task ACancelledRequestThrowsRatherThanReportingSuccess()
    {
        using CancellationTokenSource cancellationTokenSource = new();

        using TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            await cancellationTokenSource.CancelAsync();
            await Task.Delay(Timeout.Infinite, context.RequestAborted);
        });

        AtProtoHttpClient<TestRecord> client = new();
        using HttpClient httpClient = testServer.CreateClient();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await client.Get(
            service: TestServerBuilder.DefaultUri,
            endpoint: "/xrpc/test.idunno.cancel",
            httpClient: httpClient,
            cancellationToken: cancellationTokenSource.Token));
    }

    [Fact]
    public async Task CallerSuppliedContentSurvivesADPoPNonceRetry()
    {
        int callCount = 0;

        using TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            HttpResponse response = context.Response;

            callCount++;

            if (callCount == 1)
            {
                response.StatusCode = 401;
                response.ContentType = "application/json";
                response.Headers.Append("DPoP-Nonce", "newNonce");
                await response.WriteAsync("""{"error":"use_dpop_nonce","message":"Nonce required"}""");
                return;
            }

            response.ContentType = "application/json";
            await response.WriteAsync("""{"testValue":"test"}""");
        });

        using TrackingContent content = new();
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        AtProtoCredential credential = AtProtoCredential.Create(
            TestServerBuilder.DefaultUri,
            authenticationType: AuthenticationType.OAuth,
            accessJwt: JwtBuilder.CreateJwt(new Did("did:plc:identifier")),
            refreshToken: "refreshToken",
            dPoPProofKey: JsonWebKeys.CreateRsaJson(),
            dPoPNonce: "nonce");

        AtProtoHttpClient<TestRecord> client = new();
        using HttpClient httpClient = testServer.CreateClient();

        AtProtoHttpResult<TestRecord> result = await client.Post(
            service: TestServerBuilder.DefaultUri,
            endpoint: "/xrpc/test.idunno.content",
            record: content,
            credentials: credential,
            httpClient: httpClient,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, callCount);
        Assert.True(result.Succeeded);

        // The retry reissues the request with the same content instance, so the content must not have been disposed
        // along with the first request message, and must still be able to produce a body for the second attempt.
        Assert.Equal(0, content.DisposeCount);
        Assert.Equal(1, content.SerializeCount);
    }
}
