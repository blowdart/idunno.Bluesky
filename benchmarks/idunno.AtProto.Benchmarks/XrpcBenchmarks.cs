// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

using idunno.AtProto.Authentication;
using idunno.Bluesky;
using idunno.Bluesky.Feed.Model;

namespace idunno.AtProto.Benchmarks;

/// <summary>
/// Measures the <see cref="AtProtoHttpClient{TResult}"/> request and response path, replaying captured AppView responses
/// through an in memory handler so nothing touches the network.
/// </summary>
/// <remarks>
/// <para>The <c>EndToEnd</c> category is a whole call through the public API. The other categories isolate a single step of
/// that call, comparing what the client does today, the baseline, with a proposed alternative.</para>
/// </remarks>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class XrpcBenchmarks
{
    private const string AuthorFeedEndpoint = "/xrpc/app.bsky.feed.getAuthorFeed?actor=did%3Aplc%3Az72i7hdynmk6r22z27h6tvur&limit=50";

    private static readonly Uri s_service = new("https://api.bsky.app");
    private static readonly Did s_actor = new("did:plc:z72i7hdynmk6r22z27h6tvur");
    private static readonly Did[] s_profiles = [s_actor];

    private byte[] _authorFeed = null!;
    private byte[] _timeline = null!;
    private HttpClient _authorFeedClient = null!;
    private HttpClient _profilesClient = null!;
    private HttpClient _timelineClient = null!;
    private AccessCredentials _credentials = null!;
    private JsonTypeInfo<GetAuthorFeedResponse> _authorFeedTypeInfo = null!;
    private JsonTypeInfo<GetTimelineResponse> _timelineTypeInfo = null!;
    private JsonTypeInfo<Post> _postTypeInfo = null!;
    private Post _post = null!;

    [GlobalSetup]
    public void Setup()
    {
        _authorFeed = CorpusFile.Read(CorpusFile.XrpcGetAuthorFeed)[0];
        _timeline = CorpusFile.Read(CorpusFile.XrpcGetTimeline)[0];
        _authorFeedClient = new HttpClient(new ReplayHandler(_authorFeed));
        _profilesClient = new HttpClient(new ReplayHandler(CorpusFile.Read(CorpusFile.XrpcGetProfiles)[0]));
        _timelineClient = new HttpClient(new ReplayHandler(_timeline));

        // The replay handler ignores authorization, so an unsigned token which never expires is enough to authenticate the request.
        _credentials = new AccessCredentials(s_service, AuthenticationType.UsernamePassword, UnsignedJwt(CaptureScrubber.PlaceholderDid), "refresh");

        _authorFeedTypeInfo = (JsonTypeInfo<GetAuthorFeedResponse>)BlueskyJsonSerializerOptions.Options.GetTypeInfo(typeof(GetAuthorFeedResponse));
        _timelineTypeInfo = (JsonTypeInfo<GetTimelineResponse>)BlueskyJsonSerializerOptions.Options.GetTypeInfo(typeof(GetTimelineResponse));
        _postTypeInfo = (JsonTypeInfo<Post>)BlueskyJsonSerializerOptions.Options.GetTypeInfo(typeof(Post));
        _post = new Post("A post of an ordinary length, with a little text in it, which is what most create record calls send. 🦋");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _authorFeedClient.Dispose();
        _profilesClient.Dispose();
        _timelineClient.Dispose();
    }

    [Benchmark]
    [BenchmarkCategory("EndToEnd")]
    public async Task<int> GetAuthorFeed()
    {
        var result =
            await BlueskyServer.GetAuthorFeed(s_actor, 50, null, null, null, s_service, null, _authorFeedClient).ConfigureAwait(false);
        return result.Result!.Count;
    }

    [Benchmark]
    [BenchmarkCategory("EndToEnd")]
    public async Task<int> GetProfiles()
    {
        var result =
            await BlueskyServer.GetProfiles(s_profiles, s_service, null, _profilesClient).ConfigureAwait(false);
        return result.Result!.Count;
    }

    [Benchmark]
    [BenchmarkCategory("EndToEnd")]
    public async Task<int> GetTimeline()
    {
        var result =
            await BlueskyServer.GetTimeline(null, 50, null, s_service, _credentials, _timelineClient).ConfigureAwait(false);
        return result.Result!.Count;
    }

    // Today the bounded read produces a UTF-16 string, which is then deserialized.
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ResponseDecode")]
    public object? DecodeViaString() =>
        JsonSerializer.Deserialize(Encoding.UTF8.GetString(_authorFeed), _authorFeedTypeInfo);

    // Deserializing the UTF-8 bytes the bounded read already holds skips the string, which is twice the size of the body.
    [Benchmark]
    [BenchmarkCategory("ResponseDecode")]
    public object? DecodeViaUtf8() =>
        JsonSerializer.Deserialize(_authorFeed, _authorFeedTypeInfo);

    // The same comparison for an authenticated timeline, whose entries also carry viewer state.
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TimelineDecode")]
    public object? TimelineDecodeViaString() =>
        JsonSerializer.Deserialize(Encoding.UTF8.GetString(_timeline), _timelineTypeInfo);

    [Benchmark]
    [BenchmarkCategory("TimelineDecode")]
    public object? TimelineDecodeViaUtf8() =>
        JsonSerializer.Deserialize(_timeline, _timelineTypeInfo);

    // Today a record is serialized to a string, which StringContent then encodes back to UTF-8.
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RequestEncode")]
    public async Task EncodeViaString()
    {
        using StringContent content = new(JsonSerializer.Serialize(_post, _postTypeInfo), Encoding.UTF8, "application/json");
        await content.CopyToAsync(Stream.Null).ConfigureAwait(false);
    }

    [Benchmark]
    [BenchmarkCategory("RequestEncode")]
    public async Task EncodeViaUtf8Bytes()
    {
        using ByteArrayContent content = new(JsonSerializer.SerializeToUtf8Bytes(_post, _postTypeInfo));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        await content.CopyToAsync(Stream.Null).ConfigureAwait(false);
    }

    // JsonContent serializes straight into the request stream. It cannot be replayed for a DPoP nonce retry without
    // buffering, which the client already does for caller supplied content.
    [Benchmark]
    [BenchmarkCategory("RequestEncode")]
    public async Task EncodeViaJsonContent()
    {
        using JsonContent content = JsonContent.Create(_post, _postTypeInfo);
        await content.CopyToAsync(Stream.Null).ConfigureAwait(false);
    }

    // Today the endpoint name used to tag metrics is found by splitting the whole path and query string.
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("MetricsEndpointName")]
    public string EndpointNameViaSplit()
    {
        string xrpcEndpoint = AuthorFeedEndpoint.Substring("/xrpc/".Length).Split('/').FirstOrDefault() ?? string.Empty;

        if (xrpcEndpoint.Contains('?', StringComparison.Ordinal))
        {
            xrpcEndpoint = xrpcEndpoint.Split('?')[0];
        }

        return xrpcEndpoint;
    }

    [Benchmark]
    [BenchmarkCategory("MetricsEndpointName")]
    public string EndpointNameViaSpan()
    {
        ReadOnlySpan<char> name = AuthorFeedEndpoint.AsSpan("/xrpc/".Length);
        int end = name.IndexOfAny('/', '?');
        return (end < 0 ? name : name[..end]).ToString();
    }

    private static string UnsignedJwt(string subject)
    {
        static string Encode(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return $"{Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}")}.{Encode($"{{\"sub\":\"{subject}\",\"exp\":4102444800}}")}.";
    }

    private sealed class ReplayHandler(byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ByteArrayContent content = new(body);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            content.Headers.ContentLength = body.Length;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content, RequestMessage = request });
        }
    }
}
