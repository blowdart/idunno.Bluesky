// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Coravel;

using idunno.AtProto;
using idunno.AtProto.Jetstream;
using idunno.AtProto.Jetstream.Events;
using idunno.Bluesky;

namespace Samples.Bot.Watcher
{
    public sealed class Program
    {
        static int Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            WebApplicationBuilder builder = WebApplication.CreateBuilder();

            builder.Services
                .AddOptions<BotOptions>()
                .Bind(builder.Configuration.GetSection(BotOptions.ConfigurationSectionName));

            builder.Services
                .AddSingleton<IFaultWatcher, FaultWatcher>()
                .AddSingleton<IFollowersWatcher, FollowersWatcher>()
                .AddSingleton<IValidateOptions<BotOptions>, ValidateBotOptions>();

            builder.Services
                .AddScheduler()
                .AddSingleton<FollowersWatcher>()
                .AddTransient<HeartBeat>();

            builder.Services
                .AddHealthChecks()
                .AddCheck<WebSocketHealthCheck>("WebSocketFaults");

            builder.Services
                .AddHostedService<WatcherBot>();

            var app = builder.Build();

            app.Services.UseScheduler(s =>
            {
                s.Schedule<FollowersWatcher>()
                    .EveryFiveMinutes();
                s.Schedule<HeartBeat>()
                    .EveryMinute()
                    .RunOnceAtStart();
            });

            app.MapHealthChecks("/health");

            app.Run();

            return 0;
        }

        internal sealed class WatcherBot : BackgroundService, IDisposable
        {
            static readonly Meter s_meter = new("idunno.WatcherBot");
            static readonly Counter<int> s_responsesSent = s_meter.CreateCounter<int>(
                name: "idunno.watcherbot.responses_sent",
                description: "Number of bot replies sent.");

            private readonly ILoggerFactory _loggerFactory;

            private readonly IFaultWatcher _faultWatcher;

            private readonly IFollowersWatcher _followersWatcher;

            private readonly AtProtoJetstream _jetStream;

            private readonly Dictionary<Did, int> _lastFact = [];

            private BotOptions _options;

            public WatcherBot(
                IOptionsMonitor<BotOptions> options,
                IFaultWatcher faultWatcher,
                IFollowersWatcher followersWatcher,
                ILoggerFactory loggerFactory,
                IMeterFactory meterFactory)
            {
                _options = options.CurrentValue;
                _loggerFactory = loggerFactory;

                Console.WriteLine($"Authenticating as {options.CurrentValue.Handle}");

                // As we can't bind config to custom types, build a Did list from settings.
                List<Did>? dids = null;
                if (_options.WatchDids is not null && _options.WatchDids.Length != 0)
                {
                    Console.WriteLine("Watching ");
                    dids = [];
                    foreach (string did in _options.WatchDids)
                    {
                        if (_options.ExcludeDids is null)
                        {
                            dids.Add(new Did(did));
                        }
                        else if (!_options.ExcludeDids.Contains(did))
                        {
                            dids.Add(new Did(did));
                        }

                        Console.WriteLine($"\t{did}");
                    }
                }

                Console.WriteLine("Watching for");
                foreach (string word in _options.WatchWords)
                {
                    Console.WriteLine($"\t{word}");
                }

                Console.WriteLine($"{_options.Responses.Length} possible responses.");

                _faultWatcher = faultWatcher;
                _jetStream = new AtProtoJetstream(
                    dids: dids,
                    collections: ["app.bsky.feed.post"],
                    options: new JetstreamOptions()
                    {
                        UseCompression = true,
                        LoggerFactory = _loggerFactory,
                        MeterFactory = meterFactory
                    });

                _jetStream.RecordReceived += OnRecordReceived;
                _jetStream.ConnectionStateChanged += OnConnectionStateChanged;
                _jetStream.FaultRaised += OnFault;

                options.OnChange(options =>
                {
                    List<Did>? dids = null;

                    if (options.WatchDids is not null)
                    {
                        dids = [];
                        foreach (string did in options.WatchDids)
                        {
                            if (_options.ExcludeDids is null)
                            {
                                dids.Add(new Did(did));
                            }
                            else if (!_options.ExcludeDids.Contains(did))
                            {
                                dids.Add(new Did(did));
                            }
                        }

                        Console.WriteLine("Watched Dids updated.");
                        _jetStream.DidFilter = dids;

                        Console.WriteLine("Watching ");
                        dids = [];
                        foreach (string did in options.WatchDids)
                        {
                            dids.Add(new Did(did));
                            Console.WriteLine($"\t{did}");
                        }
                    }
                    else
                    {
                        if (_options.WatchDids is not null)
                        {
                            _jetStream.DidFilter = [];
                            Console.WriteLine("Watched Dids cleared.");
                        }
                    }

                    _options = options;
                });

                _followersWatcher = followersWatcher;
                _followersWatcher.FollowersChanged += OnFollowersChanged;
            }

            protected override async Task ExecuteAsync(CancellationToken stoppingToken)
            {
                // Now we're in an async context we can add followers before starting.

                var followers = await _followersWatcher.GetFollowers(cancellationToken: stoppingToken);
                List<Did> watchedDids = [];

                if (_options.WatchDids is not null)
                {
                    foreach (string did in _options.WatchDids)
                    {
                        watchedDids.Add(did);
                    }
                }

                if (followers is not null)
                {
                    Console.WriteLine("Adding followers to watched DIDs.");
                    foreach(var did in followers)
                    {
                        if (_options.ExcludeDids is null)
                        {
                            watchedDids.Add(did);
                        }
                        else if (!_options.ExcludeDids.Contains(did.ToString()))
                        {
                            watchedDids.Add(did);
                        }
                    }
                }

                if (watchedDids.Count != 0)
                {
                    _jetStream.DidFilter = watchedDids;
                }

                Console.WriteLine("Connecting to jetstream.");
                await _jetStream.ConnectAsync(cancellationToken: stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                }

                Console.WriteLine("Closing jetstream.");
                await _jetStream.CloseAsync();
            }

