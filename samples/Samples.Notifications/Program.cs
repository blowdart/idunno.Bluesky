// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.Bluesky;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Notifications;

using Samples.Common;
using idunno.AtProto.Labels;

namespace Samples.Notifications
{
    public sealed class Program
    {

        static async Task<int> Main(string[] args)
        {
            // Necessary to render emojis.
            Console.OutputEncoding = Encoding.UTF8;

            var parser = Helpers.ConfigureCommandLine(PerformOperations);
            await parser.InvokeAsync(args);

            return 0;

        }

        static async Task PerformOperations(string? handle, string? password, string? authCode, Uri? proxyUri, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(handle);
            ArgumentNullException.ThrowIfNullOrEmpty(password);

            // Uncomment the next line to route all requests through Fiddler Everywhere
            proxyUri = new Uri("http://localhost:8866");

            // Uncomment the next line to route all requests  through Fiddler Classic
            // proxyUri = new Uri("http://localhost:8888");

            // Get an HttpClient configured to use a proxy, if proxyUri is not null.
            using (HttpClient? httpClient = Helpers.CreateOptionalHttpClient(proxyUri))

            // Change the log level in the ConfigureConsoleLogging() to enable logging
            using (ILoggerFactory? loggerFactory = Helpers.ConfigureConsoleLogging(LogLevel.Debug))

            using (var agent = new BlueskyAgent(httpClient: httpClient, loggerFactory: loggerFactory))
            {
                var loginResult = await agent.Login(handle, password, authCode, cancellationToken: cancellationToken);
                if (!loginResult.Succeeded)
                {
                    if (loginResult.AtErrorDetail is not null &&
                        string.Equals(loginResult.AtErrorDetail.Error!, "AuthFactorTokenRequired", StringComparison.OrdinalIgnoreCase))
                    {
                        ConsoleColor oldColor = Console.ForegroundColor;

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Login requires an authentication code.");
                        Console.WriteLine("Check your email and use --authCode to specify the authentication code.");
                        Console.ForegroundColor = oldColor;

                        return;
                    }
                    else
                    {
                        ConsoleColor oldColor = Console.ForegroundColor;

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Login failed.");
                        Console.ForegroundColor = oldColor;

                        if (loginResult.AtErrorDetail is not null)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;

                            Console.WriteLine($"Server returned {loginResult.AtErrorDetail.Error} / {loginResult.AtErrorDetail.Message}");
                            Console.ForegroundColor = oldColor;

                            return;
                        }
                    }
                }

                Preferences preferences = new();
                var preferencesResult = await agent.GetPreferences(cancellationToken: cancellationToken);
                if (preferencesResult.Succeeded)
                {
                    preferences = preferencesResult.Result;
                }

                DateTimeOffset notificationCheckDateTime = DateTimeOffset.UtcNow;

                AtProtoHttpResult<int> unreadCount = await agent.GetNotificationUnreadCount(cancellationToken: cancellationToken);
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

                const int pageSize = 10;

                AtProtoHttpResult<NotificationCollection> notificationsListResult =
                    await agent.ListNotifications(
                        limit: pageSize,
                        subscribedLabelers: preferences.SubscribedLabelers,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                if (notificationsListResult.Succeeded && notificationsListResult.Result.Count != 0)
                {
                    do
                    {
                        foreach (Notification notification in notificationsListResult.Result)
                        {
                            if (!notification.IsRead)
                            {
                                Console.Write("\u001b[1m");
                            }

                            switch (notification.Reason)
                            {
                                case NotificationReason.Follow:
                                    Console.WriteLine($"🕵 {notification.Author} followed you.");
                                    PrintLabels(notification.Author);
                                    break;

                                case NotificationReason.Like:
                                    {
                                        if (notification.Record is PostRecord likeRecord)
                                        {
                                            if (notification.Author.Did != agent.Session!.Did)
                                            {
                                                Console.WriteLine($"❤️ {notification.Author} liked your post at {notification.Record.CreatedAt.LocalDateTime}.");
                                                PrintLabels(notification.Author);
                                            }
                                            else
                                            {
                                                Console.WriteLine($"❤️ You liked your own post at {notification.Record.CreatedAt.LocalDateTime}.");
                                            }
                                            Console.WriteLine($"   {likeRecord.Text}");
                                        }
                                    }
                                    break;

                                case NotificationReason.Mention:
                                    {
                                        if (notification.Record is PostRecord mentionRecord)
                                        {
                                            if (notification.Author.Did != agent.Session!.Did)
                                            {
                                                Console.WriteLine($"📟 {notification.Author} mentioned you at {notification.Record.CreatedAt.LocalDateTime}.");
                                                PrintLabels(notification.Author);
                                            }
                                            else
                                            {
                                                Console.WriteLine($"📟 You mentioned yourself at {notification.Record.CreatedAt.LocalDateTime}.");
                                            }
                                            Console.WriteLine($"   {mentionRecord.Text}");
                                        }
                                    }
                                    break;

                                case NotificationReason.Quote:
                                    {
                                        if (notification.Record is PostRecord quoteRecord)
                                        {
                                            if (notification.Author.Did != agent.Session!.Did)
                                            {
                                                Console.WriteLine($"🗨️ {notification.Author} quoted your post at {notification.Record.CreatedAt.LocalDateTime}.");
                                                PrintLabels(notification.Author);
                                            }
                                            else
                                            {
                                                Console.WriteLine($"🗨️ You quoted your post at {notification.Record.CreatedAt.LocalDateTime}.");
                                            }
                                            Console.WriteLine($"   \"{quoteRecord.Text}\"");

                                            if (notification.ReasonSubject is not null)
                                            {
                                                AtProtoHttpResult<PostView> getPostViewResult =
                                                    await agent.GetPostView(notification.ReasonSubject, cancellationToken: cancellationToken).ConfigureAwait(false);
                                                if (getPostViewResult.Succeeded)
                                                {
                                                    // The post that was liked hasn't been deleted.
                                                    if (!string.IsNullOrEmpty(getPostViewResult.Result.Record.Text))
                                                    {
                                                        Console.WriteLine($"\u001b[3m     {getPostViewResult.Result.Record.Text}\u001b[23m");
                                                    }
                                                }
                                                else
                                                {
                                                    Console.WriteLine("      🛈 Quoted post was deleted.");
                                                }
                                            }
                                            else
                                            {
                                                Console.WriteLine("      🛈 Quoted post was deleted.");
                                            }
                                        }
                                    }
                                    break;

                                case NotificationReason.Reply:
                                    {
                                        if (notification.Record is PostRecord replyRecord)
                                        {
                                            if (replyRecord.Reply is not null && replyRecord.Reply.Parent is not null)
                                            {
                                                string parentPostOwner = "your";

                                                AtProtoHttpResult<PostView> inReplyToFeedPost =
                                                    await agent.GetPostView(replyRecord.Reply.Parent.Uri, cancellationToken: cancellationToken).ConfigureAwait(false);

                                                if (inReplyToFeedPost.Succeeded)
                                                {
                                                    if (inReplyToFeedPost.Result.Author.Did != agent.Session!.Did)
                                                    {
                                                        parentPostOwner = $"{inReplyToFeedPost.Result.Author}'s";
                                                    }
                                                }
                                                Console.WriteLine($"↳ {notification.Author} replied to {parentPostOwner} post at {notification.Record.CreatedAt.LocalDateTime}.");
                                                if (notification.Author.Did != agent.Session!.Did)
                                                {
                                                    PrintLabels(notification.Author);
                                                }
                                                Console.WriteLine($"   {replyRecord.Text}");
                                            }
                                        }
                                    }
                                    break;

                                case NotificationReason.Repost:
                                    {
                                        if (notification.ReasonSubject is not null)
                                        {
                                            AtProtoHttpResult<PostView> repostView =
                                                await agent.GetPostView(notification.ReasonSubject!, cancellationToken: cancellationToken).ConfigureAwait(false);
                                            if (repostView.Succeeded)
                                            {

                                                Console.WriteLine($"♲ {notification.Author} reposted your post at {notification.Record.CreatedAt.LocalDateTime}.");
                                                if (notification.Author.Did != agent.Session!.Did)
                                                {
                                                    PrintLabels(notification.Author);
                                                }
                                                Console.WriteLine($"   {repostView.Result.Record.Text}");
                                            }
                                        }
                                    }
                                    break;

                                default:
                                    Console.WriteLine($"{notification.Author} did something unknown to trigger a notification at {notification.Record.CreatedAt.LocalDateTime}.");
                                    if (notification.Author.Did != agent.Session!.Did)
                                    {
                                        PrintLabels(notification.Author);
                                    }
                                    break;
                            }

                            if (!notification.IsRead)
                            {
                                Console.Write("\u001b[22m");
                            }
                        }

                        // Get the next page
                        notificationsListResult = await agent.ListNotifications(
                            limit: pageSize,
                            cursor: notificationsListResult.Result.Cursor,
                            subscribedLabelers: preferences.SubscribedLabelers,
                            cancellationToken: cancellationToken).ConfigureAwait(false);

                    } while (notificationsListResult.Succeeded && !string.IsNullOrEmpty(notificationsListResult.Result.Cursor));

                    await agent.UpdateNotificationSeenAt(notificationCheckDateTime, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            }

            return;
        }

        static void PrintLabels(ProfileViewBasic author)
        {
            if (author is null)
            {
                return;
            }

            StringBuilder labelBuilder = new();

            foreach (Label label in author.Labels)
            {
                labelBuilder.Append(CultureInfo.InvariantCulture, $"[{label.Value}] ");
            }

            if (labelBuilder.Length > 0)
            {
                labelBuilder.Length--;
            }

            string labelsAsString = labelBuilder.ToString();

            if (!string.IsNullOrWhiteSpace(labelsAsString))
            {
                Console.WriteLine($"   {labelsAsString}");
            }

            return;
        }

    }
}
