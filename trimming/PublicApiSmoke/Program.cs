using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto;
using idunno.AtProto.Jetstream;
using idunno.AtProto.Repo;
using idunno.AtProto.Server;
using idunno.Bluesky;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Feed.Gates;
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

const string extensionPostJson = """
    {"$type":"app.bsky.feed.post","$via":"kazahana","$example":{"$":"nested"},"\u0024":[1,null,true],
     "text":"hello","createdAt":"2026-10-13T10:24:57.970Z","langs":["ja"]}
    """;
Post extensionPost = JsonSerializer.Deserialize(extensionPostJson, postTypeInfo)
    ?? throw new InvalidOperationException("The post with unknown metadata was not deserialized.");
BlueskyRecord? extensionRecord = JsonSerializer.Deserialize(extensionPostJson, recordTypeInfo);
Post? extensionRoundTrip = JsonSerializer.Deserialize(JsonSerializer.Serialize(extensionPost, postTypeInfo), postTypeInfo);
foreach (Post? post in new[] { extensionPost, extensionRecord as Post, extensionRoundTrip })
{
    if (post?.ExtensionData?["$via"].GetString() != "kazahana" ||
        post.ExtensionData["$example"].GetProperty("$").GetString() != "nested" ||
        post.ExtensionData["$"].GetArrayLength() != 3 ||
        post.Text != "hello" || post.Langs?.Single() != "ja")
    {
        throw new InvalidOperationException("Unknown post metadata was not preserved.");
    }
}

JsonOptions lateJsonOptions = new();
lateJsonOptions.JsonSerializerOptions.TypeInfoResolverChain.Add(LateBoundSourceGenerationContext.Default);
JsonTypeInfo<LateBoundRecordBase> lateRecordTypeInfo =
    (JsonTypeInfo<LateBoundRecordBase>)lateJsonOptions.JsonSerializerOptions.GetTypeInfo(typeof(LateBoundRecordBase));
const string latePolymorphicJson = """
    {"$type":"smoke.container","payload":{"record":{"$type":"smoke.nested","$via":"aot","value":"nested"}}}
    """;
LateBoundRecordBase? lateRecord = JsonSerializer.Deserialize(latePolymorphicJson, lateRecordTypeInfo);
LateBoundNestedRecord? lateNested = (lateRecord as LateBoundRecord)?.Payload.Record as LateBoundNestedRecord;
if (lateNested is not { Value: "nested", ExtensionData: not null } ||
    lateNested.ExtensionData["$via"].GetString() != "aot")
{
    throw new InvalidOperationException("Late-bound polymorphic source generation or nested custom struct conversion failed.");
}

JsonSerializerOptions callerCopy = new(AtProtoJsonSerializerOptions.Default)
{
    TypeInfoResolver = LateBoundSourceGenerationContext.Default
};
foreach (JsonSerializerOptions mutableOptions in new[]
{
    AtProtoJsonSerializerOptions.Options,
    BlueskyJsonSerializerOptions.Options,
    AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(callerCopy)
})
{
    mutableOptions.TypeInfoResolverChain.Add(JsonTypeInfoResolver.Combine());
    mutableOptions.TypeInfoResolverChain[^1] = LateBoundSourceGenerationContext.Default;
    JsonTypeInfo<LateBoundRecordBase> mutableTypeInfo =
        (JsonTypeInfo<LateBoundRecordBase>)mutableOptions.GetTypeInfo(typeof(LateBoundRecordBase));
    foreach (bool outerMetadata in new[] { false, true })
    {
        string propertyJson = $$$$"""
            {"$type":"smoke.container",{{{{(outerMetadata ? "\"$via\":\"outer\"," : "")}}}}
             "propertyPayload":{"record":{"$type":"smoke.nested","$via":"property","value":"struct"}},
             "reference":{"record":{"$type":"smoke.nested","$via":"reference","value":"class"}}}
            """;
        LateBoundRecord propertyRecord = JsonSerializer.Deserialize(propertyJson, mutableTypeInfo) as LateBoundRecord
            ?? throw new InvalidOperationException("The property-converted record was not deserialized.");
        string roundTripJson = JsonSerializer.Serialize<LateBoundRecordBase>(propertyRecord, mutableTypeInfo);
        using JsonDocument roundTripDocument = JsonDocument.Parse(roundTripJson);
        if (propertyRecord.PropertyPayload.Record is not LateBoundNestedRecord { Value: "struct" } propertyNested ||
            propertyNested.ExtensionData?["$via"].GetString() != "property" ||
            propertyRecord.Reference is not LateBoundNestedRecord { Value: "class" } referenceNested ||
            referenceNested.ExtensionData?["$via"].GetString() != "reference" ||
            roundTripDocument.RootElement.GetProperty("reference").GetProperty("record").GetProperty("$via").GetString() != "reference" ||
            JsonSerializer.Deserialize(roundTripJson, mutableTypeInfo) is not LateBoundRecord)
        {
            throw new InvalidOperationException("Mutable SDK options or property converter read/write forwarding failed.");
        }
    }
}

