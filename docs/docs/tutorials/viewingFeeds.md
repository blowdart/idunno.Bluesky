# Viewing feeds

Feeds are lists of posts, [paginated by cursors](../cursorsAndPagination.md). Bluesky has a few types of feeds, including

* **timelines**: the default chronological feed of posts from users the authenticated user follows
* **feed generators**: custom feeds made by users and organiziations
* **author feeds**: a feed of posts by a single author

## Viewing a user's timeline

The Bluesky agent you created in the [Get Started](../../index.md) section has a dedicated `GetTimeline()` method that returns the authenticated user's timeline.
It accepts two key parameters: `cursor` and `limit`.


| Parameter | Type   | Description                                           | Required   | Default   |
|-----------|--------|-------------------------------------------------------|:----------:|:---------:|
| cursor    | string | A cursor that tells the server where to paginate from | No         |  *null*   |
| limit     | int    | The number of posts to return per page (max 100)      | No         |  50       |

```c#
var timelineResult = await agent.GetTimeline(limit: 10);
```

You can then iterate through the results, assuming the API call succeeded,

```c#
foreach (var feedViewPost in timelineResult.Result!)
{
  // Process the feed's view over the post
}
```

To get the next page of results take the `Cursor` property from the previous call's results and pass it into `GetTimeline()`.
If the `Cursor` is `null` there are no more results to retrieve.

```c#
if (timelineResult.Result.Cursor is not null)
{
    var nextResults = await agent.GetTimeline(cursor:timelineResult.Result.Cursor, limit: 10);
}
```

You can put it altogether;

```c#
int maximumPagesToRetrieve = 5;
int numberOfEntriesPerPage = 5;
int pageCount = 0;

var timelineResult =
    await agent.GetTimeline(limit: numberOfEntriesPerPage);

if (timelineResult.Succeeded && timelineResult.Result.Count != 0)
{
    do
    {
        // Do whatever needs to be done on the page of timeline entries.

        if (pageCount < maximumPagesToRetrieve &&
            !string.IsNullOrEmpty(timelineResult.Result.Cursor))
        {
            // Get the next page
            timelineResult =
                await agent.GetTimeline(
                    limit: numberOfEntriesPerPage,
                    cursor: timelineResult.Result.Cursor, cancellationToken: cancellationToken);
            pageCount++;
        }

    } while (timelineResult.Succeeded &&
             !string.IsNullOrEmpty(timelineResult.Result.Cursor) &&
             pageCount < maximumPagesToRetrieve);
}
```

> [!TIP]
> The code above will work for any method that returns a paginated result, like `GetFeed()`, `GetAuthorFeed()`, `ListNotifications()`, etc.

## Feed Generators

Feed generators (custom feeds) are created by users and organizations, and are therefore tied to an account via its [DID](../commonTerms.md#dids).
References to feed generators take the form of a [at:// uri](../commonTerms.md#uri) with the following shape:

```
at://<did>/app.bsky.feed.generator/<record_key>
```

To fetch a feed from a generator use the `GetFeed()` method on the agent. It accepts three key parameters,

| Parameter | Type   | Description                                               | Required   |  Default  |
|-----------|--------|-----------------------------------------------------------|:----------:|:---------:|
| feed      | AtUri  | The [at:// uri](../commonTerms.md#uri) of the feed        | Yes        |           |
| cursor    | string | A cursor that tells the server where to paginate from     | No         | *null*    |
| limit     | int    | The number of posts to return per page (max 100)          | No         | 50        |

```c#
var feedResult = await agent.GetFeed(
    feed: "at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.feed.generator/whats-hot",
    limit: 30,
    headers: [new("Accept-Language", "en")]);
```

> [!TIP]
> Bluesky recommends sending the Accept-Language header to get posts in the user's preferred language.
> This header accepts a comma separated string of two-character language codes, e.g. `en,es`.

Feed generators also described by data accessible via the `GetFeedGenerator()` method.
This method returns metadata about the generator including its name, description etc. 
`GetFeedGenerator()` accepts one key parameter,

| Parameter | Type   | Description                                            | Required   | Default   |
|-----------|--------|--------------------------------------------------------|:----------:|:---------:|
| feed      | AtUri  | The [at:// uri](../commonTerms.md#uri) of the feed     | Yes        |           |

```c#
var feedGeneratorResult = await agent.GetFeedGenerator(
    feed: "at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.feed.generator/whats-hot");
```

## Author Feeds

Author feeds return posts by a single user. You can retrieve an author feed with `GetAuthorFeed`,
which accepts four key parameters

| Parameter | Type       | Description                                                                   | Required | Options                                                      | Default  |
| --------- | ---------- | ----------------------------------------------------------------------------- | :------: | :----------------------------------------------------------- | :------: |
| actor     | Did        | The [DID](../commonTerms.md#dids) of the user whose posts you'd like to fetch | Yes      |                                                              |          |
| filter    | FeedFilter | The type of posts you'd like to receive in the results                        | No       | FeedFilter.PostsWithReplies<br />FeedFilter.PostsNoReplies<br />FeedFilter.PostsWithMedia<br />FeedFilter.PostsAndAuthorThreads | FeedFilter.PostsNoReplies |
| cursor    | string     | A cursor that tells the server where to paginate from                         | No       |                                                              | *null*   |
| limit     | int        | The number of posts to return per page (max 100)                              | No       |                                                              | 50       |
```c#

var authorFeedResult = await agent.GetAuthorFeed(
    actor: "did:plc:z72i7hdynmk6r22z27h6tvur",
    filter: FeedFilter..PostsAndAuthorThreads,
    limit: 30);
```
