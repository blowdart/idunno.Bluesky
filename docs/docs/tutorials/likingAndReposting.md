# Liking and reposting

Liking and reposting posts are a core feature of Bluesky. The SDK has dedicated methods for these actions.

## Liking a post

Liking a post is done by calling `agent.Like()` with a [strong reference](../commonTerms.md#strongReference) of the post
to like

`Like(strongReference)`
| Parameter    | Type   | Description                                                                                | Required   |
|--------------|--------|--------------------------------------------------------------------------------------------|:----------:|
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

## Unliking a post

Unliking a post requires calling `agent.DeleteLike()` with like record's [at:// uri](../commonTerms.md#uri)

| Parameter    | Type   | Description                                                                | Required   |
|--------------|--------|----------------------------------------------------------------------------|:----------:|
| uri          | AtUri  | The [at:// uri](../commonTerms.md#uri) of the post to delete the like for. | Yes        |

```c#
var unlikeResult = agent.DeleteLike(uri);
```

## Reposting a post

Reposting and un-reposting looks almost exactly the same as liking and unliking.

`Repost(strongReference)`
| Parameter    | Type   | Description                                                                                  | Required   |
|--------------|--------|----------------------------------------------------------------------------------------------|:----------:|
| strongReference | StrongReference | The [Strong Reference](../commonTerms.md#strongReference) of the post to repost. | Yes        |

```c#
var likeResult = agent.Repost(strongReference);
```

`Repost(atUri, cid)`
| Parameter    | Type   | Description                                                               | Required   |
|--------------|--------|---------------------------------------------------------------------------|:----------:|
| uri          | AtUri  | The [at:// uri](../commonTerms.md#uri) of the post to repost.             | Yes        |
| cid          | Cid    | The [CID](../commonTerms.md#cid) of the post to repost.                   | Yes        |

```c#
var likeResult = agent.DeleteREpost(uri, cid);
```

## Un-Reposting a post

