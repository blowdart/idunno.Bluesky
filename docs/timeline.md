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
        Console.WriteLine($"  {timelineView.Post.LikeCount} like{(timelineView.Post.LikeCount != 1 ? "s" : "")} {timelineView.Post.RepostCount} repost{(timelineView.Post.RepostCount != 1 ? "s" : "")}.");
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

Each `FeedViewPost`
(defined in the [lexicon for Feeds](https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/feed/defs.json) has a `Post` property,
and optional `Reply` and `Reason` properties. The `Post` property contains information about the post, such as its author, the time it was created,
etc., and a `Record` property that is typically a `PostRecord`, a type of record that has a `Text` property that contains any text for that `Post`.

The code above shows iterating through `Feed`, retrieving each individual `FeedView` and writing some information from the `FeedView`'S `Post` to
the console.

## Paginating results

The `TimeLine` class also contains a `Cursor` property. Cursors are used by some Bluesky APIs to allow pagination. If an API returns a cursor property
and it is not null or empty this indicates that there are more results that can be fetched. To fetch the next page you call the API again, passing in the
cursor from the previous call and keep doing that until the cursor is null or empty, indicating that there are no more results to fetch.

```c#
AtProtoHttpResult<idunno.Bluesky.Feed.Timeline> timelineResult = await agent.GetTimeline();

if (timelineResult.Succeeded && timelineResult.Result.Count != 0)
{
    do
    {
        // Perform your actions on this page of timeline results.

        // If there are more results waiting, go get them
        if (!string.IsNullOrEmpty(timelineResult.Result.Cursor))
        {
            timelineResult = await agent.GetTimeline(cursor: timelineResult.Result.Cursor);
        }

    } while (timelineResult.Succeeded && !string.IsNullOrEmpty(timelineResult.Result.Cursor))
}
```

A full sample can be found in the [Timeline](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.Timeline) project in the
[samples](https://github.com/blowdart/idunno.Bluesky/tree/main/samples) directory.

## Reading feeds
