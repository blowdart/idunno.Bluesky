// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.Bluesky;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Feed;

using Samples.Common;
using System.Globalization;
using System.Text;
using idunno.AtProto.Labels;

namespace Samples.Timeline
{
    public sealed class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Necessary to render emojis.
            Console.OutputEncoding = System.Text.Encoding.UTF8;

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

            // Create a new BlueSkyAgent
            using (var agent = new BlueskyAgent(httpClient: httpClient, loggerFactory: loggerFactory))
            {
                // Test code goes here.

                // Delete if your test code does not require authentication
                // START-AUTHENTICATION
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
                // END-AUTHENTICATION

                Preferences preferences = new();
                var preferencesResult = await agent.GetPreferences(cancellationToken: cancellationToken);
                if (preferencesResult.Succeeded)
                {
                    preferences = preferencesResult.Result;
                }

                // Change this to adjust how many timeline entries will be displayed before exiting.
                const int maximumNumberOfEntries = 250;

                // Change this to adjust how many entries are returned in each API call
                int? pageSize = 10;

                int page = 1;
                int postCounter = 0;

                AtProtoHttpResult<idunno.Bluesky.Feed.Timeline> timelineResult =
                    await agent.GetTimeline(
                        limit: pageSize,
                        subscribedLabelers: preferences.SubscribedLabelers,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                if (timelineResult.Succeeded && timelineResult.Result.Count != 0 && !cancellationToken.IsCancellationRequested)
                {
                    do
                    {
                        if (!string.IsNullOrEmpty(timelineResult.Result.Cursor))
                        {
                            Console.WriteLine($"\n📄 Page {page}\n");
                        }

                        foreach (FeedViewPost timelineView in timelineResult.Result)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }

                            postCounter++;
                            if (postCounter == maximumNumberOfEntries)
                            {
                                break;
                            }

                            Console.WriteLine($"Entry Type : {timelineView.Post.Record.GetType()}");

                            if (!string.IsNullOrEmpty(timelineView.Post.Record.Text))
                            {
                                Console.WriteLine($"{timelineView.Post.Record.Text}");
                            }

                            Console.WriteLine($"  From {@timelineView.Post.Author} {GetLabels(timelineView.Post.Author)}");
                            Console.WriteLine($"  Posted at: {timelineView.Post.Record.CreatedAt.ToLocalTime():G}");
                            Console.WriteLine($"  {timelineView.Post.LikeCount} like{(timelineView.Post.LikeCount != 1 ? "s" : "")} {timelineView.Post.RepostCount} repost{(timelineView.Post.RepostCount != 1 ? "s" : "")}.");
                            Console.WriteLine($"  AtUri: {timelineView.Post.Uri}");
                            Console.WriteLine($"  Cid:   {timelineView.Post.Cid}");

                            string postLabels = GetLabels(timelineView.Post);
                            if (!string.IsNullOrEmpty(postLabels))
                            {
                                Console.WriteLine($"  {postLabels}");
                            }

                            if (timelineView.Reason is ReasonRepost repost)
                            {
                                Console.WriteLine($"  ♲ Reposted by {repost.By}");
                            }

                            // The post we're looking at was a reply to another post,
                            // So let's display some information from that post.
                            if (timelineView.Reply is not null)
                            {
                                switch (timelineView.Reply.Parent)
                                {
                                    case null:
                                        break;

                                    case NotFoundPost:
                                        Console.WriteLine("  🗑  In reply to a now deleted post.");
                                        break;

                                    case BlockedPost:
                                        Console.WriteLine("  🚫  In reply to a blocked post.");
                                        break;

                                    case PostView postView:
                                        Console.WriteLine($"  ⤷ In reply to {postView.Uri}");
                                        if (!string.IsNullOrEmpty(postView.Record.Text))
                                        {
                                            Console.WriteLine($"    {postView.Record.Text}");
                                        }

                                        string replyPostLabels = GetLabels(postView);
                                        if (!string.IsNullOrEmpty(replyPostLabels))
                                        {
                                            Console.WriteLine($"    {replyPostLabels}");
                                        }

                                        Console.WriteLine($"    From {@postView.Author} {GetLabels(postView.Author)}");
                                        Console.WriteLine($"    Posted at: {postView.Record.CreatedAt.ToLocalTime():G}");
                                        break;

                                    default:
                                        Console.WriteLine("  ⤷ In reply to something I don't understand how to render.");
                                        break;
                                }
                            }
                            Console.WriteLine();
                        }

                        page++;

                        if (!cancellationToken.IsCancellationRequested && !string.IsNullOrEmpty(timelineResult.Result.Cursor))
                        {
                            timelineResult = await agent.GetTimeline(
                                limit: pageSize,
                                cursor: timelineResult.Result.Cursor,
                                subscribedLabelers: preferences.SubscribedLabelers,
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                        }
                    } while (!cancellationToken.IsCancellationRequested &&
                             postCounter <= maximumNumberOfEntries &&
                             timelineResult.Succeeded && !string.IsNullOrEmpty(timelineResult.Result.Cursor));
                }
            }
        }

        static string GetLabels(ProfileViewBasic author)
        {
            if (author is null)
            {
                return string.Empty;
            }

            StringBuilder labelBuilder = new();

            foreach (Label label in author.Labels)
            {
                labelBuilder.Append(CultureInfo.InvariantCulture, $"[{label.Value}] ");
            }

            if (labelBuilder.Length != 0)
            {
                labelBuilder.Length--;
            }

            return labelBuilder.ToString();
        }

        static string GetLabels(PostView postView)
        {
            if (postView is null)
            {
                return string.Empty;
            }

            StringBuilder labelBuilder = new();

            foreach (Label label in postView.Labels)
            {
                labelBuilder.Append(CultureInfo.InvariantCulture, $"[{label.Value}] ");
            }

            if (labelBuilder.Length != 0)
            {
                labelBuilder.Length--;
            }

            return labelBuilder.ToString();
        }
    }
}
