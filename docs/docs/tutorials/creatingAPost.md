# Creating a post

The following code will login to Bluesky and create a simple post.

[!code-csharp[](code/helloWorld.cs#L1-L5)]

The result of a successful call to `Post()` creates a [record](../commonTerms.md#records) in your Bluesky [repository](../commonTerms.md#repositories).

The call will return, amongst other information, the [at:// uri](../commonTerms.md#uri) of the post record and its [content identifier (CID)](../commonTerms.md#cid).

[!code-csharp[](code/helloWorld.cs#L7-L11)]

The post record in the repository will look something like this

```json
{
  "text": "Hello World",
  "$type": "app.bsky.feed.post",
  "createdAt": "2025-04-25T17:25:46.3164586+00:00"
}
```

## Setting a language

Setting the post's language helps custom feeds or other services filter and parse posts.

```c#
await agent.Post(
  text: "Hello World",
  langs: "en-US")


await agent.Post(
  text: "สวัสดีชาวโลก\nHello World",
  langs: ["th", "en-US"])
```

You can include multiple values in langs if there are multiple languages present in the post.

>[!TIP]
>If you're writing a UX app you can pass the user's current OS language with `langs: [Thread.CurrentThread.CurrentUICulture.Name]`.

## Mentions, hashtags and links

You can include mentions of other users, hashtags and links like you would when using the app,
mentions by @handle, hashtags with #, and links by their URL.

```c#
await agent.Post(
  text: "Hello @sinclairinat0r.com, the Heinz #beans factory is one of the largest food factories in Europe! https://en.wikipedia.org/wiki/H._J._Heinz,_Wigan");
```

>[!TIP]
>The URL extractor is simplistic and will strip things like query string, much like the Bluesky app does.
> For more complicated links you can use a `PostBuilder` to create a post and add links, hashtags and mentions into it.

## Replies and quote posts

Reply and quote posts contain [strong references](../commonTerms.md#strongReference) to other posts.

### Replies

Replies and quote posts contain strong references to other records.

* [at:// uri](../commonTerms.md#uri): indicating the repository [DID](../commonTerms.md#dids), collection and record key
* [CID](../commonTerms.md#cid): the hash to the record itself

You can get a post's strong reference from the timeline view, author view or other views that contain posts like search views or feed views.

```c#
await agent.ReplyTo(strongReference, "This is a reply");
```

### <a name="quotePosts">Quote posts</a>

A quote post embeds a reference to another post record.

```c#
await agent.Quote(strongReference, "This is a post quoting a post");
```

## Images

To add an image, or images to a post, you first upload the image(s) as a byte array, providing its MIME type, alt text and size and then refer to the upload when creating the post.

```c#
// Read the image from the file specified by pathToImage

var pathToImage = "D:\beans1000x1000.jpg";
byte[] imageAsBytes;
using (FileStream fs = File.OpenRead(pathToImage))
using (MemoryStream ms = new())
{
    fs.CopyTo(ms);
    imageAsBytes = ms.ToArray();
}

var imageUploadResult = await agent.UploadImage(
    imageAsBytes,
    mimeType: "image/jpg",
    altText: "The Bluesky Logo",
    aspectRatio: new AspectRatio(1000, 1000));

if (imageUploadResult.Succeeded)
{
    var createPostResult = await agent.Post(
        "Hello world with an image.",
        image: imageUploadResult.Result!);
}
```

You can pass an array of up to four images to `Post()` using the `images` parameter.

More detailed information on how to use the `Post()` api, as well as how to use a `PostBuild()` can be found on the [Posting](../posting.md) page.
