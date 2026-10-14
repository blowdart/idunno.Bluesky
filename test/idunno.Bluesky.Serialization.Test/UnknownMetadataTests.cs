// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Chat;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Feed.Gates;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class UnknownMetadataTests
{
    private const string PostJson = """
        {"$type":"app.bsky.feed.post","$via":"kazahana","text":"hello","createdAt":"2026-10-13T10:24:57.970Z","langs":["ja"]}
        """;

    private const string Uri = "at://did:plc:heztswuhu24kudwdaz7y4bi3/app.bsky.feed.post/3mxqswzprj42r";
    private const string Cid = "bafyreififokk7ii62nxnsoldrnm33bro62a2k36xp5fhfhgptuanobuwuq";
    private const string Author = """{"did":"did:plc:heztswuhu24kudwdaz7y4bi3","handle":"example.test"}""";

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void EverySdkOptionsSurfaceReadsUnknownDollarProperties(int surface)
    {
        JsonSerializerOptions options = surface switch
        {
            0 => BlueskyJsonSerializerOptions.Default,
            1 => BlueskyJsonSerializerOptions.Options,
            2 => BlueskyServer.BlueskyJsonSerializerOptions,
            3 => new BlueskyAgentOptions().HttpJsonOptions!.JsonSerializerOptions,
            4 => AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(BlueskyJsonSerializerOptions.TypeInfoResolver),
            5 => AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions(BlueskyJsonSerializerOptions.Default),
            6 => AtProtoServer.BuildChainedTypeInfoResolverJsonSerializerOptions([BlueskyJsonSerializerOptions.Default]),
            _ => throw new ArgumentOutOfRangeException(nameof(surface))
        };

        Post post = Assert.IsType<Post>(JsonSerializer.Deserialize<Post>(PostJson, options));
        AssertPost(post);
        AssertPost(Assert.IsType<Post>(JsonSerializer.Deserialize<BlueskyRecord>(PostJson, options)));

        string envelopeJson = $$"""{"uri":"{{Uri}}","cid":"{{Cid}}","value":{{PostJson}}}""";
        AtProtoRepositoryRecord<Post> envelope = Assert.IsType<AtProtoRepositoryRecord<Post>>(
            JsonSerializer.Deserialize<AtProtoRepositoryRecord<Post>>(envelopeJson, options));
        AssertPost(envelope.Value);

        using JsonDocument serialized = JsonDocument.Parse(JsonSerializer.Serialize(post, options));
        Assert.Equal("app.bsky.feed.post", serialized.RootElement.GetProperty("$type").GetString());
        Assert.Equal("kazahana", serialized.RootElement.GetProperty("$via").GetString());
    }

    [Theory]
    [InlineData("\"kazahana\"")]
    [InlineData("123")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("""{"$nested":"value","items":[1,null,true]}""")]
    [InlineData("""["kazahana",{"$type":"arbitrary","$via":"untouched"}]""")]
    public void ExtensionValuesAndUnusualPropertyNamesArePreserved(string value)
    {
        string json = $$"""
            {"$via":{{value}},"\u0000$via":"original","$bytes":{"$bytes":"aGVsbG8="},"$link":"extension","$ref":"ordinary","text":"hello","createdAt":"2026-10-13T10:24:57.970Z","$type":"app.bsky.feed.post"}
            """;
        Post post = Assert.IsType<Post>(JsonSerializer.Deserialize<Post>(json, BlueskyJsonSerializerOptions.Default));

        Assert.Equal(value, post.ExtensionData!["$via"].GetRawText());
        Assert.Equal("original", post.ExtensionData["\0$via"].GetString());
        Assert.Equal("aGVsbG8=", post.ExtensionData["$bytes"].GetProperty("$bytes").GetString());
        Assert.Equal("extension", post.ExtensionData["$link"].GetString());
        Assert.Equal("ordinary", post.ExtensionData["$ref"].GetString());
        Assert.DoesNotContain("$type", post.ExtensionData.Keys);
    }

    [Theory]
    [InlineData("\"$example\"", "$example")]
    [InlineData("\"$\"", "$")]
    [InlineData("\"$$\"", "$$")]
    [InlineData("\"$TYPE\"", "$TYPE")]
    [InlineData("\"$typeExtra\"", "$typeExtra")]
    [InlineData("\"\\u0024example\"", "$example")]
    [InlineData("\"\\u0024\"", "$")]
    [InlineData("\"$\\u65e5\\u672c\"", "$\u65e5\u672c")]
    [InlineData("\"$\\ud83d\\ude00\"", "$\ud83d\ude00")]
    [InlineData("\"$\\\"\\\\\\u0000\"", "$\"\\\0")]
    public void ArbitraryDollarNamesAreRetainedOrDiscardedAccordingToTheContract(string encodedName, string name)
    {
        foreach (string value in new[]
        {
            "\"kazahana\"", "123", "true", "null",
            """{"$example":"nested","$":[1,null,true]}""",
            """["kazahana",{"$example":"nested"}]"""
        })
        {
            foreach (bool discriminatorFirst in new[] { false, true })
            {
                string properties = $$"""
                    {{encodedName}}:{{value}},"text":"hello","createdAt":"2026-10-13T10:24:57.970Z"
                    """;
                string json = discriminatorFirst
                    ? $$"""{"$type":"app.bsky.feed.post",{{properties}}}"""
                    : $$"""{ {{properties}},"$type":"app.bsky.feed.post"}""";

                Post post = Assert.IsType<Post>(JsonSerializer.Deserialize<BlueskyRecord>(json, BlueskyJsonSerializerOptions.Default));
                Assert.Equal(value, post.ExtensionData![name].GetRawText());
                Assert.Single(post.ExtensionData);
                string serialized = JsonSerializer.Serialize<BlueskyRecord>(post, BlueskyJsonSerializerOptions.Default);
                using JsonDocument document = JsonDocument.Parse(serialized);
                Assert.Equal(value, document.RootElement.GetProperty(name).GetRawText());
                Assert.Equal("app.bsky.feed.post", document.RootElement.GetProperty("$type").GetString());
                Post roundTripped = Assert.IsType<Post>(JsonSerializer.Deserialize<BlueskyRecord>(serialized, BlueskyJsonSerializerOptions.Default));
                Assert.Equal(value, roundTripped.ExtensionData![name].GetRawText());

                string ruleProperties = $$"""
                    {{encodedName}}:{{value}},"list":"at://did:plc:heztswuhu24kudwdaz7y4bi3/app.bsky.graph.list/test"
                    """;
                string ruleJson = discriminatorFirst
                    ? $$"""{"$type":"app.bsky.feed.threadgate#listRule",{{ruleProperties}}}"""
                    : $$"""{ {{ruleProperties}},"$type":"app.bsky.feed.threadgate#listRule"}""";
                ListRule rule = Assert.IsType<ListRule>(JsonSerializer.Deserialize<ThreadGateRule>(ruleJson, BlueskyJsonSerializerOptions.Default));
                using JsonDocument ruleDocument = JsonDocument.Parse(JsonSerializer.Serialize<ThreadGateRule>(rule, BlueskyJsonSerializerOptions.Default));
                Assert.False(ruleDocument.RootElement.TryGetProperty(name, out _));
                Assert.Equal("app.bsky.feed.threadgate#listRule", ruleDocument.RootElement.GetProperty("$type").GetString());
            }
        }
    }

    [Fact]
    public void GeneratedDollarNamesRoundTripTogetherWithoutCollisions()
    {
        Random random = new(1729);
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789$_";
        Dictionary<string, string> expected = new(StringComparer.Ordinal)
        {
            ["$"] = "bare",
            ["$example"] = "example",
            ["$$"] = "double",
            ["\0$example"] = "one prefix",
            ["\0\0$example"] = "two prefixes"
        };
        for (int i = 0; i < 64; i++)
        {
            string suffix = new(Enumerable.Range(0, random.Next(1, 40))
                .Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
            expected.Add($"${i}_{suffix}", $"value-{i}");
        }

        JsonObject json = JsonNode.Parse(PostJson)!.AsObject();
        json.Remove("$via");
        foreach (KeyValuePair<string, string> property in expected)
        {
            json[property.Key] = property.Value;
        }

        Post post = Assert.IsType<Post>(JsonSerializer.Deserialize<Post>(json, BlueskyJsonSerializerOptions.Default));
        string serialized = JsonSerializer.Serialize(post, BlueskyJsonSerializerOptions.Default);
        Post roundTripped = Assert.IsType<Post>(JsonSerializer.Deserialize<Post>(serialized, BlueskyJsonSerializerOptions.Default));
        using JsonDocument document = JsonDocument.Parse(serialized);
        Assert.Equal(expected.Count, post.ExtensionData!.Count);
        Assert.Equal(expected.Count, roundTripped.ExtensionData!.Count);
        foreach (KeyValuePair<string, string> property in expected)
        {
            Assert.Equal(property.Value, post.ExtensionData[property.Key].GetString());
            Assert.Equal(property.Value, document.RootElement.GetProperty(property.Key).GetString());
            Assert.Equal(property.Value, roundTripped.ExtensionData[property.Key].GetString());
        }
    }

    [Theory]
    [InlineData(64)]
    [InlineData(8192)]
    [InlineData(16384)]
    public void LongLeadingNullNamesArePreservedWithoutQuadraticAllocations(int leadingNulls)
    {
        string name = new string('\0', leadingNulls) + "$example";
        JsonObject json = JsonNode.Parse(PostJson)!.AsObject();
        json[name] = "original";
        json["$example"] = new JsonArray(new JsonObject
        {
            [new string('\0', leadingNulls + 1)] = "nested"
        });
        string payload = json.ToJsonString();

        _ = JsonSerializer.Deserialize<Post>(PostJson, BlueskyJsonSerializerOptions.Default);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Post post = Assert.IsType<Post>(JsonSerializer.Deserialize<Post>(payload, BlueskyJsonSerializerOptions.Default));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < 16 * 1024 * 1024, $"Decoding allocated {allocated} bytes for {leadingNulls} leading NULs.");
        Assert.Equal("original", post.ExtensionData![name].GetString());
        Assert.Equal("nested", post.ExtensionData["$example"][0].GetProperty(new string('\0', leadingNulls + 1)).GetString());
        Assert.Equal("kazahana", post.ExtensionData["$via"].GetString());
        string serialized = JsonSerializer.Serialize(post, BlueskyJsonSerializerOptions.Default);
        Post roundTripped = Assert.IsType<Post>(JsonSerializer.Deserialize<Post>(serialized, BlueskyJsonSerializerOptions.Default));
        Assert.Equal("original", roundTripped.ExtensionData![name].GetString());
        Assert.Equal("nested", roundTripped.ExtensionData["$example"][0].GetProperty(new string('\0', leadingNulls + 1)).GetString());
        Assert.Equal("kazahana", roundTripped.ExtensionData["$via"].GetString());
    }

    [Fact]
    public void NestedFeedAndQuotedPostRecordsAreTolerant()
    {
        string quotedRecord = $$"""
            {"$type":"app.bsky.embed.record#viewRecord","$via":"quote","uri":"{{Uri}}","cid":"{{Cid}}","author":{{Author}},"value":{{PostJson}},"indexedAt":"2026-10-13T10:24:57.970Z"}
            """;
        string json = $$$"""
            {"post":{"uri":"{{{Uri}}}","cid":"{{{Cid}}}","author":{{{Author}}},"record":{{{PostJson}}},"indexedAt":"2026-10-13T10:24:57.970Z","embed":{"$type":"app.bsky.embed.record#view","$via":"embed","record":{{{quotedRecord}}} } } }
            """;
        FeedViewPost feed = Assert.IsType<FeedViewPost>(JsonSerializer.Deserialize<FeedViewPost>(json, BlueskyJsonSerializerOptions.Default));

        AssertPost(feed.Post.Record);
        EmbeddedRecordView embed = Assert.IsType<EmbeddedRecordView>(feed.Post.Embed);
        ViewRecord quoted = Assert.IsType<ViewRecord>(embed.Record);
        Assert.Equal("quote", quoted.ExtensionData!["$via"].GetString());
        AssertPost(Assert.IsType<Post>(quoted.Value));
    }

    [Fact]
    public void MultipleDollarFieldsStayWithTheirOwningNestedRecords()
    {
        string innerPost = PostJson.Replace("\"$via\":\"kazahana\"",
            "\"$example\":\"inner\",\"$\":null,\"$$\":[1,true]", StringComparison.Ordinal);
        string quotedRecord = $$"""
            {"$type":"app.bsky.embed.record#viewRecord","$example":"quote","$":{"$example":"opaque"},"uri":"{{Uri}}","cid":"{{Cid}}","author":{{Author}},"value":{{innerPost}},"indexedAt":"2026-10-13T10:24:57.970Z"}
            """;
        string outerPost = PostJson.Replace("\"$via\":\"kazahana\"",
            "\"$example\":\"outer\",\"$\":123", StringComparison.Ordinal);
        string json = $$$"""
            {"post":{"uri":"{{{Uri}}}","cid":"{{{Cid}}}","author":{{{Author}}},"record":{{{outerPost}}},"indexedAt":"2026-10-13T10:24:57.970Z","embed":{"$type":"app.bsky.embed.record#view","$example":"embed","$":[{"$example":"opaque nested"}],"record":{{{quotedRecord}}} } } }
            """;

        FeedViewPost feed = Assert.IsType<FeedViewPost>(JsonSerializer.Deserialize<FeedViewPost>(json, BlueskyJsonSerializerOptions.Default));
        string serialized = JsonSerializer.Serialize(feed, BlueskyJsonSerializerOptions.Default);
        FeedViewPost roundTripped = Assert.IsType<FeedViewPost>(JsonSerializer.Deserialize<FeedViewPost>(serialized, BlueskyJsonSerializerOptions.Default));
        foreach (FeedViewPost item in new[] { feed, roundTripped })
        {
            Assert.Equal("outer", item.Post.Record.ExtensionData!["$example"].GetString());
            Assert.Equal(123, item.Post.Record.ExtensionData["$"].GetInt32());
            EmbeddedRecordView embeddedView = Assert.IsType<EmbeddedRecordView>(item.Post.Embed);
            Assert.Equal("embed", embeddedView.ExtensionData!["$example"].GetString());
            Assert.Equal("opaque nested", embeddedView.ExtensionData["$"][0].GetProperty("$example").GetString());
            ViewRecord quote = Assert.IsType<ViewRecord>(embeddedView.Record);
            Assert.Equal("quote", quote.ExtensionData!["$example"].GetString());
            Assert.Equal("opaque", quote.ExtensionData["$"].GetProperty("$example").GetString());
            Post inner = Assert.IsType<Post>(quote.Value);
            Assert.Equal("inner", inner.ExtensionData!["$example"].GetString());
            Assert.Equal(JsonValueKind.Null, inner.ExtensionData["$"].ValueKind);
            Assert.Equal("[1,true]", inner.ExtensionData["$$"].GetRawText());
        }
        using JsonDocument document = JsonDocument.Parse(serialized);
        JsonElement embed = document.RootElement.GetProperty("post").GetProperty("embed");
        Assert.Equal("embed", embed.GetProperty("$example").GetString());
        Assert.Equal("opaque nested", embed.GetProperty("$")[0].GetProperty("$example").GetString());
    }

    [Fact]
    public void NestedImagesKeepCidLinksAndDispatchWhileDiscardingUnknownProperties()
    {
        JsonObject postJson = JsonNode.Parse(PostJson)!.AsObject();
        postJson["embed"] = JsonNode.Parse($$$"""
            {"$type":"app.bsky.embed.images","$via":"image","images":[{"alt":"hello","image":{"$type":"blob","$via":"blob","ref":{"$link":"{{{Cid}}}"},"mimeType":"image/png","size":10}}]}
            """);
        Post post = Assert.IsType<Post>(JsonSerializer.Deserialize<Post>(postJson, BlueskyJsonSerializerOptions.Default));

        EmbeddedImages embed = Assert.IsType<EmbeddedImages>(post.EmbeddedRecord);
        Assert.Equal(Cid, Assert.Single(embed.Images).Image.Reference.Link.Value);
        AssertPost(post);
    }

    [Fact]
    public void CustomMessageConvertersStillDecodeNestedPolymorphicRecords()
    {
        string quotedRecord = $$"""
            {"$type":"app.bsky.embed.record#viewRecord","$via":"quote","uri":"{{Uri}}","cid":"{{Cid}}","author":{{Author}},"value":{{PostJson}},"indexedAt":"2026-10-13T10:24:57.970Z"}
            """;
        string json = $$$"""
            {"$type":"chat.bsky.convo.defs#logCreateMessage","$via":"log","convoId":"conversation","rev":"1","relatedProfiles":[],
             "message":{"$type":"chat.bsky.convo.defs#messageView","$via":"message","id":"message","rev":"1","text":"hello",
              "sender":{"did":"did:plc:heztswuhu24kudwdaz7y4bi3"},"sentAt":"2026-10-13T10:24:57.970Z",
              "embed":{"$type":"app.bsky.embed.record#view","$via":"embed","record":{{{quotedRecord}}} } } }
            """;

        CreateMessage log = Assert.IsType<CreateMessage>(JsonSerializer.Deserialize<LogBase>(json, BlueskyJsonSerializerOptions.Default));
        MessageView message = Assert.IsType<MessageView>(log.Message);
        Assert.Equal("message", message.ExtensionData!["$via"].GetString());
        EmbeddedRecordView embed = Assert.IsType<EmbeddedRecordView>(message.EmbeddedView);
        AssertPost(Assert.IsType<Post>(Assert.IsType<ViewRecord>(embed.Record).Value));
    }

    [Fact]
    public void UnknownDollarPropertiesAreDiscardedWithoutExtensionData()
    {
        const string json = """
            {"$via":"kazahana","$example":{"$nested":[1,null,true]},"$":[{"$example":"nested"}],"$$":false,"list":"at://did:plc:heztswuhu24kudwdaz7y4bi3/app.bsky.graph.list/test","$type":"app.bsky.feed.threadgate#listRule"}
            """;
        ListRule rule = Assert.IsType<ListRule>(JsonSerializer.Deserialize<ThreadGateRule>(json, BlueskyJsonSerializerOptions.Default));
        using JsonDocument serialized = JsonDocument.Parse(JsonSerializer.Serialize<ThreadGateRule>(rule, BlueskyJsonSerializerOptions.Default));

        Assert.False(serialized.RootElement.TryGetProperty("$via", out _));
        Assert.False(serialized.RootElement.TryGetProperty("$example", out _));
        Assert.False(serialized.RootElement.TryGetProperty("$", out _));
        Assert.False(serialized.RootElement.TryGetProperty("$$", out _));
        Assert.Equal("app.bsky.feed.threadgate#listRule", serialized.RootElement.GetProperty("$type").GetString());
    }

    [Fact]
    public void UnknownDiscriminatorStillFallsBackToTheBaseContract()
    {
        const string json = """{"$type":"example.unknown.record","$via":"kazahana","payload":{"$via":"nested"}}""";
        BlueskyRecord record = Assert.IsType<BlueskyRecord>(JsonSerializer.Deserialize<BlueskyRecord>(json, BlueskyJsonSerializerOptions.Default));

        Assert.Equal("kazahana", record.ExtensionData!["$via"].GetString());
        Assert.Equal("nested", record.ExtensionData["payload"].GetProperty("$via").GetString());
    }

    [Theory]
    [InlineData("""{"$type":"wrong.type","$via":"kazahana","text":"hello","createdAt":"2026-10-13T10:24:57.970Z"}""")]
    [InlineData("""{"$type":false,"$via":"kazahana","text":"hello","createdAt":"2026-10-13T10:24:57.970Z"}""")]
    [InlineData("""{"$type":"app.bsky.feed.post","$type":"app.bsky.feed.post","$via":"kazahana","text":"hello","createdAt":"2026-10-13T10:24:57.970Z"}""")]
    [InlineData("""{"$type":"app.bsky.feed.post","$via":"kazahana","text":null,"createdAt":"2026-10-13T10:24:57.970Z"}""")]
    [InlineData("""{"$type":"app.bsky.feed.post","$via":"kazahana","createdAt":"2026-10-13T10:24:57.970Z"}""")]
    public void ExistingDiscriminatorAndRequiredPropertyValidationIsPreserved(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Post>(json, BlueskyJsonSerializerOptions.Default));
    }

    [Fact]
    public void AtProtoOptionsAreTolerantAndBytesAreUnchanged()
    {
        string json = $$"""{"$type":"blob","$via":"kazahana","ref":{"$link":"{{Cid}}"},"mimeType":"image/png","size":10}""";
        foreach (JsonSerializerOptions options in new[]
        {
            AtProtoJsonSerializerOptions.Default,
            AtProtoJsonSerializerOptions.Options,
            new JsonOptions().JsonSerializerOptions
        })
        {
            Blob blob = Assert.IsType<Blob>(JsonSerializer.Deserialize<Blob>(json, options));
            Assert.Equal(Cid, blob.Reference.Link.Value);
            Bytes bytes = Assert.IsType<Bytes>(JsonSerializer.Deserialize<Bytes>("""{"$bytes":"aGVsbG8="}""", options));
            Assert.Equal("aGVsbG8=", bytes.Base64EncodedValue);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StreamingDeserializationSupportsBothTheFastAndTolerantPaths(bool unknownProperty)
    {
        string json = unknownProperty ? PostJson : PostJson.Replace(",\"$via\":\"kazahana\"", string.Empty, StringComparison.Ordinal);
        JsonSerializerOptions options = BlueskyJsonSerializerOptions.Options;
        options.DefaultBufferSize = 8;
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));

        Post post = Assert.IsType<Post>(await JsonSerializer.DeserializeAsync<Post>(stream, options, TestContext.Current.CancellationToken));

        Assert.Equal("hello", post.Text);
        if (unknownProperty)
        {
            AssertPost(post);
        }
        else
        {
            Assert.Empty(post.ExtensionData!);
        }
    }

    private static void AssertPost(Post post)
    {
        Assert.Equal("hello", post.Text);
        Assert.Equal(new DateTimeOffset(2026, 10, 13, 10, 24, 57, 970, TimeSpan.Zero), post.CreatedAt);
        Assert.Equal("ja", Assert.Single(post.Langs!));
        Assert.Equal("kazahana", post.ExtensionData!["$via"].GetString());
    }
}
