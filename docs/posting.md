# <a name="posting">Posting</a>

## <a name="creatingAPost">Creating a post</a>

Let's start off by creating a simple post with the `CreatePost()` method.

```c#
HttpResult<StrongReference> postResult = 
    await agent.Post("Hello world!");

if (postResult.Succeeded)
{
    Console.WriteLine("Post created");
    Console.WriteLine($"  Post AT URI: {postResult.Result!.Uri}");
    Console.WriteLine($"  Post CID:    {postResult.Result!.Cid}");
}}
```

The result from creating a post contains of an [AT URI](https://atproto.com/specs/at-uri-scheme) and a Content Identifier ([CID](https://github.com/multiformats/cid)). An AT URI is a way to reference individual records in a specific repository (every Bluesky user has their own repository). A CID is a way to identify the contents of a record using a fingerprint hash. These two identifiers combined create a strong reference to a record and its contents. The AT URI, and also a record's `StrongReference` are used as a parameters which deal with existing Bluesky data, for example, liking or deleting a post.

### Setting the language on a post

Setting the post's language helps custom feeds or other services filter and parse posts. You can set the posts language or languages using the language argument

```c#
await agent.Post("G'day world!", language: "en-au");
```

Or if you have multiple languages

```C#
await agent.Post("สวัสดีชาวโลก!\nHello World!"", languages: new string[] {"th", "en-US"});
```

### Setting the creation date on a post

You can also set a specific create date and time on a post by using the `createdAt` parameter.

```c#
await agent.Post("Hello world from the past.", 
                 createdAt: new DateTimeOffset(new DateTime(1900, 1, 1)));
```

If you don't set `createdAt` the current date and time will be used.

## <a name="deletingAPost">Deleting a post</a>

"Hello world" isn't exactly the most engaging post, so now is a good time to look at how to delete posts. To delete a post you can use a post's AT URI, pass it to `DeletePost()` and now the post is gone. For example, to delete the post you just made using the first code snippet above you would pass either a strong reference to the post, or an AT URI pointing to the post into `DeletePost()`.

```c#
HttpResult<bool> deleteResult = await agent.DeletePost(postResult.Result!);
if (!deleteResult.Succeeded || !deleteResult.Result)
{
	Console.ForegroundColor = ConsoleColor.Red;
	Console.WriteLine($"{deleteResult.StatusCode} occurred when deleting the post.");
}
```

## Replying to a post

To reply to a post again you need a post's `StrongReference` which you pass into `ReplyTo()`.

```c#
// Create a test post we will reply to.
HttpResult<StrongReference> newPostStrongReference = 
    await agent.Post("Another test post, this time to check replying.");

// Reply to the post we just created
HttpResult<StrongReference> replyStrongReference = 
    await agent.ReplyTo(newPostStrongReference.Result!, "This is a reply.");

// Reply to the reply using the reply's StrongReference
HttpResult<StrongReference> replyToReplyStrongReference = 
  await agent.ReplyTo(replyStrongReference.Result!, "This is a reply to the reply.");

```

## Liking, reposting and quote posting posts

To like a post you need its strong reference, which you pass to `agent.Like()`.

```	c#
HttpResult<StrongReference> likeResult = await agent.Like(postReference);
```

Liking a post will return a `StrongReference` to the record for your like. You can use this strong reference to delete your like with `UndoLike()`.

```c#
HttpResult<bool> undoResult = await agent.UndoLike(likeResult.Result!);
```

Reposting works in just the same way.

```c#
HttpResult<StrongReference> repostResult = await agent.Repost(postReference);
HttpResult<bool> undoRepostResult = await agent.UndoRepost(repostResult.Result!);
```

Quoting a post requires both the post strong reference, and the text you the quote post to contain. Deleting a post quoting another post is like deleting a regular post, you call `DeletePost`;

```c#
HttpResult<StrongReference> quoteResult = 
    await agent.Quote(postReference, "This is a quote of a post.");
HttpResult<bool> deleteResult = await agent.DeletePost(quoteResult.Result!);
```

## Making rich posts with the PostBuilder class

[Rich Posts](https://docs.bsky.app/docs/advanced-guides/post-richtext), in Bluesky parlance, are posts which have facets. Facets are post features, three of which are currently supported, links, mentions, and hashtags. If you don't want to create these manually you can use the `PostBuilder` class. Each facet has its own class which you can add to the `PostBuilder`. Each of these classes a parameter specific to the facet type, DIDs for mentions, strings for hashtags and URIs for links. They also a text parameter, the text in a post you want the facet to apply to.

To mention someone in a post you must know their DID, which you can get by resolving their handle. Then create a `Mention` instance and add it to your `PostBuilder`.

```c#
string userToTag = "userHandle.test";
HttpResult<Did> didLookup = await agent.ResolveHandle(userToTag);

var builder = new PostBuilder("Hello ") + new Mention(didLookup.Result!, $"@{userToTag}");
var mentionPostResult = await agent.Post(builder);
```

One thing of note: the text doesn't have to match the "@handle" format, that's just convention.

For links to web sites you create a new instance of a `Link`:

```c#
Uri uriToLinkTo = new("https://bsky.app/");

var builder = new PostBuilder("Click me ") + new Link(uriToLinkTo, uriToLinkTo.ToString());
var linkPostResult = await agent.Post(builder);
```

Again you can choose whatever text you want, so you could generate links in posts where the linked part of the post is just textual rather than a URI:

```c#
Uri uriToLinkTo = new("https://bsky.app/");

var builder = new PostBuilder("Click me ") + new Link(uriToLinkTo, "visit Bluesky");
var linkPostResult = await agent.Post(builder);
```

To insert a hashtag you create a new `Hashtag` instance:

```c#
PostBuilder hashtagBuilder = new PostBuilder("This will have a hashtag. ") + new Hashtag("#test");
var hashtagPostResult = await agent.Post(hashtagBuilder);
```

And using the builder you can chain everything together:

```c#
var chainedBuilder = new PostBuilder("Hello ");
chainedBuilder += new Mention(agent.Session.Did, $"@{agent.Session.Handle}");
chainedBuilder += ' ';
chainedBuilder += "Click me ";
chainedBuilder += new Link(uriToLinkTo, uriToLinkTo.ToString());
chainedBuilder += ' ';
chainedBuilder += new Hashtag("#test");

var chainedPostResult = await agent.Post(chainedBuilder);
```

## Posting with images

Creating a post with one or more images is a two step process, you upload the image as a blob, then create a post with a reference to the newly uploaded blob.

To upload an image you need the image as a byte array, which you can get by copying a stream to a memory stream, for example:

```c#
byte[] imageAsBytes;
using (FileStream fs = File.OpenRead(pathToImage))
using (MemoryStream ms = new())
{
    fs.CopyTo(memoryStream);
    imageAsBytes = memoryStream.ToArray();
}
```

Once you have your byte array you upload it using `UploadBlob`:

```c#
HttpResult<Blob?> imageBlobLink = await agent.UploadBlob(imageAsBytes, "image/jpg");
```

There is no validation done on the MIME type when uploading a blob, it is up to you to choose the [correct one](https://developer.mozilla.org/en-US/docs/Web/HTTP/Basics_of_HTTP/MIME_types/Common_types). If you upload a blob but don't use it in a post it will get deleted within an unspecified amount of time. If you delete a post containing references to blobs those will also get deleted after an unspecified amount of time.

Once the blob has been uploaded you can use a `PostBuilder` to create your post and add the image using the `EmbeddedImage` class:

```c#
var imagePostBuilder = new("Hello with images");
imagePostBuilder += 
    new EmbeddedImage(imageBlobLink.Result!, "Image alttext", new AspectRatio(1000, 1000)); 
var imagePostResult = await agent.Post(imagePostBuilder);
```

When creating a new `EmbeddedImage` you supply the result of the call to `UploadBlob`, some alternative text (alt text) to describe the meaning and context of the image which will be the description supplied to visually impaired users, and the dimensions of the image.

You can also, as you might have guessed, use the `PostBuilder` to create a reply containing images, or add an image to a quote post:

```c#
HttpResult<Blob?> replyImageBlobLink = await agent.UploadBlob(imageAsBytes, "image/jpg");

PostBuilder replyWithImageBuilder = new("A reply with an image.");
replyWithImageBuilder += 
    new EmbeddedImage(replyImageBlobLink.Result!, "Image alttext", new AspectRatio(1000, 1000));

var replyResult = await agent.ReplyTo(imagePostResult.Result!, replyWithImageBuilder);

PostBuilder quoteWithImageBuilder = new("A quote post with an image.");
quoteWithImageBuilder += 
    new EmbeddedImage(replyImageBlobLink.Result!, "Image alttext", new AspectRatio(1000, 1000));

var quoteResult = await agent.Quote(imagePostResult.Result, quoteWithImageBuilder);
```

## Thread gates