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

const string ignoredPostPropertyJson = """
    {"$type":"app.bsky.feed.post","$via":"kazahana","length":42,"text":"hello","createdAt":"2026-10-13T10:24:57.970Z"}
    """;
Post ignoredPostProperty = JsonSerializer.Deserialize(ignoredPostPropertyJson, postTypeInfo)
    ?? throw new InvalidOperationException("The post with an ignored property was not deserialized.");
if (ignoredPostProperty.ExtensionData?.ContainsKey("length") is true)
{
    throw new InvalidOperationException("An ignored post property was captured as extension data.");
}

JsonSerializerOptions referenceOptions = BlueskyJsonSerializerOptions.Create(options =>
    options.ReferenceHandler = ReferenceHandler.Preserve);
JsonTypeInfo<AtProtoRepositoryRecord<Post>> referenceEnvelopeInfo =
    (JsonTypeInfo<AtProtoRepositoryRecord<Post>>)referenceOptions.GetTypeInfo(typeof(AtProtoRepositoryRecord<Post>));
string referenceEnvelopeJson = $$"""
    {
      "$id": "1",
      "uri": "at://did:plc:kaxwhrcwrqdxm2ppaaczwhcn/app.bsky.feed.post/3mxesfgzw4q2r",
      "cid": "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
      "value": {{extensionPostJson}}
    }
    """;
AtProtoRepositoryRecord<Post>? referenceEnvelope = JsonSerializer.Deserialize(referenceEnvelopeJson, referenceEnvelopeInfo);
if (referenceEnvelope?.Value.ExtensionData?["$via"].GetString() != "kazahana")
{
    throw new InvalidOperationException("Reference-preserving repository normalization lost unknown metadata.");
}

JsonOptions lateJsonOptions = new();
JsonOptions decoratedOptions = new();
decoratedOptions.JsonSerializerOptions.TypeInfoResolver =
    decoratedOptions.JsonSerializerOptions.TypeInfoResolver!.WithAddedModifier(_ => { });
using AtProtoAgent decoratedAgent = new(new Uri("https://offline.invalid"), new AtProtoAgentOptions { HttpJsonOptions = decoratedOptions });
_ = decoratedAgent.JsonOptions.JsonSerializerOptions.GetTypeInfo(typeof(StrongReference));

JsonSerializerOptions keyOptions = AtProtoJsonSerializerOptions.Create(options =>
    options.TypeInfoResolver = LateBoundSourceGenerationContext.Default);
JsonTypeInfo<SmokeKeyRecordBase> keyInfo = (JsonTypeInfo<SmokeKeyRecordBase>)keyOptions.GetTypeInfo(typeof(SmokeKeyRecordBase));
SmokeKeyRecord keyRecord = JsonSerializer.Deserialize(
    """{"$type":"smoke.keys","$via":"extension","values":{"key:first":"value"}}""", keyInfo) as SmokeKeyRecord
    ?? throw new InvalidOperationException("Custom-key record could not be decoded.");
keyRecord.RuntimeValue = new Dictionary<SmokeKey, string> { [new SmokeKey("runtime")] = "value" };
string keyJson = JsonSerializer.Serialize<SmokeKeyRecordBase>(keyRecord, keyInfo);
if (keyRecord.Values[new SmokeKey("first")] != "value" ||
    !keyJson.Contains("\"key:first\":\"value\"", StringComparison.Ordinal) ||
    !keyJson.Contains("\"key:runtime\":\"value\"", StringComparison.Ordinal))
{
    throw new InvalidOperationException("Custom dictionary-key hooks were not retained for declared and runtime-discovered dictionaries.");
}
lateJsonOptions.JsonSerializerOptions.TypeInfoResolverChain.Add(LateBoundSourceGenerationContext.Default);
JsonSerializerOptions keyValueOptions = AtProtoJsonSerializerOptions.Create(options =>
{
    options.TypeInfoResolver = LateBoundSourceGenerationContext.Default;
    options.ReferenceHandler = ReferenceHandler.Preserve;
});
JsonTypeInfo<Dictionary<SmokeKey, SmokeKey>> keyValueInfo =
    (JsonTypeInfo<Dictionary<SmokeKey, SmokeKey>>)keyValueOptions.GetTypeInfo(typeof(Dictionary<SmokeKey, SmokeKey>));
