# Liking and reposting

Liking and reposting posts are a core feature of Bluesky. The SDK has dedicated methods for these actions.

## Liking a post

Liking a post is done by calling `agent.Like()` with a [strong reference](../commonTerms.md#strongReference) of the post
to like

`Like(post)`

| Parameter       | Type            | Description                                                                                                                         | Required   |
|-----------------|-----------------|---------------------------------------------------------------------------------------------------------------=---------------------|:----------:|
| post            | FeedViewPost    | The FeedViewPost (the items in the collection returned by `GetTimeline()` or `GetFeed()` or `GetAuthorFeed()`) of the post to like. | Yes        |

>[!Tip]
>It is recommended use the `Like()` overload that takes a `FeedViewPost` where possible as this will check if you are liking a repost, and ensures the correct notification is send to the
>repost author.

`Like(strongReference)`

| Parameter       | Type            | Description                                                                    | Required   |
|-----------------|-----------------|--------------------------------------------------------------------------------|:----------:|
| strongReference | StrongReference | The [Strong Reference](../commonTerms.md#strongReference) of the post to like. | Yes        |

```c#
var likeResult = agent.Like(strongReference);
```

`agent.Like()` also has an overload which takes the [at:// uri](../commonTerms.md#uri) and [CID](../commonTerms.md#cid) of
the post to like

`Like(atUri, cid)`

| Parameter    | Type   | Description                                                             | Required   |
|--------------|--------|-------------------------------------------------------------------------|:----------:|
| uri          | AtUri  | The [at:// uri](../commonTerms.md#uri) of the post to like.             | Yes        |
| cid          | Cid    | The [CID](../commonTerms.md#cid) of the post to like.                   | Yes        |

```c#
var likeResult = agent.Like(uri, cid);
```

## Un-liking a post

Un-liking a post requires calling `agent.DeleteLike()` with original post's [at:// uri](../commonTerms.md#uri).

| Parameter    | Type   | Description                                                                | Required   |
|--------------|--------|----------------------------------------------------------------------------|:----------:|
| uri          | AtUri  | The [at:// uri](../commonTerms.md#uri) of the post to delete the like for. | Yes        |

```c#
var deleteLikeResult = agent.DeleteLike(uri);
```

## Reposting a post

Reposting and un-reposting looks almost exactly the same as liking and un-liking.

`Repost(post)`

| Parameter       | Type            | Description                                                                                                                           | Required   |
|-----------------|-----------------|---------------------------------------------------------------------------------------------------------------------------------------|:----------:|
| post            | FeedViewPost    | The FeedViewPost (the items in the collection returned by `GetTimeline()` or `GetFeed()` or `GetAuthorFeed()`) of the post to repost. | Yes        |

>[!Tip]
>It is recommended use the Repost() overload that takes a `FeedViewPost` where possible as this will check if you are reposting a repost, and ensures the correct notification is send to the
>repost author.

`Repost(strongReference)`

| Parameter       | Type            | Description                                                                                  | Required   |
|-----------------|-----------------|----------------------------------------------------------------------------------------------|:----------:|
| strongReference | StrongReference | The [Strong Reference](../commonTerms.md#strongReference) of the post to repost.             | Yes        |

```c#
var repostResult = agent.Repost(strongReference);
```

.Repost(atUri, cid)`

| Parameter    | Type   | Description                                                               | Required   |
|--------------|--------|---------------------------------------------------------------------------|:----------:|
| uri          | AtUri  | The [at:// uri](../commonTerms.md#uri) of the post to repost.             | Yes        |
| cid          | Cid    | The [CID](../commonTerms.md#cid) of the post to repost.                   | Yes        |

```c#
var repostResult = agent.Repost(uri, cid);
```

## Un-Reposting a post

Just like un-liking a post deleting a repost requires the original post's [at:// uri](../commonTerms.md#uri).

| Parameter    | Type   | Description                                                                | Required   |
|--------------|--------|----------------------------------------------------------------------------|:----------:|
| uri          | AtUri  | The [at:// uri](../commonTerms.md#uri) of the post to delete the like for. | Yes        |

```c#
var deleteRepostResult = agent.DeleteRepost(uri);
```

### Quoting a post

>[!Note]
>For information on quote posts see the [Posting tutorial](../posting.md#likeRepostQuote).

