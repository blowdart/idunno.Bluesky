# Getting started with `idunno.AtProto`, a .NET class library for using Bluesky

This document provides a starting point to reading from, and posting to, the [Bluesky social network](https://bsky.app/), using the `idunno.AtProto`. 

Bluesky's HTTP Reference can be found in their [documentation](https://docs.bsky.app/docs/category/http-reference).

You can view a status page for `idunno.AtProto`'s'[current and planned APIs](endpointStatus.md) along with the Bluesky and AT Proto endpoints they call. 

## <a name="gettingStarted">Getting started</a>

As is now tradition we start with "Hello Word".

```c#
BlueskyAgent agent = new ();
HttpResult<bool> loginResult = await agent.Login(username, password);
if (loginResult.Succeeded && agent.Session is not null)
{
    HttpResult<CreateRecordResponse> response = 
        await agent.CreatePost("Hello World");

    if (response.Succeeded)
    {
       // It worked.
       // To get the results of the post creation 
       // you can check response.Result
    }
}
```
## <a name="makingRequests">Making requests to Bluesky and understanding the results</a>

All the supported Bluesky operations are contained in the `BlueskyAgent` class, which takes care of session management for you.

Requests to Bluesky are made over [HTTP](https://docs.bsky.app/docs/category/http-reference) and the results are wrapped up in an `HttpResult` instance, which contains any successful result returned, along with the `200 - Ok` status code, or an HTTP status code indicating an error, and, optionally, some error details. 

These two possibilities are wrapped together in an `HttpResult` that is returned by every API call.

For example, a login call returns an `HttpResult<bool>`. To check the operation succeeded you would

1. Check the `Succeeded` property on the `HttpResult`.
2. If `Succeded` is `true` you can use the `Result` property and continue on your way.
3. If `Succeded` is `false` you can use the `StatusCode` property to examine the HTTP status code returned by the API and, if the API has given a detailed error response, you can use the `Error` property to view any extended error information returned, which may have an `Error` and a `Message` set.

For a successful login `Succeded` will be `true`, and the `Result` property is available. 

For a failed login `Succeded` will be `false`, the `StatusCode` property will be `HttpStatusCode.Unauthorized`, and, as this API returns a detailed error, the `Error` property will be populated, with an `Error` of "AuthenticationRequired" and `Message` of "Invalid identifier or password".

## <a name="connecting">Connecting to Bluesky</a>

As you can see from the [Hello World](#gettingStarted) example connecting to Bluesky consists of creating an instance of a `BlueskyAgent` and calling the login method.

```c#
using (BlueskyAgent agent = new ())
{
    HttpResult<bool> loginResult = 
        await agent.Login(username, password);
    if (loginResult.Succeeded && agent.Session is not null)
    {
        // Do your Bluesky thing
    }
}
```

When a login is successful the agent will store the information needed for subsequent API calls in its `Session` property. API calls that require authentication will use this information automatically. The session tokens will also be refreshed automatically.

### <a name="usingAProxy">Using a proxy server<a>

The agent constructors can take an `HttpClientHandler` that you can use to configure a proxy for each request the agent makes. For example, to use Fiddler as a proxy you would initialize the agent using the following code.

```c#
var proxy = new WebProxy
{
    Address = new Uri($"http://127.0.0.1:8888"),
    BypassProxyOnLocal = false,
    UseDefaultCredentials = false,
};

using (var httpClientHandler = new HttpClientHandler { Proxy = proxy })
{
    using (BlueskyAgent agent = 
        new (httpClientHandler : httpClientHandler))
    {
        // Send a sample request through the configured agent.
        HttpResult<Did> did = await agent.ResolveHandle("blowdart.me");
    }
};
```

### <a name="overridingTheService">Overriding the service used for API calls</a>

If you are using a private or test instance of Bluesky you can tell the agent to use a specific service URI with the `service` constructor parameter. 

```c#
using (BlueskyAgent agent = 
  new (service: new Uri("https://sandbox.mybluesky.local")))
{
    Console.WriteLine($"Connecting to {agent.DefaultService}");
    // Send a sample request through the configured agent.
    HttpResult<Did> did = await agent.ResolveHandle("blowdart.me");
}
```

### <a name="disablingTokenRefresh">Disabling token refresh</a>

If you want to disable automatic token refresh you can do that by the `enableTokenRefresh` constructor parameter to false.

```c#
using (BlueskyAgent agent = new (enableTokenRefresh: false)
{   
    HttpResult<Did> did = await agent.ResolveHandle("blowdart.me");
}
```

## <a name="timeline">Reading your timeline</a>

To get the timeline for a logged in account you call `agent.GetTimeLine()`. If you look at the Bluesky documentation for [getTimeline](https://docs.bsky.app/docs/api/app-bsky-feed-get-timeline) you will see the description of the API as "Get a view of the requesting account's home timeline." A lot of Bluesky's APIs return views over data, rather than just a collection of the data itself, and APIs that view a lot of data also return cursors to allow for pagination. This means in some cases the results you want a buried just a little deeper in the returned results. As getting a timeline returns a [cursor](https://atproto.com/specs/xrpc#cursors-and-pagination) you need to access the `Feed` property on the result to get to the post records that the timeline consists of.

```c#
HttpResult<Timeline> timelineResult = await agent.GetTimeline();
if (timelineResult.Succeeded)
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
            $"  Created At: {feedView.Post.Record.CreatedAt}");
        Console.WriteLine(
            $"  {feedView.Post.LikeCount} like(s)";
        Console.WriteLine(
            $" {feedView.Post.RepostCount} repost(s)");
        Console.WriteLine($"  AtUri={feedView.Post.Uri});
        Console.WriteLine($"  Cid={feedView.Post.Cid}.\n\n");           
        }
    }
}
```

Here we can see the common pattern for checking an outbound API call was successful, before attempt to access the results you check whether the request was successful. You may be familiar with this pattern from [ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/web-api/action-return-types?view=aspnetcore-8.0#httpresults-type). Results from API calls will always be wrapped in a `typed HttpResult<T>`.

Before attempting to iterate through the timeline the code above first checks if the API call was successful, using the `Succeeded` property on `HttpResult<T>`. If that property is `true` then the `Result` property will contain the results of the API call (assuming the API endpoint is responding correctly).

Then for timelines the `Results` property contains a `TimeLine` record which in turn contains a  a `Feed` property which is a collection of `FeedView `instances and an `Cursor` property, which may or may not be populated, based on whether pagination is needed to retrieve a complete timeline.

The code above shows iterating through `Feed`, retrieving each individual `FeedView` and writing some information from the `FeedView` to the console. 

### <a name="errorHandling">Error Handling</a>
The sample code above leaves out error handling when a request fails. The error handling pattern looks like this:

```c#
HttpResult<Timeline> timelineResult = await agent.GetTimeline();

if (timelineResult.Succeeded && timelineResult.Result is not null)
{
    // Everything was successful
}
else if (timelineResult.Succeeded && timelineResult.Result is null)
{
    // This should never happen, it would indicate a
    // misbheaving API. You can choose to react to this
    // gracefully with this check, or just let an exception
    // happen if you try to access the Result property and 
    // it is null by using the ! (null forgiving) operator.
}
else
{
    Console.WriteLine(
        $"getTimelineResult failed: {timelineResult.StatusCode}");
    if (timelineResult.Error is not null)
    {
        Console.Write(
            $"\t{timelineResult.Error.Error}");
        Console.WriteLine(
            $"\t{timelineResult.Error.Message}");
    }
}
```
The result's `StatusCode` property exposes the HTTP status code the API endpoint returned. Further useful error  information may be present in the `Error` property.

A full sample can be found in the [Timeline](../samples/Samples.TimeLine) project in the [samples](../samples/) directory in this GitHub repository.

## <a name="atURIs">AT URIs</a>

You may notice from the [timeline sample](#timeline) that `FeedView` has a `Uri` property. This is not an http URI, it is an [AT URI](https://atproto.com/specs/at-uri-scheme). 

An AT URI is a unique reference to an individual record on the network, and actions that work on records, such as liking a post, require the AT URI for the record they're acting on or for.

## <a name="checkingNotifications">Checking your notifications</a>
Like the  [timeline sample](#timeline) notifications can be retrieved and iterated through, but Bluesky allows you to check your unread count first.
```c#
HttpResult<int> unreadCount = await agent.GetNotificationUnreadCount();
```
`GetNotificationUnreadCount()` allows you to check if there's anything unread before you consider retrieving notifications. This could also be used for an indicatior in an application or badge. `GetNotificationUnreadCount()` also takes an optional `DateTimeOffset` parameter to allow you to get the unread count from a particular date and time.

To retrieve your notifications, read or unread, you call `ListNotifications()`.
```c#
var notifications = 
    await agent.ListNotifications().ConfigureAwait(false);
```
From there, you would perform the `.Succeded` check and work your way through the notifications collection exposed in the `Result` property. Each notification has a reason property. 

Each type of notification, `Follow`, `Like`, `Mention`, `Quote`, `Reply`, and `Repost` having varying types of information used to supplement the notification with appropriate information.

```c#
foreach (
    Notification notification in notifications.Result!.Notifications)
{
    // As in the timeline example we first look at how
    // the author should be displayed, using their display
    // name if one exists, otherwise we just use their handle.
    string? author = notification.Author.Handle.ToString();
    if (notification.Author.DisplayName is not null && 
        notification.Author.DisplayName.Length > 0)
    {
        author = notification.Author.DisplayName;
    }

    switch (notification.Reason)
    {
        case NotificationReason.Follow:
            Console.Write(
                $" {author} followed you");
            Console.WriteLine(
                $" at {notification.Record.CreatedAt}.");
            // There's no other information that a follow record 
            // provides, so we're done.
            break;

        case NotificationReason.Like:
            Console.Write($"{author} liked your post");
            Console.WriteLine(
                $" at {notification.Record.CreatedAt}.");
            
            // A like happens on a post. 
            // So let's try to get that post
            // ResultSubject is an AT URI that points to that post,
            // so let's get it.
            HttpResult<FeedPost> likedPost = await 
                agent.GetPost(notification.ReasonSubject!)
                     .ConfigureAwait(false);
            
            // However people delete their posts, so the API status code
            // would be 200 OK but the actual result property would be 
            // null.
            if (likedPost.Result is not null)
            {
                // The post still exists
                Console.WriteLine($"  {likedPost.Result.Record.Text}");
            }
            else
            {
                // The post was deleted
                Console.WriteLine($"  Liked post was deleted.");
            }
            break;

        case NotificationReason.Mention:
            Console.Write(
                $" {author} mentioned you ");
            Console.WriteLine(
                $" at {notification.Record.CreatedAt}.");
            
            // Mentions, quotes, and replys all have a record property
            // that is basically a FeedPost record, 
            // and FeedPost contains the text which triggered the
            // notification.            
            FeedPostRecord mentionRecord = 
                (FeedPostRecord)notification.Record;
            Console.WriteLine($"  {mentionRecord.Text}.");
            break;

        case NotificationReason.Quote:
            Console.Write(
                $"{author} quoted your post");
            Console.WriteLine(
                $" at {notification.Record.CreatedAt}.");                
            FeedPostRecord quoteRecord = 
                (FeedPostRecord)notification.Record;
            Console.WriteLine($"  {quoteRecord.Text}.");
            break;

        case NotificationReason.Reply:
            Console.Write(
                $"{author} replied to your post");
            Console.WriteLine(
                $" at {notification.Record.CreatedAt}.");
            FeedPostRecord replyRecord = 
                (FeedPostRecord)notification.Record;
            Console.WriteLine($"  {replyRecord.Text}.");
            break;

        case NotificationReason.Repost:
            Console.Write($"{author} reposted your post");
            Console.WriteLine($" at {notification.Record.CreatedAt}.");

            // Reposts work like likes, so we need to grab the post
            // that was reposted.
            HttpResult<FeedPost> repostedPost = 
            await agent.GetPost(notification.ReasonSubject!)
                       .ConfigureAwait(false);
            if (repostedPost.Result is not null)
            {
                Console.WriteLine(
                    $"  {repostedPost.Result.Record.Text}");
            }
            else
            {
                Console.WriteLine($"Reposted post was deleted.");
            }
            break;

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
`UpdateNotificationSeenAt()` takes an optional `DateTimeOffset seenAt` parameter, so you can, and probably should, save a timestamp before you start working through notifications, and then use that timestamp once you've finished, so that notifications that happen after you retrieved the notification list don't get marked as seen.

A full sample can be found in the [Notifications](../samples/Samples.Notifications) project in the [samples](../samples/) directory in this GitHub repository.

## <a name=cursorsPagination>Cursors and Pagination</a>
If you've taken a look at the  [Notifications](../samples/Samples.Notifications) sample project you may have noticed it pages through notifications rather than get all the notifications at once.

The Notifications API has a few different ways to call it,

```c#
public async Task<HttpResult<NotificationsView>> ListNotifications()
public async Task<HttpResult<NotificationsView>> ListNotifications(
            CancellationToken cancellationToken = default)
public async Task<HttpResult<NotificationsView>> ListNotifications(
            int? limit = null,
            string? cursor = null,
            DateTimeOffset? seenAt = null,
            CancellationToken cancellationToken = default)
public async Task<HttpResult<NotificationsView>> ListNotifications(
            int? limit,
            string? cursor,
            DateTimeOffset? seenAt,
            Uri service,
            CancellationToken cancellationToken = default)
```

The sample uses the `limit` and `cursor` parameters to get notifications one page at a time, consisting of five notifications per page.

```c#
bool done = false;

HttpResult<NotificationsView> notifications = 
     await agent.ListNotifications(5).ConfigureAwait(false);
```

The first call to `ListNotifications()` uses the `limit` parameter to control how many  notifications are returned from the API, in the code above we are asking for fix notifications at a time. You can see a boolean value `done` just waiting to be used.

Next the code loops until either the notification api call fails, or until `done` becomes `true`.

```c#
while (notifications.Succeeded && !done)
{
    // Do whatever needs to be done on the page
    // of notifications.

    // Now get the next page if one exists.
    if (notifications.Result!.Cursor is null)
    {
        done = true;
    }
    else
    {
        // Get the next page
        notifications = 
            await agent.ListNotifications(
                limit: 5, 
                cursor: notifications.Result.Cursor)
                .ConfigureAwait(false);
    }
}
```

Once you've done whatever you want to do on the first page of notifications you will want to go retrieve the next page. The call to do that has gained a new parameter, `cursor`, that comes from the results of the first call.
```c#
notifications = 
    await agent.ListNotifications(
        limit: 5, 
        cursor: notifications.Result.Cursor).ConfigureAwait(false);
```

The response from the `ListNotifications()` API includes a cursor every time more records are available to be retrieved. Passing the cursor from a previous API response to a new request gets the next page of data. When a cursor is no longer returned no more data remains. The AT Proto documentation has a section on [Cursors and Pagination](https://atproto.com/specs/xrpc#cursors-and-pagination).

APIs that support pagination include ListNotifications(), SearchActors(), GetTimeline() and GetSuggestions().

## <a name="creatingAPost">Creating a post</a>
## Replying to a post
## Resolving DIDs