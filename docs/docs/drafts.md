# Working with drafts

Bluesky has a drafts feature, where you can save an in progress post or thread and return to it in the original client,
or another client, recall it, and turn it into a post. This is useful if you're writing a Bluesky client.

Drafts are not public data and cannot be accessed as PDS records,
they most be accessed through the drafts API, `CreateDaft`, `DeleteDraft`, `GetDrafts `and `UpdateDraft`.

# Saving a draft

To store a draft on the Bluesky servers construct an instance of the `Draft` class and then use the `CreateDraft` method.
The `Draft` class looks a little different from the `Post` class you might be used to. It takes a collection of `DraftPost`,
a `Guid` representing the device the draft was created on, and a `DateTime` representing when the draft was created, an optional
name for the client, and, optionally collections of languages,
[post gate](threadGatesAndPostGates.md#postGates) and [thread gate]((threadGatesAndPostGates.md#threadGates) rules that
will be applied to the draft when it is turned into a post.

To create a draft and save with a single post, you can use the following code:

```c#
// These should consistently identify the device and client the draft was created on
var deviceId = new Guid("5c76194c-fc19-4413-ac45-bf851a289459");
var deviceName = "Beans Web";

var draftPost = new DraftPost("My draft post text");

var createDraftResult = await agent.CreateDraft(
    draftPost,
    deviceId: deviceId,
    deviceName: deviceName);
```

To create a draft with multiple posts, you would construct a collection of `DraftPost`s and pass that to the `CreateDraft` method:

```c#
var draftPosts = new List<DraftPost>
{
    new DraftPost("My first draft post text"),
    new DraftPost("My second draft post text")
};

var createDraftResult = await agent.CreateDraft(
    draftPost,
    deviceId: deviceId,
    deviceName: deviceName);
```

When the draft is eventually created the first `DraftPost` in the collection will become the root post of a thread,
and the rest will become replies to that root post, in order.

To embed images or videos in a raft the DraftPost constructor takes collections of `DraftEmbedImage` and `DraftEmbedVideo` objects.
These both share constructor parameters for the device local path to the media, and an alt text parameter. For videos you can
also specify captions. Media takes a device local path as it is not uploaded to the Bluesky servers until the draft is turned into a post.
This means you cannot share drafts between devices if they contain media, as the media will not be available on the other device.

To embed an external link in a draft post, you can use the `DraftEmbedExternal` class, which takes a URI, and to embed a Bluesky post
use the DraftEmbedRecord class which takes a `StrongReference` to the post (or list, or starterpack) you want to embed.

# Retrieving and manipulating drafts

To retrieve a draft, use the `GetDrafts` method, which will return a collection of `Draft`s. This returns a pageable view which 
you can then present the list to your user and allow them to pick which one to reload into your UI.

If the user edits and draft and wants to resave it as a draft, use the `UpdateDraft` method, which takes a `DraftWithId` instance.

If a user wants to delete a draft , use the `DeleteDraft` method, which takes the `draftId` for the draft presented in the `DraftView`
returned by `GetDrafts`.

# Turning a daft into a post

To turn a draft into a post, use the `Post` method, passing in the `DraftWithId` instance for the draft you want to post.
If you want the draft to be automatically deleted if the post is successfully created, set the `deleteDraft` parameter to true.
If you want links and mentions in the draft to be parsed from the draft text set the `extractFacets` parameter to true.

Draft posts allow a much greater post length than an actual post, to allow storing a larger text that can later be refined
into smaller posts. If you attempt to Post a draft which contains a draft post which is too large a `DraftException` will be thrown.
You can validate the length by checking the character length against
`Maximum.PostLengthInCharacters` and the grapheme length of the post against `Maximum.PostLengthInGraphemes`. For example,

```c#
if (draftPost.Text.Length > Maximum.PostLengthInCharacters ||
    draftPost.Text.GetGraphemeLength() > Maximum.PostLengthInGraphemes)
{
    // Inform the user that they need to split the draft into
    // multiple posts under the individual post limit.
}
```

>[!TIP]
> Remember that if a draft contains media , that media must be available on the device you are posting from,
> as media is not uploaded until the draft is turned into a post. If the media is not available an exception
> will be thrown when you try to post the draft.
>
> Use the `deviceId` to give your client devices a consistent identity
> and only post drafts with media from the device they were created on.
