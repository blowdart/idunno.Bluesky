# Cursors & Pagination

If you've looked at the source code for the [Notifications](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.Notifications) or
[Timeline](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.Timeline) samples you may have noticed they through notifications
rather than get all the notifications at once.

Each sample uses the limit and cursor parameters to get their results one page at a time.
For example, to page through notifications, with each page containing a maximum of five results you would write the following:.

```c#
HttpResult<NotificationsView> notifications = 
     await agent.ListNotifications(5);
```

The first call to `ListNotifications()` uses the limit parameter to control how many notifications are returned from the API.

Then the code loops until either the the call to `ListNotifications()` returns an empty cursor, or it fails.

```c#
if (notifications.Succeeded && notifications.Result.Count != 0)
{
    do
    {
        // Do whatever needs to be done on the page
        // of notifications.

        // Get the next page
        notifications = 
            await agent.ListNotifications(
                limit: 5, 
                cursor: notifications.Result.Cursor));

    } while (notificationsListResult.Succeeded &&
             !string.IsNullOrEmpty(notificationsListResult.Result.Cursor))
}
```

You can see that there's a difference between the first call to `ListNotifications()` and the second, the addition of the `cursor` parameter.
This parameter is how Bluesky APIs implement paging.

The response from the `ListNotifications()` API, or any API that supports paging, includes a cursor every time more things are available.
Passing the cursor from a previous API response to a new request gets the next page of data.
The AT Proto documentation has a section on [Cursors and Pagination](https://atproto.com/specs/xrpc).

APIs that support pagination include `ListNotifications()`, `SearchActors()`, `GetTimeline()`, `GetFeed()` and `GetSuggestions()`.

> [!TIP]
> Cursors aren't a standard format - a feed can generate whatever they type of cursor they want,
> and use that to decide what to serve next. The [Feed sample](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.Feed) pages
> through the Bluesky Discovery feed. This feed uses the cursor to track what it's already shown you, so as you load more and more pages the cursor
> grows and grows, until, if you page for long enough, the cursor is too big to send in the request and you get a `400 Bad Request` response.
> This is why the feed sample only loads 10 pages of 5 posts.
