# <a name="checkingNotifications">Checking your notifications</a>

Like the [timeline](timeline.md) notifications can be retrieved and iterated through. Bluesky also allows you to check your unread notification count.

```c#
HttpResult<int> unreadCount = await agent.GetNotificationUnreadCount();
```

`GetNotificationUnreadCount()` allows you to check if there's anything unread before you consider retrieving notifications. This could also be used for an indicator in an application or badge.
`GetNotificationUnreadCount()` also takes an optional `DateTimeOffset` parameter to allow you to get the unread count from a particular date and time.

To retrieve your notifications, read or unread, call `ListNotifications()`.

```c#
var notifications = 
    await agent.ListNotifications().ConfigureAwait(false);
```
From there, you would perform the `.Succeded` check and work your way through the notifications collection exposed in the `Result` property. Each notification has a reason property.

Each type of notification, for example `Follow`, `Mention` or `Quote`, have varying types of information used to supplement the notification with appropriate information for its type.

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

## <a name=cursorsPagination>Paging results</a>

`ListNotifications()` returns results a page at a time, more results may be waiting for a subsequent call.

The [Notifications sample](https://github.com/blowdart/idunno.atproto/tree/main/samples/Samples.Notifications) uses the `limit` and `cursor` parameters to get notifications
one page at a time, consisting of five notifications per page.

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
This parameter is how Bluesky APIs implement paging. If there are no more results then the cursor returned from the API call will be null.

For more details see [Cursors and Pagination](cursorsAndPagination.md).
or AT Proto documentation section on [Cursors and Pagination](https://atproto.com/specs/xrpc#cursors-and-pagination).

## <a name="activity">Subscribing to and viewing subscriptions of activity notifications</a>

To subscribe to activity notifications for an account call `agent.SetActivitySubscription()` with the account's DID or handle, and the type of activity you want to subscribe to.

```c#
agent.SetActivitySubscription(
   did: "did:plc:3iicfxmgcfr32lansi4ju7oa",
   posts: true,
   replies: true
)
```

To unsubscribe set both the `posts` and `replies` values to `false`.

To view which accounts the user is currently subscribed to call `agent.ListActivitySubscriptions()`. This returns a pageable list of views over the profiles whose activity
you have subscribed to. To see your subscription settings for each profile you must drill down into the `Viewer` property on the profile,
then check the `ActivitySubscription` property on `Viewer`.

## Controlling who can subscribe to your activities

You can control who has the ability to subscribe to the current user's activity using `SetNotificationDeclaration()`. This takes a `NotificationAllowedFrom` enum, which allows you to choose
`None`, `Followers` or `Mutals`.

## <a name="preferences">Getting and setting notification preferences</a>

You can retrieve a user's current notification preferences with `GetNotificationPreferences()`, which returns a `NotificationPreferences` instance.

To set preferences take the `NotificationPreferences` instance you retrieved with `GetNotificationPreferences()`, change the preferences to be what you require, and then call
`SetNotificationPreferences` with the updated `NotificationPreferences` instance.

```c#
var notificationPreferences = await agent.GetNotificationPreferences();

notificationPreferences.Result.SubscribedPost.Push = false;
await agent.SetNotificationPreferences(notificationPreferences.Result);
```