            public override async Task StopAsync(CancellationToken cancellationToken)
            {
                await _jetStream.CloseAsync();
                await base.StopAsync(cancellationToken);
            }

            private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
            {
                if (e.State == System.Net.WebSockets.WebSocketState.Aborted)
                {
                    Console.WriteLine($"Connection state changed to {e.State}");
                }
            }

            private void OnFault(object? sender, FaultRaisedEventArgs e)
            {
                Console.WriteLine($"fault {e.Fault} raised.");
                _faultWatcher.LogFault(e.Fault);
            }

            private void OnFollowersChanged(object? sender, FollowersChangedEventArgs e)
            {
                List<Did> subscribers = [];

                if (_options.WatchDids is not null)
                {
                    foreach (string did in _options.WatchDids)
                    {
                        if (_options.ExcludeDids is null)
                        {
                            subscribers.Add(new Did(did));
                        }
                        else if (!_options.ExcludeDids.Contains(did))
                        {
                            subscribers.Add(new Did(did));
                        }
                    }
                }

                if (e.Followers is not null)
                {
                    subscribers.AddRange(e.Followers);
                }

                _jetStream.DidFilter = subscribers;

                Console.WriteLine("Followers changed");

                Console.WriteLine("Watching ");
                foreach (string did in subscribers)
                {
                    Console.WriteLine($"\t{did}");
                }
            }

            [UnconditionalSuppressMessage(
                "Trimming",
                "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
                Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
            [UnconditionalSuppressMessage("AOT",
                "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
                Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
            private async void OnRecordReceived(object? sender, RecordReceivedEventArgs e)
            {
                switch (e.ParsedEvent)
                {
                    case AtJetstreamCommitEvent commitEvent:
                        if (string.Equals(commitEvent.Commit.Operation, "create", StringComparison.OrdinalIgnoreCase) &&
                            commitEvent.Commit.Record is not null)
                        {
                            // A new record has been created in the monitored collections by a monitored did.
                            // Let's try to convert it to a Bluesky post

                            try
                            {
                                Post? post = JsonSerializer.Deserialize<Post>(
                                    commitEvent.Commit.Record,
                                    BlueskyServer.BlueskyJsonSerializerOptions);

                                if (post is not null && !string.IsNullOrEmpty(post.Text))
                                {
                                    if (post.Text.ContainsAny(_options.WatchWords, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        using (var agent = new BlueskyAgent(
                                            options: new BlueskyAgentOptions()
                                            {
                                                LoggerFactory = _loggerFactory,
                                                EnableBackgroundTokenRefresh = false
                                            }))
                                        {
                                            await agent.Login(_options.Handle, _options.AppPassword);

                                            // Check we haven't been blocked
                                            var getProfileViewResult = await agent.GetProfile(commitEvent.Did).ConfigureAwait(false);

                                            if (getProfileViewResult.Succeeded &&
                                                getProfileViewResult.Result.Viewer is not null &&
                                                !getProfileViewResult.Result.Viewer.BlockedBy &&
                                                getProfileViewResult.Result.Viewer.Blocking is null)
                                            {
                                                var postUri = new AtUri($"at://{commitEvent.Did}/{commitEvent.Commit.Collection}/{commitEvent.Commit.RKey}");

                                                var getPostResult = await agent.GetPost(postUri);

                                                if (getPostResult.Succeeded)
                                                {
                                                    // Pick a random response
                                                    int responseOffset = Random.Shared.Next(0, _options.Responses.Length);

                                                    if (!_lastFact.TryGetValue(commitEvent.Did, out int previousOffset))
                                                    {
                                                        // This is the first time the individual DID has had a reply, so we
                                                        // don't need to worry about checking for dupes.

                                                        _lastFact.Add(commitEvent.Did, responseOffset);
                                                    }
                                                    else
                                                    {
                                                        do
                                                        {
                                                            responseOffset = Random.Shared.Next(0, _options.Responses.Length);
                                                        } while (responseOffset == previousOffset);

                                                        _lastFact[commitEvent.Did] = responseOffset;
                                                    }

                                                    Console.WriteLine($"{commitEvent.Commit.Operation} {commitEvent.Commit.Rev} {post.Text}");
                                                    Console.WriteLine($"↳ {_options.Responses[responseOffset]}");

                                                    await agent.ReplyTo(
                                                        parent: getPostResult.Result.StrongReference,
                                                        text: _options.Responses[responseOffset]);
                                                    s_responsesSent.Add(1);
                                                }
                                                else
                                                {
                                                    Console.WriteLine("GetPost failed for {postUri}");
                                                }
                                            }
                                            else
                                            {
                                                if (!getProfileViewResult.Succeeded)
                                                {
                                                    Console.WriteLine($"GetProfile for {commitEvent.Did} failed");
                                                }
                                                else
                                                {
                                                    Console.WriteLine($"{commitEvent.Did} has blocked the bot, or been blocked by the bot.");
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch (JsonException)
                            {
                            }
                        }
                        break;

                    default:
                        break;
                }
            }
        }
    }
}

