// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using idunno.AtProto;
using idunno.AtProto.Bluesky.Feed;
using idunno.AtProto.Bluesky.Notifications;

// Necessary to render emojis.
Console.OutputEncoding = System.Text.Encoding.UTF8;

string? username;
string? password;
Uri? proxyUri = null;

if (args.Length == 3)
{
    username = args[0];
    password = args[1];
    proxyUri = new Uri(args[2]);
}
else if (args.Length == 2)
{
    username = args[0];
    password = args[1];
}
else if (args.Length == 1)
{
    username = Environment.GetEnvironmentVariable("_BlueskyUserName");
    password = Environment.GetEnvironmentVariable("_BlueskyPassword");
    proxyUri = new Uri(args[0]);
}
else if (args.Length == 0)
{
    username = Environment.GetEnvironmentVariable("_BlueskyUserName");
    password = Environment.GetEnvironmentVariable("_BlueskyPassword");
}
else
{
    Console.WriteLine("Usage: Notifications <blueskyUsername> <blueskyPassword> (<proxyuri>)");
    Console.WriteLine();
    Console.WriteLine("Or use the _BlueskyUserName and _BlueskyPassword environment variables to provide credentials and use Notifications (<proxyuri>)");
    return;
}

if (username is null || password is null)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Set _BlueSkyUserName and _BlueSkyPassword environment variables before running.");
    return;
}

using (HttpClientHandler handler = new())
{
    if (proxyUri is not null)
    {
        handler.Proxy = new WebProxy
        {
            Address = proxyUri,
            BypassProxyOnLocal = false,
            UseDefaultCredentials = false,
        };
    }

    using (BlueskyAgent agent = new(handler))
    {
        HttpResult<bool> loginResult = await agent.Login(username, password);

        if (loginResult.Succeeded && agent.Session is not null)
        {
            DateTimeOffset notificationCheckDateTime = DateTimeOffset.UtcNow;

            HttpResult<int> unreadCount = await agent.GetNotificationUnreadCount();
            if (unreadCount.Succeeded)
            {
                Console.WriteLine($"You have {unreadCount.Result} unread notification{(unreadCount.Result != 1 ? "s" : "")}.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"getTimelineResult failed: {unreadCount.StatusCode}");
                return;
            }

            if (unreadCount.Result > 0)
            {
                int page = 1;
                bool done = false;

                HttpResult<NotificationsView> notifications = await agent.ListNotifications(5).ConfigureAwait(false);

                while (notifications.Succeeded && !done)
                {
                    Console.WriteLine($"\n📄 Page {page}\n");

                    foreach (Notification notification in notifications.Result!.Notifications)
                    {

                        string? author = notification.Author.Handle.ToString();
                        if (notification.Author.DisplayName is not null && notification.Author.DisplayName.Length > 0)
                        {
                            author = notification.Author.DisplayName;
                        }

                        switch (notification.Reason)
                        {
                            case NotificationReason.Follow:
                                Console.WriteLine($"👥 {author} followed you.");
                                break;

                            case NotificationReason.Like:
                                Console.WriteLine($"❤️ {author} liked your post at {notification.Record.CreatedAt}.");
                                HttpResult<FeedPost> likedPost = await agent.GetPost(notification.ReasonSubject!).ConfigureAwait(false);

                                if (likedPost.Result is not null)
                                {
                                    Console.WriteLine($"  {likedPost.Result.Record.Text}");
                                }
                                else
                                {
                                    Console.WriteLine($"  🗑️ Liked post was deleted.");
                                }
                                break;

                            case NotificationReason.Mention:
                                Console.WriteLine($"📟 {author} mentioned you at {notification.Record.CreatedAt}.");
                                var mentionRecord = (FeedPostRecord)notification.Record;
                                Console.WriteLine($"  {mentionRecord.Text}.");
                                break;

                            case NotificationReason.Quote:
                                Console.WriteLine($"🗨️ {author} quoted your post at {notification.Record.CreatedAt}.");
                                var quoteRecord = (FeedPostRecord)notification.Record;
                                Console.WriteLine($"  {quoteRecord.Text}.");
                                break;

                            case NotificationReason.Reply:
                                Console.WriteLine($"🗣️ {author} replied to your post at {notification.Record.CreatedAt}.");
                                var replyRecord = (FeedPostRecord)notification.Record;
                                Console.WriteLine($"  {replyRecord.Text}.");
                                break;

                            case NotificationReason.Repost:
                                Console.WriteLine($"♻️ {author} reposted your post at {notification.Record.CreatedAt}.");
                                HttpResult<FeedPost> repostedPost = await agent.GetPost(notification.ReasonSubject!).ConfigureAwait(false);
                                if (repostedPost.Result is not null)
                                {
                                    Console.WriteLine($"  {repostedPost.Result.Record.Text}");
                                }
                                else
                                {
                                    Console.WriteLine($"  🗑️ Reposted post was deleted.");
                                }
                                break;

                            default:
                                Console.WriteLine($"{author} did something unknown to trigger a notification.");
                                break;
                        }
                    }

                    if (notifications.Result!.Cursor is null)
                    {
                        done = true;
                    }
                    else
                    {
                        // Get the next page
                        notifications = await agent.ListNotifications(limit: 5, cursor: notifications.Result.Cursor).ConfigureAwait(false);
                        page++;
                    }
                }
            }

            HttpResult<EmptyResponse> updateSeen = await agent.UpdateNotificationSeenAt(notificationCheckDateTime);
        }
        else if (loginResult.Succeeded)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Login succeeded but a session was not created.");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Login failed: {loginResult.StatusCode}");
            if (loginResult.Error is not null)
            {
                Console.WriteLine($"\t{loginResult.Error.Error} {loginResult.Error.Message}");
            }
        }
    }
}
