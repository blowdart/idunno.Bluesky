// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Coravel.Invocable;

using idunno.AtProto;
using idunno.Bluesky;

namespace Samples.Bot.Watcher
{
    internal interface IFollowersWatcher
    {
        public Task<List<Did>> GetFollowers(CancellationToken cancellationToken);

        public event EventHandler<FollowersChangedEventArgs>? FollowersChanged;
    }

    internal sealed class FollowersWatcher : IInvocable, IFollowersWatcher
    {
        private BotOptions _options;

        private readonly ILoggerFactory _loggerFactory;

        private readonly List<Did> _lastCheckedFollowers = [];

        public FollowersWatcher(IOptionsMonitor<BotOptions> options, ILoggerFactory loggerFactory)
        {
            _options = options.CurrentValue;
            _loggerFactory = loggerFactory;

            options.OnChange(options =>
            {
                _options = options;
            });
        }

        public async Task<List<Did>> GetFollowers(CancellationToken cancellationToken = default)
        {
            using (var agent = new BlueskyAgent(
                options: new BlueskyAgentOptions()
                {
                    LoggerFactory = _loggerFactory,
                    EnableBackgroundTokenRefresh = false
                }))
            {
                var loginResult = await agent.Login(_options.Handle, _options.AppPassword, cancellationToken: cancellationToken);

                if (loginResult.Succeeded)
                {
                    Console.WriteLine("Rechecking followers.");
                    List<Did> currentFollowers = [];

                    var getFollowersResult = await agent.GetFollowers(cancellationToken: cancellationToken);

                    if (getFollowersResult.Succeeded)
                    {
                        do
                        {
                            foreach (var follower in getFollowersResult.Result)
                            {
                                if (_options.ExcludeDids is null || !Array.Exists(_options.ExcludeDids, element => element == follower.Did))
                                {
                                    currentFollowers.Add(follower.Did);
                                }

                                if (getFollowersResult.Succeeded && !string.IsNullOrWhiteSpace(getFollowersResult.Result.Cursor))
                                {
                                    getFollowersResult = await agent.GetFollowers(cursor: getFollowersResult.Result.Cursor, cancellationToken: cancellationToken);
                                }
                            }
                        } while (getFollowersResult.Succeeded && !string.IsNullOrWhiteSpace(getFollowersResult.Result.Cursor));

                        return currentFollowers;
                    }
                }
            }

            return [];
        }

        public async Task Invoke()
        {
            var currentFollowers = await GetFollowers();

            if (!Enumerable.SequenceEqual(_lastCheckedFollowers, currentFollowers))
            {
                OnFollowersChanged(new FollowersChangedEventArgs() { Followers = new List<Did>(currentFollowers).AsReadOnly() });
            }
        }

        public event EventHandler<FollowersChangedEventArgs>? FollowersChanged;

        void OnFollowersChanged(FollowersChangedEventArgs e)
        {
            if (FollowersChanged is not null)
            {
                Console.WriteLine("Followers updated.");
            }
            FollowersChanged?.Invoke(this, e);
        }
    }

    internal sealed class FollowersChangedEventArgs: EventArgs
    {
        public required IReadOnlyList<Did> Followers { get; init; }
    }
}
