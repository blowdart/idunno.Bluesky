// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Bluesky.Feed;
using System.Net;

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
    Console.WriteLine("Usage: Timeline <blueskyUsername> <blueskyPassword> (<proxyuri>)");
    Console.WriteLine();
    Console.WriteLine("Or use the _BlueskyUserName and _BlueskyPassword environment variables to provide credentials and use Timeline (<proxyuri>)");
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
            HttpResult<Timeline> timelineResult = await agent.GetTimeline();

            if (timelineResult.Succeeded && timelineResult.Result is not null)
            {
                Console.WriteLine($"Timeline: {timelineResult.Result.Feed.Count} post{(timelineResult.Result.Feed.Count != 1 ? "s" : "")}.\n");

                foreach (FeedView feedView in timelineResult.Result.Feed)
                {
                    string? author = feedView.Post.Author.Handle.ToString();
                    if (feedView.Post.Author.DisplayName is not null && feedView.Post.Author.DisplayName.Length > 0)
                    {
                        author = feedView.Post.Author.DisplayName;
                    }

                    Console.WriteLine($"{feedView.Post.Record.Text} - {author} {feedView.Post.Author.Handle}/{feedView.Post.Author.Did}.");
                    Console.WriteLine($"  Created At: {feedView.Post.Record.CreatedAt}");
                    Console.WriteLine($"  {feedView.Post.LikeCount} like{(feedView.Post.LikeCount != 1 ? "s" : "")} {feedView.Post.RepostCount} repost{(feedView.Post.RepostCount != 1 ? "s" : "")}.");
                    Console.WriteLine($"  AtUri={feedView.Post.Uri}\n  Cid={feedView.Post.Cid}.\n\n");
                }
            }
            else if (timelineResult.Succeeded && timelineResult.Result is null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("getTimelineResult succeeded but a session was not created.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"getTimelineResult failed: {timelineResult.StatusCode}");
                if (timelineResult.Error is not null)
                {
                    Console.WriteLine($"\t{timelineResult.Error.Error} {timelineResult.Error.Message}");
                }
            }
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
