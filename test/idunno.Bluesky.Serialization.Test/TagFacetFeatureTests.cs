// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class TagFacetFeatureTests
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t\t")]
    [InlineData("\n\n")]
    [InlineData("\r\n")]
    [InlineData(" \t\r\n ")]
    [InlineData("normal")]
    [InlineData("\u65e5\u672c\u8a9e")]
    public void TagValuesRoundTripUnchanged(string tag)
    {
        string json = CreateTagJson(tag);

        foreach (JsonSerializerOptions options in new[] { _jsonSerializerOptions, BlueskyServer.BlueskyJsonSerializerOptions })
        {
            TagFacetFeature? concrete = JsonSerializer.Deserialize<TagFacetFeature>(json, options);
            Assert.NotNull(concrete);
            Assert.Equal(tag, concrete.Tag);

            FacetFeature? feature = JsonSerializer.Deserialize<FacetFeature>(json, options);
            Assert.Equal(tag, Assert.IsType<TagFacetFeature>(feature).Tag);

            string serialized = JsonSerializer.Serialize(feature, options);
            using JsonDocument document = JsonDocument.Parse(serialized);
            Assert.Equal("app.bsky.richtext.facet#tag", document.RootElement.GetProperty("$type").GetString());
            Assert.Equal(tag, document.RootElement.GetProperty("tag").GetString());
            Assert.Equal(tag, Assert.IsType<TagFacetFeature>(JsonSerializer.Deserialize<FacetFeature>(serialized, options)).Tag);

            string concreteSerialized = JsonSerializer.Serialize(concrete, options);
            using JsonDocument concreteDocument = JsonDocument.Parse(concreteSerialized);
            Assert.Equal(tag, concreteDocument.RootElement.GetProperty("tag").GetString());
            Assert.Equal(tag, JsonSerializer.Deserialize<TagFacetFeature>(concreteSerialized, options)!.Tag);
        }
    }

    [Fact]
    public void PostWithEmptyAndNormalTagFacetsDeserializesWithProductionMetadata()
    {
        const string json = """
            {
              "$type": "app.bsky.feed.post",
              "text": "# #normal @user https://example.com",
              "createdAt": "2026-10-08T15:43:06.08711400Z",
              "facets": [
                {"index":{"byteStart":0,"byteEnd":1},"features":[{"$type":"app.bsky.richtext.facet#tag","tag":""}]},
                {"index":{"byteStart":2,"byteEnd":9},"features":[{"$type":"app.bsky.richtext.facet#tag","tag":"normal"}]},
                {"index":{"byteStart":10,"byteEnd":15},"features":[{"$type":"app.bsky.richtext.facet#mention","did":"did:plc:axnk4tq6meineaeb4xzzvji6"}]},
                {"index":{"byteStart":16,"byteEnd":35},"features":[{"$type":"app.bsky.richtext.facet#link","uri":"https://example.com"}]}
              ],
              "tags": ["normal"]
            }
            """;
        JsonTypeInfo<Post> metadata = (JsonTypeInfo<Post>)BlueskyServer.BlueskyJsonSerializerOptions.GetTypeInfo(typeof(Post));
        Post? post = JsonSerializer.Deserialize(json, metadata);
        Assert.NotNull(post);

        AssertPostFacets(post);
        string serialized = JsonSerializer.Serialize(post, metadata);
        Post? roundTripped = JsonSerializer.Deserialize(serialized, metadata);
        Assert.NotNull(roundTripped);
        Assert.Equal(post.Text, roundTripped.Text);
        AssertPostFacets(roundTripped);
    }

    [Fact]
    public void PostWithEmptyTopLevelTagDeserializesWithProductionMetadata()
    {
        const string json = """
            {
              "$type":"app.bsky.feed.post",
              "text":"post",
              "createdAt":"2026-10-08T15:43:06.08711400Z",
              "tags":["","normal"]
            }
            """;
        JsonTypeInfo<Post> metadata = (JsonTypeInfo<Post>)BlueskyServer.BlueskyJsonSerializerOptions.GetTypeInfo(typeof(Post));
        Post? post = JsonSerializer.Deserialize(json, metadata);
        Assert.NotNull(post);
        Assert.Equal(["", "normal"], post.Tags);

        string serialized = JsonSerializer.Serialize(post, metadata);
        Post? roundTripped = JsonSerializer.Deserialize(serialized, metadata);
        Assert.NotNull(roundTripped);
        Assert.Equal(["", "normal"], roundTripped.Tags);
    }

    [Fact]
    public void FacetWithEmptyFeaturesDeserializesWithProductionMetadata()
    {
        const string json = """{"$type":"app.bsky.richtext.facet","index":{"byteStart":0,"byteEnd":0},"features":[]}""";
        Facet? facet = JsonSerializer.Deserialize<Facet>(json, BlueskyServer.BlueskyJsonSerializerOptions);
        Assert.NotNull(facet);
        Assert.Empty(facet.Features);

        string serialized = JsonSerializer.Serialize(facet, BlueskyServer.BlueskyJsonSerializerOptions);
        Facet? roundTripped = JsonSerializer.Deserialize<Facet>(serialized, BlueskyServer.BlueskyJsonSerializerOptions);
        Assert.NotNull(roundTripped);
        Assert.Empty(roundTripped.Features);
    }

    [Fact]
    public void FacetStillRejectsNullFeatures()
    {
        Assert.Throws<ArgumentNullException>(() => new Facet(new ByteSlice(0, 0), null!));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Facet>(
            """{"$type":"app.bsky.richtext.facet","index":{"byteStart":0,"byteEnd":0},"features":null}""",
            BlueskyServer.BlueskyJsonSerializerOptions));
    }

    [Fact]
    public void PostStillRejectsNullTopLevelTags()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new Post("post", tags: new string[] { null! }));
        Assert.Equal("tags", exception.ParamName);
    }

    private static void AssertPostFacets(Post post)
    {
        Assert.NotNull(post.Facets);
        Facet[] facets = post.Facets.ToArray();
        Assert.Equal(4, facets.Length);
        Assert.Equal(string.Empty, Assert.IsType<TagFacetFeature>(Assert.Single(facets[0].Features)).Tag);
        Assert.Equal("normal", Assert.IsType<TagFacetFeature>(Assert.Single(facets[1].Features)).Tag);
        Assert.Equal("did:plc:axnk4tq6meineaeb4xzzvji6",
            Assert.IsType<MentionFacetFeature>(Assert.Single(facets[2].Features)).Did.Value);
        Assert.Equal("https://example.com", Assert.IsType<LinkFacetFeature>(Assert.Single(facets[3].Features)).Uri);
        Assert.Equal(0, facets[0].Index.ByteStart);
        Assert.Equal(1, facets[0].Index.ByteEnd);
        Assert.NotNull(post.Tags);
        Assert.Equal("normal", Assert.Single(post.Tags));
    }

    [Fact]
    public void NullTagStillFailsDeserialization()
    {
        foreach (JsonSerializerOptions options in new[] { _jsonSerializerOptions, BlueskyServer.BlueskyJsonSerializerOptions })
        {
            if (options.RespectNullableAnnotations)
            {
                Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<FacetFeature>(CreateTagJson(null), options));
            }
            else
            {
                ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                    () => JsonSerializer.Deserialize<FacetFeature>(CreateTagJson(null), options));
                Assert.Equal("tag", exception.ParamName);
            }
        }
    }

    [Fact]
    public void MissingTagStillFailsDeserialization()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<FacetFeature>(
            """{"$type":"app.bsky.richtext.facet#tag"}""", BlueskyServer.BlueskyJsonSerializerOptions));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TagLengthLimitsRemainEnforced(bool testBytes)
    {
        string atLimit = testBytes
            ? "a" + new string('\u0301', (Maximum.TagLengthInBytes - 2) / 2) + "b"
            : new string('\u65e5', Maximum.TagLengthInGraphemes);
        string overLimit = atLimit + "c";
        Assert.Equal(testBytes ? Maximum.TagLengthInBytes : Maximum.TagLengthInGraphemes,
            testBytes ? atLimit.GetUtf8Length() : atLimit.GetGraphemeLength());
        Assert.Equal(testBytes ? Maximum.TagLengthInBytes + 1 : Maximum.TagLengthInGraphemes + 1,
            testBytes ? overLimit.GetUtf8Length() : overLimit.GetGraphemeLength());
        Assert.True(testBytes ? overLimit.GetGraphemeLength() <= Maximum.TagLengthInGraphemes
            : overLimit.GetUtf8Length() <= Maximum.TagLengthInBytes);

        Assert.Equal(atLimit, new TagFacetFeature(atLimit).Tag);
        Assert.Throws<ArgumentOutOfRangeException>(() => new TagFacetFeature(overLimit));

        foreach (JsonSerializerOptions options in new[] { _jsonSerializerOptions, BlueskyServer.BlueskyJsonSerializerOptions })
        {
            FacetFeature? feature = JsonSerializer.Deserialize<FacetFeature>(CreateTagJson(atLimit), options);
            Assert.Equal(atLimit, Assert.IsType<TagFacetFeature>(feature).Tag);
            Assert.Throws<ArgumentOutOfRangeException>(() => JsonSerializer.Deserialize<FacetFeature>(CreateTagJson(overLimit), options));
        }
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void OtherFacetAndTopLevelTagValidationIsUnchanged(string value)
    {
        string json = $$"""
            {"$type":"app.bsky.richtext.facet#link","uri":{{JsonSerializer.Serialize(value, _jsonSerializerOptions)}}}
            """;
        Assert.Throws<ArgumentException>(() => JsonSerializer.Deserialize<FacetFeature>(json, BlueskyServer.BlueskyJsonSerializerOptions));
        Assert.Throws<ArgumentNullException>(() => new MentionFacetFeature(null!));

        Assert.Equal(value, Assert.Single(new Post("Post text", tags: [value]).Tags!));
    }

    private string CreateTagJson(string? tag)
    {
        return $$"""
            {"$type":"app.bsky.richtext.facet#tag","tag":{{JsonSerializer.Serialize(tag, _jsonSerializerOptions)}}}
            """;
    }
}
