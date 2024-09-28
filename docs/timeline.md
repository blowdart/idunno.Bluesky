# Timelines and Feeds

## <a name="timeline">Reading your timeline</a>

To get the timeline for a logged in account you call `agent.GetTimeLine()`.
If you look at the Bluesky documentation for [getTimeline](https://docs.bsky.app/docs/api/app-bsky-feed-get-timeline) you will see the description
of the API as "Get a view of the requesting account's home timeline." A lot of the Bluesky APIs return views over data,
rather than just a collection of the data itself, and APIs that view a lot of data also return cursors to allow for pagination.
This means in some cases the results you want a buried just a little deeper in the returned results.
As getting a timeline returns a [cursor](https://atproto.com/specs/xrpc#cursors-and-pagination) you need to access the `Feed` property on the result
to get to the post records for the timeline.

```c#
HttpResult<Timeline> timelineResult = await agent.GetTimeline();
if (timelineResult)
{
    Console.WriteLine(
        $"Timeline: {timelineResult.Result!.Feed.Count} post(s)");

    foreach (FeedView feedView in timelineResult.Result.Feed)
    {
        string? author = feedView.Post.Author.Handle.ToString();
        
        if (feedView.Post.Author.DisplayName is not null && 
            feedView.Post.Author.DisplayName.Length > 0)
        {
            author = feedView.Post.Author.DisplayName;
        }

        Console.WriteLine($"{feedView.Post.Record.Text}";
        Console.Write($"{author} ");
        Console.Write($"{feedView.Post.Author.Handle}/");
        Console.WriteLine{$"{feedView.Post.Author.Did}.");
        Console.WriteLine(
            $"  Created At: {feedView.Post.Record.CreatedAt!.Value.LocalDateTime}");
        Console.WriteLine(
            $"  {feedView.Post.LikeCount} like(s)";
        Console.WriteLine(
            $" {feedView.Post.RepostCount} repost(s)");
        Console.WriteLine($"  AtUri={feedView.Post.Uri});
        Console.WriteLine($"  Cid  ={feedView.Post.Cid}.\n\n");           
        }
    }
}
```

Before attempting to iterate through the timeline the code above first checks if the API call was successful.

Then for timelines the `Results` property contains a `TimeLine` record which in turn contains a  a `Feed` property which is a collection
of `FeedView`instances and a `Cursor` property, which may or may not be `null`, based on whether pagination is needed to retrieve a complete timeline.

The code above shows iterating through `Feed`, retrieving each individual `FeedView` and writing some information from the `FeedView` to the console. 

A full sample can be found in the [Timeline](https://github.com/blowdart/idunno.atproto/tree/main/samples/Samples.TimeLine) project in the
[samples](https://github.com/blowdart/idunno.atproto/tree/main/samples) directory.

## Reading feeds
