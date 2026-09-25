// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Embed;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class PostBuilderValueSemanticsTests
{
    private const string FamilyGrapheme = "\U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466";

    private static EmbeddedImage CreateImage(string altText = "alt text") =>
        new(new Blob(new CidLink("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), "image/jpg", 1), altText);

    [Fact]
    public void GetHashCodeReturnsTheSameValueForAnUnmutatedBuilder()
    {
        PostBuilder postBuilder = new("hello");

        int first = postBuilder.GetHashCode();
        int second = postBuilder.GetHashCode();
        int third = postBuilder.GetHashCode();

        Assert.Equal(first, second);
        Assert.Equal(first, third);
    }

    [Fact]
    public void GetHashCodeReturnsTheSameValueForAnUnmutatedBuilderWithImages()
    {
        PostBuilder postBuilder = new("hello");
        postBuilder.Add(CreateImage());

        int first = postBuilder.GetHashCode();
        int second = postBuilder.GetHashCode();

        Assert.Equal(first, second);
    }

    [Fact]
    public void TwoBuildersWithTheSameContentAreEqual()
    {
        DateTimeOffset createdAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        PostBuilder left = new("hello", createdAt: createdAt);
        PostBuilder right = new("hello", createdAt: createdAt);

        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void TwoBuildersWithTheSameImagesAreEqual()
    {
        DateTimeOffset createdAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        PostBuilder left = new("hello", createdAt: createdAt);
        left.Add(CreateImage());

        PostBuilder right = new("hello", createdAt: createdAt);
        right.Add(CreateImage());

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void TwoBuildersWithDifferentImagesAreNotEqual()
    {
        DateTimeOffset createdAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        PostBuilder left = new("hello", createdAt: createdAt);
        left.Add(CreateImage("left"));

        PostBuilder right = new("hello", createdAt: createdAt);
        right.Add(CreateImage("right"));

        Assert.NotEqual(left, right);
        Assert.True(left != right);
    }

    [Fact]
    public void TwoBuildersWithDifferentTextAreNotEqual()
    {
        DateTimeOffset createdAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        PostBuilder left = new("hello", createdAt: createdAt);
        PostBuilder right = new("goodbye", createdAt: createdAt);

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void TwoPostsWithTheSameContentAreEqual()
    {
        DateTimeOffset createdAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Post left = new("hello", createdAt: createdAt) { Langs = ["en"], Tags = ["tag"] };
        Post right = new("hello", createdAt: createdAt) { Langs = ["en"], Tags = ["tag"] };

        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void TwoPostsWithDifferentLanguagesAreNotEqual()
    {
        DateTimeOffset createdAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Post left = new("hello", createdAt: createdAt) { Langs = ["en"] };
        Post right = new("hello", createdAt: createdAt) { Langs = ["fr"] };

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void TwoPostsCreatedAtDifferentTimesAreNotEqual()
    {
        Post left = new("hello", createdAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Post right = new("hello", createdAt: new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void TwoEmbeddedImagesWithTheSameImagesAreEqual()
    {
        EmbeddedImages left = new([CreateImage()]);
        EmbeddedImages right = new([CreateImage()]);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void EmbeddedImagesDoesNotAliasTheCollectionItWasConstructedFrom()
    {
        List<EmbeddedImage> images = [CreateImage()];

        EmbeddedImages embeddedImages = new(images);

        images.Add(CreateImage("second"));

        Assert.Single(embeddedImages.Images);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(272)]
    public void AppendRejectsTextWhoseUtf8LengthExceedsTheMaximumEvenWhenItsUtf16LengthDoesNot(int graphemeCount)
    {
        string text = string.Concat(Enumerable.Repeat(FamilyGrapheme, graphemeCount));

        PostBuilder postBuilder = new();

        Assert.True(text.Length <= postBuilder.MaxCapacity, "the UTF-16 length must be within the limit for this test to be meaningful");
        Assert.True(text.GetUtf8Length() > postBuilder.MaxCapacity);

        Assert.Throws<ArgumentOutOfRangeException>(() => postBuilder.Append(text));
    }

    [Fact]
    public void WithTextRejectsTextWhoseUtf8LengthExceedsTheMaximumEvenWhenItsUtf16LengthDoesNot()
    {
        string text = string.Concat(Enumerable.Repeat(FamilyGrapheme, 200));

        PostBuilder postBuilder = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => postBuilder.WithText(text));
    }

    [Fact]
    public void RepeatedAppendsCannotExceedTheMaximumUtf8Length()
    {
        string chunk = string.Concat(Enumerable.Repeat(FamilyGrapheme, 50));

        PostBuilder postBuilder = new();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            for (int i = 0; i < 10; i++)
            {
                postBuilder.Append(chunk);
            }
        });

        Assert.True(postBuilder.Text is null || postBuilder.Text.GetUtf8Length() <= postBuilder.MaxCapacity);
    }

    [Fact]
    public void ToPostReturnsAPostWhichDoesNotAliasTheBuildersImages()
    {
        PostBuilder postBuilder = new("hello");
        postBuilder.Add(CreateImage());

        Post post = postBuilder.ToPost();

        EmbeddedImages embeddedImages = Assert.IsType<EmbeddedImages>(post.EmbeddedRecord);
        Assert.Single(embeddedImages.Images);

        postBuilder.Add(CreateImage("second"));

        Assert.Single(embeddedImages.Images);
    }

    [Fact]
    public void ToPostDoesNotMutateTheBuilder()
    {
        PostBuilder postBuilder = new("hello");
        postBuilder.Add(CreateImage());

        Assert.False(postBuilder.HasEmbed);

        postBuilder.ToPost();

        Assert.False(postBuilder.HasEmbed);
    }

    [Fact]
    public void ToPostLeavesTheBuilderEqualToAnIdenticalUnconvertedBuilder()
    {
        DateTimeOffset createdAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        PostBuilder converted = new("hello", createdAt: createdAt);
        converted.Add(CreateImage());

        PostBuilder untouched = new("hello", createdAt: createdAt);
        untouched.Add(CreateImage());

        converted.ToPost();

        Assert.Equal(untouched, converted);
    }

    [Fact]
    public void ToPostCanBeCalledRepeatedlyAndReturnsEqualPosts()
    {
        PostBuilder postBuilder = new("hello");
        postBuilder.Add(CreateImage());

        Post first = postBuilder.ToPost();
        Post second = postBuilder.ToPost();

        Assert.Equal(first, second);
    }
}
