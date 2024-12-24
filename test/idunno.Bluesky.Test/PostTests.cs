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
        public void TextOnlyConstructorSetsText()
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
            List<string> expectedLanguages = new() { "en-gb", "en-us" };

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
            StrongReference strongReference = new(new AtUri("at://foo.com/com.example.foo/123"), new Cid("\"bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4"));

            List<string> expectedLanguages = new() { "en-gb", "en-us" };
            List<Facet> expectedFacets = new() { new Facet(new ByteSlice(0, 1), new List<FacetFeature>() { new MentionFacetFeature(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk")) }) };
            EmbeddedBase expectedEmbedded = new EmbeddedRecord(strongReference);
            ReplyReferences expectedReplyReferences = new(strongReference, strongReference);
            SelfLabels expectedLabels = new();
            expectedLabels.AddLabel("labelTest");
            List<string> expectedTags = new() { "selfTag" };

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
            Assert.Equal(expectedEmbedded, post.Embed);
            Assert.Equal(expectedReplyReferences, post.Reply);
            Assert.Equal(expectedReplyReferences, post.Reply);
            Assert.Equal(expectedLabels, post.Labels);
            Assert.Equal(expectedTags, post.Tags);
        }

        [Fact]
        public void TextCreationDateAndPropertiesConstructorSetsEverythingCorrectly()
        {
            const string postText = "post text";
            StrongReference strongReference = new(new AtUri("at://foo.com/com.example.foo/123"), new Cid("\"bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4"));

            DateTimeOffset expectedDate = DateTimeOffset.UtcNow;
            List<string> expectedLanguages = new() { "en-gb", "en-us" };
            List<Facet> expectedFacets = new() { new Facet(new ByteSlice(0, 1), new List<FacetFeature>() { new MentionFacetFeature(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk")) }) };
            EmbeddedBase expectedEmbedded = new EmbeddedRecord(strongReference);
            ReplyReferences expectedReplyReferences = new(strongReference, strongReference);
            SelfLabels expectedLabels = new();
            expectedLabels.AddLabel("labelTest");
            List<string> expectedTags = new() { "selfTag" };

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
            Assert.Equal(expectedEmbedded, post.Embed);
            Assert.Equal(expectedReplyReferences, post.Reply);
            Assert.Equal(expectedReplyReferences, post.Reply);
            Assert.Equal(expectedLabels, post.Labels);
            Assert.Equal(expectedTags, post.Tags);
        }

        [Fact]
        public void CopyConstructorCopiesEverythingCorrectly()
        {
            const string postText = "post text";
            StrongReference strongReference = new(new AtUri("at://foo.com/com.example.foo/123"), new Cid("\"bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4"));

            DateTimeOffset expectedDate = DateTimeOffset.UtcNow;
            List<string> expectedLanguages = new() { "en-gb", "en-us" };
            List<Facet> expectedFacets = new() { new Facet(new ByteSlice(0, 1), new List<FacetFeature>() { new MentionFacetFeature(new Did("did:plc:ec72yg6n2sydzjvtovvdlxrk")) }) };
            EmbeddedBase expectedEmbedded = new EmbeddedRecord(strongReference);
            ReplyReferences expectedReplyReferences = new(strongReference, strongReference);
            SelfLabels expectedLabels = new();
            expectedLabels.AddLabel("labelTest");
            List<string> expectedTags = new() { "selfTag" };

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
            Assert.Equal(expectedEmbedded, post.Embed);
            Assert.Equal(expectedReplyReferences, post.Reply);
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
            ArgumentNullException caughtException = Assert.Throws<ArgumentNullException>(() => new Post(string.Empty, new List<string>() { "en-gb" }));

            Assert.Equal("text", caughtException.ParamName);
        }

        [Fact]
        public void TextLangsConstructorThrowsIfLangsIsEmpty()
        {
            ArgumentOutOfRangeException caughtException = Assert.Throws<ArgumentOutOfRangeException>(() => new Post("text", new List<string>()));

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
            StrongReference strongReference = new(new AtUri("at://foo.com/com.example.foo/123"), new Cid("\"bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4"));
            EmbeddedBase expectedEmbedded = new EmbeddedRecord(strongReference);

            var post = new Post(null, embed : expectedEmbedded);

            Assert.Null(post.Text);
            Assert.Equal(expectedEmbedded, post.Embed);
        }

        [Fact]
        public void ConstructorDoesNotThrowWhenTextIsEmptyAndThereIsAnEmbed()
        {
            StrongReference strongReference = new(new AtUri("at://foo.com/com.example.foo/123"), new Cid("\"bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4"));
            EmbeddedBase expectedEmbedded = new EmbeddedRecord(strongReference);

            var post = new Post(string.Empty, embed: expectedEmbedded);

            Assert.Equal(string.Empty, post.Text);
            Assert.Equal(expectedEmbedded, post.Embed);
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
            List<string> tags = new ();

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
            List<string> tags = new() { string.Empty };

            ArgumentException caughtException = Assert.Throws<ArgumentException>(() => new Post("text", tags: tags));

            Assert.Equal("tags", caughtException.ParamName);
        }

        [Fact]
        public void ConstructorThrowsWhenAnTooLongTagIsPassed()
        {
            List<string> tags = new() { new('x', Maximum.TagLengthInGraphemes + 1) };

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
        public void ConstructorThrowsWhenImagesIsNull()
        {
            List<EmbeddedImage>? images = null;

            ArgumentNullException caughtException = Assert.Throws<ArgumentNullException>(() => new Post("text", images: images!));

            Assert.Equal("images", caughtException.ParamName);
        }

        [Fact]
        public void ConstructorThrowsWhenImagesIsEmpty()
        {
            List<EmbeddedImage> images = new();

            ArgumentOutOfRangeException caughtException = Assert.Throws<ArgumentOutOfRangeException>(() => new Post("text", images: images));

            Assert.Equal("images.Count", caughtException.ParamName);
        }

        [Fact]
        public void ConstructorThrowsWhenImagesIsHasTooManyImages()
        {
            List<EmbeddedImage> images = new();

            for (int i = 0; i <= Maximum.ImagesInPost; i++)
            {
                images.Add(new EmbeddedImage(new Blob(new BlobReference("https://example.org/image"), "image/jpg", 1024), "alt text"));
            }

            ArgumentOutOfRangeException caughtException = Assert.Throws<ArgumentOutOfRangeException>(() => new Post("text", images: images));

            Assert.Equal("images.Count", caughtException.ParamName);
        }
    }
}
