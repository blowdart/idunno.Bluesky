// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Security.Cryptography;

using idunno.AtProto.Repo;

namespace Samples.ReactBff;

internal sealed class BffSession(ISessionClient client, DateTimeOffset expiresAt)
{
    internal ISessionClient Client { get; } = client;
    internal DateTimeOffset ExpiresAt { get; } = expiresAt;
    internal SemaphoreSlim Gate { get; } = new(1, 1);
    internal bool Closed { get; set; }
    internal StrongReference? PostReference { get; set; }
    internal CreatedPost? CreatedPost { get; set; }
}

// The library identity stores are DID-keyed. A BFF instead needs independent browser sessions,
// including independent refresh tokens and proof keys when two browsers log in to the same DID.
internal sealed class BffSessions(TimeProvider clock) : BackgroundService
{
    internal static readonly TimeSpan s_lifetime = TimeSpan.FromHours(1);
    internal const int Capacity = 100;

    private readonly ConcurrentDictionary<string, BffSession> _sessions = new(StringComparer.Ordinal);
    private readonly Lock _creationLock = new();

    internal string Add(ISessionClient client)
    {
        lock (_creationLock)
        {
            if (_sessions.Count >= Capacity)
            {
                throw new BffException(StatusCodes.Status503ServiceUnavailable, "The sample session limit has been reached. Try later.");
            }

            string id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            _sessions[id] = new BffSession(client, clock.GetUtcNow().Add(s_lifetime));
            return id;
        }
    }

    internal BffSession? Find(string? id) =>
        id is not null && _sessions.TryGetValue(id, out BffSession? session) ? session : null;

    internal bool IsValid(BffSession session) => !session.Closed && session.ExpiresAt > clock.GetUtcNow();

    // Called with the session gate held; queued requests recheck Closed before using its agent.
    internal void Remove(string id, BffSession session)
    {
        _sessions.TryRemove(new KeyValuePair<string, BffSession>(id, session));
        session.Closed = true;
        session.Client.Dispose();
    }

    internal async Task SweepAsync(CancellationToken cancellationToken)
    {
        foreach ((string id, BffSession session) in _sessions)
        {
            if (session.ExpiresAt > clock.GetUtcNow())
            {
                continue;
            }

            await session.Gate.WaitAsync(cancellationToken);
            try
            {
                if (!session.Closed)
                {
                    Remove(id, session);
                }
            }
            finally
            {
                session.Gate.Release();
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SweepAsync(stoppingToken);
        }
    }

    public override void Dispose()
    {
        foreach (BffSession session in _sessions.Values)
        {
            session.Client.Dispose();
        }

        _sessions.Clear();
        base.Dispose();
    }
}
