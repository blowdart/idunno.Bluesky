// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Embed;

namespace idunno.Bluesky.Test
{
    [ExcludeFromCodeCoverage]
    public class PostBuilderTests
    {
        [Fact]
        public void SettingTextViaBuilderConstructorShouldSetPostRecordText()
        {
            const string expected = "Test Text";

            PostBuilder builder = new(expected);
            Post postRecord = builder.ToPost();

            Assert.Equal(expected, postRecord.Text);
        }

        [Fact]
        public void SettingTextViaBuilderConstructorShouldReflectInBuilderTextProperty()
        {
            const string expected = "Test Text";

            var builder = new PostBuilder(expected);

            Assert.Equal(expected, builder.Text);
        }

        [Fact]
        public void SettingALanguageViaBuilderConstructorShouldSetPostRecordLangs()
        {
            const string expected = "en-uk";

            var builder = new PostBuilder("hello", expected);

            Post postRecord = builder.ToPost();

            Assert.NotNull(postRecord.Langs);
            Assert.Single(postRecord.Langs);
            Assert.Equal(expected, postRecord.Langs[0]);
        }

        [Fact]
        public void SettingALanguageViaBuilderConstructorShouldReflectInBuilderLanguageProperty()
        {
            const string expected = "en-uk";

            var builder = new PostBuilder("hello", expected);

            Assert.NotNull(builder.Languages);
            Assert.Single(builder.Languages);
            Assert.Equal(expected, builder.Languages[0]);
        }

        [Fact]
        public void SettingALanguagesViaBuilderConstructorShouldSetPostRecordLangs()
        {
            string[] expected = ["en-uk", "en-ie"];

            var builder = new PostBuilder("hello", expected);

            Post postRecord = builder.ToPost();

            Assert.NotNull(postRecord.Langs);
            Assert.Equal(expected, postRecord.Langs);
        }

        [Fact]
        public void SettingLanguagesViaBuilderConstructorShouldReflectInBuilderLanguageProperty()
        {
            string[] expected = ["en-uk", "en-ie"];

            var builder = new PostBuilder("hello", expected);

            Assert.NotNull(builder.Languages);
            Assert.Equal(expected, builder.Languages);
        }

        [Fact]
        public void ConstructorShouldNotAllowPostsOverMaximumLength()
        {
            string exact = new ('a', Maximum.PostLengthInGraphemes);
            string tooLong = new ('a', Maximum.PostLengthInGraphemes + 1);

            _ = new PostBuilder(exact);

            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>("text", () =>
            {
                _ = new PostBuilder(tooLong);
            });
        }

        [Fact]
        public void AddingAStringToAPostBuilderAppendsTheString()
        {
            var builder = new PostBuilder("hello");
            builder += " world";

            Assert.Equal("hello world", builder.Text);

            Post postRecord = builder.ToPost();

            Assert.Equal("hello world", postRecord.Text);
        }

        [Fact]
        public void AddingACharacterToAPostBuilderAppendsTheString()
        {
            var builder = new PostBuilder("hello");
            builder += '!';

            Assert.Equal("hello!", builder.Text);

            Post postRecord = builder.ToPost();

            Assert.Equal("hello!", postRecord.Text);
        }

        [Fact]
        public void AddingAStringToAPostBuilderCreatedWithNoTextAppendsTheString()
        {
            var builder = new PostBuilder("");
            builder += "hello world";

            Assert.Equal("hello world", builder.Text);

            Post postRecord = builder.ToPost();

            Assert.Equal("hello world", postRecord.Text);
        }

        [Fact]
        public void AddingACharacterToAPostBuilderCreatedWithNoTextAppendsTheCharacter()
        {
            var builder = new PostBuilder("");
            builder += '!';

            Assert.Equal("!", builder.Text);

            Post postRecord = builder.ToPost();

            Assert.Equal("!", postRecord.Text);
        }

        [Fact]
        public void TryingToGetThePostRecordForAPostWithNoTextNoImagesAndIsNotARepostThrowsPostBuilderException()
        {
            var builder = new PostBuilder("");
            Assert.Throws<PostBuilderException>(() =>
            {
                _ = builder.ToPost();
            });
        }

        [Fact]
        public void TryingToGetThePostRecordForAPostWithAnEmptyQuotePostSucceeds()
        {
            var builder = new PostBuilder("Quote")
            {
                QuotePost = new StrongReference(
                                new AtUri("at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3ksayja4oof2z"),
                                new Cid("bafyreih5pxnryqrmcfefnxcpqezzosgozd4n4nw37o6cu52h4m7wefjmmq"))

        };

            _ = builder.ToPost();
        }

        [Fact]
        public void TryingToGetThePostRecordForAnTextReplyPostSucceeds()
        {
            var builder = new PostBuilder("Reply")
            {
                InReplyTo = new ReplyReferences(
                                new StrongReference(
                                    new AtUri("at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3ksayja4oof2z"),
                                    new Cid("bafyreih5pxnryqrmcfefnxcpqezzosgozd4n4nw37o6cu52h4m7wefjmmq")),
                                new StrongReference(
                                    new AtUri("at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3ksayja4oof2z"),
                                    new Cid("bafyreih5pxnryqrmcfefnxcpqezzosgozd4n4nw37o6cu52h4m7wefjmmq")))

            };

            _ = builder.ToPost();
        }

        [Fact]
        public void TryingToGetThePostRecordForAnEmptyReplyPostThrowsPostBuilderException()
        {
            var builder = new PostBuilder("")
            {
                InReplyTo = new ReplyReferences(
                                new StrongReference(
                                    new AtUri("at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3ksayja4oof2z"),
                                    new Cid("bafyreih5pxnryqrmcfefnxcpqezzosgozd4n4nw37o6cu52h4m7wefjmmq")),
                                new StrongReference(
                                    new AtUri("at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3ksayja4oof2z"),
                                    new Cid("bafyreih5pxnryqrmcfefnxcpqezzosgozd4n4nw37o6cu52h4m7wefjmmq")))

            };

            Assert.Throws<PostBuilderException>(() =>
            {
                _ = builder.ToPost();
            });
        }

        [Fact]
        public void ConstructorShouldThrowWhenAddingTooManyImages()
        {
            _ = new PostBuilder(
                "Image Test",
                images:
                [
                        new (new Blob(new BlobReference("link"), "image/jpg", 1), "alt text"),
                        new (new Blob(new BlobReference("link"), "image/jpg", 1), "alt text"),
                        new (new Blob(new BlobReference("link"), "image/jpg", 1), "alt text"),
                        new (new Blob(new BlobReference("link"), "image/jpg", 1), "alt text"),
                ]);

            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>("images", () =>
            {
                _ = new PostBuilder(
                    "Image Test",
                    images:
                    [
                        new (new Blob(new BlobReference("link"), "image/jpg", 1), "alt text"),
                        new (new Blob(new BlobReference("link"), "image/jpg", 1), "alt text"),
                        new (new Blob(new BlobReference("link"), "image/jpg", 1), "alt text"),
                        new (new Blob(new BlobReference("link"), "image/jpg", 1), "alt text"),
                        new (new Blob(new BlobReference("link"), "image/jpg", 1), "alt text")
                    ]);
            });
        }
    }
}
