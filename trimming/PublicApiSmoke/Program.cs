using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto;
using idunno.AtProto.Jetstream;
using idunno.AtProto.Repo;
using idunno.AtProto.Server;
using idunno.Bluesky;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Record;
using idunno.Bluesky.RichText;

const string whitespacePostJson = """
    {
      "text": " ",
      "$type": "app.bsky.feed.post",
      "createdAt": "2026-10-08T15:43:06.08711400Z"
    }
    """;

JsonTypeInfo<Post> postTypeInfo = (JsonTypeInfo<Post>)BlueskyJsonSerializerOptions.Default.GetTypeInfo(typeof(Post));
JsonTypeInfo<BlueskyRecord> recordTypeInfo = (JsonTypeInfo<BlueskyRecord>)BlueskyJsonSerializerOptions.Default.GetTypeInfo(typeof(BlueskyRecord));
Post whitespacePost = JsonSerializer.Deserialize(whitespacePostJson, postTypeInfo)
    ?? throw new InvalidOperationException("The whitespace-only post was not deserialized.");
BlueskyRecord? whitespaceRecord = JsonSerializer.Deserialize(whitespacePostJson, recordTypeInfo);
using (JsonDocument serializedPost = JsonDocument.Parse(JsonSerializer.Serialize(whitespacePost, postTypeInfo)))
{
    if (whitespacePost.Text != " " || whitespaceRecord is not Post { Text: " " } ||
        serializedPost.RootElement.GetProperty("text").GetString() != " ")
    {
        throw new InvalidOperationException("The whitespace-only post text was not preserved.");
    }
}

string whitespaceEnvelopeJson = $$"""
    {
      "uri": "at://did:plc:kaxwhrcwrqdxm2ppaaczwhcn/app.bsky.feed.post/3mxesfgzw4q2r",
      "cid": "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
      "value": {{whitespacePostJson}}
    }
    """;
JsonTypeInfo<AtProtoRepositoryRecord<Post>> envelopeTypeInfo =
    (JsonTypeInfo<AtProtoRepositoryRecord<Post>>)BlueskyJsonSerializerOptions.Default.GetTypeInfo(typeof(AtProtoRepositoryRecord<Post>));
AtProtoRepositoryRecord<Post>? whitespaceEnvelope = JsonSerializer.Deserialize(whitespaceEnvelopeJson, envelopeTypeInfo);
if (whitespaceEnvelope?.Value.Text != " " || new PostBuilder(" ").ToPost().Text != " ")
{
    throw new InvalidOperationException("The whitespace-only repository record or post builder text was not preserved.");
}

const string emptyTagPostJson = """
    {
      "$type":"app.bsky.feed.post",
      "text":"# #normal",
      "createdAt":"2026-10-08T15:43:06.08711400Z",
      "facets":[
        {"index":{"byteStart":0,"byteEnd":1},"features":[{"$type":"app.bsky.richtext.facet#tag","tag":""}]},
        {"index":{"byteStart":2,"byteEnd":9},"features":[{"$type":"app.bsky.richtext.facet#tag","tag":"normal"}]}
      ],
      "tags":["","normal"]
    }
    """;
Post emptyTagPost = JsonSerializer.Deserialize(emptyTagPostJson, postTypeInfo)
    ?? throw new InvalidOperationException("The post with an empty tag facet was not deserialized.");
Post? emptyTagRoundTrip = JsonSerializer.Deserialize(JsonSerializer.Serialize(emptyTagPost, postTypeInfo), postTypeInfo);
if (emptyTagRoundTrip?.Facets is not { Count: 2 } tagFacets ||
    tagFacets.First().Features.Single() is not TagFacetFeature { Tag: "" } ||
    tagFacets.Last().Features.Single() is not TagFacetFeature { Tag: "normal" })
{
    throw new InvalidOperationException("The empty and normal tag facets were not preserved.");
}
if (emptyTagRoundTrip.Tags is not { Count: 2 } externalTags ||
    externalTags.First() != "" ||
    externalTags.Last() != "normal")
{
    throw new InvalidOperationException("The empty top-level post tag was not preserved.");
}

const string emptyFeaturesFacetJson = """
    {"$type":"app.bsky.richtext.facet","index":{"byteStart":0,"byteEnd":0},"features":[]}
    """;
JsonTypeInfo<Facet> facetTypeInfo = (JsonTypeInfo<Facet>)BlueskyJsonSerializerOptions.Default.GetTypeInfo(typeof(Facet));
Facet emptyFeaturesFacet = JsonSerializer.Deserialize(emptyFeaturesFacetJson, facetTypeInfo)
    ?? throw new InvalidOperationException("The empty-features facet was not deserialized.");
Facet? emptyFeaturesRoundTrip = JsonSerializer.Deserialize(
    JsonSerializer.Serialize(emptyFeaturesFacet, facetTypeInfo),
    facetTypeInfo);