Dictionary<SmokeKey, SmokeKey> keyValues = JsonSerializer.Deserialize(
    """{"key:first":{"$type":"smoke.nested","$via":"extension","value":"first"}}""", keyValueInfo)
    ?? throw new InvalidOperationException("Custom-key dictionary values could not be decoded.");
string keyValuesJson = JsonSerializer.Serialize(keyValues, keyValueInfo);
Dictionary<SmokeKey, SmokeKey>? keyValuesRoundTrip = JsonSerializer.Deserialize(keyValuesJson, keyValueInfo);
if (keyValues[new SmokeKey("first")].Record?.ExtensionData?["$via"].GetString() != "extension" ||
    keyValuesRoundTrip?[new SmokeKey("first")].Record?.ExtensionData?["$via"].GetString() != "extension")
{
    throw new InvalidOperationException("Custom-key dictionary value forwarding lost nested metadata.");
}
JsonSerializerOptions collectionOptions = AtProtoJsonSerializerOptions.Create(options =>
    options.TypeInfoResolver = LateBoundSourceGenerationContext.Default);
JsonSerializerOptions sharedReferenceOptions = AtProtoJsonSerializerOptions.Create(options =>
{
    options.TypeInfoResolver = LateBoundSourceGenerationContext.Default;
    options.ReferenceHandler = new SmokeReferenceHandler();
});
using JsonDocument sharedMetadata = JsonDocument.Parse("\"extension\"");
LateBoundNestedRecord sharedRecord = new()
{
    Value = "shared",
    ExtensionData = new() { ["$via"] = sharedMetadata.RootElement.Clone() }
};
SmokeRecordList sharedRecords = [sharedRecord, sharedRecord];
JsonTypeInfo<SmokeRecordList> sharedListInfo = (JsonTypeInfo<SmokeRecordList>)sharedReferenceOptions.GetTypeInfo(typeof(SmokeRecordList));
string sharedListJson = JsonSerializer.Serialize(sharedRecords, sharedListInfo);
SmokeRecordList? sharedRoundTrip = JsonSerializer.Deserialize(sharedListJson, sharedListInfo);
if (sharedRoundTrip is null || sharedRoundTrip.Count != 2 ||
    !ReferenceEquals(sharedRoundTrip[0], sharedRoundTrip[1]) ||
    sharedRoundTrip[0] is not LateBoundNestedRecord { ExtensionData: not null } sharedResult ||
    sharedResult.ExtensionData["$via"].GetString() != "extension")
{
    throw new InvalidOperationException("Reference-preserving collection normalization lost shared references or unknown metadata.");
}
JsonTypeInfo<SmokeCollectionRecordBase> collectionTypeInfo =
    (JsonTypeInfo<SmokeCollectionRecordBase>)collectionOptions.GetTypeInfo(typeof(SmokeCollectionRecordBase));
foreach (bool outerMetadata in new[] { false, true })
{
    string metadata = outerMetadata ? "\"$via\":\"outer\"," : "";
    string collectionJson = $$$$"""
        {"$type":"smoke.collections",{{{{metadata}}}}
         "records":[{"$type":"smoke.nested","$via":"list","value":"first"}],
         "dictionary":{"entry":{"$type":"smoke.nested","$via":"dictionary","value":"second"}}}
        """;
    SmokeCollectionRecord collection = JsonSerializer.Deserialize(collectionJson, collectionTypeInfo) as SmokeCollectionRecord
        ?? throw new InvalidOperationException("The derived collection container was not deserialized.");
    if (collection.Records.Single() is not LateBoundNestedRecord { Value: "first" } first ||
        first.ExtensionData?["$via"].GetString() != "list" ||
        collection.Dictionary["entry"] is not LateBoundNestedRecord { Value: "second" } second ||
        second.ExtensionData?["$via"].GetString() != "dictionary")
    {
        throw new InvalidOperationException("Derived collection metadata was not preserved.");
    }
}
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
using AtProtoAgent finalizedAtProtoAgent = AtProtoAgent.CreateBuilder()
    .ForService(new Uri("https://offline.invalid"))
    .ConfigureHttpJsonOptions(options => options.JsonSerializerOptions.WriteIndented = true)
    .Build();
