// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Chat;
using idunno.Bluesky.Drafts;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Notifications.PreferenceTypes;
using idunno.Bluesky.Record;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky.Serialization.Test;

public class LexiconAuditRegressionTests
{
    private static readonly JsonSerializerOptions s_options = BlueskyJsonSerializerOptions.Default;
    private const string GeneratorJson = """
        {"uri":"at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.feed.generator/test",
        "cid":"bafyreiaxqkvwjv5y45xq7huunf6mjvh7tf6ls44imkgjkqi3uthn57vjxu",
        "did":"did:plc:ewvi7nxzyoun6zhxrhs64oiz",
        "creator":{"did":"did:plc:ewvi7nxzyoun6zhxrhs64oiz","handle":"bsky.app"},
        "displayName":"","descriptionFacets":[{"index":{"byteStart":0,"byteEnd":1},
        "features":[{"$type":"app.bsky.richtext.facet#tag","tag":"x"}]}],
        "indexedAt":"2026-10-12T00:00:00Z"}
        """;

    [Theory]
    [InlineData(typeof(Post), """{"text":""}""")]
    [InlineData(typeof(Post), """{"createdAt":"2026-10-12T00:00:00Z"}""")]
    [InlineData(typeof(Post), """{"text":null,"createdAt":"2026-10-12T00:00:00Z"}""")]
    [InlineData(typeof(Like), """{"subject":{"uri":"at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.feed.post/test","cid":"bafyreiaxqkvwjv5y45xq7huunf6mjvh7tf6ls44imkgjkqi3uthn57vjxu"}}""")]
    [InlineData(typeof(Follow), """{"subject":"did:plc:ewvi7nxzyoun6zhxrhs64oiz"}""")]
    [InlineData(typeof(FilterablePreference), """{}""")]
    [InlineData(typeof(FilterablePreference), """{"include":"all","list":false}""")]
    [InlineData(typeof(FilterablePreference), """{"include":"all","push":false}""")]
    [InlineData(typeof(FilterablePreference), """{"list":false,"push":false}""")]
    [InlineData(typeof(BlockedPost), """{"uri":"at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.feed.post/test","blocked":true}""")]
    [InlineData(typeof(BlockedPost), """{"uri":"at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.feed.post/test","author":{"did":"did:plc:ewvi7nxzyoun6zhxrhs64oiz"}}""")]
    [InlineData(typeof(BlockedPost), """{"uri":"at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.feed.post/test","blocked":true,"author":null}""")]
    [InlineData(typeof(Chat.Embed.JoinLinkView), """{}""")]
    [InlineData(typeof(MessageView), """{}""")]
    [InlineData(typeof(MessageView), """{"id":"","rev":"","text":"","sentAt":"2026-10-12T00:00:00Z"}""")]
    [InlineData(typeof(MessageView), """{"id":"","rev":"","text":"","sender":{"did":"did:plc:ewvi7nxzyoun6zhxrhs64oiz"}}""")]
    [InlineData(typeof(Post), """{"text":null,"createdAt":"2026-10-12T00:00:00Z","embed":{"$type":"app.bsky.embed.external","external":{"uri":"https://example.com","title":"","description":""}}}""")]
    public void MissingOrNullRequiredPropertiesAreRejected(Type type, string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, type, s_options));
    }

    [Theory]
    [InlineData(typeof(ConversationView), """{"id":"","rev":"","members":[null],"muted":false,"unreadCount":0}""")]
    [InlineData(typeof(MessageView), """{"id":"","rev":"","text":"","facets":[null],"sender":{"did":"did:plc:ewvi7nxzyoun6zhxrhs64oiz"},"sentAt":"2026-10-12T00:00:00Z"}""")]
    [InlineData(typeof(MessageView), """{"id":"","rev":"","text":"","reactions":[null],"sender":{"did":"did:plc:ewvi7nxzyoun6zhxrhs64oiz"},"sentAt":"2026-10-12T00:00:00Z"}""")]
    [InlineData(typeof(BlockedPost), """{"uri":"at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.feed.post/test","blocked":false,"author":{"did":"did:plc:ewvi7nxzyoun6zhxrhs64oiz"}}""")]
    [InlineData(typeof(Chat.Actor.ProfileViewBasic), """{"did":"did:plc:ewvi7nxzyoun6zhxrhs64oiz","handle":"bsky.app","labels":[null]}""")]
    [InlineData(typeof(Chat.Group.Model.GetJoinLinkPreviewsResponse), """{"joinLinkPreviews":[null]}""")]
    [InlineData(typeof(Chat.Group.Model.AddMembersResponse), """{"convo":{"id":"","rev":"","members":[],"muted":false,"unreadCount":0},"addedMembers":[null]}""")]
    [InlineData(typeof(Facet), """{"index":{"byteStart":0,"byteEnd":0},"features":[null]}""")]
    [InlineData(typeof(MessageView), """{"id":"","rev":"","text":"","facets":[{"index":{"byteStart":0,"byteEnd":0},"features":[null]}],"sender":{"did":"did:plc:ewvi7nxzyoun6zhxrhs64oiz"},"sentAt":"2026-10-12T00:00:00Z"}""")]
    [InlineData(typeof(ConversationView), """{"id":"","rev":"","members":[{"did":"did:plc:ewvi7nxzyoun6zhxrhs64oiz","handle":"bsky.app","labels":[null]}],"muted":false,"unreadCount":0}""")]
    [InlineData(typeof(CreateMessage), """{"convoId":"","rev":"","message":{"$type":"chat.bsky.convo.defs#messageView","id":"","rev":"","text":"","sender":{"did":"did:plc:ewvi7nxzyoun6zhxrhs64oiz"},"sentAt":"2026-10-12T00:00:00Z"},"relatedProfiles":[null]}""")]
    public void InvalidNestedValuesAreRejected(Type type, string json)
    {
        Assert.Throws<ArgumentException>(() => JsonSerializer.Deserialize(json, type, s_options));
    }

    [Fact]
    public void FacetFeatureCollectionsRejectNullEntriesAndRetainEmptyArrays()
    {
        ByteSlice index = new(0, 0);

        Assert.Throws<ArgumentException>(() => new Facet(index, [null!]));
        Assert.Throws<ArgumentException>(() => new Facet(index, []) { Features = [null!] });
        Facet facet = new(index, []);
        Assert.Empty(facet.Features);
        Assert.Empty(JsonSerializer.Deserialize<Facet>(JsonSerializer.Serialize(facet, s_options), s_options)!.Features);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void DraftStringsPreserveEmptyAndWhitespaceValues(string value)
    {
        DraftPost draft = new(value);
        DraftEmbedCaption caption = new(value, "en");

        Assert.Equal(value, JsonSerializer.Deserialize<DraftPost>(JsonSerializer.Serialize(draft, s_options), s_options)!.Text);
        Assert.Equal(value, JsonSerializer.Deserialize<DraftEmbedCaption>(JsonSerializer.Serialize(caption, s_options), s_options)!.Content);
    }

    [Fact]
    public void EmbeddedOnlyPostsWriteTheRequiredEmptyText()
    {
        Post post = new(null, embeddedRecord: new EmbeddedRecord(new StrongReference(
            new AtUri("at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.feed.post/test"),
            new Cid("bafyreiaxqkvwjv5y45xq7huunf6mjvh7tf6ls44imkgjkqi3uthn57vjxu"))));

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(post, s_options));
        Assert.Equal(string.Empty, document.RootElement.GetProperty("text").GetString());
        Assert.Equal(string.Empty, JsonSerializer.Deserialize<Post>(document.RootElement, s_options)!.Text);
    }

    [Fact]
    public void GeneratorDescriptionsUseDescriptionFacetsAndAllowEmptyDisplayNames()
    {
        GeneratorView? generator = JsonSerializer.Deserialize<GeneratorView>(GeneratorJson, s_options);

        Assert.NotNull(generator);
        Assert.Equal(string.Empty, generator.DisplayName);
        Assert.Single(generator.Facets);
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(generator, s_options));
        Assert.Single(document.RootElement.GetProperty("descriptionFacets").EnumerateArray());
        Assert.False(document.RootElement.TryGetProperty("facets", out _));
    }

    [Theory]
    [InlineData("""{"$type":"app.bsky.richtext.facet#tag","tag":""}""", typeof(TagFacetFeature))]
    [InlineData("""{"$type":"app.bsky.richtext.facet#mention","did":"did:plc:ewvi7nxzyoun6zhxrhs64oiz"}""", typeof(MentionFacetFeature))]
    [InlineData("""{"$type":"app.bsky.richtext.facet#link","uri":"https://example.com"}""", typeof(LinkFacetFeature))]
    [InlineData("""{"$type":"app.example.facet#future","extra":{"value":1,"items":[false,null]}}""", typeof(FacetFeature))]
    public void FacetFeaturesPreserveTheirDiscriminatorsAndValues(string json, Type expectedType)
    {
        FacetFeature? feature = JsonSerializer.Deserialize<FacetFeature>(json, s_options);

        Assert.NotNull(feature);
        Assert.IsType(expectedType, feature);
        using JsonDocument original = JsonDocument.Parse(json);
        using JsonDocument roundTrip = JsonDocument.Parse(JsonSerializer.Serialize(feature, s_options));
        Assert.Equal(original.RootElement.GetRawText(), roundTrip.RootElement.GetRawText());
    }

    [Fact]
    public void ChatJoinLinkEmbedsDeserializeAndRoundTrip()
    {
        const string json = """
            {"id":"","rev":"","text":"","sender":{"did":"did:plc:ewvi7nxzyoun6zhxrhs64oiz"},
            "sentAt":"2026-10-12T00:00:00Z","embed":{"$type":"chat.bsky.embed.joinLink#view",
            "joinLinkPreview":{"$type":"chat.bsky.group.defs#invalidJoinLinkPreviewView","code":""}}}
            """;
        MessageView? message = JsonSerializer.Deserialize<MessageView>(json, s_options);

        Assert.NotNull(message);
        Assert.Null(message.Embed);
        Chat.Embed.JoinLinkView embed = Assert.IsType<Chat.Embed.JoinLinkView>(message.EmbeddedView);
        Assert.IsType<Chat.Group.InvalidJoinLinkPreviewView>(embed.JoinLinkPreview);
        MessageView? roundTrip = JsonSerializer.Deserialize<MessageView>(JsonSerializer.Serialize(message, s_options), s_options);
        Assert.NotNull(roundTrip);
        Assert.IsType<Chat.Embed.JoinLinkView>(roundTrip.EmbeddedView);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChatMessageAdaptersPreserveExtraFieldsAndRecordEmbeds(bool useBase)
    {
        const string json = """
            {"$type":"chat.bsky.convo.defs#messageView","id":"","rev":"","text":"",
            "sender":{"did":"did:plc:ewvi7nxzyoun6zhxrhs64oiz"},"sentAt":"2026-10-12T00:00:00Z",
            "future":{"items":[1,false,null]},
            "embed":{"$type":"app.bsky.embed.record#view","record":{"$type":"app.bsky.embed.record#viewNotFound",
            "uri":"at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.feed.post/test","notFound":true}}}
            """;
        MessageView? message = useBase
            ? Assert.IsType<MessageView>(JsonSerializer.Deserialize<MessageViewBase>(json, s_options))
            : JsonSerializer.Deserialize<MessageView>(json, s_options);

        Assert.NotNull(message);
        Assert.IsType<EmbeddedRecordView>(message.Embed);
        Assert.Same(message.Embed, message.EmbeddedView);
        string serialized = useBase
            ? JsonSerializer.Serialize<MessageViewBase>(message, s_options)
            : JsonSerializer.Serialize(message, s_options);
        using JsonDocument document = JsonDocument.Parse(serialized);
        Assert.Equal("""{"items":[1,false,null]}""", document.RootElement.GetProperty("future").GetRawText());
        Assert.Equal("app.bsky.embed.record#view", document.RootElement.GetProperty("embed").GetProperty("$type").GetString());
    }

    [Fact]
    public void UnknownMessageViewsPreserveTheirDiscriminatorsAndFields()
    {
        const string json = """{"$type":"chat.example.defs#futureMessage","extra":{"items":[1,false,null]}}""";
        MessageViewBase? message = JsonSerializer.Deserialize<MessageViewBase>(json, s_options);

        Assert.NotNull(message);
        Assert.Equal(json, JsonSerializer.Serialize(message, s_options));
    }

    [Fact]
    public void DeclarationDispatchWorksForConcreteBaseAndEnvelopeReads()
    {
        const string json = """{"$type":"chat.bsky.actor.declaration","allowIncoming":""}""";

        Chat.Actor.Declaration? declaration = JsonSerializer.Deserialize<Chat.Actor.Declaration>(json, s_options);
        Assert.NotNull(declaration);
        Assert.IsType<Chat.Actor.Declaration>(JsonSerializer.Deserialize<BlueskyRecord>(json, s_options));
        string envelopeJson = $$"""
            {"uri":"at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/chat.bsky.actor.declaration/self",
            "cid":"bafyreiaxqkvwjv5y45xq7huunf6mjvh7tf6ls44imkgjkqi3uthn57vjxu","value":{{json}}}
            """;
        Assert.NotNull(JsonSerializer.Deserialize<AtProtoRepositoryRecord<Chat.Actor.Declaration>>(envelopeJson, s_options));
        using JsonDocument serialized = JsonDocument.Parse(JsonSerializer.Serialize(declaration, s_options));
        Assert.Equal("chat.bsky.actor.declaration", serialized.RootElement.GetProperty("$type").GetString());
    }

    [Fact]
    public void ProfileAssociatedWritesTheExactFeedgensPropertyName()
    {
        ProfileAssociated? associated = JsonSerializer.Deserialize<ProfileAssociated>("""{"feedgens":2}""", s_options);

        Assert.NotNull(associated);
        Assert.Equal(2, associated.FeedGens);
        using JsonDocument serialized = JsonDocument.Parse(JsonSerializer.Serialize(associated, s_options));
        Assert.Equal(2, serialized.RootElement.GetProperty("feedgens").GetInt32());
        Assert.False(serialized.RootElement.TryGetProperty("feedGens", out _));
    }

    [Fact]
    public void ViewPolymorphismMetadataResolvesAllRegisteredTypes()
    {
        Assert.NotNull(s_options.GetTypeInfo(typeof(View)));
        string json = GeneratorJson.Replace("\"displayName\"", "\"$type\":\"app.bsky.feed.defs#generatorView\",\"displayName\"", StringComparison.Ordinal);
        Assert.IsType<GeneratorView>(JsonSerializer.Deserialize<View>(json, s_options));
    }

    [Fact]
    public void LabelerViewsDeserializeThroughTheirViewBase()
    {
        string json = GeneratorJson.Replace("\"displayName\"", "\"$type\":\"app.bsky.labeler.defs#labelerView\",\"displayName\"", StringComparison.Ordinal);

        Assert.IsType<Labeler.LabelerView>(JsonSerializer.Deserialize<View>(json, s_options));
    }

    [Fact]
    public void RequiredBooleanValuesMayBeFalse()
    {
        FilterablePreference? preference = JsonSerializer.Deserialize<FilterablePreference>(
            """{"include":"all","list":false,"push":false}""", s_options);

        Assert.NotNull(preference);
        Assert.False(preference.List);
        Assert.False(preference.Push);
    }

    [Fact]
    public void BlockedPostsPreserveTheirAuthorAndRequiredFlag()
    {
        const string json = """{"uri":"at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.feed.post/test","blocked":true,"author":{"did":"did:plc:ewvi7nxzyoun6zhxrhs64oiz"}}""";
        BlockedPost? post = JsonSerializer.Deserialize<BlockedPost>(json, s_options);

        Assert.NotNull(post);
        Assert.True(post.Blocked);
        Assert.Equal("did:plc:ewvi7nxzyoun6zhxrhs64oiz", post.Author.Did.Value);
        using JsonDocument serialized = JsonDocument.Parse(JsonSerializer.Serialize(post, s_options));
        Assert.True(serialized.RootElement.GetProperty("blocked").GetBoolean());
        Assert.Equal(post.Author.Did.Value, serialized.RootElement.GetProperty("author").GetProperty("did").GetString());
    }
}
