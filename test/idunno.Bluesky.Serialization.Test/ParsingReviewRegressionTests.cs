// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Text.Json;

using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Chat;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Feed.Gates;

using GraphList = idunno.Bluesky.Graph.List;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class ParsingReviewRegressionTests
{
    private const string Blob = """
        {"$type":"blob","ref":{"$link":"bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"},"mimeType":"image/png","size":1}
        """;
    private const string CreatedAt = "\"createdAt\":\"2026-10-08T15:43:06Z\"";
    private const string PostUri = "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3ldegderd6s2e";

    [Theory]
    [InlineData("profile")]
    [InlineData("list")]
    [InlineData("link")]
    [InlineData("generator")]
    [InlineData("images")]
    [InlineData("gallery")]
    [InlineData("video")]
    [InlineData("caption")]
    [InlineData("external")]
    [InlineData("status")]
    public void BlobParsingRequiresAllStructuralFieldsInBothModes(string kind)
    {
        string[] malformed =
        [
            "{}",
            Blob.Replace("\"size\":1", "\"other\":1", StringComparison.Ordinal),
            Blob.Replace("\"mimeType\":\"image/png\"", "\"other\":\"image/png\"", StringComparison.Ordinal),
            Blob.Replace("\"ref\":", "\"other\":", StringComparison.Ordinal),
            Blob.Replace("\"ref\":{\"$link\":\"bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a\"}", "\"ref\":null", StringComparison.Ordinal),
            Blob.Replace("\"mimeType\":\"image/png\"", "\"mimeType\":null", StringComparison.Ordinal),
            Blob.Replace("\"size\":1", "\"size\":null", StringComparison.Ordinal),
            Blob.Replace("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a", "invalid-cid", StringComparison.Ordinal)
        ];
        foreach (string blob in malformed)
        {
            using JsonDocument document = JsonDocument.Parse(WithBlob(kind, blob));
            foreach (bool validate in new[] { true, false })
            {
                Assert.Throws<JsonException>(() => Parse(kind, document, validate));
            }
        }
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("list")]
    [InlineData("link")]
    [InlineData("generator")]
    [InlineData("images")]
    [InlineData("gallery")]
    [InlineData("external")]
    [InlineData("status")]
    public void BlobConstraintViolationsRemainInspectableWithoutChangingOrdinaryReads(string kind)
    {
        string blob = Blob.Replace("\"size\":1", "\"size\":-1", StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(WithBlob(kind, blob));
        Assert.Throws<JsonException>(() => Parse(kind, document, validate: true));
        IValidatableObject value = Parse(kind, document, validate: false);
        Assert.Contains(value.Validate(new ValidationContext(value)), error => error.MemberNames.Any(name => name.EndsWith(".Size", StringComparison.Ordinal)));
        Assert.NotNull(JsonSerializer.Deserialize(document.RootElement, value.GetType(), BlueskyJsonSerializerOptions.Default));
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("list")]
    [InlineData("link")]
    [InlineData("generator")]
    [InlineData("images")]
    [InlineData("gallery")]
    [InlineData("video")]
    [InlineData("caption")]
    [InlineData("external")]
    [InlineData("status")]
    public void CompleteBlobsStillParseInBothModes(string kind)
    {
        using JsonDocument document = JsonDocument.Parse(WithBlob(kind, Blob));
        foreach (bool validate in new[] { true, false })
        {
            IValidatableObject value = Parse(kind, document, validate);
            Assert.Empty(value.Validate(new ValidationContext(value)));
        }
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("list")]
    [InlineData("link")]
    [InlineData("generator")]
    [InlineData("status")]
    [InlineData("post")]
    [InlineData("starterPack")]
    [InlineData("threadGate")]
    [InlineData("postGate")]
    public void DollarPrefixedExtensionsSurviveBothParsingModes(string kind)
    {
        string json = BasicRecord(kind).Insert(1, "\"$via\":{\"source\":\"client\"},");
        using JsonDocument document = JsonDocument.Parse(json);
        foreach (bool validate in new[] { true, false })
        {
            AtProtoRecord value = Assert.IsAssignableFrom<AtProtoRecord>(Parse(kind, document, validate));
            Assert.Equal("client", value.ExtensionData["$via"].GetProperty("source").GetString());
            Assert.DoesNotContain("$type", value.ExtensionData.Keys);
        }
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("list")]
    [InlineData("link")]
    [InlineData("generator")]
    [InlineData("status")]
    [InlineData("post")]
    [InlineData("starterPack")]
    [InlineData("threadGate")]
    [InlineData("postGate")]
    public void NonStringDiscriminatorsAreRejectedInBothModes(string kind)
    {
        foreach (string discriminator in new[] { "42", "null", "true", "{}", "[]" })
        {
            using JsonDocument document = JsonDocument.Parse(BasicRecord(kind).Insert(1, $"\"$type\":{discriminator},"));
            Assert.Throws<JsonException>(() => Parse(kind, document, validate: true));
            Assert.Throws<JsonException>(() => Parse(kind, document, validate: false));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedMediaRetainsUnknownMetadataInBothModes(bool invalidDimensions)
    {
        string embed = $$$"""
            {"$type":"app.bsky.embed.images","$via":{"value":42},"images":[{"image":{{{Blob}}},"alt":"","aspectRatio":{"width":{{{(invalidDimensions ? 0 : 1)}}},"height":1}}]}
            """;
        using JsonDocument document = JsonDocument.Parse(PostWithEmbed(embed));
        foreach (bool validate in new[] { true, false })
        {
            if (validate && invalidDimensions)
            {
                Assert.False(Post.TryParse(document, out _));
            }
            else
            {
                Post post = Post.Parse(document, validate);
                Assert.Equal(42, post.EmbeddedRecord!.ExtensionData["$via"].GetProperty("value").GetInt32());
            }
        }
    }

    [Theory]
    [InlineData("42")]
    [InlineData("null")]
    [InlineData("{}")]
    public void TolerantExternalPropertiesDoNotDiscardMalformedDiscriminators(string discriminator)
    {
        string embed = $$$"""
            {"$type":"app.bsky.embed.external","external":{"$type":{{{discriminator}}},"uri":" ","title":"","description":""}}
            """;
        using JsonDocument document = JsonDocument.Parse(PostWithEmbed(embed));
        Assert.False(Post.TryParse(document, out _));
        Assert.False(Post.TryParse(document, validate: false, out _));
    }

    [Fact]
    public void ExternalPropertiesRetainExistingUnknownStringDiscriminatorHandling()
    {
        using JsonDocument document = JsonDocument.Parse(PostWithEmbed("""
            {"$type":"app.bsky.embed.external","external":{"$type":"example.test#future","$via":"client","uri":" ","title":"","description":""}}
            """));
        Assert.True(Post.TryParse(document, validate: false, out _));
    }

    [Theory]
    [InlineData("""{"messageId":null}""")]
    [InlineData("{}")]
    public void ReplyReferencesRequireNonNullMessageIdsInBothModes(string reply)
    {
        using JsonDocument document = JsonDocument.Parse($$"""{"text":"","replyTo":{{reply}}}""");
        Assert.False(MessageInput.TryParse(document, out _));
        Assert.False(MessageInput.TryParse(document, validate: false, out _));
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(11, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void GalleryValidationUsesSchemaLimitWithoutChangingAuthoringLimit(int count, bool valid)
    {
        string item = $$$"""{"$type":"app.bsky.embed.gallery#image","image":{{{Blob}}},"alt":"","aspectRatio":{"width":1,"height":1}}""";
        using JsonDocument document = JsonDocument.Parse(PostWithEmbed($$"""{"$type":"app.bsky.embed.gallery","items":[{{string.Join(',', Enumerable.Repeat(item, count))}}]}"""));
        Assert.Equal(valid, Post.TryParse(document, out _));
        Post post = Post.Parse(document, validate: false);
        Assert.Equal(!valid, post.Validate(new ValidationContext(post)).Any());
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void PostLanguageCountMatchesLexicon(int count, bool valid)
    {
        using JsonDocument document = JsonDocument.Parse(BasicRecord("post").Insert(1, $"\"langs\":[{string.Join(',', Enumerable.Repeat("\"en\"", count))}],"));
        Assert.Equal(valid, Post.TryParse(document, out _));
        Post post = Post.Parse(document, validate: false);
        Assert.Equal(!valid, post.Validate(new ValidationContext(post)).Any(result => result.MemberNames.Contains(nameof(Post.Langs))));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MessageJoinLinkEmbedsParseAndValidate(bool validate)
    {
        using JsonDocument document = JsonDocument.Parse("""
            {"text":"join","embed":{"$type":"chat.bsky.embed.joinLink","code":"example"}}
            """);
        Assert.True(MessageInput.TryParse(document, validate, out MessageInput? message));
        Chat.Embed.JoinLink embed = Assert.IsType<Chat.Embed.JoinLink>(message.Embed);
        Assert.Equal("example", embed.Code);
        Assert.Empty(message.Validate(new ValidationContext(message)));
        MessageInput constructed = new("join") { Embed = new Chat.Embed.JoinLink("example") };
        Assert.Empty(constructed.Validate(new ValidationContext(constructed)));
    }

    [Theory]
    [InlineData("threadGate", false)]
    [InlineData("postGate", false)]
    [InlineData("threadGate", true)]
    [InlineData("postGate", true)]
    public void GateTargetsRequireRecordKeysWithoutPreventingTolerantParsing(string kind, bool hasRecordKey)
    {
        string uri = hasRecordKey ? PostUri : PostUri[..PostUri.LastIndexOf('/')];
        using JsonDocument document = JsonDocument.Parse($$"""{"post":"{{uri}}",{{CreatedAt}}}""");
        if (kind == "threadGate")
        {
            Assert.Equal(hasRecordKey, ThreadGate.TryParse(document, out _));
        }
        else
        {
            Assert.Equal(hasRecordKey, PostGate.TryParse(document, out _));
        }

        IValidatableObject gate = Parse(kind, document, validate: false);
        Assert.Equal(!hasRecordKey, gate.Validate(new ValidationContext(gate)).Any(result => result.MemberNames.Contains("Post")));
        Assert.Equal(uri, gate is ThreadGate thread ? thread.Post.ToString() : ((PostGate)gate).Post.ToString());
    }

    private static IValidatableObject Parse(string kind, JsonDocument document, bool validate) => kind switch
    {
        "profile" => Profile.Parse(document, validate),
        "list" => GraphList.Parse(document, validate),
        "link" => Actor.Link.Parse(document, validate),
        "generator" => Generator.Parse(document, validate),
        "status" => Status.Parse(document, validate),
        "starterPack" => Graph.StarterPack.Parse(document, validate),
        "threadGate" => ThreadGate.Parse(document, validate),
        "postGate" => PostGate.Parse(document, validate),
        _ => Post.Parse(document, validate)
    };

    private static string BasicRecord(string kind) => kind switch
    {
        "profile" => """{"displayName":"name"}""",
        "list" => $$"""{"name":"list","purpose":"app.bsky.graph.defs#curatelist",{{CreatedAt}}}""",
        "link" => $$"""{"url":"https://example.com",{{CreatedAt}}}""",
        "generator" => $$"""{"did":"did:plc:ec72yg6n2sydzjvtovvdlxrk","displayName":"feed",{{CreatedAt}}}""",
        "status" => $$"""{"status":"example.test#future",{{CreatedAt}}}""",
        "starterPack" => $$"""{"name":"pack","list":"{{PostUri}}",{{CreatedAt}}}""",
        "threadGate" or "postGate" => $$"""{"post":"{{PostUri}}",{{CreatedAt}}}""",
        _ => $$"""{"text":"",{{CreatedAt}}}"""
    };

    private static string WithBlob(string kind, string blob) => kind switch
    {
        "profile" or "list" or "generator" => BasicRecord(kind).Insert(1, $"\"avatar\":{blob},"),
        "link" => BasicRecord(kind).Insert(1, $"\"icon\":{blob},"),
        "images" => PostWithEmbed($$"""{"$type":"app.bsky.embed.images","images":[{"image":{{blob}},"alt":""}]}"""),
        "gallery" => PostWithEmbed($$$"""{"$type":"app.bsky.embed.gallery","items":[{"$type":"app.bsky.embed.gallery#image","image":{{{blob}}},"alt":"","aspectRatio":{"width":1,"height":1}}]}"""),
        "video" => PostWithEmbed($$"""{"$type":"app.bsky.embed.video","video":{{blob.Replace("image/png", "video/mp4", StringComparison.Ordinal)}}}"""),
        "caption" => PostWithEmbed($$"""{"$type":"app.bsky.embed.video","video":{{Blob.Replace("image/png", "video/mp4", StringComparison.Ordinal)}},"captions":[{"lang":"en","file":{{blob.Replace("image/png", "text/vtt", StringComparison.Ordinal)}}}]}"""),
        "status" => BasicRecord("status").Insert(1, $"\"embed\":{External(blob)},"),
        "external" => PostWithEmbed(External(blob)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static string External(string blob) => $$$"""
        {"$type":"app.bsky.embed.external","external":{"uri":"https://example.com","title":"","description":"","thumb":{{{blob}}}}}
        """;

    private static string PostWithEmbed(string embed) => BasicRecord("post").Insert(1, $"\"embed\":{embed},");
}
