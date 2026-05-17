// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.Embed;

namespace idunno.Bluesky.Test;

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

        var builder = new PostBuilder("hello", lang: expected);

        Post postRecord = builder.ToPost();

        Assert.NotNull(postRecord.Langs);
        Assert.Single(postRecord.Langs);
        Assert.Equal(expected, postRecord.Langs.ElementAt(0));
    }

    [Fact]
    public void SettingALanguageViaBuilderConstructorShouldReflectInBuilderLanguageProperty()
    {
        const string expected = "en-uk";

        var builder = new PostBuilder("hello", lang: expected);

        Assert.NotNull(builder.Langs);
        Assert.Single(builder.Langs);
        Assert.Equal(expected, builder.Langs.ElementAt(0));
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

        var builder = new PostBuilder("hello", langs: expected);

        Assert.NotNull(builder.Langs);
        Assert.Equal(expected, builder.Langs);
    }

    [Fact]
    public void ConstructorShouldNotAllowPostsOverMaximumLength()
    {
        string exact = new('a', Maximum.PostLengthInGraphemes);
        string tooLong = new('a', Maximum.PostLengthInGraphemes + 1);

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
                    new (new Blob(new BlobReference("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), "image/jpg", 1), "alt text"),
                    new (new Blob(new BlobReference("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), "image/jpg", 1), "alt text"),
                    new (new Blob(new BlobReference("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), "image/jpg", 1), "alt text"),
                    new (new Blob(new BlobReference("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), "image/jpg", 1), "alt text"),
            ]);

        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>("images", () =>
        {
            _ = new PostBuilder(
                "Image Test",
                images:
                [
                    new (new Blob(new BlobReference("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), "image/jpg", 1), "alt text"),
                    new (new Blob(new BlobReference("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), "image/jpg", 1), "alt text"),
                    new (new Blob(new BlobReference("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), "image/jpg", 1), "alt text"),
                    new (new Blob(new BlobReference("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), "image/jpg", 1), "alt text"),
                    new (new Blob(new BlobReference("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), "image/jpg", 1), "alt text")
                ]);
        });
    }

    [Fact]
    public void ConstructorThrowsWhenAnEmptyTagIsPassed()
    {
        List<string> tags = [string.Empty];

        ArgumentException caughtException = Assert.Throws<ArgumentException>(() => new PostBuilder("text", tags: tags));

        Assert.Equal("tags", caughtException.ParamName);
    }

    [Fact]
    public void ConstructorThrowsWhenAnTooLongTagInGraphemesIsPassed()
    {
        List<string> tags = [new('x', Maximum.TagLengthInGraphemes + 1)];

        ArgumentOutOfRangeException caughtException = Assert.Throws<ArgumentOutOfRangeException>(() => new PostBuilder("text", tags: tags));

        Assert.Equal("tags", caughtException.ParamName);
    }

    [Fact]
    public void ConstructorThrowsWhenAnTooLongTagInCharactersIsPassed()
    {
        List<string> tags = [new('x', Maximum.TagLengthInCharacters + 1)];

        ArgumentOutOfRangeException caughtException = Assert.Throws<ArgumentOutOfRangeException>(() => new PostBuilder("text", tags: tags));

        Assert.Equal("tags", caughtException.ParamName);
    }

    [Fact]
    public void ConstructorSetsTagProperty()
    {
        List<string> tags = ["1", "2", "3", "4", "5", "6", "7", "8"];

        var actual = new PostBuilder("text", createdAt: DateTimeOffset.UtcNow, tags: tags);

        Assert.Equal(tags, actual.Tags);
    }

    [Fact]
    public void InReplyToIsSetInResultantPost()
    {
        var replyReferences = new ReplyReferences(
            new StrongReference("at://did:plc:identifier/app.bsky.feed.post/rkey", "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4"),
            new StrongReference("at://did:plc:identifier/app.bsky.feed.post/rkey", "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4"));

        var postBuilder = new PostBuilder("test")
        {
            InReplyTo = replyReferences
        };

        var post = postBuilder.ToPost();

        Assert.NotNull(post.Reply);
        Assert.Equal(replyReferences.Parent, post.Reply.Parent);
        Assert.Equal(replyReferences.Root, post.Reply.Root);

        postBuilder = new PostBuilder("test")
            .ReplyTo(replyReferences);

        post = postBuilder.ToPost();

        Assert.NotNull(post.Reply);
        Assert.Equal(replyReferences.Parent, post.Reply.Parent);
        Assert.Equal(replyReferences.Root, post.Reply.Root);
    }

    [Fact]
    public void QuoteIsSetInResultantPost()
    {
        var quotedPost = new StrongReference("at://did:plc:identifier/app.bsky.feed.post/rkey", "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4");

        var postBuilder = new PostBuilder("test")
        {
            QuotePost = quotedPost
        };

        var post = postBuilder.ToPost();

        Assert.NotNull(post.EmbeddedRecord);
        Assert.IsType<EmbeddedRecord>(post.EmbeddedRecord);
        var embeddedRecord = post.EmbeddedRecord as EmbeddedRecord;
        Assert.NotNull(embeddedRecord);
        Assert.Equal(quotedPost.Uri, embeddedRecord.Record.Uri);
        Assert.Equal(quotedPost.Cid, embeddedRecord.Record.Cid);

        postBuilder = new PostBuilder("test").Quote(quotedPost);

        post = postBuilder.ToPost();
        Assert.NotNull(post.EmbeddedRecord);
        Assert.IsType<EmbeddedRecord>(post.EmbeddedRecord);
        embeddedRecord = post.EmbeddedRecord as EmbeddedRecord;
        Assert.NotNull(embeddedRecord);
        Assert.Equal(quotedPost.Uri, embeddedRecord.Record.Uri);
        Assert.Equal(quotedPost.Cid, embeddedRecord.Record.Cid);
    }

    [Fact]
    public void CanSetBothQuoteAndReplyToInResultantPost()
    {
        var quotedPost = new StrongReference("at://did:plc:identifier/app.bsky.feed.post/rkey", "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4");
        var replyReferences = new ReplyReferences(
            new StrongReference("at://did:plc:identifier/app.bsky.feed.post/rkey", "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4"),
            new StrongReference("at://did:plc:identifier/app.bsky.feed.post/rkey", "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4"));

        var postBuilder = new PostBuilder("test")
        {
            QuotePost = quotedPost,
            InReplyTo = replyReferences
        };

        var post = postBuilder.ToPost();
        Assert.NotNull(post.EmbeddedRecord);
        Assert.IsType<EmbeddedRecord>(post.EmbeddedRecord);
        var embeddedRecord = post.EmbeddedRecord as EmbeddedRecord;
        Assert.NotNull(embeddedRecord);
        Assert.Equal(quotedPost.Uri, embeddedRecord.Record.Uri);
        Assert.Equal(quotedPost.Cid, embeddedRecord.Record.Cid);
        Assert.NotNull(post.Reply);
        Assert.Equal(replyReferences.Parent, post.Reply.Parent);
        Assert.Equal(replyReferences.Root, post.Reply.Root);

        postBuilder = new PostBuilder("test")
            .Quote(quotedPost)
            .ReplyTo(replyReferences);
        post = postBuilder.ToPost();

        Assert.NotNull(post.EmbeddedRecord);
        Assert.IsType<EmbeddedRecord>(post.EmbeddedRecord);
        embeddedRecord = post.EmbeddedRecord as EmbeddedRecord;
        Assert.NotNull(embeddedRecord);
        Assert.Equal(quotedPost.Uri, embeddedRecord.Record.Uri);
        Assert.Equal(quotedPost.Cid, embeddedRecord.Record.Cid);
        Assert.NotNull(post.Reply);
        Assert.Equal(replyReferences.Parent, post.Reply.Parent);
        Assert.Equal(replyReferences.Root, post.Reply.Root);

        postBuilder = new PostBuilder("test")
            .ReplyTo(replyReferences)
            .Quote(quotedPost);
        post = postBuilder.ToPost();

        Assert.NotNull(post.EmbeddedRecord);
        Assert.IsType<EmbeddedRecord>(post.EmbeddedRecord);
        embeddedRecord = post.EmbeddedRecord as EmbeddedRecord;
        Assert.NotNull(embeddedRecord);
        Assert.Equal(quotedPost.Uri, embeddedRecord.Record.Uri);
        Assert.Equal(quotedPost.Cid, embeddedRecord.Record.Cid);
        Assert.NotNull(post.Reply);
        Assert.Equal(replyReferences.Parent, post.Reply.Parent);
        Assert.Equal(replyReferences.Root, post.Reply.Root);

        postBuilder = new PostBuilder("test")
        {
            QuotePost = quotedPost,
        }.ReplyTo(replyReferences);
        post = postBuilder.ToPost();

        Assert.NotNull(post.EmbeddedRecord);
        Assert.IsType<EmbeddedRecord>(post.EmbeddedRecord);
        embeddedRecord = post.EmbeddedRecord as EmbeddedRecord;
        Assert.NotNull(embeddedRecord);
        Assert.Equal(quotedPost.Uri, embeddedRecord.Record.Uri);
        Assert.Equal(quotedPost.Cid, embeddedRecord.Record.Cid);
        Assert.NotNull(post.Reply);
        Assert.Equal(replyReferences.Parent, post.Reply.Parent);
        Assert.Equal(replyReferences.Root, post.Reply.Root);

        postBuilder = new PostBuilder("test")
        {
            InReplyTo = replyReferences,
        }.Quote(quotedPost);
        post = postBuilder.ToPost();

        Assert.NotNull(post.EmbeddedRecord);
        Assert.IsType<EmbeddedRecord>(post.EmbeddedRecord);
        embeddedRecord = post.EmbeddedRecord as EmbeddedRecord;
        Assert.NotNull(embeddedRecord);
        Assert.Equal(quotedPost.Uri, embeddedRecord.Record.Uri);
        Assert.Equal(quotedPost.Cid, embeddedRecord.Record.Cid);
        Assert.NotNull(post.Reply);
        Assert.Equal(replyReferences.Parent, post.Reply.Parent);
        Assert.Equal(replyReferences.Root, post.Reply.Root);
    }

    [Fact]
    public void SelfLabelsAreSetInResultantPost()
    {
        var selfLabels = new SelfLabels(new SelfLabel(SelfLabelValues.Nudity));
        var postSelfLabels = new PostSelfLabels(selfLabels);
        var postBuilder = new PostBuilder(
            "text",
            images: [new(new Blob(new BlobReference("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), "image/jpg", 1), "alt text")],
            createdAt: DateTimeOffset.UtcNow)
            .Label(postSelfLabels);

        var post = postBuilder.ToPost();
        Assert.Equivalent(
            selfLabels,
            post.Labels);

        postBuilder = new PostBuilder(
            "text",
            images: [new(new Blob(new BlobReference("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), "image/jpg", 1), "alt text")],
            createdAt: DateTimeOffset.UtcNow)
            .ContainsNudity();

        post = postBuilder.ToPost();
        Assert.Equivalent(
            selfLabels,
            post.Labels);

        postBuilder = new PostBuilder(
            "text",
            images: [new(new Blob(new BlobReference("bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"), "image/jpg", 1), "alt text")],
            createdAt: DateTimeOffset.UtcNow)
            .Add(new SelfLabel(SelfLabelValues.Nudity));

        post = postBuilder.ToPost();
        Assert.Equivalent(
            selfLabels,
            post.Labels);
    }
}
