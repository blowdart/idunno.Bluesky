// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Text.Json;

using idunno.AtProto.Repo;
using idunno.Bluesky.Chat;
using idunno.Bluesky.Drafts;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Feed.Gates;
using idunno.Bluesky.Graph;

using ActorLink = idunno.Bluesky.Actor.Link;
using ActorStatus = idunno.Bluesky.Actor.Status;
using GraphList = idunno.Bluesky.Graph.List;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class RecordValidationTests
{
    private const string PostUri = "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3ldegderd6s2e";
    private const string ListUri = "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.list/3ldegderd6s2e";
    private const string CreatedAt = "2026-10-08T15:43:06Z";
    private const string ImageBlob = """
        {"$type":"blob","ref":{"$link":"bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"},"mimeType":"image/png","size":100000}
        """;

    [Theory]
    [InlineData("list", "Name")]
    [InlineData("starterPack", "Name")]
    [InlineData("threadGate", "Post")]
    [InlineData("postGate", "Post")]
    [InlineData("message", "Text")]
    [InlineData("draft", "Posts")]
    [InlineData("draftPost", "Text")]
    [InlineData("actorLink", "Title")]
    [InlineData("status", "DurationMinutes")]
    [InlineData("generator", "DisplayName")]
    public void ParsingModesPreserveInvalidValuesWithoutWeakeningStrictReads(string kind, string member)
    {
        using JsonDocument document = JsonDocument.Parse(CreateJson(kind, invalid: true));

        Assert.Null(TryDocument(kind, document, validate: true));
        Assert.Null(TryElement(kind, document.RootElement, validate: true));
        Assert.Throws<JsonException>(() => ParseDocument(kind, document));
        Assert.Throws<JsonException>(() => ParseElement(kind, document.RootElement));

        IValidatableObject value = ParseDocument(kind, document, validate: false);
        Assert.Contains(value.Validate(new ValidationContext(value)), result => result.MemberNames.Contains(member));
        Assert.NotNull(ParseElement(kind, document.RootElement, validate: false));
        Assert.NotNull(TryDocument(kind, document, validate: false));
        Assert.NotNull(TryElement(kind, document.RootElement, validate: false));

        Assert.Null(TryDocument(kind, document, validate: true));
        using JsonDocument validDocument = JsonDocument.Parse(CreateJson(kind, invalid: false));
        IValidatableObject valid = ParseDocument(kind, validDocument);
        Assert.Empty(valid.Validate(new ValidationContext(valid)));
        Assert.NotNull(ParseElement(kind, validDocument.RootElement));
        Assert.NotNull(TryDocument(kind, validDocument, validate: true));
        Assert.NotNull(TryElement(kind, validDocument.RootElement, validate: true));
    }

    [Theory]
    [InlineData("list")]
    [InlineData("starterPack")]
    [InlineData("threadGate")]
    [InlineData("postGate")]
    [InlineData("message")]
    [InlineData("draft")]
    [InlineData("draftPost")]
    [InlineData("actorLink")]
    [InlineData("status")]
    [InlineData("generator")]
    public void ParsingRejectsMalformedShapesAndMissingRequiredMembers(string kind)
    {
        foreach (string json in new[] { "null", "[]", "1", "\"text\"", "{}" })
        {
            using JsonDocument document = JsonDocument.Parse(json);
            foreach (bool validate in new[] { true, false })
            {
                Assert.Null(TryDocument(kind, document, validate));
                Assert.Null(TryElement(kind, document.RootElement, validate));
                Assert.Throws<JsonException>(() => ParseDocument(kind, document, validate));
                Assert.Throws<JsonException>(() => ParseElement(kind, document.RootElement, validate));
                Assert.Null(TryElement(kind, default, validate));
            }
        }

        Assert.Null(TryDocument(kind, null, validate: true));
        Assert.Null(TryDocument(kind, null, validate: false));
        Assert.Throws<ArgumentNullException>(() => ParseDocument(kind, null!));
        Assert.Throws<ArgumentNullException>(() => ParseDocument(kind, null!, validate: false));
    }

    [Theory]
    [InlineData("list")]
    [InlineData("starterPack")]
    [InlineData("threadGate")]
    [InlineData("postGate")]
    [InlineData("actorLink")]
    [InlineData("status")]
    [InlineData("generator")]
    public void TolerantRecordsRetainExtensionDataBeyondDocumentLifetime(string kind)
    {
        AtProtoRecord record;
        using (JsonDocument document = JsonDocument.Parse(CreateJson(kind, invalid: true).Insert(1, "\"custom\":{\"value\":42},")))
        {
            record = Assert.IsType<AtProtoRecord>(ParseDocument(kind, document, validate: false), exactMatch: false);
        }

        Assert.Equal(42, record.ExtensionData["custom"].GetProperty("value").GetInt32());
        Assert.DoesNotContain("$type", record.ExtensionData.Keys);
    }

    [Fact]
    public void UnknownListPurposeIsRetainedAndNotTreatedAsInvalid()
    {
        using JsonDocument document = JsonDocument.Parse(CreateJson("list", invalid: false)
            .Replace("app.bsky.graph.defs#curatelist", "example.test#futurePurpose", StringComparison.Ordinal));

        foreach (bool validate in new[] { true, false })
        {
            GraphList list = GraphList.Parse(document, validate);
            Assert.Equal("example.test#futurePurpose", list.PurposeValue);
            Assert.Equal(ListPurpose.Unknown, list.Purpose);
            Assert.Empty(list.Validate(new ValidationContext(list)));
        }
    }

    [Theory]
    [InlineData("list", "DescriptionFacets[0].Index.ByteStart")]
    [InlineData("message", "Facets[0].Index.ByteStart")]
    [InlineData("generator", "DescriptionFacets[0].Index.ByteStart")]
    public void TolerantParsingCascadesThroughFacets(string kind, string member)
    {
        string property = kind is "list" or "generator" ? "descriptionFacets" : "facets";
        string json = CreateJson(kind, invalid: false).Insert(1,
            $"\"{property}\":" + """[{"index":{"byteStart":-1,"byteEnd":1},"features":[{"$type":"app.bsky.richtext.facet#tag","tag":"tag"}]}],""");
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.Null(TryDocument(kind, document, validate: true));
        IValidatableObject value = ParseDocument(kind, document, validate: false);
        Assert.Contains(value.Validate(new ValidationContext(value)), result => result.MemberNames.Contains(member));
    }

    [Theory]
    [InlineData("threadGate", "allow", 5, """{"$type":"app.bsky.feed.threadgate#mentionRule"}""", "Rules")]
    [InlineData("threadGate", "hiddenReplies", 300, "\"" + PostUri + "\"", "HiddenReplies")]
    [InlineData("postGate", "embeddingRules", 5, """{"$type":"app.bsky.feed.postgate#disableRule"}""", "Rules")]
    [InlineData("postGate", "detachedEmbeddingUris", 50, "\"" + PostUri + "\"", "DetachedEmbeddingUris")]
    [InlineData("starterPack", "feeds", 3, """{"uri":"at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.generator/3ldegderd6s2e"}""", "Feeds")]
    public void RecordCollectionLimitsAreValidatedWithoutRejectingTolerantReads(string kind, string property, int maximum, string entry, string member)
    {
        foreach (int count in new[] { 0, maximum, maximum + 1 })
        {
            string json = CreateJson(kind, invalid: false).Insert(1, $"\"{property}\":[{string.Join(',', Enumerable.Repeat(entry, count))}],");
            using JsonDocument document = JsonDocument.Parse(json);

            Assert.Equal(count <= maximum, TryDocument(kind, document, validate: true) is not null);
            IValidatableObject value = ParseDocument(kind, document, validate: false);
            Assert.Equal(count > maximum, value.Validate(new ValidationContext(value)).Any(result => result.MemberNames.Contains(member)));
        }
    }

    [Theory]
    [InlineData("""{"text":"","embedImages":[{"localRef":{"path":" "},"alt":""}]}""", "EmbedImages[0].LocalRef.Path")]
    [InlineData("""{"text":"","embedGallery":{"items":[{"localRef":{"path":" "}}]}}""", "EmbedGallery.Items[0].LocalRef.Path")]
    [InlineData("""{"text":"","embedVideos":[{"localRef":{"path":"video.mp4"},"captions":[{"lang":" ","content":""}]}]}""", "EmbedVideos[0].Captions[0].Lang")]
    public void NestedDraftFailuresHaveIndexedPaths(string postJson, string member)
    {
        using JsonDocument postDocument = JsonDocument.Parse(postJson);
        Assert.False(DraftPost.TryParse(postDocument, out _));
        DraftPost post = DraftPost.Parse(postDocument, validate: false);
        Assert.Contains(post.Validate(new ValidationContext(post)), result => result.MemberNames.Contains(member));

        using JsonDocument draftDocument = JsonDocument.Parse($$"""{"posts":[{{postJson}}]}""");
        Assert.False(Draft.TryParse(draftDocument, out _));
        Draft draft = Draft.Parse(draftDocument, validate: false);
        Assert.Contains(draft.Validate(new ValidationContext(draft)), result => result.MemberNames.Contains($"Posts[0].{member}"));
    }

    [Theory]
    [InlineData("""{"text":"","embedImages":[{"localRef":{"path":123}}]}""")]
    [InlineData("""{"text":"","embedImages":[{"localRef":{}}]}""")]
    [InlineData("""{"text":"","embedVideos":[{"localRef":{"path":"video.mp4"},"captions":[{"lang":"en"}]}]}""")]
    [InlineData("""{"text":"","embedGallery":{"items":null}}""")]
    public void TolerantDraftParsingStillRejectsInvalidNestedShapes(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.False(DraftPost.TryParse(document, validate: false, out DraftPost? post));
        Assert.Null(post);
        Assert.Throws<JsonException>(() => DraftPost.Parse(document, validate: false));
    }

    [Fact]
    public void OptionalDraftCollectionsMayBeEmptyAndGalleryUsesSchemaLimit()
    {
        using JsonDocument document = JsonDocument.Parse("""
            {"posts":[{"text":"","embedImages":[],"embedVideos":[],"embedExternals":[],"embedRecords":[]}],
             "langs":[],"postgateEmbeddingRules":[],"threadgateAllow":[]}
            """);
        Assert.Empty(Draft.Parse(document).Validate(new ValidationContext(Draft.Parse(document))));

        foreach (int count in new[] { 0, 10, 20, 21 })
        {
            string images = string.Join(',', Enumerable.Repeat("""{"localRef":{"path":"image.jpg"}}""", count));
            using JsonDocument galleryDocument = JsonDocument.Parse($$$"""{"text":"","embedGallery":{"items":[{{{images}}}]}}""");
            Assert.Equal(count <= 20, DraftPost.TryParse(galleryDocument, out _));
            DraftPost post = DraftPost.Parse(galleryDocument, validate: false);
            Assert.Equal(count > 20, post.Validate(new ValidationContext(post)).Any());
        }
    }

    [Theory]
    [InlineData("list", "name", 64)]
    [InlineData("list", "description", 3000)]
    [InlineData("starterPack", "name", 500)]
    [InlineData("starterPack", "description", 3000)]
    [InlineData("message", "text", 10000)]
    [InlineData("draftPost", "text", 10000)]
    [InlineData("draft", "deviceId", 100)]
    [InlineData("draft", "deviceName", 100)]
    [InlineData("actorLink", "title", 320)]
    [InlineData("generator", "displayName", 240)]
    [InlineData("generator", "description", 3000)]
    public void TextByteLimitsAreIndependentOfGraphemeLimits(string kind, string property, int maximum)
    {
        foreach (int bytes in new[] { maximum, maximum + 2 })
        {
            string text = "xx" + new string('\u0301', (bytes - 2) / 2);
            string json = CreateJson(kind, invalid: false);
            string literal = JsonSerializer.Serialize(text, SourceGenerationContext.Default.String);
            if (property is "name" or "text" or "displayName" or "title")
            {
                json = json.Replace($"\"{property}\":\"x\"", $"\"{property}\":{literal}", StringComparison.Ordinal);
            }
            else
            {
                json = json.Insert(1, $"\"{property}\":{literal},");
            }

            using JsonDocument document = JsonDocument.Parse(json);
            Assert.Equal(bytes <= maximum, TryDocument(kind, document, validate: true) is not null);
            IValidatableObject value = ParseDocument(kind, document, validate: false);
            Assert.Equal(bytes > maximum, value.Validate(new ValidationContext(value)).Any());
        }
    }

    [Theory]
    [InlineData("posts", 100, """{"text":""}""", "Posts")]
    [InlineData("langs", 3, "\"en\"", "Langs")]
    [InlineData("postgateEmbeddingRules", 5, """{"$type":"app.bsky.feed.postgate#disableRule"}""", "PostGateEmbeddingRules")]
    [InlineData("threadgateAllow", 5, """{"$type":"app.bsky.feed.threadgate#mentionRule"}""", "ThreadGateAllowRules")]
    public void DraftCollectionLimitsArePreservedForLaterValidation(string property, int maximum, string entry, string member)
    {
        foreach (int count in new[] { maximum, maximum + 1 })
        {
            string collection = $"\"{property}\":[{string.Join(',', Enumerable.Repeat(entry, count))}]";
            string json = property == "posts" ? $"{{{collection}}}" : $$"""{"posts":[{"text":""}],{{collection}}}""";
            using JsonDocument document = JsonDocument.Parse(json);

            Assert.Equal(count <= maximum, Draft.TryParse(document, out _));
            Draft draft = Draft.Parse(document, validate: false);
            Assert.Equal(count > maximum, draft.Validate(new ValidationContext(draft)).Any(result => result.MemberNames.Contains(member)));
        }
    }

    [Theory]
    [InlineData("embedImages", 4, """{"localRef":{"path":"image.jpg"}}""", "EmbedImages")]
    [InlineData("embedVideos", 1, """{"localRef":{"path":"video.mp4"}}""", "EmbedVideos")]
    [InlineData("embedExternals", 1, """{"uri":"https://example.com"}""", "EmbedExternals")]
    [InlineData("embedRecords", 1, """{"record":{"uri":"at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3ldegderd6s2e","cid":"bafyreiaahtgpdozb4wjdymb3tvifuwdmxjtumhjxl25lykvsoq3v3l4une"}}""", "EmbedRecords")]
    public void DraftPostMediaCountLimitsArePreservedForLaterValidation(string property, int maximum, string entry, string member)
    {
        foreach (int count in new[] { maximum, maximum + 1 })
        {
            string json = $$"""{"text":"","{{property}}":[{{string.Join(',', Enumerable.Repeat(entry, count))}}]}""";
            using JsonDocument document = JsonDocument.Parse(json);

            Assert.Equal(count <= maximum, DraftPost.TryParse(document, out _));
            DraftPost post = DraftPost.Parse(document, validate: false);
            Assert.Equal(count > maximum, post.Validate(new ValidationContext(post)).Any(result => result.MemberNames.Contains(member)));
        }
    }

    [Theory]
    [InlineData("imageAlt", 2000, "EmbedImages[0].AltText")]
    [InlineData("videoAlt", 2000, "EmbedVideos[0].AltText")]
    [InlineData("caption", 10000, "EmbedVideos[0].Captions[0].Content")]
    [InlineData("path", 1024, "EmbedImages[0].LocalRef.Path")]
    public void DraftNestedTextLimitsAreRetainedWithoutWeakeningOrdinaryDeserialization(string kind, int maximum, string member)
    {
        foreach (int length in new[] { maximum, maximum + 1 })
        {
            string text = new('x', length);
            string json = kind switch
            {
                "imageAlt" => $$"""{"text":"","embedImages":[{"localRef":{"path":"image.jpg"},"alt":"{{text}}"}]}""",
                "videoAlt" => $$"""{"text":"","embedVideos":[{"localRef":{"path":"video.mp4"},"alt":"{{text}}"}]}""",
                "caption" => $$$"""{"text":"","embedVideos":[{"localRef":{"path":"video.mp4"},"captions":[{"lang":"en","content":"{{{text}}}"}]}]}""",
                "path" => $$$"""{"text":"","embedImages":[{"localRef":{"path":"{{{text}}}"}}]}""",
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            using JsonDocument document = JsonDocument.Parse(json);
            Assert.Equal(length <= maximum, DraftPost.TryParse(document, out _));
            DraftPost post = DraftPost.Parse(document, validate: false);
            Assert.Equal(length > maximum, post.Validate(new ValidationContext(post)).Any(result => result.MemberNames.Contains(member)));
            if (length > maximum)
            {
                Exception? exception = Xunit.Record.Exception(() => JsonSerializer.Deserialize<DraftPost>(document.RootElement, BlueskyJsonSerializerOptions.Default));
                Assert.True(exception is ArgumentException or JsonException, exception?.ToString());
            }
        }
    }

    [Fact]
    public void DraftCaptionCountIsValidatedRecursively()
    {
        string captions = string.Join(',', Enumerable.Repeat("""{"lang":"en","content":""}""", Maximum.DraftEmbedVideoCaptions + 1));
        using JsonDocument document = JsonDocument.Parse($$"""{"text":"","embedVideos":[{"localRef":{"path":"video.mp4"},"captions":[{{captions}}]}]}""");
        Assert.False(DraftPost.TryParse(document, out _));
        DraftPost post = DraftPost.Parse(document, validate: false);
        Assert.Contains(post.Validate(new ValidationContext(post)), result => result.MemberNames.Contains("EmbedVideos[0].Captions"));
    }

    [Theory]
    [InlineData("""{"text":"","embedImages":[{"$type":"example.test#unknown","localRef":{"path":"image.jpg"}}]}""")]
    [InlineData("""{"text":"","embedImages":[{"localRef":{"$type":123,"path":"image.jpg"}}]}""")]
    [InlineData("""{"$type":"example.test#unknown","text":""}""")]
    public void TolerantDraftParsingDoesNotIgnoreTypeDiscriminators(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.False(DraftPost.TryParse(document, validate: false, out _));
        Assert.False(DraftPost.TryParse(document, out _));
    }

    [Theory]
    [InlineData("list")]
    [InlineData("starterPack")]
    [InlineData("threadGate")]
    [InlineData("postGate")]
    [InlineData("message")]
    [InlineData("draft")]
    [InlineData("draftPost")]
    [InlineData("actorLink")]
    [InlineData("status")]
    [InlineData("generator")]
    public void StrictAndTolerantReadsRetainTheSameKnownFields(string kind)
    {
        string post = """
            {"$type":"app.bsky.draft.defs#draftPost","text":"draft","labels":{"$type":"com.atproto.label.defs#selfLabels","values":[{"val":"porn"}]},
             "embedImages":[{"$type":"app.bsky.draft.defs#draftEmbedImage","localRef":{"path":"image.jpg"},"alt":"image"}],
             "embedGallery":{"items":[{"localRef":{"path":"gallery.jpg"},"alt":"gallery"}]},
             "embedVideos":[{"localRef":{"path":"video.mp4"},"alt":"video","captions":[{"lang":"en","content":"WEBVTT"}]}],
             "embedExternals":[{"uri":"https://example.com"}],
             "embedRecords":[{"record":{"uri":"at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3ldegderd6s2e","cid":"bafyreiaahtgpdozb4wjdymb3tvifuwdmxjtumhjxl25lykvsoq3v3l4une"}}]}
            """;
        string json = kind switch
        {
            "draftPost" => post,
            "draft" => $$"""{"posts":[{{post}}],"deviceId":"device","deviceName":"client","langs":["en"],"threadgateAllow":[{"$type":"app.bsky.feed.threadgate#listRule","list":"{{ListUri}}"}],"postgateEmbeddingRules":[{"$type":"app.bsky.feed.postgate#disableRule"}]}""",
            "list" => CreateJson(kind, invalid: false).Insert(1, "\"description\":\"description\",\"labels\":{\"$type\":\"com.atproto.label.defs#selfLabels\",\"values\":[{\"val\":\"porn\"}]},"),
            "starterPack" => CreateJson(kind, invalid: false).Insert(1, $"\"description\":\"description\",\"updatedAt\":\"{CreatedAt}\",\"feeds\":[{{\"uri\":\"{PostUri}\"}}],"),
            "threadGate" => CreateJson(kind, invalid: false).Insert(1, $"\"allow\":[{{\"$type\":\"app.bsky.feed.threadgate#listRule\",\"list\":\"{ListUri}\"}}],\"hiddenReplies\":[\"{PostUri}\"],"),
            "postGate" => CreateJson(kind, invalid: false).Insert(1, $"\"embeddingRules\":[{{\"$type\":\"app.bsky.feed.postgate#disableRule\"}}],\"detachedEmbeddingUris\":[\"{PostUri}\"],"),
            "message" => CreateJson(kind, invalid: false).Insert(1, $"\"replyTo\":{{\"messageId\":\"message\"}},\"embed\":{{\"$type\":\"app.bsky.embed.record\",\"record\":{{\"uri\":\"{PostUri}\",\"cid\":\"bafyreiaahtgpdozb4wjdymb3tvifuwdmxjtumhjxl25lykvsoq3v3l4une\"}}}},"),
            "actorLink" => CreateJson(kind, invalid: false).Insert(1, "\"labels\":{\"$type\":\"com.atproto.label.defs#selfLabels\",\"values\":[{\"val\":\"porn\"}]},"),
            "status" => CreateJson(kind, invalid: false).Insert(1, "\"embed\":{\"$type\":\"app.bsky.embed.external\",\"external\":{\"uri\":\"https://example.com\",\"title\":\"title\",\"description\":\"description\"}},"),
            "generator" => CreateJson(kind, invalid: false).Insert(1, "\"description\":\"description\",\"acceptsInteractions\":true,\"contentMode\":\"app.bsky.feed.defs#contentModeVideo\",\"labels\":{\"$type\":\"com.atproto.label.defs#selfLabels\",\"values\":[{\"val\":\"porn\"}]},"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        using JsonDocument document = JsonDocument.Parse(json);
        IValidatableObject strict = ParseDocument(kind, document);
        IValidatableObject tolerant = ParseDocument(kind, document, validate: false);

        Assert.Empty(tolerant.Validate(new ValidationContext(tolerant)));
        Assert.Equal(
            JsonSerializer.Serialize(strict, strict.GetType(), BlueskyJsonSerializerOptions.Default),
            JsonSerializer.Serialize(tolerant, tolerant.GetType(), BlueskyJsonSerializerOptions.Default));
    }

    [Theory]
    [InlineData("actorLink", "title", 40)]
    [InlineData("generator", "displayName", 24)]
    [InlineData("generator", "description", 300)]
    public void ActorAndGeneratorTextLimitsCountGraphemes(string kind, string property, int maximum)
    {
        foreach (int length in new[] { maximum, maximum + 1 })
        {
            string text = string.Concat(Enumerable.Repeat("e\u0301", length));
            string json = CreateJson(kind, invalid: false);
            if (property is "title" or "displayName")
            {
                json = json.Replace($"\"{property}\":\"x\"", $"\"{property}\":\"{text}\"", StringComparison.Ordinal);
            }
            else
            {
                json = json.Insert(1, $"\"{property}\":\"{text}\",");
            }

            using JsonDocument document = JsonDocument.Parse(json);
            Assert.Equal(length <= maximum, TryDocument(kind, document, validate: true) is not null);
            IValidatableObject value = ParseDocument(kind, document, validate: false);
            Assert.Equal(length > maximum, value.Validate(new ValidationContext(value)).Any());
        }
    }

    [Theory]
    [InlineData("actorLink", "icon", "Icon", "image/png", 100000, true)]
    [InlineData("actorLink", "icon", "Icon", "image/webp", 100000, true)]
    [InlineData("actorLink", "icon", "Icon", "image/jpeg", 100001, false)]
    [InlineData("actorLink", "icon", "Icon", "image/png", -1, false)]
    [InlineData("actorLink", "icon", "Icon", "text/plain", 1, false)]
    [InlineData("generator", "avatar", "Avatar", "image/png", 1000000, true)]
    [InlineData("generator", "avatar", "Avatar", "image/jpeg", 1000001, false)]
    [InlineData("generator", "avatar", "Avatar", "image/webp", 100000, false)]
    [InlineData("generator", "avatar", "Avatar", "image/jpeg", -1, false)]
    public void ActorAndGeneratorBlobMetadataIsValidated(string kind, string property, string member, string mimeType, int size, bool valid)
    {
        string blob = ImageBlob.Replace("image/png", mimeType, StringComparison.Ordinal).Replace("100000", size.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(CreateJson(kind, invalid: false).Insert(1, $"\"{property}\":{blob},"));

        Assert.Equal(valid, TryDocument(kind, document, validate: true) is not null);
        IValidatableObject value = ParseDocument(kind, document, validate: false);
        Assert.Equal(!valid, value.Validate(new ValidationContext(value)).Any(result => result.MemberNames.Any(path => path.StartsWith(member + ".", StringComparison.Ordinal))));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void StatusRetainsUnknownValuesAndValidatesDuration(int? duration, bool valid)
    {
        string json = $$"""{"status":"example.test#future","createdAt":"{{CreatedAt}}"}""";
        if (duration is not null)
        {
            json = json.Insert(1, $"\"durationMinutes\":{duration},");
        }

        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(valid, ActorStatus.TryParse(document, out _));
        ActorStatus status = ActorStatus.Parse(document, validate: false);
        Assert.Equal("example.test#future", status.AccountStatus);
        Assert.Equal(duration, status.DurationMinutes);
        Assert.Equal(!valid, status.Validate(new ValidationContext(status)).Any());
        if (!valid)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => JsonSerializer.Deserialize<ActorStatus>(document.RootElement, BlueskyJsonSerializerOptions.Default));
        }
    }

    [Fact]
    public void StatusParsingCascadesIntoExternalCardsWithoutChangingStrictDeserialization()
    {
        string json = CreateJson("status", invalid: false).Insert(1, "\"embed\":{\"$type\":\"app.bsky.embed.external\",\"external\":{\"uri\":\" \",\"title\":\"\",\"description\":\"\"}},");
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.False(ActorStatus.TryParse(document, out _));
        ActorStatus status = ActorStatus.Parse(document, validate: false);
        Assert.Equal(" ", Assert.IsType<Embed.EmbeddedExternal>(status.Embed).External.Uri);
        Assert.Contains(status.Validate(new ValidationContext(status)), result => result.MemberNames.Contains("Embed.External.Uri"));
        Exception? exception = Xunit.Record.Exception(() => JsonSerializer.Deserialize<ActorStatus>(document.RootElement, BlueskyJsonSerializerOptions.Default));
        Assert.True(exception is ArgumentException or JsonException, exception?.ToString());
    }

    [Theory]
    [InlineData("actorLink", "\"url\":null,")]
    [InlineData("actorLink", "\"url\":1,")]
    [InlineData("actorLink", "\"title\":[],")]
    [InlineData("status", "\"status\":null,")]
    [InlineData("status", "\"durationMinutes\":{},")]
    [InlineData("status", "\"embed\":{\"$type\":\"app.bsky.embed.external\",\"external\":{\"uri\":\"https://example.com\",\"title\":\"\"}},")]
    [InlineData("generator", "\"did\":null,")]
    [InlineData("generator", "\"did\":\"not-a-did\",")]
    [InlineData("generator", "\"displayName\":null,")]
    [InlineData("generator", "\"descriptionFacets\":{},")]
    public void ActorAndGeneratorTolerantParsingRejectsMalformedFields(string kind, string property)
    {
        string json = CreateJson(kind, invalid: false);
        string name = property.Split('"')[1];
        using JsonDocument original = JsonDocument.Parse(json);
        string existing = $"\"{name}\":{(original.RootElement.TryGetProperty(name, out JsonElement element) ? element.GetRawText() : "null")},";
        json = json.Replace(existing, "", StringComparison.Ordinal).Insert(1, property);
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Null(TryDocument(kind, document, validate: false));
        Assert.Null(TryDocument(kind, document, validate: true));
    }

    [Theory]
    [InlineData("actorLink", "url")]
    [InlineData("actorLink", "createdAt")]
    [InlineData("status", "status")]
    [InlineData("status", "createdAt")]
    [InlineData("generator", "did")]
    [InlineData("generator", "displayName")]
    [InlineData("generator", "createdAt")]
    public void ActorAndGeneratorParsingRequiresEachRequiredField(string kind, string field)
    {
        using JsonDocument original = JsonDocument.Parse(CreateJson(kind, invalid: false));
        string json = "{" + string.Join(',', original.RootElement.EnumerateObject().Where(property => property.Name != field)
            .Select(property => $"\"{property.Name}\":{property.Value.GetRawText()}")) + "}";
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Null(TryDocument(kind, document, validate: true));
        Assert.Null(TryDocument(kind, document, validate: false));
    }

    [Fact]
    public void UnknownGeneratorContentModeRetainsExistingEnumBehavior()
    {
        using JsonDocument document = JsonDocument.Parse(CreateJson("generator", invalid: false)
            .Insert(1, "\"contentMode\":\"example.test#future\","));
        foreach (bool validate in new[] { true, false })
        {
            Generator generator = Generator.Parse(document, validate);
            Assert.Equal(GeneratorContentMode.Unknown, generator.ContentMode);
            Assert.Empty(generator.Validate(new ValidationContext(generator)));
        }
    }

    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("http://example.com", false)]
    [InlineData("relative", false)]
    public void LinkValidationRequiresHttpsWithoutChangingOrdinaryDeserialization(string url, bool valid)
    {
        using JsonDocument document = JsonDocument.Parse(CreateJson("actorLink", invalid: false)
            .Replace("https://example.com", url, StringComparison.Ordinal));
        Assert.Equal(valid, ActorLink.TryParse(document, out _));
        Assert.NotNull(JsonSerializer.Deserialize<ActorLink>(document.RootElement, BlueskyJsonSerializerOptions.Default));
        ActorLink link = ActorLink.Parse(document, validate: false);
        Assert.Equal(url, link.Url.OriginalString);
        Assert.Equal(!valid, link.Validate(new ValidationContext(link)).Any());
    }

    [Fact]
    public void GeneratorValidationDoesNotChangeOrdinaryConstructionOrDeserialization()
    {
        using JsonDocument document = JsonDocument.Parse(CreateJson("generator", invalid: true));
        Assert.NotNull(JsonSerializer.Deserialize<Generator>(document.RootElement, BlueskyJsonSerializerOptions.Default));
        Generator generator = Generator.Parse(document, validate: false);
        Assert.Equal(25, generator.DisplayName.Length);
        Assert.False(Generator.TryParse(document, out _));
        Assert.False(Generator.TryParse(document.RootElement, out _));
    }

    [Fact]
    public void DefaultTryParseOverloadsValidateActorRecords()
    {
        using JsonDocument linkDocument = JsonDocument.Parse(CreateJson("actorLink", invalid: true));
        Assert.False(ActorLink.TryParse(linkDocument, out _));
        Assert.False(ActorLink.TryParse(linkDocument.RootElement, out _));
        using JsonDocument statusDocument = JsonDocument.Parse(CreateJson("status", invalid: true));
        Assert.False(ActorStatus.TryParse(statusDocument, out _));
        Assert.False(ActorStatus.TryParse(statusDocument.RootElement, out _));
        using JsonDocument validLinkDocument = JsonDocument.Parse(CreateJson("actorLink", invalid: false));
        Assert.True(ActorLink.TryParse(validLinkDocument, out _));
        Assert.True(ActorLink.TryParse(validLinkDocument.RootElement, out _));
        using JsonDocument validStatusDocument = JsonDocument.Parse(CreateJson("status", invalid: false));
        Assert.True(ActorStatus.TryParse(validStatusDocument, out _));
        Assert.True(ActorStatus.TryParse(validStatusDocument.RootElement, out _));
        using JsonDocument validGeneratorDocument = JsonDocument.Parse(CreateJson("generator", invalid: false));
        Assert.True(Generator.TryParse(validGeneratorDocument, out _));
        Assert.True(Generator.TryParse(validGeneratorDocument.RootElement, out _));
    }

    private static string CreateJson(string kind, bool invalid) => kind switch
    {
        "list" => $$"""{"$type":"app.bsky.graph.list","name":"{{new string('x', invalid ? Maximum.ListNameLengthInBytes + 1 : 1)}}","purpose":"app.bsky.graph.defs#curatelist","createdAt":"{{CreatedAt}}"}""",
        "starterPack" => $$"""{"$type":"app.bsky.graph.starterpack","name":"{{new string('x', invalid ? Maximum.StarterPackNameLengthInGraphemes + 1 : 1)}}","list":"{{ListUri}}","createdAt":"{{CreatedAt}}"}""",
        "threadGate" => $$"""{"$type":"app.bsky.feed.threadgate","post":"{{(invalid ? ListUri : PostUri)}}","createdAt":"{{CreatedAt}}"}""",
        "postGate" => $$"""{"$type":"app.bsky.feed.postgate","post":"{{(invalid ? ListUri : PostUri)}}","createdAt":"{{CreatedAt}}"}""",
        "message" => $$"""{"text":"{{new string('x', invalid ? Maximum.MessageLengthInGraphemes + 1 : 1)}}"}""",
        "draft" => invalid ? """{"posts":[]}""" : """{"posts":[{"text":""}]}""",
        "draftPost" => $$"""{"text":"{{new string('x', invalid ? Maximum.DraftTextLengthInGraphemes + 1 : 1)}}"}""",
        "actorLink" => $$"""{"$type":"app.bsky.actor.link","url":"https://example.com","title":"{{new string('x', invalid ? 41 : 1)}}","createdAt":"{{CreatedAt}}"}""",
        "status" => $$"""{"$type":"app.bsky.actor.status","status":"app.bsky.actor.status#live","durationMinutes":{{(invalid ? 0 : 1)}},"createdAt":"{{CreatedAt}}"}""",
        "generator" => $$"""{"$type":"app.bsky.feed.generator","did":"did:plc:ec72yg6n2sydzjvtovvdlxrk","displayName":"{{new string('x', invalid ? 25 : 1)}}","createdAt":"{{CreatedAt}}"}""",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static IValidatableObject ParseDocument(string kind, JsonDocument document, bool? validate = null) => kind switch
    {
        "list" => validate.HasValue ? GraphList.Parse(document, validate.Value) : GraphList.Parse(document),
        "starterPack" => validate.HasValue ? StarterPack.Parse(document, validate.Value) : StarterPack.Parse(document),
        "threadGate" => validate.HasValue ? ThreadGate.Parse(document, validate.Value) : ThreadGate.Parse(document),
        "postGate" => validate.HasValue ? PostGate.Parse(document, validate.Value) : PostGate.Parse(document),
        "message" => validate.HasValue ? MessageInput.Parse(document, validate.Value) : MessageInput.Parse(document),
        "draft" => validate.HasValue ? Draft.Parse(document, validate.Value) : Draft.Parse(document),
        "draftPost" => validate.HasValue ? DraftPost.Parse(document, validate.Value) : DraftPost.Parse(document),
        "actorLink" => validate.HasValue ? ActorLink.Parse(document, validate.Value) : ActorLink.Parse(document),
        "status" => validate.HasValue ? ActorStatus.Parse(document, validate.Value) : ActorStatus.Parse(document),
        "generator" => validate.HasValue ? Generator.Parse(document, validate.Value) : Generator.Parse(document),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static IValidatableObject ParseElement(string kind, JsonElement element, bool? validate = null) => kind switch
    {
        "list" => validate.HasValue ? GraphList.Parse(element, validate.Value) : GraphList.Parse(element),
        "starterPack" => validate.HasValue ? StarterPack.Parse(element, validate.Value) : StarterPack.Parse(element),
        "threadGate" => validate.HasValue ? ThreadGate.Parse(element, validate.Value) : ThreadGate.Parse(element),
        "postGate" => validate.HasValue ? PostGate.Parse(element, validate.Value) : PostGate.Parse(element),
        "message" => validate.HasValue ? MessageInput.Parse(element, validate.Value) : MessageInput.Parse(element),
        "draft" => validate.HasValue ? Draft.Parse(element, validate.Value) : Draft.Parse(element),
        "draftPost" => validate.HasValue ? DraftPost.Parse(element, validate.Value) : DraftPost.Parse(element),
        "actorLink" => validate.HasValue ? ActorLink.Parse(element, validate.Value) : ActorLink.Parse(element),
        "status" => validate.HasValue ? ActorStatus.Parse(element, validate.Value) : ActorStatus.Parse(element),
        "generator" => validate.HasValue ? Generator.Parse(element, validate.Value) : Generator.Parse(element),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static IValidatableObject? TryDocument(string kind, JsonDocument? document, bool validate) => kind switch
    {
        "list" => CheckTry(GraphList.TryParse(document, validate, out GraphList? list), list),
        "starterPack" => CheckTry(StarterPack.TryParse(document, validate, out StarterPack? pack), pack),
        "threadGate" => CheckTry(ThreadGate.TryParse(document, validate, out ThreadGate? threadGate), threadGate),
        "postGate" => CheckTry(PostGate.TryParse(document, validate, out PostGate? postGate), postGate),
        "message" => CheckTry(MessageInput.TryParse(document, validate, out MessageInput? message), message),
        "draft" => CheckTry(Draft.TryParse(document, validate, out Draft? draft), draft),
        "draftPost" => CheckTry(DraftPost.TryParse(document, validate, out DraftPost? post), post),
        "actorLink" => CheckTry(ActorLink.TryParse(document, validate, out ActorLink? link), link),
        "status" => CheckTry(ActorStatus.TryParse(document, validate, out ActorStatus? status), status),
        "generator" => CheckTry(Generator.TryParse(document, validate, out Generator? generator), generator),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static IValidatableObject? TryElement(string kind, JsonElement element, bool validate) => kind switch
    {
        "list" => CheckTry(GraphList.TryParse(element, validate, out GraphList? list), list),
        "starterPack" => CheckTry(StarterPack.TryParse(element, validate, out StarterPack? pack), pack),
        "threadGate" => CheckTry(ThreadGate.TryParse(element, validate, out ThreadGate? threadGate), threadGate),
        "postGate" => CheckTry(PostGate.TryParse(element, validate, out PostGate? postGate), postGate),
        "message" => CheckTry(MessageInput.TryParse(element, validate, out MessageInput? message), message),
        "draft" => CheckTry(Draft.TryParse(element, validate, out Draft? draft), draft),
        "draftPost" => CheckTry(DraftPost.TryParse(element, validate, out DraftPost? post), post),
        "actorLink" => CheckTry(ActorLink.TryParse(element, validate, out ActorLink? link), link),
        "status" => CheckTry(ActorStatus.TryParse(element, validate, out ActorStatus? status), status),
        "generator" => CheckTry(Generator.TryParse(element, validate, out Generator? generator), generator),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static IValidatableObject? CheckTry(bool success, IValidatableObject? value)
    {
        Assert.Equal(success, value is not null);

        return value;
    }
}