string extensionEnvelopeJson = $$"""
    {"uri":"at://did:plc:heztswuhu24kudwdaz7y4bi3/app.bsky.feed.post/3mxqswzprj42r",
     "cid":"bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4","value":{{extensionPostJson}}}
    """;
if (JsonSerializer.Deserialize(extensionEnvelopeJson, envelopeTypeInfo)?.Value.ExtensionData?["$via"].GetString() != "kazahana")
{
    throw new InvalidOperationException("Repository envelope metadata was not preserved.");
}

string nestedFeedJson = $$"""
    {"post":{"uri":"at://did:plc:heztswuhu24kudwdaz7y4bi3/app.bsky.feed.post/3mxqswzprj42r",
     "cid":"bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
     "author":{"did":"did:plc:heztswuhu24kudwdaz7y4bi3","handle":"example.test"},
     "record":{{extensionPostJson}},"indexedAt":"2026-10-13T10:24:57.970Z",
     "embed":{"$type":"app.bsky.embed.record#view","$example":"embed","record":{
       "$type":"app.bsky.embed.record#viewRecord","$via":"quote",
       "uri":"at://did:plc:heztswuhu24kudwdaz7y4bi3/app.bsky.feed.post/3mxqswzprj42r",
       "cid":"bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
       "author":{"did":"did:plc:heztswuhu24kudwdaz7y4bi3","handle":"example.test"},
       "value":{{extensionPostJson}},"indexedAt":"2026-10-13T10:24:57.970Z"} } } }
    """;
JsonTypeInfo<FeedViewPost> feedTypeInfo = (JsonTypeInfo<FeedViewPost>)BlueskyJsonSerializerOptions.Default.GetTypeInfo(typeof(FeedViewPost));
FeedViewPost feed = JsonSerializer.Deserialize(nestedFeedJson, feedTypeInfo)
    ?? throw new InvalidOperationException("The nested feed was not deserialized.");
FeedViewPost? feedRoundTrip = JsonSerializer.Deserialize(JsonSerializer.Serialize(feed, feedTypeInfo), feedTypeInfo);
foreach (FeedViewPost? item in new[] { feed, feedRoundTrip })
{
    if (item?.Post.Record.ExtensionData?["$via"].GetString() != "kazahana" ||
        item.Post.Embed is not EmbeddedRecordView embedded ||
        embedded.ExtensionData?["$example"].GetString() != "embed" ||
        embedded.Record is not ViewRecord quote ||
        quote.ExtensionData?["$via"].GetString() != "quote" ||
        quote.Value is not Post quotedPost ||
        quotedPost.ExtensionData?["$via"].GetString() != "kazahana")
    {
        throw new InvalidOperationException("Nested metadata was not retained on its owning object.");
    }
}

const string extensionRuleJson = """
    {"$example":{"$":"discard"},"$":[1,null],"$type":"app.bsky.feed.threadgate#listRule",
     "list":"at://did:plc:heztswuhu24kudwdaz7y4bi3/app.bsky.graph.list/test"}
    """;
