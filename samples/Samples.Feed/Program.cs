// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Actor;

using Samples.Common;

namespace Samples.Feed
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
           // proxyUri = new Uri("http://localhost:8866");

            // Uncomment the next line to route all requests  through Fiddler Classic
            // proxyUri = new Uri("http://localhost:8888");

            // Get an HttpClient configured to use a proxy, if proxyUri is not null.
            using (HttpClient? httpClient = Helpers.CreateOptionalHttpClient(proxyUri))

            // Change the log level in the ConfigureConsoleLogging() to enable logging
            using (ILoggerFactory? loggerFactory = Helpers.ConfigureConsoleLogging(LogLevel.Error))

            // Create a new BlueSkyAgent
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

                // This is the AT Uri for the Discover feed.
                AtUri feedUri = new("at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.feed.generator/whats-hot");

                AtProtoHttpResult<FeedGenerator> feedGenerator = await agent.GetFeedGenerator(feedUri, cancellationToken : cancellationToken);

                Console.WriteLine($"{feedGenerator.Result!.View.DisplayName} - {feedGenerator.Result.View.Description}");
                Console.WriteLine($"By {feedGenerator.Result.View.Creator.DisplayName} @{feedGenerator.Result.View.Creator.Handle}");

                const int pageSize = 25;
                const int maxPagesToIterate = 10;
                int page = 1;

                var getFeedResult = await agent.GetFeed(
                    feedUri,
                    limit: pageSize,
                    subscribedLabelers: preferences.SubscribedLabelers,
                    cancellationToken: cancellationToken);

                if (getFeedResult.Succeeded && getFeedResult.Result.Count != 0 && !cancellationToken.IsCancellationRequested)
                {
                    do
                    {
                        Console.WriteLine($"\n📄 Page {page++}\n");

                        foreach (var feedViewPost in getFeedResult.Result)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }

                            Console.WriteLine($"{feedViewPost.Post.Record.Text ?? "\u001b[3mNo post text\u001b[23m"}");
                            Console.WriteLine($"  From {feedViewPost.Post.Author}");
                            PrintLabels(feedViewPost.Post.Author);

                            Console.WriteLine($"  Posted at: {feedViewPost.Post.Record.CreatedAt.ToLocalTime():G}");
                            Console.WriteLine($"  {feedViewPost.Post.LikeCount} like{(feedViewPost.Post.LikeCount != 1 ? "s" : "")} {feedViewPost.Post.RepostCount} repost{(feedViewPost.Post.RepostCount != 1 ? "s" : "")}.");
                            Console.WriteLine($"  AtUri : {feedViewPost.Post.Uri}");
                            Console.WriteLine($"  Cid   : {feedViewPost.Post.Cid}");
                            Console.WriteLine();
                        }

                        if (!cancellationToken.IsCancellationRequested && !string.IsNullOrEmpty(getFeedResult.Result.Cursor))
                        {
                            await agent.GetFeed(
                            feedUri,
                            limit: pageSize,
                            cursor: getFeedResult.Result.Cursor,
                            subscribedLabelers: preferences.SubscribedLabelers,
                            cancellationToken: cancellationToken);
                        }

                    } while (!cancellationToken.IsCancellationRequested &&
                             getFeedResult.Succeeded &&
                             !string.IsNullOrEmpty(getFeedResult.Result.Cursor) &&
                             page < maxPagesToIterate) ;
                }
            }
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
                Console.WriteLine($"  {labelsAsString}");
            }

            return;
        }
    }
}