JsonOptions configuredAgentOptions = new();
configuredAgentOptions.JsonSerializerOptions.TypeInfoResolver = LateBoundSourceGenerationContext.Default;
using AtProtoAgent customAtProtoAgent = new(new Uri("https://offline.invalid"), new AtProtoAgentOptions { HttpJsonOptions = configuredAgentOptions });
BlueskyAgentOptions configuredBlueskyOptions = new();
configuredBlueskyOptions.HttpJsonOptions!.JsonSerializerOptions.TypeInfoResolver = LateBoundSourceGenerationContext.Default;
using BlueskyAgent customBlueskyAgent = new(configuredBlueskyOptions);
BlueskyAgentOptions modifiedBlueskyOptions = new();
int postCallbackCount = 0;
JsonSerializerOptions modifiedBlueskyJsonOptions = modifiedBlueskyOptions.HttpJsonOptions!.JsonSerializerOptions;
modifiedBlueskyJsonOptions.TypeInfoResolver = modifiedBlueskyJsonOptions.TypeInfoResolver!.WithAddedModifier(info =>
{
    if (info.Type == typeof(Post) && info.Kind == JsonTypeInfoKind.Object)
    {
        info.OnDeserialized = _ => postCallbackCount++;
    }
});
using BlueskyAgent modifiedBlueskyAgent = new(modifiedBlueskyOptions);
JsonTypeInfo<Post> modifiedPostTypeInfo =
    (JsonTypeInfo<Post>)modifiedBlueskyAgent.JsonOptions.JsonSerializerOptions.GetTypeInfo(typeof(Post));
_ = JsonSerializer.Deserialize(
    """{"$type":"app.bsky.feed.post","text":"modified","createdAt":"2026-10-13T10:24:57.970Z"}""",
    modifiedPostTypeInfo)
    ?? throw new InvalidOperationException("The modified Bluesky source-generated Post was not deserialized.");