JsonTypeInfo<ThreadGateRule> ruleTypeInfo = (JsonTypeInfo<ThreadGateRule>)BlueskyJsonSerializerOptions.Default.GetTypeInfo(typeof(ThreadGateRule));
ThreadGateRule rule = JsonSerializer.Deserialize(extensionRuleJson, ruleTypeInfo)
    ?? throw new InvalidOperationException("The rule with unknown metadata was not deserialized.");
using (JsonDocument serializedRule = JsonDocument.Parse(JsonSerializer.Serialize(rule, ruleTypeInfo)))
{
    if (rule is not ListRule ||
        serializedRule.RootElement.TryGetProperty("$example", out _) ||
        serializedRule.RootElement.TryGetProperty("$", out _))
    {
        throw new InvalidOperationException("Unknown metadata without extension data was not discarded.");
    }
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

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(LateBoundRecord), "smoke.container")]
internal abstract class LateBoundRecordBase;

internal sealed class LateBoundRecord : LateBoundRecordBase
{
    public LateBoundStructPayload Payload { get; set; }

    [JsonConverter(typeof(LateBoundStructPayloadConverter))]
    public LateBoundStructPayload PropertyPayload { get; set; }

    [JsonConverter(typeof(LateBoundReferenceConverter))]
    public LateBoundNestedRecordBase? Reference { get; set; }
}

internal sealed class LateBoundReferenceConverter : JsonConverter<LateBoundNestedRecordBase>
{
    public override LateBoundNestedRecordBase? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonTypeInfo<LateBoundNestedRecordBase?> typeInfo =
            (JsonTypeInfo<LateBoundNestedRecordBase?>)options.GetTypeInfo(typeof(LateBoundNestedRecordBase));
        return JsonSerializer.Deserialize(document.RootElement.GetProperty("record"), typeInfo);
    }

    public override void Write(Utf8JsonWriter writer, LateBoundNestedRecordBase value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("record");
        JsonTypeInfo<LateBoundNestedRecordBase?> typeInfo =
            (JsonTypeInfo<LateBoundNestedRecordBase?>)options.GetTypeInfo(typeof(LateBoundNestedRecordBase));
        JsonSerializer.Serialize(writer, value, typeInfo);
        writer.WriteEndObject();
    }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(LateBoundNestedRecord), "smoke.nested")]
internal abstract class LateBoundNestedRecordBase;

internal sealed class LateBoundNestedRecord : LateBoundNestedRecordBase
{
    public string Value { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

[JsonConverter(typeof(LateBoundStructPayloadConverter))]
internal readonly record struct LateBoundStructPayload(LateBoundNestedRecordBase? Record);

internal sealed class LateBoundStructPayloadConverter : JsonConverter<LateBoundStructPayload>
{
    public override LateBoundStructPayload Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonTypeInfo<LateBoundNestedRecordBase?> typeInfo =
            (JsonTypeInfo<LateBoundNestedRecordBase?>)options.GetTypeInfo(typeof(LateBoundNestedRecordBase));
        return new(JsonSerializer.Deserialize(document.RootElement.GetProperty("record"), typeInfo));
    }

    public override void Write(Utf8JsonWriter writer, LateBoundStructPayload value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("record");
        JsonTypeInfo<LateBoundNestedRecordBase?> typeInfo =
            (JsonTypeInfo<LateBoundNestedRecordBase?>)options.GetTypeInfo(typeof(LateBoundNestedRecordBase));
        JsonSerializer.Serialize(writer, value.Record, typeInfo);
        writer.WriteEndObject();
    }
}

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(LateBoundRecordBase))]
[JsonSerializable(typeof(LateBoundRecord))]
[JsonSerializable(typeof(LateBoundStructPayload))]
[JsonSerializable(typeof(LateBoundNestedRecordBase))]
[JsonSerializable(typeof(LateBoundNestedRecord))]
internal sealed partial class LateBoundSourceGenerationContext : JsonSerializerContext;

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
