# Timelines and Feeds

## <a name="timeline">Reading your timeline</a>

To get the timeline for a logged in account you call `agent.GetTimeLine()`.

```c#
HttpResult<Timeline> timelineResult = await agent.GetTimeline();
if (timelineResult.Succeeded && timelineResult.Result.Count != 0)
{
    foreach (FeedViewPost timelineView in timelineResult.Result)
    {
        if (timelineView.Post.Record is Post postRecord && !string.IsNullOrEmpty(postRecord.Text))
        {
            Console.WriteLine($"{feedView.Post.Record.Text}";
        }
        Console.WriteLine($"  From {@timelineView.Post.Author} {GetLabels(timelineView.Post.Author)}");
        Console.WriteLine($"  Posted at: {timelineView.Post.Record.CreatedAt.ToLocalTime():G}");
        Console.Write($"  {timelineView.Post.LikeCount} like{(timelineView.Post.LikeCount != 1 ? "s" : "")} ");
        Console.WriteLine($"{timelineView.Post.RepostCount} repost{(timelineView.Post.RepostCount != 1 ? "s" : "")}.");
        Console.WriteLine($"  AtUri: {timelineView.Post.Uri}");
        Console.WriteLine($"  Cid:   {timelineView.Post.Cid}");

        if (timelineView.Reason is ReasonRepost repost)
        {
            Console.WriteLine($"  ♲ Reposted by {repost.By}");
        }

        if (timelineView.Reply is not null)
        {
            // Display the parent of this post, as it was in reply to something.
        }
    }
}
```

Before attempting to iterate through the timeline the code above first checks if the API call was successful, if so the result from the API call
will contain a collection of `FeedViewPost`s.

Each `FeedViewPost` (defined in the [lexicon for Feeds](https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/feed/defs.json) has a
`Post` property, and optional `Reply` and `Reason` properties. The `Post` property contains information about the post, such as its author,
the time it was created, etc., and a `Record` property that is typically a `PostRecord`, a type of record that has a `Text` property that contains any
text for that `Post`.

The code above shows iterating through `Feed`, retrieving each individual `FeedView` and writing some information from the `FeedView.Post` to the console.

The [Timeline sample](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.Timeline) also contains code which pages through results.
When browsing timelines and feeds you will need to understand how AtProto implements [pagination](cursorsAndPagination.md) with cursors.

## <a name="feeds">Reading feeds</a>

A timeline is a well known feed. A feed is a view created by a feed generator over collections of posts, the criteria for which the feed controls.

A feed is referenced by its `AT URI `, loaded with `GetFeed()` rather than `GetTimeLine()` and then paginated in exactly the same way. 

```c#
AtUri feedUri = new("at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.feed.generator/whats-hot");

const int pageSize = 25;
const int maxPagesToIterate = 10;

var getFeedResult = await agent.GetFeed(feedUri, limit: pageSize);

if (getFeedResult.Succeeded && getFeedResult.Result.Count != 0)
{
    do
    {
        // Display the feed data

        await agent.GetFeed(feedUri,limit: pageSize, cursor: getFeedResult.Result.Cursor);

    } while (getFeedResult.Succeeded &&
             !string.IsNullOrEmpty(getFeedResult.Result.Cursor) &&
             page < maxPagesToIterate) ;
}
```

> [!TIP]
> Some feeds, like the Discover feed use a specialized cursor, rather than the more typical timestamp.
> This can grow with each page, to the point when it is too large to send back to the server, hence the code above,
> taken from the The [Feed sample](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.Feed) limiting the do while loop not just until
> data runs out but also to a maximum number of pages.

## <a name="searching">Searching</a>

Feeds aren't just lists of posts, `SearchPosts()` returns its results as a feed.

```c#
const int pageSize = 25;
const int maxPagesToIterate = 10;

var searchResult = await agent.SearchPosts("#beans", limit: pageSize);

if (searchResult.Succeeded && getFeedResult.Result.Count != 0)
{
    do
    {
        // Display the feed data

        await agent.GetFeed(feedUri,limit: pageSize, cursor: searchResult.Result.Cursor);

    } while (searchResult.Succeeded &&
             !string.IsNullOrEmpty(getFeedResult.Result.Cursor) &&
             page < maxPagesToIterate) ;
}
```

`SearchActors` also returns a feed of results. `GetActorLikes` returns a feed of likes for an actor, `GetLikes` returns a feed of likes for an individual post,
and so on.

You can find these APIs and more in the [HTTP reference](https://docs.bsky.app/docs/api/at-protocol-xrpc-api), each API has its equivalent `BlueskyAgent` method.
