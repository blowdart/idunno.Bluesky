// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class AtProtoHttpClientRequestHeaderTests
{
    private const string Endpoint = "/xrpc/com.atproto.server.describeServer";

    private static readonly Uri s_service = new("https://service.test/");

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<KeyValuePair<string, IEnumerable<string>>> Headers { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            Headers.AddRange(request.Headers);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok", Encoding.UTF8, MediaTypeNames.Application.Json)
            });
        }
    }

    private static async Task<CapturingHandler> Send(
        AtProtoHttpClient<string> client,
        ICollection<NameValueHeaderValue>? requestHeaders)
    {
        CapturingHandler handler = new();

        using HttpClient httpClient = new(handler);

        await client.Get(
            service: s_service,
            endpoint: Endpoint,
            credentials: null,
            httpClient: httpClient,
            jsonSerializerOptions: AtProtoServer.AtProtoJsonSerializerOptions,
            requestHeaders: requestHeaders,
            cancellationToken: TestContext.Current.CancellationToken);

        return handler;
    }

    private static string[] ValuesOf(CapturingHandler handler, string name) =>
        [.. handler.Headers
            .Where(h => h.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            .SelectMany(h => h.Value)];

    [Fact]
    public async Task HeadersSuppliedForASingleCallAreSentWithTheRequest()
    {
        CapturingHandler handler = await Send(new AtProtoHttpClient<string>(), [new NameValueHeaderValue("x-per-call", "value")]);

        Assert.Equal(["value"], ValuesOf(handler, "x-per-call"));
    }

    [Fact]
    public async Task HeadersConfiguredOnTheClientAreStillSentAlongsideThoseSuppliedForTheCall()
    {
        CapturingHandler handler = await Send(
            new AtProtoHttpClient<string>(serviceProxy: "did:web:configured#bsky_appview"),
            [new NameValueHeaderValue("x-per-call", "value")]);

        Assert.Equal(["value"], ValuesOf(handler, "x-per-call"));
        Assert.Equal(["did:web:configured#bsky_appview"], ValuesOf(handler, "atproto-proxy"));
    }

    [Fact]
    public async Task HeadersConfiguredOnTheClientAreSentWhenTheCallSuppliesNone()
    {
        CapturingHandler handler = await Send(new AtProtoHttpClient<string>(serviceProxy: "did:web:configured#bsky_appview"), null);

        Assert.Equal(["did:web:configured#bsky_appview"], ValuesOf(handler, "atproto-proxy"));
    }

    [Fact]
    public async Task AHeaderSuppliedForASingleCallReplacesTheOneConfiguredOnTheClientRatherThanBeingSentTwice()
    {
        CapturingHandler handler = await Send(
            new AtProtoHttpClient<string>(serviceProxy: "did:web:configured#bsky_appview"),
            [new NameValueHeaderValue("atproto-proxy", "did:web:percall#bsky_appview")]);

        Assert.Equal(["did:web:percall#bsky_appview"], ValuesOf(handler, "atproto-proxy"));
    }

    [Fact]
    public async Task TheHeaderCollectionSuppliedByACallerIsNotModified()
    {
        AtProtoHttpClient<string> client = new(serviceProxy: "did:web:configured#bsky_appview");

        ICollection<NameValueHeaderValue> callerHeaders = [new NameValueHeaderValue("x-per-call", "value")];

        await Send(client, callerHeaders);
        await Send(client, callerHeaders);

        NameValueHeaderValue only = Assert.Single(callerHeaders);
        Assert.Equal("x-per-call", only.Name);
    }

    [Fact]
    public async Task ASecondRequestOnTheSameClientDoesNotFailBecauseTheFirstDisposedTheSharedHandler()
    {
        AtProtoHttpClient client = new();

        // There is nothing listening, so both calls are expected to fail at the transport. The point of the test is that
        // the second failure is a transport failure and not an ObjectDisposedException from a handler the first call
        // disposed out from underneath it.
        Uri unroutable = new("https://127.0.0.1:1/");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.Get(unroutable, Endpoint, null, null, cancellationToken: TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.Get(unroutable, Endpoint, null, null, cancellationToken: TestContext.Current.CancellationToken));
    }
}
