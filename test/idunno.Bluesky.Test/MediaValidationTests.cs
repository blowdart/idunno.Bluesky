// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Embed.Gallery;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class MediaValidationTests
{
    public static IEnumerable<TheoryDataRow<IValidatableObject>> ValidMedia =>
    [
        new(new AspectRatio(1, 1)),
        new(new EmbeddedImage(CreateBlob("image/jpeg", 2_000_000), "")),
        new(new GalleryImage(CreateBlob("image/png", 2_000_000), "", new AspectRatio(1, 1))),
        new(new Caption("en", CreateBlob("text/vtt", 20_000))),
        new(new EmbeddedVideo(CreateBlob("video/mp4", 300_000_000), presentation: "future-hint")),
        new(new EmbeddedImages([new EmbeddedImage(CreateBlob("image/jpeg", 1), new string('x', 10_000))])),
        new(new EmbeddedImages()),
        new(new EmbeddedGallery()),
        new(new EmbeddedExternal("https://example.com", "", "")),
        new(new EmbeddedExternalMedia(new Embed.External.Properties("https://example.com", "", ""))),
        new(new Embed.External.Properties("https://example.com", "", "", CreateBlob("image/jpeg", 1_000_000))),
        new(new EmbeddedRecord(CreateReference())),
        new(new EmbeddedRecordWithMedia(new EmbeddedRecord(CreateReference()), new EmbeddedImages())),
        new(new Profile("name", "description", "pronouns", avatar: CreateBlob("image/jpeg", 1_000_000)))
    ];

    [Theory]
    [MemberData(nameof(ValidMedia))]
    public void ValidModelsReturnNoFailuresAndRejectNullContexts(IValidatableObject value)
    {
        Assert.Empty(value.Validate(new ValidationContext(value)));
        Assert.Throws<ArgumentNullException>(() => value.Validate(null!));
        List<ValidationResult> results = [];
        Assert.True(Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true));
        Assert.Empty(results);
    }

    [Theory]
    [InlineData("image/jpeg", 2_000_000, true)]
    [InlineData("image/png", 2_000_001, false)]
    [InlineData("image/gif", 0, true)]
    [InlineData("image/jpeg", -1, false)]
    [InlineData("video/mp4", 1024, false)]
    [InlineData("image/", 1024, false)]
    public void ImageValidationChecksBlobConstraints(string mimeType, int size, bool valid)
    {
        EmbeddedImage image = new(CreateBlob(mimeType, size), "");
        GalleryImage galleryImage = new(image with { AspectRatio = new AspectRatio(1, 1) });

        Assert.Equal(valid, !image.Validate(new ValidationContext(image)).Any());
        Assert.Equal(valid, !galleryImage.Validate(new ValidationContext(galleryImage)).Any());
    }

    [Theory]
    [InlineData("video/mp4", 300_000_000, true)]
    [InlineData("video/mp4", 300_000_001, false)]
    [InlineData("video/webm", 1024, false)]
    public void VideoValidationChecksBlobConstraints(string mimeType, int size, bool valid)
    {
        EmbeddedVideo video = new(CreateBlob(mimeType, size));

        Assert.Equal(valid, !video.Validate(new ValidationContext(video)).Any());
    }

    [Theory]
    [InlineData("text/vtt", 20_000, true)]
    [InlineData("text/vtt", 20_001, false)]
    [InlineData("text/plain", 1024, false)]
    public void CaptionValidationChecksBlobConstraints(string mimeType, int size, bool valid)
    {
        Caption caption = new("en", CreateBlob(mimeType, size));

        Assert.Equal(valid, !caption.Validate(new ValidationContext(caption)).Any());
    }

    [Theory]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void VideoValidationChecksCaptionCount(int count, bool valid)
    {
        Caption caption = new("en", CreateBlob("text/vtt", 1024));
        EmbeddedVideo video = new(CreateBlob("video/mp4", 1024), Enumerable.Repeat(caption, count).ToArray());

        Assert.Equal(valid, !video.Validate(new ValidationContext(video)).Any());
    }

    [Fact]
    public void ValidationReportsNestedNullValuesWithoutThrowing()
    {
        EmbeddedImage image = new(CreateBlob("image/jpeg", 1), "") { Image = null!, AltText = null! };
        EmbeddedImages images = new([image]) { Images = [image, null!] };
        Post post = new("post", embeddedRecord: new EmbeddedRecordWithMedia(new EmbeddedRecord(CreateReference()), images));

        ValidationResult[] results = [.. post.Validate(new ValidationContext(post))];

        Assert.Contains(results, result => result.MemberNames.Contains("EmbeddedRecord.Media.Images[0].Image"));
        Assert.Contains(results, result => result.MemberNames.Contains("EmbeddedRecord.Media.Images[0].AltText"));
        Assert.Contains(results, result => result.MemberNames.Contains("EmbeddedRecord.Media.Images[1]"));
        EmbeddedImages missing = new() { Images = null! };
        Assert.Single(missing.Validate(new ValidationContext(missing)));
        GalleryImage galleryImage = new(CreateBlob("image/jpeg", 1), "", new AspectRatio(1, 1)) { AspectRatio = null! };
        Assert.Contains(galleryImage.Validate(new ValidationContext(galleryImage)), result => result.MemberNames.Contains(nameof(GalleryImage.AspectRatio)));
    }

    [Fact]
    public void VideoValidationCascadesIntoCaptionAndDimensionFailures()
    {
        EmbeddedVideo video = new(CreateBlob("video/mp4", 1), [new Caption("", CreateBlob("text/plain", 20_001), validate: false), null!])
        {
            AspectRatio = new AspectRatio(0, -1, validate: false)
        };
        Post post = new("post", embeddedRecord: video);
        string[] members = [.. post.Validate(new ValidationContext(post)).SelectMany(result => result.MemberNames)];

        Assert.Contains("EmbeddedRecord.Captions[0].Lang", members);
        Assert.Contains("EmbeddedRecord.Captions[0].File.MimeType", members);
        Assert.Contains("EmbeddedRecord.Captions[0].File.Size", members);
        Assert.Contains("EmbeddedRecord.Captions[1]", members);
        Assert.Contains("EmbeddedRecord.AspectRatio.Width", members);
        Assert.Contains("EmbeddedRecord.AspectRatio.Height", members);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExternalValidationCascadesThroughBothCardRepresentations(bool withRecord)
    {
        Embed.External.Properties properties = new("not a URI", "", "", CreateBlob("video/mp4", 1_000_001));
        EmbeddedBase embed = withRecord
            ? new EmbeddedRecordWithMedia(new EmbeddedRecord(CreateReference()), new EmbeddedExternal(properties))
            : new EmbeddedExternal(properties);
        Post post = new("post", embeddedRecord: embed);
        string prefix = withRecord ? "EmbeddedRecord.Media.External." : "EmbeddedRecord.External.";
        string[] members = [.. post.Validate(new ValidationContext(post)).SelectMany(result => result.MemberNames)];

        Assert.Contains(prefix + "Uri", members);
        Assert.Contains(prefix + "Thumbnail.Size", members);
        Assert.Contains(prefix + "Thumbnail.MimeType", members);
    }

    [Theory]
    [InlineData("image/jpeg", 1_000_000, true)]
    [InlineData("image/png", 1_000_000, true)]
    [InlineData("image/gif", 1, false)]
    [InlineData("image/jpeg", 1_000_001, false)]
    public void ProfileValidationChecksAvatarAndBannerConstraints(string mimeType, int size, bool valid)
    {
        Blob image = CreateBlob(mimeType, size);
        Profile profile = new(avatar: image, banner: image);
        ValidationResult[] results = [.. profile.Validate(new ValidationContext(profile))];

        Assert.Equal(valid, results.Length == 0);
        if (!valid)
        {
            Assert.Contains(results, result => result.MemberNames.Any(member => member.StartsWith("Avatar.", StringComparison.Ordinal)));
            Assert.Contains(results, result => result.MemberNames.Any(member => member.StartsWith("Banner.", StringComparison.Ordinal)));
        }
    }

    private static Blob CreateBlob(string mimeType, int size) =>
        new(new CidLink("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), mimeType, size);

    private static StrongReference CreateReference() =>
        new(new AtUri("at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3ldegderd6s2e"),
            new Cid("bafyreiaahtgpdozb4wjdymb3tvifuwdmxjtumhjxl25lykvsoq3v3l4une"));
}
