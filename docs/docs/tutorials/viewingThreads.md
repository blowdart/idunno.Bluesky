# Viewing threads 

A *thread* refers to a post, its replies (descendants), and its parents (ancestors). Fetching a thread is done with `agent.GetPostThread()` , which accepts the following key parameters:

`GetPostThread(uri, depth?, parentHeight?)`

| Parameter    | Type   | Description                                                             | Required   | Default | Minimum | Maximum |
|--------------|--------|-------------------------------------------------------------------------|:----------:|:-------:|:-------:|:-------:|
| uri          | AtUri  | The [at:// uri](../commonTerms.md#uri) of the post you'd like to fetch. | Yes        |         |         |         |
| depth        | int    | The *depth* of the descendent post tree you'd like to fetch.            | No         | 6       | 0       | 1000    |
| parentHeight | int    | The *height* of the ancestor post tree you'd like to fetch.             | No         | 80      | 0       | 1000    |

```c#
var getPostThreadResult = await agent.GetPostThread(
                    uri: "at://did:plc:oio4hkxaop4ao4wz2pp3f4cr/app.bsky.feed.post/3lnoktfc7l22i");
```

## Result shape

The result of a `GetPostThread()` is a `PostThread` which contains the `Thread` and any `ThreadGate` rules applied to it.

`Thread` has a `Post` property, this is the root post, the one whose [at:// uri](../commonTerms.md#uri) you provided,
a `Parent` property, which is a nested tree of ancestors (if any), and `Replies`, a one dimensional array of replies (if any).

As `Replies` is one-dimensional, to facilitate the construction of a tree from this data, each reply in the array contains references to its
immediate parent (ancestor) and immediate child (descendent).

`Post`, `Parent` and each entry in `Replies` can either a `ThreadViewPost`, a `NotFoundPost` or a`BlockedPost` type.

## The depth and parentHeight parameters
depth and parentHeight can be thought of as the distance from the root post to its most distant child or parent.

```
root
├── child 1
│   ├── child 1.1
│   │   └── child 1.1.1
│   └── child 1.2
└── child 2
    └── child 2.1
        └── child 2.1.1
```

Above, if you were to fetch the root post, child 1.2 is at a depth of 2 from the root, its ancestor. If you were to fetch child 2.1.1 directly,
root would be at a height of 3 from child 2.1.1, its descendent.

## Handling blocks, takedowns, and not found

As you can see in the types above, the root post, parent or reply in the response can be either a `ThreadViewPost`, a `NotFoundPost`, or a `BlockedPost`.

A `NotFoundPost` is returned when the post either does not exist, has been deleted by its author, or has been taken down by a moderation service.

A `BlockedPost` is returned when either the post author has blocked the viewer, or the viewer has blocked the author.

You can use C# pattern matching to perform different actions depending on the type of the `Post`, its `Parent` or any of its `Replies`.

```c#
var getPostThreadResult = await agent.GetPostThread(
    uri: "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3lnorz7eorc2z", cancellationToken: cancellationToken);

if (getPostThreadResult.Succeeded)
{
    switch (getPostThreadResult.Result.Thread)
    {
        case ThreadViewPost threadViewPost:
            // The post exists so you can act on it.
            Console.WriteLine(threadViewPost.Post.Record.Text);
            break;

        case NotFoundPost notFoundPost:
            // The post never existed, was deleted, or taken down.
            Console.WriteLine($"Post at {notFoundPost.Uri} is unavailable");
            break;

        case BlockedPost blockedPost:
            // The post author blocked the viewer,
            // or the view blocked the post author.
            Console.WriteLine($"Post at {blockedPost.Uri} is blocked");
            break;
    }
}
```

These posts types are included in the response so that a complete tree can be constructed. It's up to the you to decide how to render these different states.
