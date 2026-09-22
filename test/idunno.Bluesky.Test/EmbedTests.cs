// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Embed.Gallery;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class EmbedTests
{
    private static readonly JsonSerializerOptions s_options = BlueskyServer.BlueskyJsonSerializerOptions;

    private const string BlobJson = """{"$type":"blob","ref":{"$link":"bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"},"mimeType":"image/jpeg","size":1024}""";

    private static Blob CreateBlob(string mimeType = "image/jpeg") =>
        new(new CidLink("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), mimeType: mimeType, size: 1024);

    private static GalleryImage CreateGalleryImage(string altText = "alt") =>
        new(CreateBlob(), altText, new AspectRatio(1, 1));

    private static string CreateGalleryJson(int itemCount)
    {
        IEnumerable<string> items = Enumerable
            .Range(0, itemCount)
            .Select(index => $$$"""{"$type":"app.bsky.embed.gallery#image","image":{{{BlobJson}}},"alt":"alt {{{index}}}","aspectRatio":{"width":1,"height":1}}""");

        return $$"""{"items":[{{string.Join(",", items)}}]}""";
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(20)]
    [InlineData(25)]
    public void EmbeddedGalleryDeserializesAnyNumberOfItems(int itemCount)
    {
        EmbeddedGallery? gallery = JsonSerializer.Deserialize<EmbeddedGallery>(CreateGalleryJson(itemCount), s_options);

        Assert.NotNull(gallery);
        Assert.Equal(itemCount, gallery.Count);
        Assert.Equal(itemCount, gallery.Items.Count);
    }

    [Fact]
    public void EmbeddedGalleryConstructorRejectsAnEmptyCollection()
    {
        Assert.Throws<ArgumentException>(() => new EmbeddedGallery(new List<GalleryImage>()));
    }

    [Fact]
    public void EmbeddedGalleryConstructorRejectsMoreItemsThanTheAuthoringMaximum()
    {
        List<GalleryImage> items = [.. Enumerable.Range(0, Maximum.GalleryItems + 1).Select(index => CreateGalleryImage($"alt {index}"))];

        Assert.Throws<ArgumentException>(() => new EmbeddedGallery(items));
    }

    [Fact]
    public void EmbeddedGalleryAddRejectsNull()
    {
        EmbeddedGallery gallery = new([CreateGalleryImage()]);

        Assert.Throws<ArgumentNullException>(() => gallery.Add(null!));
    }

    [Fact]
    public void EmbeddedGalleryAddRejectsANonImageMimeType()
    {
        EmbeddedGallery gallery = new([CreateGalleryImage()]);

        GalleryImage video = new(CreateBlob("video/mp4"), "alt", new AspectRatio(1, 1));

        Assert.Throws<ArgumentException>(() => gallery.Add(video));
    }

    [Fact]
    public void EmbeddedGalleryAddCannotExceedTheAuthoringMaximum()
    {
        EmbeddedGallery gallery = new([.. Enumerable.Range(0, Maximum.GalleryItems).Select(index => CreateGalleryImage($"alt {index}"))]);

        Assert.Throws<InvalidOperationException>(() => gallery.Add(CreateGalleryImage("one too many")));
    }

    [Fact]
    public void EmbeddedGalleryItemsAreCopiedByAWithExpression()
    {
        EmbeddedGallery gallery = new([CreateGalleryImage()]);
        EmbeddedGallery copy = gallery with { };

        copy.Add(CreateGalleryImage("added to the copy"));

        Assert.Equal(1, gallery.Count);
        Assert.Equal(2, copy.Count);
    }

    [Fact]
    public void EmbeddedGalleryItemsAreCopiedFromTheConstructorCollection()
    {
        List<GalleryImage> items = [CreateGalleryImage()];
        EmbeddedGallery gallery = new(items);

        items.Add(CreateGalleryImage("added after construction"));

        Assert.Equal(1, gallery.Count);
    }

    [Fact]
    public void EmbeddedVideoViewDeserializesWithoutAThumbnailOrAltText()
    {
        const string json = """{"cid":"bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a","playlist":"https://video.bsky.app/playlist.m3u8"}""";

        EmbeddedVideoView? view = JsonSerializer.Deserialize<EmbeddedVideoView>(json, s_options);

        Assert.NotNull(view);
        Assert.Null(view.ThumbnailUri);
        Assert.Null(view.AltText);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public void AspectRatioRejectsValuesBelowOne(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AspectRatio(width, height));
    }

    [Fact]
    public void AspectRatioRejectsValuesBelowOneInAWithExpression()
    {
        AspectRatio aspectRatio = new(16, 9);

        Assert.Throws<ArgumentOutOfRangeException>(() => aspectRatio with { Width = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => aspectRatio with { Height = 0 });
    }

    [Fact]
    public void EmbeddedImageConstructorRejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new EmbeddedImage(null!, "alt"));
        Assert.Throws<ArgumentNullException>(() => new EmbeddedImage(CreateBlob(), null!));
    }

    [Fact]
    public void GalleryViewImageAcceptsRelativeUris()
    {
        ViewImage viewImage = new(new Uri("/thumb", UriKind.Relative), new Uri("/fullsize", UriKind.Relative), "alt", new AspectRatio(1, 1));

        Assert.False(viewImage.Thumbnail.IsAbsoluteUri);
        Assert.False(viewImage.FullSize.IsAbsoluteUri);
    }

    [Fact]
    public void GalleryViewItemsAreCopiedFromTheConstructorCollection()
    {
        List<ViewImage> items = [new(new Uri("https://example.org/thumb"), new Uri("https://example.org/full"), "alt", new AspectRatio(1, 1))];

        Embed.Gallery.View view = new(items);

        items.Clear();

        Assert.Single(view.Items);
    }

    [Fact]
    public void ANotFoundRecordViewDeserializesToViewNotFound()
    {
        const string json = """{"$type":"app.bsky.embed.record#view","record":{"$type":"app.bsky.embed.record#viewNotFound","uri":"at://did:plc:test/app.bsky.feed.post/abcdefghijklm","notFound":true}}""";

        EmbeddedRecordView? view = JsonSerializer.Deserialize<EmbeddedView>(json, s_options) as EmbeddedRecordView;

        Assert.NotNull(view);
        ViewNotFound notFound = Assert.IsType<ViewNotFound>(view.Record);
        Assert.True(notFound.NotFound);
    }

    [Fact]
    public void ABlockedRecordViewDeserializesToViewBlocked()
    {
        const string json = """{"$type":"app.bsky.embed.record#view","record":{"$type":"app.bsky.embed.record#viewBlocked","uri":"at://did:plc:test/app.bsky.feed.post/abcdefghijklm","blocked":true,"author":{"did":"did:plc:test","viewer":{"blockedBy":true}}}}""";

        EmbeddedRecordView? view = JsonSerializer.Deserialize<EmbeddedView>(json, s_options) as EmbeddedRecordView;

        Assert.NotNull(view);
        ViewBlocked blocked = Assert.IsType<ViewBlocked>(view.Record);
        Assert.True(blocked.Blocked);
    }

    [Fact]
    public void ADetachedRecordViewDeserializesToViewDetached()
    {
        const string json = """{"$type":"app.bsky.embed.record#view","record":{"$type":"app.bsky.embed.record#viewDetached","uri":"at://did:plc:test/app.bsky.feed.post/abcdefghijklm","detached":true}}""";

        EmbeddedRecordView? view = JsonSerializer.Deserialize<EmbeddedView>(json, s_options) as EmbeddedRecordView;

        Assert.NotNull(view);
        ViewDetached detached = Assert.IsType<ViewDetached>(view.Record);
        Assert.True(detached.Detached);
    }
}