if (emptyFeaturesRoundTrip is null || emptyFeaturesRoundTrip.Features.Count != 0)
{
    throw new InvalidOperationException("The empty-features facet was not preserved.");
}

Uri service = new("https://offline.invalid");
using OfflineHttpMessageHandler handler = new();
OfflineHttpClientFactory httpClientFactory = new(handler);

JetstreamOptions replayOptions = new() { ReplayLiveBufferCapacity = 8192 };
using (AtProtoJetstream replay = new(options: replayOptions))
using (AtProtoJetstream factoryReplay = new(httpClientFactory, options: replayOptions))
using (AtProtoJetstream builtReplay = AtProtoJetstream.CreateBuilder()
    .SetReplayLiveBufferCapacity(8192)
    .WithHttpClientFactory(httpClientFactory)
    .Build())
{
    if (!replayOptions.ToString().Contains("ReplayLiveBufferCapacity = 8192", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("The replay capacity was not retained in option diagnostics.");
    }
}

using (AtProtoAgent agent = new(service, httpClientFactory))
{
    AtProtoHttpResult<ServerDescription> describeServerResult = await agent.DescribeServer(service);
    describeServerResult.EnsureSucceeded();

    if (describeServerResult.Result.Did != "did:web:offline.invalid" ||
        describeServerResult.Result.Contact?.Email != "test@offline.invalid")
    {
        throw new InvalidOperationException("The server description was not deserialized correctly.");
    }

    AtProtoHttpResult<AtProtoRepositoryRecord> getRecordResult = await agent.GetRawRecord(
        new AtUri("at://did:plc:test/app.bsky.actor.profile/self"),
        service,
        CancellationToken.None);
    getRecordResult.EnsureSucceeded();

    if (getRecordResult.Result.Value?["displayName"]?.GetValue<string>() != "Offline Profile")
    {
        throw new InvalidOperationException("The repository record was not deserialized correctly.");
    }
}

using (BlueskyAgent agent = new(httpClientFactory))
{
    AtProtoHttpResult<ProfileViewDetailed> getProfileResult = await agent.GetProfile(
        AtIdentifier.Create("offline.invalid"));
    getProfileResult.EnsureSucceeded();

    if (getProfileResult.Result.Did != "did:plc:test" ||
        getProfileResult.Result.Handle != "offline.invalid" ||
        getProfileResult.Result.DisplayName != "Offline User")
    {
        throw new InvalidOperationException("The Bluesky profile was not deserialized correctly.");
    }
}

if (handler.RequestCount != 3)
{
    throw new InvalidOperationException($"Expected three offline requests, but received {handler.RequestCount}.");
}

Console.WriteLine("Offline trimming and Native AOT smoke test passed.");

internal sealed class OfflineHttpClientFactory(OfflineHttpMessageHandler handler) : IHttpClientFactory
{
    private readonly OfflineHttpMessageHandler _handler = handler;

    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
}

internal sealed class OfflineHttpMessageHandler : HttpMessageHandler
{
    private const string DescribeServerResponse = """
        {
          "did": "did:web:offline.invalid",
          "availableUserDomains": ["offline.invalid"],
          "inviteCodeRequired": false,
          "phoneVerificationRequired": false,
          "links": {
            "privacyPolicy": "https://offline.invalid/privacy",
            "termsOfService": "https://offline.invalid/terms"
          },
          "contact": {
            "email": "test@offline.invalid"
          },
          "blobUploadLimit": 10000000
        }
        """;

    private const string GetRecordResponse = """
        {
          "uri": "at://did:plc:test/app.bsky.actor.profile/self",
          "cid": "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
          "value": {
            "$type": "app.bsky.actor.profile",
            "displayName": "Offline Profile",
            "description": "An offline repository record."
          }
        }
        """;

    private const string GetProfileResponse = """
        {
          "did": "did:plc:test",
          "handle": "offline.invalid",
          "displayName": "Offline User",
          "description": "An offline profile.",
          "followersCount": 1,
          "followsCount": 2,
          "postsCount": 3,
          "labels": []
        }
        """;

    public int RequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Headers.Authorization is not null)
        {
            throw new InvalidOperationException("The public API smoke test sent authentication credentials.");
        }

        RequestCount++;

        string response = request.RequestUri?.AbsolutePath switch
        {
            "/xrpc/com.atproto.server.describeServer" => DescribeServerResponse,
            "/xrpc/com.atproto.repo.getRecord" => GetRecordResponse,
            "/xrpc/app.bsky.actor.getProfile" => GetProfileResponse,
            _ => throw new InvalidOperationException($"Unexpected request URI {request.RequestUri}.")
        };

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(response, Encoding.UTF8, "application/json"),
            RequestMessage = request
        });
    }
}
