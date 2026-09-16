// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class ResolutionHardeningTests
{
    private const string DidDocumentWithoutAPersonalDataServer = """
        {
            "@context": [
                "https://www.w3.org/ns/did/v1"
            ],
            "id": "did:plc:identifier",
            "service": [
                {
                    "id": "#atproto_labeler",
                    "type": "AtprotoLabeler",
                    "serviceEndpoint": "https://labeler.example.org"
                }
            ]
        }
        """;

    [Fact]
    public async Task ResolvePdsReturnsNullWhenTheDidDocumentHasNoPersonalDataServer()
    {
        Uri server = new("https://test.invalid");
        const string did = "did:plc:identifier";

        TestServer testServer = TestServerBuilder.CreateServer(server, async context =>
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (request.Host.Host == "plc.directory" && request.Path.Value == $"/{did}")
            {
                response.StatusCode = 200;
                response.ContentType = "application/json";
                await response.WriteAsync(DidDocumentWithoutAPersonalDataServer);
                return;
            }

            response.StatusCode = 404;
        });

        using (AtProtoAgent agent = new(server, new TestHttpClientFactory(testServer)))
        {
            Uri? pds = await agent.ResolvePds(new Did(did), TestContext.Current.CancellationToken);

            Assert.Null(pds);
        }
    }

    [Theory]
    [InlineData("did:plc:identifier")]
    [InlineData("did:plc:identifier\n")]
    [InlineData("did:plc:identifier\r\n")]
    [InlineData("  did:plc:identifier  ")]
    public async Task ResolveHandleTrimsTheWellKnownResponse(string wellKnownResponse)
    {
        Handle handle = new(TestServerBuilder.DefaultDomainName);

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(wellKnownResponse);
        });

        using (HttpClient httpClient = testServer.CreateClient())
        {
            Did? did = await AtProtoServer.ResolveHandle(handle, httpClient, cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(did);
            Assert.Equal("did:plc:identifier", did.Value);
        }
    }

    [Fact]
    public async Task ResolveHandleDoesNotReadAnOverLongWellKnownResponse()
    {
        Handle handle = new(TestServerBuilder.DefaultDomainName);

        // A response longer than the default maximum must be rejected rather than read into memory.
        string overLongResponse = "did:plc:" + new string('a', AtProtoServer.DefaultMaximumWellKnownResponseSize);

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(overLongResponse);
        });

        using (HttpClient httpClient = testServer.CreateClient())
        {
            Did? did = await AtProtoServer.ResolveHandle(handle, httpClient, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Null(did);
        }
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    public async Task ResolveDidDocumentForAHandleHonoursTheConfiguredWellKnownResponseSize(int maximumWellKnownResponseSize)
    {
        Handle handle = new(TestServerBuilder.DefaultDomainName);

        // Comfortably under the default well known maximum, but over the one the caller asks for.
        string response = "did:plc:" + new string('a', maximumWellKnownResponseSize * 2);

        using TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(response);
        });

        using HttpClient httpClient = testServer.CreateClient();

        await Assert.ThrowsAsync<ArgumentException>(async () => await Resolution.ResolveDidDocument(
            handle,
            httpClient: httpClient,
            maximumWellKnownResponseSize: maximumWellKnownResponseSize,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ResolveHandleDoesNotBufferAnOverLongWellKnownResponseIntoMemory()
    {
        Handle handle = new(TestServerBuilder.DefaultDomainName);

        const int chunkSize = 64 * 1024;
        const int maximumChunks = 1024;
        int bytesWritten = 0;

        byte[] chunk = new byte[chunkSize];
        Array.Fill(chunk, (byte)'a');

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";

            // Deliberately omit Content-Length so the size cannot be rejected up front, then stream far more
            // than the maximum. A client which reads the body to completion before inspecting it will drain all of this.
            for (int i = 0; i < maximumChunks && !context.RequestAborted.IsCancellationRequested; i++)
            {
                await context.Response.Body.WriteAsync(chunk, context.RequestAborted);
                Interlocked.Add(ref bytesWritten, chunkSize);
            }
        });

        using (HttpClient httpClient = testServer.CreateClient())
        {
            Did? did = await AtProtoServer.ResolveHandle(handle, httpClient, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Null(did);
        }

        // Only the bounded read should have been satisfied, so the server must have been stopped long before
        // it wrote everything it was prepared to write.
        Assert.True(
            Volatile.Read(ref bytesWritten) < chunkSize * maximumChunks,
            $"The whole response body was read into memory: the server wrote {Volatile.Read(ref bytesWritten)} bytes.");
    }

    [Fact]
    public async Task ResolveHandleDoesNotDisposeACallerSuppliedHttpClient()
    {
        Handle handle = new(TestServerBuilder.DefaultDomainName);

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("did:plc:identifier");
        });

        using (HttpClient httpClient = testServer.CreateClient())
        {
            Did? did = await Resolution.ResolveHandle(
                handle,
                httpClient: httpClient,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(did);

            // The caller owns the client it supplied, so it must still be usable afterwards.
            using (HttpResponseMessage response = await httpClient.GetAsync(
                new Uri(TestServerBuilder.DefaultUri, "/.well-known/atproto-did"),
                TestContext.Current.CancellationToken))
            {
                Assert.True(response.IsSuccessStatusCode);
            }
        }
    }

    [Fact]
    public async Task ResolveAuthorizationServerPreservesThePortOfThePds()
    {
        Uri pds = new("https://test.internal:8443");
        Uri? requestedUri = null;

        TestServer testServer = TestServerBuilder.CreateServer("*", async context =>
        {
            HttpRequest request = context.Request;

            requestedUri = new Uri($"{request.Scheme}://{request.Host}{request.Path}");

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{ "authorization_servers": [ "https://auth.example.org" ] }""");
        });

        using (AtProtoAgent agent = new(pds, new TestHttpClientFactory(testServer)))
        {
            Uri? authorizationServer = await agent.ResolveAuthorizationServer(pds, TestContext.Current.CancellationToken);

            Assert.Equal(new Uri("https://auth.example.org"), authorizationServer);
            Assert.NotNull(requestedUri);
            Assert.Equal(8443, requestedUri.Port);
        }
    }

    [Theory]
    [InlineData("""{ "authorization_servers": [ "http://auth.example.org" ] }""")]
    [InlineData("""{ "authorization_servers": [ "not a uri" ] }""")]
    [InlineData("""{ "authorization_servers": [ "" ] }""")]
    [InlineData("""{ "authorization_servers": [ 42 ] }""")]
    [InlineData("""{ "authorization_servers": [ ] }""")]
    [InlineData("""{ "authorization_servers": "https://auth.example.org" }""")]
    [InlineData("""{ }""")]
    public async Task ResolveAuthorizationServerReturnsNullForUnusableMetadata(string metadata)
    {
        Uri pds = TestServerBuilder.DefaultUri;

        TestServer testServer = TestServerBuilder.CreateServer(pds, async context =>
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(metadata);
        });

        using (AtProtoAgent agent = new(pds, new TestHttpClientFactory(testServer)))
        {
            Uri? authorizationServer = await agent.ResolveAuthorizationServer(pds, TestContext.Current.CancellationToken);

            Assert.Null(authorizationServer);
        }
    }

    [Fact]
    public async Task ResolveAuthorizationServerSkipsUnusableEntriesAndReturnsTheFirstSecureOne()
    {
        Uri pds = TestServerBuilder.DefaultUri;

        TestServer testServer = TestServerBuilder.CreateServer(pds, async context =>
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{ "authorization_servers": [ "", "http://insecure.example.org", "https://auth.example.org" ] }""");
        });

        using (AtProtoAgent agent = new(pds, new TestHttpClientFactory(testServer)))
        {
            Uri? authorizationServer = await agent.ResolveAuthorizationServer(pds, TestContext.Current.CancellationToken);

            Assert.Equal(new Uri("https://auth.example.org"), authorizationServer);
        }
    }

    [Fact]
    public void TheDefaultResolutionHttpClientIsProtectedAgainstServerSideRequestForgery()
    {
        using (HttpClientLease lease = new(httpClient: null, timeout: null))
        {
            Assert.NotNull(lease.Client);
        }

        object? handler = typeof(HttpClientLease)
            .GetField("s_httpMessageHandler", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(null);

        Assert.NotNull(handler);

        // A plain HttpClientHandler would let the static resolvers, which fetch from hosts chosen by whoever owns the
        // handle being resolved, reach loopback and private addresses. The SSRF protected handler vets the addresses a
        // request is about to connect to through a connect callback.
        Assert.IsNotType<HttpClientHandler>(handler);
        SocketsHttpHandler socketsHttpHandler = Assert.IsType<SocketsHttpHandler>(handler);
        Assert.NotNull(socketsHttpHandler.ConnectCallback);
    }

    [Fact]
    public void ALeaseDisposesOnlyTheHttpClientItCreated()
    {
        HttpClient callerSuppliedClient = new();

        using (HttpClientLease lease = new(callerSuppliedClient, timeout: null))
        {
            Assert.Same(callerSuppliedClient, lease.Client);
        }

        // Disposal of a caller supplied client is the caller's business, so the client must still be usable.
        callerSuppliedClient.Timeout = TimeSpan.FromSeconds(30);
        callerSuppliedClient.Dispose();

        HttpClient createdClient;

        using (HttpClientLease lease = new(httpClient: null, timeout: null))
        {
            createdClient = lease.Client;
        }

        Assert.Throws<ObjectDisposedException>(() => createdClient.Timeout = TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void ALeaseAppliesTheRequestedTimeoutToAClientItCreates()
    {
        TimeSpan timeout = TimeSpan.FromSeconds(17);

        using (HttpClientLease lease = new(httpClient: null, timeout: timeout))
        {
            Assert.Equal(timeout, lease.Client.Timeout);
        }
    }

    [Fact]
    public async Task ReadContentAsStringReturnsNullWhenTheContentLengthHeaderExceedsTheMaximum()
    {
        using (HttpContent content = new ByteArrayContent(Encoding.UTF8.GetBytes(new string('a', 64))))
        {
            Assert.Null(await InvokeReadContentAsString(content, 32));
        }
    }

    [Theory]
    [InlineData(31, false)]
    [InlineData(32, false)]
    [InlineData(33, true)]
    public async Task ReadContentAsStringStopsAtTheMaximumLengthWhenTheContentLengthIsNotKnown(int contentLength, bool expectedToBeRejected)
    {
        // A chunked response carries no content length, so the read itself has to be bounded.
        using (Stream contentStream = new MemoryStream(Encoding.UTF8.GetBytes(new string('a', contentLength))))
        using (HttpContent content = new StreamContent(contentStream))
        {
            string? result = await InvokeReadContentAsString(content, 32);

            if (expectedToBeRejected)
            {
                Assert.Null(result);
            }
            else
            {
                Assert.NotNull(result);
                Assert.Equal(contentLength, result.Length);
            }
        }
    }

    [Fact]
    public void TheWellKnownResponseIsCappedByDefaultAtFourKilobytes()
    {
        // The host a handle resolves through is chosen by whoever owns the handle, so the amount read from it is bounded
        // by a default rather than by whatever the host chooses to send.
        Assert.Equal(4096, AtProtoServer.DefaultMaximumWellKnownResponseSize);
        Assert.Equal(AtProtoServer.DefaultMaximumWellKnownResponseSize, new AtProtoAgentOptions().MaximumWellKnownResponseSize);
    }

    [Fact]
    public async Task ResolveHandleHonoursAConfiguredMaximumWellKnownResponseSize()
    {
        Handle handle = new(TestServerBuilder.DefaultDomainName);
        const string wellKnownResponse = "did:plc:identifier";

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(wellKnownResponse);
        });

        using (HttpClient httpClient = testServer.CreateClient())
        {
            Did? resolvedWithinTheLimit = await AtProtoServer.ResolveHandle(
                handle,
                httpClient,
                maximumWellKnownResponseSize: wellKnownResponse.Length,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(resolvedWithinTheLimit);

            Did? resolvedBeyondTheLimit = await AtProtoServer.ResolveHandle(
                handle,
                httpClient,
                maximumWellKnownResponseSize: wellKnownResponse.Length - 1,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Null(resolvedBeyondTheLimit);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AnInvalidMaximumWellKnownResponseSizeThrows(int maximumWellKnownResponseSize)
    {
        Handle handle = new(TestServerBuilder.DefaultDomainName);

        TestServer testServer = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, context =>
        {
            context.Response.StatusCode = 404;
            return Task.CompletedTask;
        });

        using (HttpClient httpClient = testServer.CreateClient())
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => AtProtoServer.ResolveHandle(
                    handle,
                    httpClient,
                    maximumWellKnownResponseSize: maximumWellKnownResponseSize,
                    cancellationToken: TestContext.Current.CancellationToken));
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AtProtoAgentOptions { MaximumWellKnownResponseSize = maximumWellKnownResponseSize });
    }

    private static async Task<string?> InvokeReadContentAsString(HttpContent content, int maximumLength)
    {
        System.Reflection.MethodInfo method = typeof(AtProtoServer).Assembly
            .GetType("idunno.AtProto.HttpContentReader", throwOnError: true)!
            .GetMethod(
                "ReadAsString",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)!;

        return await (Task<string?>)method.Invoke(null, [content, maximumLength, TestContext.Current.CancellationToken])!;
    }
}
