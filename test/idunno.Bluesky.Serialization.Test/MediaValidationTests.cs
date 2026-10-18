// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Text.Json;

using idunno.Bluesky.Embed;
using idunno.Bluesky.Embed.Gallery;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class MediaValidationTests
{
    private const string ImageBlob = """
        {"$type":"blob","ref":{"$link":"bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"},"mimeType":"image/jpeg","size":1024}
        """;

    private const string RecordReference = """
        {"$type":"app.bsky.embed.record","record":{"uri":"at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3ldegderd6s2e","cid":"bafyreiaahtgpdozb4wjdymb3tvifuwdmxjtumhjxl25lykvsoq3v3l4une"}}
        """;

    [Fact]
    public void NewMediaConstraintsDoNotChangeOrdinaryDeserialization()
    {
        string invalidImage = ImageBlob.Replace("image/jpeg", "text/plain", StringComparison.Ordinal)
            .Replace("\"size\":1024", "\"size\":2000001", StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(WrapPost($$"""{"$type":"app.bsky.embed.images","images":[{"image":{{invalidImage}},"alt":""}]}"""));

        Assert.NotNull(JsonSerializer.Deserialize<Post>(document.RootElement, BlueskyJsonSerializerOptions.Default));
        Assert.False(Post.TryParse(document, out _));
        Post post = Post.Parse(document, validate: false);
        Assert.Contains(post.Validate(new ValidationContext(post)), result => result.MemberNames.Contains("EmbeddedRecord.Images[0].Image.Size"));
        Assert.Contains(post.Validate(new ValidationContext(post)), result => result.MemberNames.Contains("EmbeddedRecord.Images[0].Image.MimeType"));
    }

    [Theory]
    [InlineData("images", false, "Images[0].AspectRatio.Width")]
    [InlineData("images", true, "Images[0].AspectRatio.Width")]
    [InlineData("gallery", false, "Items[0].AspectRatio.Width")]
    [InlineData("gallery", true, "Items[0].AspectRatio.Width")]
    [InlineData("video", false, "Captions[0].Lang")]
    [InlineData("video", true, "Captions[0].Lang")]
    [InlineData("external", false, "External.Uri")]
    [InlineData("external", true, "External.Uri")]
    public void TolerantPostParsingRetainsInvalidNestedMedia(string kind, bool withRecord, string expectedMember)
    {
        string embed = CreateEmbed(kind);
        if (withRecord)
        {
            embed = $$"""{"$type":"app.bsky.embed.recordWithMedia","record":{{RecordReference}},"media":{{embed}}}""";
        }

        using JsonDocument document = JsonDocument.Parse(WrapPost(embed));

        Assert.False(Post.TryParse(document, out Post? rejected));
        Assert.Null(rejected);
        Assert.Throws<JsonException>(() => Post.Parse(document));
        Assert.True(Post.TryParse(document, validate: false, out Post? post));
        Assert.NotNull(post);
        Assert.True(Post.TryParse(document.RootElement, validate: false, out _));
        Assert.NotNull(Post.Parse(document.RootElement, validate: false));

        string member = $"EmbeddedRecord.{(withRecord ? "Media." : string.Empty)}{expectedMember}";
        Assert.Contains(post.Validate(new ValidationContext(post)), result => result.MemberNames.Contains(member));
        EmbeddedBase? media = withRecord ? Assert.IsType<EmbeddedRecordWithMedia>(post.EmbeddedRecord).Media : post.EmbeddedRecord;
        switch (media)
        {
            case EmbeddedImages images:
                Assert.Equal(0, Assert.Single(images.Images).AspectRatio!.Width);
                break;
            case EmbeddedGallery gallery:
                Assert.Equal(0, Assert.Single(gallery.Items).AspectRatio.Width);
                break;
            case EmbeddedVideo video:
                Assert.Equal(" ", Assert.Single(video.Captions!).Lang);
                Assert.Equal(0, video.AspectRatio!.Width);
                break;
            case EmbeddedExternal external:
                Assert.Equal(" ", external.External.Uri);
                break;
            case EmbeddedExternalMedia external:
                Assert.Equal(" ", external.External.Uri);
                break;
            default:
                Assert.Fail("The tolerant parser did not retain the media type.");
                break;
        }

        Exception? exception = Xunit.Record.Exception(() => JsonSerializer.Deserialize<Post>(document.RootElement, BlueskyJsonSerializerOptions.Default));
        Assert.True(exception is JsonException or ArgumentException, exception?.ToString());
    }

    [Theory]
    [InlineData("images")]
    [InlineData("gallery")]
    [InlineData("video")]
    [InlineData("external")]
    public void TolerantFallbackAlsoWorksWhenPostTextIsInvalid(string kind)
    {
        string json = WrapPost(CreateEmbed(kind)).Replace("\"text\":\"post\"", $"\"text\":\"{new string('x', Maximum.PostLengthInGraphemes + 1)}\"", StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(json);

        Post post = Post.Parse(document, validate: false);
        ValidationResult[] results = [.. post.Validate(new ValidationContext(post))];

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(Post.Text)));
        Assert.Contains(results, result => result.MemberNames.Any(member => member.StartsWith("EmbeddedRecord.", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("""{"$type":"app.bsky.embed.images","images":[{"image":null,"alt":"x"}]}""")]
    [InlineData("""{"$type":"app.bsky.embed.images","images":[{"alt":"x"}]}""")]
    [InlineData("""{"$type":"app.bsky.embed.images","images":[{"image":null,"alt":"x","aspectRatio":{"width":"bad","height":1}}]}""")]
    [InlineData("""{"$type":"app.bsky.embed.external","external":{"uri":"","title":"title"}}""")]
    [InlineData("""{"$type":"app.bsky.embed.video","video":null,"captions":[{"lang":"","file":null}]}""")]
    public void TolerantModeStillRejectsMalformedMediaShapes(string embed)
    {
        using JsonDocument document = JsonDocument.Parse(WrapPost(embed));

        Assert.False(Post.TryParse(document, validate: false, out Post? result));
        Assert.Null(result);
        Assert.Throws<JsonException>(() => Post.Parse(document, validate: false));
    }

    [Fact]
    public void TolerantParsingDoesNotWeakenPublicConstructionOrInitAccessors()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AspectRatio(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AspectRatio(1, 1) { Height = -1 });
        Assert.Throws<ArgumentException>(() => new Caption(" ", CreateBlob("text/vtt")));
        Assert.Throws<ArgumentException>(() => new Embed.External.Properties(" ", "", ""));
        Assert.Throws<ArgumentException>(() => new Embed.External.Properties("https://example.com", "", "") { Uri = " " });
    }

    [Fact]
    public void TolerantParsingRetainsExtensionDataAndDoesNotChangeSubsequentStrictReads()
    {
        string embed = CreateEmbed("images").Replace("\"images\":", "\"custom\":{\"value\":42},\"images\":", StringComparison.Ordinal);
        Post post;
        using (JsonDocument document = JsonDocument.Parse(WrapPost(embed).Replace("\"embed\":", "\"topExtension\":{\"value\":43},\"embed\":", StringComparison.Ordinal)))
        {
            post = Post.Parse(document, validate: false);
        }

        Assert.Equal(42, post.EmbeddedRecord!.ExtensionData["custom"].GetProperty("value").GetInt32());
        Assert.Equal(43, post.ExtensionData["topExtension"].GetProperty("value").GetInt32());
        Assert.DoesNotContain("$type", post.ExtensionData.Keys);
        string validJson = WrapPost(CreateEmbed("images").Replace("\"width\":0", "\"width\":1", StringComparison.Ordinal));
        using JsonDocument validDocument = JsonDocument.Parse(validJson);
        Assert.True(Post.TryParse(validDocument, out Post? validPost));
        Assert.Empty(validPost!.Validate(new ValidationContext(validPost)));
        using JsonDocument invalidDocument = JsonDocument.Parse(WrapPost(CreateEmbed("images")));
        Assert.False(Post.TryParse(invalidDocument, out _));
    }

    private static idunno.AtProto.Blob CreateBlob(string mimeType) =>
        new(new idunno.AtProto.CidLink("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), mimeType, 1024);

    private static string WrapPost(string embed) =>
        $$"""{"$type":"app.bsky.feed.post","text":"post","createdAt":"2026-10-08T15:43:06Z","embed":{{embed}}}""";

    private static string CreateEmbed(string kind) => kind switch
    {
        "images" => $$$"""{"$type":"app.bsky.embed.images","images":[{"image":{{{ImageBlob}}},"alt":"","aspectRatio":{"width":0,"height":1}}]}""",
        "gallery" => $$$"""{"$type":"app.bsky.embed.gallery","items":[{"$type":"app.bsky.embed.gallery#image","image":{{{ImageBlob}}},"alt":"","aspectRatio":{"width":0,"height":1}}]}""",
        "video" => $$$"""{"$type":"app.bsky.embed.video","video":{{{ImageBlob.Replace("image/jpeg", "video/mp4", StringComparison.Ordinal)}}},"aspectRatio":{"width":0,"height":1},"captions":[{"lang":" ","file":{{{ImageBlob.Replace("image/jpeg", "text/vtt", StringComparison.Ordinal)}}}}]}""",
        "external" => """{"$type":"app.bsky.embed.external","external":{"uri":" ","title":"","description":""}}""",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