if (postCallbackCount != 1)
{
    throw new InvalidOperationException("The caller's modifier was not applied to the Bluesky source-generated Post.");
}
var finalizedBuilder = BlueskyAgent.CreateBuilder();
finalizedBuilder.ConfigureHttpJsonOptions(options => options.JsonSerializerOptions.WriteIndented = true);
finalizedBuilder.JsonOptions!.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, LateBoundSourceGenerationContext.Default);
using BlueskyAgent customBuiltBlueskyAgent = finalizedBuilder.Build();
if (configuredAgentOptions.JsonSerializerOptions.IsReadOnly || configuredBlueskyOptions.HttpJsonOptions.JsonSerializerOptions.IsReadOnly ||
    !finalizedAtProtoAgent.JsonOptions.JsonSerializerOptions.IsReadOnly)
{
    throw new InvalidOperationException("Agent finalization froze caller options or returned mutable options.");
}
foreach (JsonSerializerOptions mutableOptions in new[]
{
    AtProtoJsonSerializerOptions.Options,
    BlueskyJsonSerializerOptions.Options,
    AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(callerCopy),
    AtProtoJsonSerializerOptions.Create(options => options.TypeInfoResolver = LateBoundSourceGenerationContext.Default),
    AtProtoJsonSerializerOptions.Create(options => options.TypeInfoResolverChain.Insert(0, LateBoundSourceGenerationContext.Default)),
    BlueskyJsonSerializerOptions.Create(options => options.TypeInfoResolver = LateBoundSourceGenerationContext.Default),
    BlueskyJsonSerializerOptions.Create(options => options.TypeInfoResolverChain.Insert(0, LateBoundSourceGenerationContext.Default)),
    customAtProtoAgent.JsonOptions.JsonSerializerOptions,
    customBlueskyAgent.JsonOptions.JsonSerializerOptions,
    customBuiltBlueskyAgent.JsonOptions.JsonSerializerOptions,
    sharedReferenceOptions
})
{
    if (!mutableOptions.IsReadOnly)
    {
        mutableOptions.TypeInfoResolverChain.Add(JsonTypeInfoResolver.Combine());
        mutableOptions.TypeInfoResolverChain[^1] = LateBoundSourceGenerationContext.Default;
    }
    JsonTypeInfo<LateBoundRecordBase> mutableTypeInfo =
        (JsonTypeInfo<LateBoundRecordBase>)mutableOptions.GetTypeInfo(typeof(LateBoundRecordBase));
    foreach (bool outerMetadata in new[] { false, true })
    {
        string metadata = outerMetadata ? "\"$via\":\"outer\"," : "";
        string nullJson = $$"""{"$type":"smoke.container",{{metadata}}"sentinel":null,"defaultNull":null}""";
        LateBoundRecord nullRecord = JsonSerializer.Deserialize(nullJson, mutableTypeInfo) as LateBoundRecord
            ?? throw new InvalidOperationException("The null-policy container was not deserialized.");
        if (nullRecord.Sentinel?.Value != "read-null" || nullRecord.DefaultNull is not null)
        {
            throw new InvalidOperationException("A type-attached reference converter's null read policy was bypassed.");
        }
        nullRecord.Sentinel = null;
        using (JsonDocument nullDocument = JsonDocument.Parse(JsonSerializer.Serialize<LateBoundRecordBase>(nullRecord, mutableTypeInfo)))
        {
            if (nullDocument.RootElement.GetProperty("sentinel").GetString() != "write-null" ||
                nullDocument.RootElement.GetProperty("defaultNull").ValueKind != JsonValueKind.Null)
            {
                throw new InvalidOperationException("A type-attached reference converter's null write policy was bypassed.");
            }
        }
        try
        {
            _ = JsonSerializer.Deserialize($$"""{"$type":"smoke.rejectedNull",{{metadata}}"value":null}""", mutableTypeInfo);
            throw new InvalidOperationException("The type-attached converter did not reject null on read.");
        }
        catch (JsonException)
        {
        }
        try
        {
            _ = JsonSerializer.Serialize<LateBoundRecordBase>(new LateBoundRejectedNullRecord(), mutableTypeInfo);
            throw new InvalidOperationException("The type-attached converter did not reject null on write.");
        }
        catch (JsonException)
        {
        }
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
[JsonDerivedType(typeof(LateBoundRejectedNullRecord), "smoke.rejectedNull")]
internal abstract class LateBoundRecordBase;

internal sealed class LateBoundRecord : LateBoundRecordBase
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public SmokeSentinelNullValue? Sentinel { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public SmokeDefaultNullValue? DefaultNull { get; set; }

    public LateBoundStructPayload Payload { get; set; }

    [JsonConverter(typeof(LateBoundStructPayloadConverter))]
    public LateBoundStructPayload PropertyPayload { get; set; }

    [JsonConverter(typeof(LateBoundReferenceConverter))]
    public LateBoundNestedRecordBase? Reference { get; set; }
}

internal sealed class LateBoundRejectedNullRecord : LateBoundRecordBase
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public SmokeRejectedNullValue? Value { get; set; }
}

[JsonConverter(typeof(SmokeSentinelNullConverter))]
internal sealed record SmokeSentinelNullValue(string Value);

[JsonConverter(typeof(SmokeDefaultNullConverter))]
internal sealed record SmokeDefaultNullValue(string Value);

[JsonConverter(typeof(SmokeRejectedNullConverter))]
internal sealed record SmokeRejectedNullValue(string Value);

internal sealed class SmokeSentinelNullConverter : JsonConverter<SmokeSentinelNullValue>
{
    public override bool HandleNull => true;

    public override SmokeSentinelNullValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.TokenType == JsonTokenType.Null ? "read-null" : reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, SmokeSentinelNullValue value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value?.Value ?? "write-null");
}

internal sealed class SmokeDefaultNullConverter : JsonConverter<SmokeDefaultNullValue>
{
    public override SmokeDefaultNullValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.TokenType == JsonTokenType.Null ? throw new JsonException("Unexpected null.") : reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, SmokeDefaultNullValue value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value?.Value ?? throw new JsonException("Unexpected null."));
}

internal sealed class SmokeRejectedNullConverter : JsonConverter<SmokeRejectedNullValue>
{
    public override bool HandleNull => true;

