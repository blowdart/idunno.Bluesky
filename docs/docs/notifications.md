﻿# <a name="checkingNotifications">Checking your notifications</a>

Like the [timeline sample](timeline.md) notifications can be retrieved and iterated through, but Bluesky allows you to check your unread count first.

```c#
HttpResult<int> unreadCount = await agent.GetNotificationUnreadCount();
```

`GetNotificationUnreadCount()` allows you to check if there's anything unread before you consider retrieving notifications. This could also be used for an indicator in an application or badge.
`GetNotificationUnreadCount()` also takes an optional `DateTimeOffset` parameter to allow you to get the unread count from a particular date and time.

To retrieve your notifications, read or unread, you call `ListNotifications()`.

```c#
var notifications = 
    await agent.ListNotifications().ConfigureAwait(false);
```

From there, you would perform the `.Succeded` check and work your way through the notifications collection exposed in the `Result` property. Each notification has a reason property.

Each type of notification, `Follow`, `Like`, `Mention`, `Quote`, `Reply`, and `Repost` having varying types of information used to supplement the notification with appropriate information.

```c#
foreach (Notification notification in notifications.Result!.Notifications)
{
    switch (notification.Reason)
    {
        case NotificationReason.Follow:
            Console.Write($" {notification.Author} followed you");
            // There's no other information that a follow record 
            // provides, so we're done.
            break;

        case NotificationReason.Like:
            if (notification.ReasonSubject is not null)
            {
                // A like happens on a post.
                // ResultSubject is an AT URI that points to that post,
                // So let's go get a hydrated PostView for that post.

                var getLikedPost = await agent.GetPostView(notification.ReasonSubject, cancellationToken: cancellationToken);

                if (getLikedPost.Succeeded)
                {
                    if (notification.Author.Did != getLikedPost.Result.Author.Did)
                    {
                        Console.WriteLine($"{notification.Author} liked your post at {getLikedPost.Result.Record.CreatedAt.ToLocalTime():G}.");
                    }
                    else
                    {
                        Console.WriteLine($"You liked your own post at {getLikedPost.Result.Record.CreatedAt.ToLocalTime():G}.");
                    }

                    Console.WriteLine($"{getLikedPost.Result.Record.Text}");
                }
                else if (getLikedPost.StatusCode == System.Net.HttpStatusCode.OK && getLikedPost.Result is null)
                {
                    Console.WriteLine("Liked post was deleted");
                }
            }
            break;

        // See the notification sample for a full illustration of each of teh notification types.

        default:
            // Error handling in the case of an 
            // unknown notification type goes here.
            break;
    }
}
```

Then finally, once you've displayed all the unread (and/or previously read) notifications you would tell Bluesky that they've been read with `UpdateNotificationSeenAt()`.

```c#
HttpResult<EmptyResponse> updateSeen = 
    await agent.UpdateNotificationSeenAt();
```

`UpdateNotificationSeenAt()` takes an optional `DateTimeOffset seenAt` parameter, so you can, and probably should, save a timestamp before you start working through notifications,
and then use the saved timestamp once you've finished, so that notifications that happen after you retrieved the notification list don't get marked as seen.
`UpdateNotificationSeenAt()` can also take a `seenAt` parameter in the past, which allows you to reset when Bluesky things you last saw notifications, which is very handy
for testing any notification viewer you've written.

A full sample can be found in the [Notifications](https://github.com/blowdart/idunno.atproto/tree/main/samples/Samples.Notifications) project in the
[samples](https://github.com/blowdart/idunno.atproto/tree/main/samples) directory in this GitHub repository.

## <a name=cursorsPagination>Cursors and Pagination</a>

If you've looked at the source code for the [Notifications sample](https://github.com/blowdart/idunno.atproto/tree/main/samples/Samples.Notifications) you may have noticed it pages
through notifications rather than get all the notifications at once.

The sample uses the `limit` and `cursor` parameters to get notifications one page at a time, consisting of five notifications per page.

```c#
HttpResult<NotificationsView> notifications = 
     await agent.ListNotifications(5);
```

The first call to `ListNotifications()` uses the `limit` parameter to control how many notifications are returned from the API.

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
The AT Proto documentation has a section on [Cursors and Pagination](https://atproto.com/specs/xrpc#cursors-and-pagination).

APIs that support pagination include `ListNotifications()`, `SearchActors()`, `GetTimeline()`, `GetFeed()` and `GetSuggestions()`.
One thing to note for feeds is that cursors aren't a standard format, an API can generate whatever they want, and use that to
decide what to serve next. The [Feed](https://github.com/blowdart/idunno.atproto/tree/main/samples/Samples.Feed) sample pages
through the Bluesky Discovery feed. This feed users the cursor to track what it's already shown you, so as you load more and more pages the
cursor grows and grows, until, if you page for long enough the cursor is too big to send in the request and you get a `400 Bad Request`` response.
This is why the feed sample only loads 10 pages of 5 posts.
