// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.Embed;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky.Test
{
    [ExcludeFromCodeCoverage]
    public class PostTests
    {
        [Fact]
        public void EmptyConstructorSetsText()
        {
            DateTimeOffset start = DateTimeOffset.UtcNow;
            var post = new Post();
            DateTimeOffset end = DateTimeOffset.UtcNow;

            Assert.True(post.CreatedAt >= start);
            Assert.True(post.CreatedAt <= end);
        }

        [Fact]
        public void TextOnlyConstructorSetsText()
        {
            const string postText = "post text";

            DateTimeOffset start = DateTimeOffset.UtcNow;
            var post = new Post(postText);
            DateTimeOffset end = DateTimeOffset.UtcNow;

            Assert.Equal(postText, post.Text);
            Assert.True(post.CreatedAt >= start);
            Assert.True(post.CreatedAt <= end);
        }

        [Fact]
        public void TextOnlyConstructorSetsTextAndCreatedAt()
        {
            const string postText = "post text";

            var post = new Post(postText);
            Assert.Equal(postText, post.Text);


        }

        [Fact]
        public void TextAndLanguageOnlyConstructorSetsTextAndLanguages()
        {
            const string postText = "post text";
            DateTimeOffset expectedCreationDate = DateTimeOffset.UtcNow;

            var post = new Post(postText, expectedCreationDate);
            Assert.Equal(postText, post.Text);
            Assert.Equal(expectedCreationDate, post.CreatedAt);
        }

        [Fact]
        public void TextAndLanguagesConstructorSetsTextAndLanguages()
        {
            const string postText = "post text";
            List<string> expectedLanguages = ["en-gb", "en-us"];

            var post = new Post(postText, langs: expectedLanguages);
            Assert.Equal(postText, post.Text);
            Assert.NotNull(post.Langs);
            Assert.NotEmpty(post.Langs);
            Assert.Equal(expectedLanguages, post.Langs);
        }

        [Fact]
        public void TextAndPropertiesConstructorSetsEverythingCorrectly()
        {
            const string postText = "post text";
            StrongReference strongReference = new(new AtUri("at://foo.com/com.example.foo/123"), new Cid("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4"));

            List<string> expectedLanguages = ["en-gb", "en-us"];
            List<Facet> expectedFacets = [new Facet(new ByteSlice(0, 1), [new MentionFacetFeature(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"))])];
            EmbeddedBase expectedEmbedded = new EmbeddedRecord(strongReference);
            ReplyReferences expectedReplyReferences = new(strongReference, strongReference);
            SelfLabels expectedLabels = new();
            expectedLabels.AddLabel("labelTest");
            List<string> expectedTags = ["selfTag"];

            var post = new Post(
                postText,
                expectedFacets,
                expectedLanguages,
                expectedEmbedded,
                expectedReplyReferences,
                expectedLabels,
                expectedTags);

            Assert.Equal(postText, post.Text);
            Assert.Equal(expectedFacets, post.Facets);
            Assert.Equal(expectedLanguages, post.Langs);
            Assert.Equal(expectedEmbedded, post.EmbeddedRecord);
            Assert.Equal(expectedReplyReferences, post.Reply);
            Assert.Equal(expectedReplyReferences, post.Reply);
            Assert.Equal(expectedLabels, post.Labels);
            Assert.Equal(expectedTags, post.Tags);
        }

        [Fact]
        public void TextCreationDateAndPropertiesConstructorSetsEverythingCorrectly()
        {
            const string postText = "post text";
            StrongReference strongReference = new(new AtUri("at://foo.com/com.example.foo/123"), new Cid("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4"));

            DateTimeOffset expectedDate = DateTimeOffset.UtcNow;
            List<string> expectedLanguages = ["en-gb", "en-us"];
            List<Facet> expectedFacets = [new Facet(new ByteSlice(0, 1), [new MentionFacetFeature(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"))])];
            EmbeddedBase expectedEmbedded = new EmbeddedRecord(strongReference);
            ReplyReferences expectedReplyReferences = new(strongReference, strongReference);
            SelfLabels expectedLabels = new();
            expectedLabels.AddLabel("labelTest");
            List<string> expectedTags = ["selfTag"];

            var post = new Post(
                postText,
                expectedDate,
                expectedFacets,
                expectedLanguages,
                expectedEmbedded,
                expectedReplyReferences,
                expectedLabels,
                expectedTags);

            Assert.Equal(postText, post.Text);
            Assert.Equal(expectedDate, post.CreatedAt);
            Assert.Equal(expectedFacets, post.Facets);
            Assert.Equal(expectedLanguages, post.Langs);
            Assert.Equal(expectedEmbedded, post.EmbeddedRecord);
            Assert.Equal(expectedReplyReferences, post.Reply);
            Assert.Equal(expectedReplyReferences, post.Reply);
            Assert.Equal(expectedLabels, post.Labels);
            Assert.Equal(expectedTags, post.Tags);
        }

        [Fact]
        public void CopyConstructorCopiesEverythingCorrectly()
        {
            const string postText = "post text";
            StrongReference strongReference = new(new AtUri("at://foo.com/com.example.foo/123"), new Cid("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4"));

            DateTimeOffset expectedDate = DateTimeOffset.UtcNow;
            List<string> expectedLanguages = ["en-gb", "en-us"];
            List<Facet> expectedFacets = [new Facet(new ByteSlice(0, 1), [new MentionFacetFeature(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk"))])];
            EmbeddedBase expectedEmbedded = new EmbeddedRecord(strongReference);
            ReplyReferences expectedReplyReferences = new(strongReference, strongReference);
            SelfLabels expectedLabels = new();
            expectedLabels.AddLabel("labelTest");
            List<string> expectedTags = ["selfTag"];

            var originalPost = new Post(
                postText,
                expectedDate,
                expectedFacets,
                expectedLanguages,
                expectedEmbedded,
                expectedReplyReferences,
                expectedLabels,
                expectedTags);

            var post = new Post(originalPost);

            Assert.Equal(postText, post.Text);
            Assert.Equal(expectedDate, post.CreatedAt);
            Assert.Equal(expectedFacets, post.Facets);
            Assert.Equal(expectedLanguages, post.Langs);
            Assert.Equal(expectedEmbedded, post.EmbeddedRecord);
            Assert.Equal(expectedReplyReferences, post.Reply);
            Assert.NotNull(post.Labels);
            Assert.Equal(expectedLabels.Values, post.Labels.Values);
            Assert.Equal(expectedTags, post.Tags);
        }

        [Fact]
        public void TextLangConstructorThrowsIfTextIsEmpty()
        {
            ArgumentNullException caughtException = Assert.Throws<ArgumentNullException>(() => new Post(string.Empty));

            Assert.Equal("text", caughtException.ParamName);
        }

        [Fact]
        public void TextLangConstructorThrowsIfLangIsEmpty()
        {
            ArgumentException caughtException = Assert.Throws<ArgumentException>(() => new Post("text", string.Empty));

            Assert.Equal("lang", caughtException.ParamName);
        }

        [Fact]
        public void TextLangsConstructorThrowsIfTextIsEmpty()
        {
            ArgumentNullException caughtException = Assert.Throws<ArgumentNullException>(() => new Post(string.Empty, ["en-gb"]));

            Assert.Equal("text", caughtException.ParamName);
        }

        [Fact]
        public void TextLangsConstructorThrowsIfLangsIsEmpty()
        {
            ArgumentOutOfRangeException caughtException = Assert.Throws<ArgumentOutOfRangeException>(() => new Post("text", langs: []));

            Assert.Equal("langs.Count", caughtException.ParamName);
        }

        [Fact]
        public void ConstructorWithoutCreatedAtThrowsIfTextIsNullAndEmbedIsNull()
        {
            ArgumentNullException caughtException = Assert.Throws<ArgumentNullException>(() => new Post(text: null));

            Assert.Equal("text", caughtException.ParamName);
        }

        [Fact]
        public void ConstructorWithCreatedAtThrowsIfTextIsNullAndEmbedIsNull()
        {
            ArgumentNullException caughtException = Assert.Throws<ArgumentNullException>(() => new Post(text: null, createdAt: DateTimeOffset.UtcNow));

            Assert.Equal("text", caughtException.ParamName);
        }

        [Fact]
        public void ConstructorDoesNotThrowWhenTextIsNullAndThereIsAnEmbed()
        {
            StrongReference strongReference = new(new AtUri("at://foo.com/com.example.foo/123"), new Cid("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4"));
            EmbeddedBase expectedEmbedded = new EmbeddedRecord(strongReference);

            var post = new Post(null, embeddedRecord : expectedEmbedded);

            Assert.Null(post.Text);
            Assert.Equal(expectedEmbedded, post.EmbeddedRecord);
        }

        [Fact]
        public void ConstructorDoesNotThrowWhenTextIsEmptyAndThereIsAnEmbed()
        {
            StrongReference strongReference = new(new AtUri("at://foo.com/com.example.foo/123"), new Cid("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4"));
            EmbeddedBase expectedEmbedded = new EmbeddedRecord(strongReference);

            var post = new Post(string.Empty, embeddedRecord: expectedEmbedded);

            Assert.Equal(string.Empty, post.Text);
            Assert.Equal(expectedEmbedded, post.EmbeddedRecord);
        }

        [Fact]
        public void ConstructorThrowsWhenTextIsTooLongInCharacters()
        {
            string text = new ('a', Maximum.PostLengthInCharacters + 1);
            ArgumentOutOfRangeException caughtException = Assert.Throws<ArgumentOutOfRangeException>(() => new Post(text));

            Assert.Equal("text", caughtException.ParamName);
        }

        [Fact]
        public void ConstructorThrowsWhenTextIsTooInGraphemes()
        {
            string text = new('a', Maximum.PostLengthInGraphemes + 1);
            ArgumentOutOfRangeException caughtException = Assert.Throws<ArgumentOutOfRangeException>(() => new Post(text));

            Assert.Equal("text", caughtException.ParamName);
        }

        [Fact]
        public void ConstructorDoesNotThrowsWhenTextIsTooInGraphemes()
        {
            string text = new('a', Maximum.PostLengthInGraphemes);

            var post = new Post(text);

            Assert.NotNull(post.Text);
            Assert.Equal(Maximum.PostLengthInGraphemes, post.Text.GetGraphemeLength());
        }


        [Fact]
        public void ConstructorThrowsWhenTheMaximumNumberOfAllowedTagsIsExceeded()
        {
            List<string> tags = [];

            for (int i=0; i<= Maximum.ExternalTagsInPost; i++)
            {
                tags.Add(i.ToString());
            }

            ArgumentOutOfRangeException caughtException = Assert.Throws<ArgumentOutOfRangeException>(() => new Post("text", tags: tags));

            Assert.Equal("tags", caughtException.ParamName);
        }

        [Fact]
        public void ConstructorThrowsWhenAnEmptyTagIsPassed()
        {
            List<string> tags = [string.Empty];

            ArgumentException caughtException = Assert.Throws<ArgumentException>(() => new Post("text", tags: tags));

            Assert.Equal("tags", caughtException.ParamName);
        }

        [Fact]
        public void ConstructorThrowsWhenAnTooLongTagInGraphemesIsPassed()
        {
            List<string> tags = [new('x', Maximum.TagLengthInGraphemes + 1)];

            ArgumentException caughtException = Assert.Throws<ArgumentException>(() => new Post("text", tags: tags));

            Assert.Equal("tags", caughtException.ParamName);
        }

        [Fact]
        public void ConstructorThrowsWhenAnTooLongTagInCharactersIsPassed()
        {
            List<string> tags = [new('x', Maximum.TagLengthInCharacters + 1)];

            ArgumentException caughtException = Assert.Throws<ArgumentException>(() => new Post("text", tags: tags));

            Assert.Equal("tags", caughtException.ParamName);
        }

        [Fact]
        public void ConstructorThrowsWhenImageIsNull()
        {
            EmbeddedImage? image = null;

            ArgumentNullException caughtException = Assert.Throws<ArgumentNullException>(() => new Post("text", image: image!));

            Assert.Equal("image", caughtException.ParamName);
        }

        [Fact]
        public void ConstructorDoesNotThrowWhenImageAndTextIsProvided()
        {
            var image = new EmbeddedImage(new Blob(new BlobReference("https://example.org/image"), "image/jpg", 1024), "alt text");

            var post = new Post("text", image);

            Assert.Equal("text", post.Text);
            Assert.IsType<EmbeddedImages>(post.EmbeddedRecord);

            EmbeddedImages postImages = (EmbeddedImages)post.EmbeddedRecord;

            Assert.Single(postImages.Images);
        }

        [Fact]
        public void ConstructorThrowsWhenImageIsNullAndCreatedAtProvided()
        {
            EmbeddedImage? image = null;

            ArgumentNullException caughtException = Assert.Throws<ArgumentNullException>(() => new Post("text", createdAt: DateTimeOffset.UtcNow, image: image!));

            Assert.Equal("image", caughtException.ParamName);
        }

        [Fact]
        public void ConstructorDoesNotThrowWhenImageAndTextAndCreatedAtIsProvided()
        {
            var image = new EmbeddedImage(new Blob(new BlobReference("https://example.org/image"), "image/jpg", 1024), "alt text");

            var post = new Post("text", DateTimeOffset.UtcNow, image);

            Assert.Equal("text", post.Text);
            Assert.IsType<EmbeddedImages>(post.EmbeddedRecord);

            EmbeddedImages postImages = (EmbeddedImages)post.EmbeddedRecord;

            Assert.Single(postImages.Images);
        }

        [Fact]
        public void ConstructorThrowsWhenImagesIsNull()
        {
            List<EmbeddedImage>? images = null;

            ArgumentNullException caughtException = Assert.Throws<ArgumentNullException>(() => new Post("text", images: images!));

            Assert.Equal("images", caughtException.ParamName);
        }

        [Fact]
        public void ConstructorThrowsWhenImagesIsNullAndCreatedAtProvided()
        {
            List<EmbeddedImage>? images = null;

            ArgumentNullException caughtException = Assert.Throws<ArgumentNullException>(() => new Post("text", createdAt: DateTimeOffset.UtcNow, images: images!));

            Assert.Equal("images", caughtException.ParamName);
        }

        [Fact]
        public void ConstructorThrowsWhenImagesIsEmpty()
        {
            List<EmbeddedImage> images = [];

            ArgumentOutOfRangeException caughtException = Assert.Throws<ArgumentOutOfRangeException>(() => new Post("text", images: images));

            Assert.Equal("images.Count", caughtException.ParamName);
        }

        [Fact]
        public void ConstructorThrowsWhenImagesIsEmptyAndCreatedAtProvided()
        {
            List<EmbeddedImage> images = [];

            ArgumentOutOfRangeException caughtException = Assert.Throws<ArgumentOutOfRangeException>(() => new Post("text", createdAt: DateTimeOffset.UtcNow, images: images));

            Assert.Equal("images.Count", caughtException.ParamName);
        }

        [Fact]
        public void ConstructorThrowsWhenImagesIsHasTooManyImages()
        {
            List<EmbeddedImage> images = [];

            for (int i = 0; i <= Maximum.ImagesInPost; i++)
            {
                images.Add(new EmbeddedImage(new Blob(new BlobReference("https://example.org/image"), "image/jpg", 1024), "alt text"));
            }

            ArgumentOutOfRangeException caughtException = Assert.Throws<ArgumentOutOfRangeException>(() => new Post("text", images: images));

            Assert.Equal("images.Count", caughtException.ParamName);
        }

        [Fact]
        public void ConstructorDoesNotThrowsWhenImagesIsHasEnoughImages()
        {
            List<EmbeddedImage> images = [];

            for (int i = 0; i < Maximum.ImagesInPost; i++)
            {
                images.Add(new EmbeddedImage(new Blob(new BlobReference("https://example.org/image"), "image/jpg", 1024), "alt text"));
            }

            var post = new Post("text", images);

            Assert.Equal("text", post.Text);
            Assert.IsType<EmbeddedImages>(post.EmbeddedRecord);

            EmbeddedImages postImages = (EmbeddedImages)post.EmbeddedRecord;

            Assert.Equal(Maximum.ImagesInPost, postImages.Images.Count);
        }

        [Fact]
        public void ConstructorThrowsWhenImagesIsHasTooManyImagesAndCreatedAtProvided()
        {
            List<EmbeddedImage> images = [];

            for (int i = 0; i <= Maximum.ImagesInPost; i++)
            {
                images.Add(new EmbeddedImage(new Blob(new BlobReference("https://example.org/image"), "image/jpg", 1024), "alt text"));
            }

            ArgumentOutOfRangeException caughtException = Assert.Throws<ArgumentOutOfRangeException>(() => new Post("text", createdAt: DateTimeOffset.UtcNow, images: images));

            Assert.Equal("images.Count", caughtException.ParamName);
        }

        [Fact]
        public void ConstructorDoesNotThrowsWhenImagesIsHasEnoughImagesAndCreatedAtIsProvided()
        {
            List<EmbeddedImage> images = [];

            for (int i = 0; i < Maximum.ImagesInPost; i++)
            {
                images.Add(new EmbeddedImage(new Blob(new BlobReference("https://example.org/image"), "image/jpg", 1024), "alt text"));
            }

            var post = new Post("text", DateTimeOffset.UtcNow, images);

            Assert.Equal("text", post.Text);
            Assert.IsType<EmbeddedImages>(post.EmbeddedRecord);

            EmbeddedImages postImages = (EmbeddedImages)post.EmbeddedRecord;

            Assert.Equal(Maximum.ImagesInPost, postImages.Images.Count);
        }

        [Fact]
        public void ConstructorDoesNotThrowWhenTextAndLangAreProvided()
        {
            Post post = new ("text", "en-gb");

            Assert.Equal("text", post.Text);
            Assert.NotNull(post.Langs);
            Assert.Single(post.Langs);
            Assert.Contains("en-gb", post.Langs);
        }

        [Fact]
        public void ConstructorDoesNotThrowWhenTextAndLangsAreProvided()
        {
            Post post = new("text", ["en-gb", "en-us"]);

            Assert.Equal("text", post.Text);
            Assert.NotNull(post.Langs);
            Assert.Equal(2, post.Langs.Count());
            Assert.Contains("en-gb", post.Langs);
            Assert.Contains("en-us", post.Langs);
        }

        [Fact]
        public void SettingSelfLabelFlagsSetsTheEntry()
        {
            Post post = new("Flags");
            Assert.Null(post.Tags);
            Assert.Null(post.Labels);
            Assert.False(post.ContainsPorn);
            Assert.False(post.ContainsSexualContent);
            Assert.False(post.ContainsGraphicMedia);
            Assert.False(post.ContainsNudity);

            post.ContainsPorn = true;
            Assert.NotNull(post.Labels);
            Assert.True(post.Labels.Contains("porn"));
            Assert.True(post.ContainsPorn);

            post.ContainsSexualContent = true;
            Assert.NotNull(post.Labels);
            Assert.True(post.Labels.Contains("sexual"));
            Assert.True(post.ContainsSexualContent);

            post.ContainsGraphicMedia = true;
            Assert.NotNull(post.Labels);
            Assert.True(post.Labels.Contains("graphic-media"));
            Assert.True(post.ContainsGraphicMedia);

            post.ContainsNudity = true;
            Assert.NotNull(post.Labels);
            Assert.True(post.Labels.Contains("nudity"));
            Assert.True(post.ContainsNudity);
        }

        [Fact]
        public void UnsettingSelfLabelsUnsetsTheUnderlyingEntry()
        {
            Post post = new("Flags")
            {
                ContainsPorn = true,
                ContainsSexualContent = true,
                ContainsGraphicMedia = true,
                ContainsNudity = true
            };

            Assert.NotNull(post.Labels);
            Assert.True(post.ContainsPorn);
            Assert.True(post.ContainsSexualContent);
            Assert.True(post.ContainsGraphicMedia);
            Assert.True(post.ContainsNudity);

            post.ContainsPorn = false;
            Assert.NotNull(post.Labels);
            Assert.False(post.ContainsPorn);
            Assert.False(post.Labels.Contains("porn"));

            post.ContainsSexualContent = false;
            Assert.NotNull(post.Labels);
            Assert.False(post.ContainsSexualContent);
            Assert.False(post.Labels.Contains("sexual"));

            post.ContainsGraphicMedia = false;
            Assert.NotNull(post.Labels);
            Assert.False(post.ContainsGraphicMedia);
            Assert.False(post.Labels.Contains("graphic-media"));

            post.ContainsNudity = false;
            Assert.NotNull(post.Labels);
            Assert.False(post.ContainsNudity);
            Assert.False(post.Labels.Contains("nudity"));
        }

        [Fact]
        public void SettingSelfLabelsViaSetPostSelfLabelsSetsTheUnderlyingEntry()
        {
            PostSelfLabels selfLabels = new() { Porn = true, GraphicMedia = true, Nudity= true, SexualContent = true };

            Post post = new ("text");

            post.SetSelfLabels(selfLabels);

            Assert.NotNull(post.Labels);
            Assert.True(post.ContainsPorn);
            Assert.True(post.ContainsSexualContent);
            Assert.True(post.ContainsGraphicMedia);
            Assert.True(post.ContainsNudity);
        }

        [Theory]
        [InlineData("Hello", 5, 5, 5)]
        [InlineData("👨‍👩‍👧‍👧", 11, 1, 25)]
        [InlineData("🤦🏼‍♂️", 7, 1, 17)]
        [InlineData("💩", 2, 1, 4)]
        [InlineData("\"", 1, 1, 1)]
        public void LengthPropertiesReturnsCorrectValue(string text, int expectedLength, int expectedGraphemeLength, int expectedUtf8Length)
        {
            var post = new Post(text);

            Assert.Equal(expectedLength, post.Length);
            Assert.Equal(expectedGraphemeLength, post.GraphemeLength);
            Assert.Equal(expectedUtf8Length, post.Utf8Length);
        }

        [Fact]
        public void PostSelfLabelsStartsOffWithFalseEverywhere()
        {
            var postSelfLabels = new PostSelfLabels();

            Assert.False(postSelfLabels.Porn);
            Assert.False(postSelfLabels.SexualContent);
            Assert.False(postSelfLabels.GraphicMedia);
            Assert.False(postSelfLabels.Nudity);
        }

        [Fact]
        public void LengthPropertiesAreZeroWhenTextIsNull()
        {
            List<EmbeddedImage> images = [];

            for (int i = 0; i < Maximum.ImagesInPost; i++)
            {
                images.Add(new EmbeddedImage(new Blob(new BlobReference("https://example.org/image"), "image/jpg", 1024), "alt text"));
            }

            Post post = new (null, images: images);

            Assert.Equal(0, post.Length);
            Assert.Equal(0, post.GraphemeLength);
            Assert.Equal(0, post.Utf8Length); ;
        }
    }
}