    public override SmokeRejectedNullValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.TokenType == JsonTokenType.Null ? throw new JsonException("Null is not permitted.") : reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, SmokeRejectedNullValue value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value?.Value ?? throw new JsonException("Null is not permitted."));
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

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(SmokeCollectionRecord), "smoke.collections")]
internal abstract class SmokeCollectionRecordBase;

internal sealed class SmokeCollectionRecord : SmokeCollectionRecordBase
{
    public SmokeRecordList Records { get; set; } = [];

    public SmokeRecordDictionary Dictionary { get; set; } = [];
}

internal sealed class SmokeRecordList : List<LateBoundNestedRecordBase>;

internal sealed class SmokeRecordDictionary : Dictionary<string, LateBoundNestedRecordBase>;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(SmokeKeyRecord), "smoke.keys")]
internal abstract class SmokeKeyRecordBase;

internal sealed class SmokeKeyRecord : SmokeKeyRecordBase
{
    public Dictionary<SmokeKey, string> Values { get; set; } = [];

    public object? RuntimeValue { get; set; }
}

[JsonConverter(typeof(SmokeKeyConverter))]
internal sealed record SmokeKey(string Value)
{
    public LateBoundNestedRecord? Record { get; init; }
}

internal sealed class SmokeKeyConverter : JsonConverter<SmokeKey>
{
    public override SmokeKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            JsonTypeInfo<LateBoundNestedRecordBase> info =
                (JsonTypeInfo<LateBoundNestedRecordBase>)options.GetTypeInfo(typeof(LateBoundNestedRecordBase));
            LateBoundNestedRecord record = JsonSerializer.Deserialize(ref reader, info) as LateBoundNestedRecord
                ?? throw new InvalidOperationException("The nested key value could not be decoded.");
            return new(record.Value) { Record = record };
        }
        return new(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, SmokeKey value, JsonSerializerOptions options)
    {
        if (value.Record is not null)
        {
            JsonTypeInfo<LateBoundNestedRecordBase> info =
                (JsonTypeInfo<LateBoundNestedRecordBase>)options.GetTypeInfo(typeof(LateBoundNestedRecordBase));
            JsonSerializer.Serialize<LateBoundNestedRecordBase>(writer, value.Record, info);
        }
        else
        {
            writer.WriteStringValue(value.Value);
        }
    }

    public override SmokeKey ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString()!["key:".Length..]);

    public override void WriteAsPropertyName(Utf8JsonWriter writer, SmokeKey value, JsonSerializerOptions options) =>
        writer.WritePropertyName("key:" + value.Value);
}

internal sealed class SmokeReferenceHandler : ReferenceHandler
{
    public override ReferenceResolver CreateResolver() => new SmokeReferenceResolver();
}

internal sealed class SmokeReferenceResolver : ReferenceResolver
{
    private readonly Dictionary<string, object> _readReferences = new(StringComparer.Ordinal);
    private readonly Dictionary<object, string> _writeReferences = new(ReferenceEqualityComparer.Instance);

    public override void AddReference(string referenceId, object value) => _readReferences.Add(referenceId, value);

    public override object ResolveReference(string referenceId) => _readReferences[referenceId];

    public override string GetReference(object value, out bool alreadyExists)
    {
        alreadyExists = _writeReferences.TryGetValue(value, out string? id);
        if (alreadyExists)
        {
            return id!;
        }
        id = (_writeReferences.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        _writeReferences.Add(value, id);
        return id;
    }
}

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(LateBoundRecordBase))]
[JsonSerializable(typeof(LateBoundRecord))]
[JsonSerializable(typeof(LateBoundRejectedNullRecord))]
[JsonSerializable(typeof(LateBoundStructPayload))]
[JsonSerializable(typeof(LateBoundNestedRecordBase))]
[JsonSerializable(typeof(LateBoundNestedRecord))]
[JsonSerializable(typeof(SmokeCollectionRecordBase))]
[JsonSerializable(typeof(SmokeCollectionRecord))]
[JsonSerializable(typeof(SmokeKeyRecordBase))]
[JsonSerializable(typeof(SmokeKeyRecord))]
[JsonSerializable(typeof(Dictionary<SmokeKey, SmokeKey>))]
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
