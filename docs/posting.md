# <a name="posting">Posting</a>

## <a name="creatingAPost">Creating a post</a>

Let's start off by creating a simple post with the `CreatePost()` method.

```c#
AtProtoHttpResult<CreateRecordResponse> postResult = 
    await agent.Post("Hello world!");

if (postResult.Succeeded)
{
    Console.WriteLine("Post created");
    Console.WriteLine($"  Post AT URI: {postResult.Result.StrongReference.Uri}");
    Console.WriteLine($"  Post CID:    {postResult.Result.StrongReference.Cid}");
}
```

The result from creating a post contains. amongst other things, a strong reference to the new record. This `StrongReference` consists of an
[AT URI](https://atproto.com/specs/at-uri-scheme) and a Content Identifier ([CID](https://github.com/multiformats/cid)).
An AT URI is a way to reference individual records in a specific repository (every Bluesky user has their own repository).
A CID is a way to identify the contents of a record using a fingerprint hash.
The AT URI, or a record's complete `StrongReference` are used as a parameters in methods which deal with existing Bluesky records,
for example, liking or deleting a post.

### Setting the language on a post

Setting the post's language helps custom feeds or other services filter and parse posts.
You can set the posts language or languages using the language argument

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

If you don't provide `createdAt` the current date and time will be used.

## <a name="understandingResult">Understanding the results from a post call</a>

The `Post()` method creates a record in your Bluesky repo and returns an`AtProtoHttpResult<CreateRecordResponse>` This encapsulates
the HTTP status code returned by the Bluesky API, the result of the operation, if the operation was successful, any error messages
the api returned, and information on the current rate limits applied to you, which can be useful for making sure you don't flood the servers
and get locked by a rate limiter.

To check if the call was successful you can check the `Succeeded` property of the `HttpResult`, which will be `true` if the operation succeeded.
If its false, the `StatusCode` property will contain the HTTP status code returned by the Bluesky API, and the `AtErrorDetail` property will contain
any error information the API returned.

```c#
AtProtoHttpResult<CreateRecordResponse> postResult =  await agent.Post("Hello world!");

if (postResult.Succeeded)
{
    // The post was created successfully.
    // postResult.Result contains the CreateRecordResponse returned by the API.

    Console.WriteLine($"  Post AT URI: {postResult.Result.StrongReference.Uri}");
}
else
{
    Console.WriteLine($"{postResult.StatusCode} occurred when creating the post.");
    Console.WriteLine($"Error details: {postResult.AtErrorDetail}");
}
```

## <a name="deletingAPost">Deleting a post</a>

"Hello world" isn't exactly the most engaging post, so now is a good time to look at how to delete posts.
To delete a post you can use a post's AT URI, or a post's strong reference, pass it to `DeletePost()` and now the post is gone.
For example, to delete the post you just made using the first code snippet above you would pass the an AT URI
returned as part of the strong reference you got from creating the post, or the strong reference itself.

```c#
HttpResult<Commit> deleteResult = await agent.DeletePost(postResult.Result.StrongReference.Uri);
if (!deleteResult.Succeeded)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"{deleteResult.StatusCode} occurred when deleting the post.");
}
```

## Replying to a post

To reply to a post, again, you need a post's `StrongReference`, which you pass into `ReplyTo()`.

```c#
// Create a test post we will reply to.
var createPostResponse = 
    await agent.Post("Another test post, this time to check replying.");

// Reply to the post we just created
var replyCreatePostResponse = 
    await agent.ReplyTo(createPostResponse.Result.StrongReference, "This is a reply.");

// Reply to the reply using the reply's StrongReference
var replyToReplyStrongReference = 
  await agent.ReplyTo(replyCreatePostResponse.StrongReference, "This is a reply to the reply.");
```

Reply to a post creates a new record, and it may surprise you to see that the `ReplyTo()`
methods returns an `HttpResult<CreateRecordResponse>` just like creating a post does.

## Liking, reposting and quote posting posts

To like a post, once again, you need a post's AT URI, or its `StrongReference`, which you then pass to `agent.Like()`.

```c#
var likeResult = await agent.Like(postStrongReference);
```

Liking a post will return a `StrongReference` to the record for your like. You can use this strong reference to delete your like with `UndoLike()`.

```c#
HttpResult<bool> undoResult = await agent.UndoLike(likeResult.Result);
```

Reposting works in just the same way.

```c#
HttpResult<StrongReference> repostResult = await agent.Repost(postReference);
HttpResult<bool> undoRepostResult = await agent.UndoRepost(repostResult.Result);
```

Quoting a post requires both the post strong reference, and the text you the quote post to contain. Deleting a post quoting another post is like deleting a regular post, you call `DeletePost`;

```c#
HttpResult<StrongReference> quoteResult = 
    await agent.Quote(postReference, "This is a quote of a post.");
HttpResult<bool> deleteResult = await agent.DeleteQuote(quoteResult.Result!);
```

## Viewing your relationships with a post.

You can see if you have liked or reposted by examining the view of the post you get from a feed. If you're
not dealing with feeds you can get a `PostView` by calling `GetPostView()` with a `StrongReference` to the post. A `PostView`
contains an optional `Viewer` property, which is present should you have reposted, liked, pinned or muted the post, or if
the post author has disabled replies to, or embedding of the post.

If you liked a post then its `PostView.Viewer.Like` property will contain an AT Uri to your own like record, which you can use to unlike.
If you reposted the post then `PostView.Viewer.Repost` will contain the AT Uri of your repost record, which you can use to delete the repost record. 


## Making rich posts with the PostBuilder class

[Rich Posts](https://docs.bsky.app/docs/advanced-guides/post-richtext), in Bluesky parlance, are posts which have facets. Facets are post features, three of which are currently supported, links, mentions, and hashtags. If you don't want to create these manually you can use the `PostBuilder` class. Each facet has its own class which you can add to the `PostBuilder`. Each of these classes a parameter specific to the facet type, DIDs for mentions, strings for hashtags and URIs for links. They also a text parameter, the text in a post you want the facet to apply to.

While you can create facets manually, and attach the to a `PostRecord`and call down into the lower levels of the library to create a post record
an easier option is provided, a `PostBuilder`.

`PostBuilder` works much like a `StringBuilder` does, you create an instance of it, and build your post bit by bit, adding/appending to the `PostBuilder`
until you're ready to create a post from it, which you do by calling `agent.Post()` with the `PostBuilder`.

### Mentions

To mention someone in a post you must know their DID, which you can get by resolving their handle.
Then create a `Mention` instance and add it to your `PostBuilder`, then finally call agent.Post() with your 

```c#
string userToTagHandle = "userHandle.test";
var userToTagDid = await agent.ResolveHandle(userToTagHandle);
if (did is null)
{
  // handle did not resolve to a did, react accordindly.
}

var builder = new PostBuilder("Hello ") + new Mention(userToTagDid, $"@{userToTagHandle}");
var mentionPostResult = await agent.Post(builder);
```
One thing of note: the text doesn't have to match the "@handle" format, that's just convention.

### Links to external web sites

For links to web sites you create a new instance of a `Link`:

```c#
Uri uriToLinkTo = new("https://bsky.app/");

var builder = new PostBuilder("Click me ") + new Link(uriToLinkTo, uriToLinkTo.ToString());
var linkPostResult = await agent.Post(builder);
```

Again you can choose whatever text you want, so you could generate links in posts where the linked part of the post is just textual rather than a URI:

```c#
Uri uriToLinkTo = new("https://bsky.app/");

var builder = new PostBuilder("Click me to ") + new Link(uriToLinkTo, "visit Bluesky");
var linkPostResult = await agent.Post(builder);
```

### HashTags

To insert a hashtag you create a new `Hashtag` instance:

```c#
PostBuilder hashtagBuilder = new PostBuilder("This will have a hashtag. ") + new Hashtag("test");
var hashtagPostResult = await agent.Post(hashtagBuilder);
```

Note that the `HashTag` does not being with the # character. If you include a hash character you end up with a double hashed tag,  for example,
if you created a new instance with `new Hashtag("#test")` the hashtag Bluesky and other clients will open a page for `##test`.

Of course, you can chain everything together:

```c#
string userToTagHandle = "userHandle.test";
var userToTagDid = await agent.ResolveHandle(userToTagHandle);

var postBuilder = new("Hey ");

postBuilder.Append(new Mention(userToTagDid, $"@{userToTagHandle}"));

postBuilder.Append(" why not try some delicious ");

var shroudedLink = new Link("https://www.heinz.com/en-GB/products/05000157152886-baked-beanz", "beans");
postBuilder.Append(shroudedLink);
postBuilder.Append("? ");

postBuilder.Append("\nRead more: ");
var link = new Link("https://en.wikipedia.org/wiki/Heinz_Baked_Beans");
postBuilder.Append(' ');
postBuilder.Append(link);
postBuilder.Append('.');

var hashTag = new HashTag("beans");
postBuilder.Append(hashTag);

AtProtoHttpResult<CreateRecordResponse> facetedCreatePostResponse = await agent.Post(postBuilder, cancellationToken: cancellationToken);
```

## Posting with images

Creating a post with one or more images is a two step process, upload the image as a blob, then create a post with a reference to
the newly created blob.

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

Once you have your byte array upload it using `UploadImage`:

```c#
AtProtoHttpResult<EmbeddedImage> imageUploadResult = await agent.UploadImage(
    imageAsBytes,
    "image/jpg",
    "The Bluesky Logo",
    new AspectRatio(1000, 1000),
    cancellationToken: cancellationToken);
```

There is no validation done on the MIME type by Bluesky when uploading a blob, it is up to you to choose the
[correct one](https://developer.mozilla.org/en-US/docs/Web/HTTP/Basics_of_HTTP/MIME_types/Common_types).

If you upload a blob but don't use it in a post it will get deleted within an unspecified amount of time.

If the call to `UploadImage` didn't error you can pass its result to `agent.Post()`.

```c#
if (imageUploadResult.Succeeded)
{
    var createPostResult = await agent.Post("Hello world with an image.", imageUploadResult.Result, cancellationToken: cancellationToken);
}
```

If you have multiple images you can pass an `ICollection<EmbeddedImage>` into `agent.Post()`

You can, of course, add images to a `PostBuilder`:

```c#
PostBuilder postBuilder = new("A reply with an image.");

AtProtoHttpResult<EmbeddedImage> imageUploadResult = await agent.UploadImage(
    imageAsBytes,
    "image/jpg",
    "The Bluesky Logo",
    new AspectRatio(1000, 1000),
    cancellationToken: cancellationToken);
if (imageUploadResult.Succeeded)
{
    postBuilder += 
        new EmbeddedImage(replyImageBlobLink.Result!, "Image alttext", new AspectRatio(1000, 1000));
}
```

## Thread Gates and Post Gates

### Thread Gates

Thread gates allow the original author of a thread to control who can reply to the thread, allowing people mentioned in the root post
to reply, people the author is following to reply, replies from actors in a list or allow no replies at all. A thread gate can have up to five
rules, but allowing no replies is an exclusive rule, no other rules can be applied. A thread gate can also be used to hide replies in a thread.

You can apply a thread gate to an existing using `AddThreadGate()`. This method requires the `AtUri` of the post to be gated,
and, optionally a collection of gate rules and/or a collection of `AtUri`s of thread replies to be hidden.
If you provide no rules then the post will not allow any replies at all.

The following example demonstrates how to gate a thread so that replies are restricted to users the post author follows.

```c#
await agent.AddThreadGate(
    postUri,
    new List<ThreadGateRuleBase>()
    {
        new FollowingRule()
    },
    cancellationToken: cancellationToken);
```

The three types of thread gate rules are `FollowingRule`, `MentionRule` and `ListRule`. Note that adding, or updating a thread gate replaces any gate
already in place. If you want to update rules or hidden posts first get any existing rule with `GetThreadGate()`, if that is successful update the returned
`ThreadGate` class then apply it with with `UpdateThreadGate()`.

You can use `GetPostThread()` to see a view over a thread, including replies.

### Post Gates

Post gates allow the post author to remove it from someone else's post quoting their post, and also to disable embedding on a post.

```c#
await agent.AddPostGate(
    postUri,
    new List<PostGateRule>()
    {
        new DisableEmbeddingRule()
    },
    cancellationToken: cancellationToken);
```

You can get a view of posts quoting a post with `GetQuotes()`.

You can also specify gates when creating a post:

```c#
await agent.Post("New gated post",
    threadGateRules: new List<ThreadGateRule>() { new FollowingRule() },
    postGateRules: new List<PostGateRule>() { new DisableEmbeddingRule() },
    cancellationToken: cancellationToken);
```
