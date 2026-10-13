// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.BotSdk.DataProviders;

namespace idunno.Bluesky.BotSdk;

// The reader and sender wait here until OwnershipWorker gives this process permission to work.
// Losing permission cancels their current operations. This is a quick local stop signal;
// database operations still check the permission stored by the chosen provider before changing anything.
/// <summary>Signals local permission to work and cancels operations when the database lease is lost.</summary>
internal sealed class Ownership : IDisposable
{
    private readonly object _gate = new();
    private readonly List<CancellationTokenSource> _retiredLost = [];
    private CancellationTokenSource _lost = new();
    private bool _active;
    private bool _disposed;

    /// <summary>Starts a new ownership period with a fresh cancellation token.</summary>
    public void Activate()
    {
        lock (_gate)
        {
            if (!_active)
            {
                // Cancellation is permanent for a token. A new ownership period needs a new
                // token; previously running operations must keep their cancelled old token.
                _retiredLost.Add(_lost);
                _lost = new();
                _active = true;
            }
        }
    }

    /// <summary>Stops admission and cancels all operations from the current ownership period.</summary>
    public void Suspend()
    {
        lock (_gate)
        {
            _active = false;
            _lost.Cancel();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lost.Dispose();
            foreach (CancellationTokenSource retired in _retiredLost)
            {
                retired.Dispose();
            }

            _retiredLost.Clear();
        }
    }

    /// <summary>Waits in healthy standby until ownership is available or the host stops.</summary>
    /// <param name="worker">The worker requesting permission.</param>
    /// <param name="health">The deadline tracker used while waiting.</param>
    /// <param name="stoppingToken">The host shutdown token.</param>
    /// <returns>The token cancelled when this ownership period ends.</returns>
    public async Task<CancellationToken> WaitAsync(
        string worker,
        WorkerHealth health,
        CancellationToken stoppingToken)
    {
        while (true)
        {
            stoppingToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_active)
                {
                    return _lost.Token;
                }
            }

            health.Set(worker, "waiting for ownership", TimeSpan.FromSeconds(15), "database/ownership");
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
        }
    }
}

// Prepare the database and coordinate which running copy of the bot may read and send.
// A deployment can briefly run both the old and new copies, even with instance_count=1.
// Permission is stored in the chosen database with an expiry time (a "lease"), which this worker
// keeps renewing. If the active process crashes, its permission expires and another can take over.
// Until then, other copies wait rather than processing the same stream and actions.
/// <summary>Initializes storage and renews the lease that selects the active process during deployment overlap.</summary>
/// <param name="database">The chosen storage provider and lease authority.</param>
/// <param name="ownership">The local permission signal shared with the reader and sender.</param>
/// <param name="health">The in-memory deadline tracker.</param>
/// <param name="logger">The ownership diagnostic logger.</param>
/// <param name="time">The clock used to wait for the deployment drain period.</param>
internal sealed class OwnershipWorker(
    IDatabaseProvider database,
    Ownership ownership,
    WorkerHealth health,
    ILogger<OwnershipWorker> logger,
    TimeProvider time) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool initialized = false;
        DateTimeOffset? priorReady = null;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                health.Set("ownership", "checking database ownership", TimeSpan.FromSeconds(45));

                try
                {
                    if (!initialized)
                    {
                        await database.InitializeAsync(stoppingToken).ConfigureAwait(false);
                        initialized = true;
                    }

                    DateTimeOffset? ready = await database.AcquireOrRenewAsync(stoppingToken).ConfigureAwait(false);

                    if (ready is null)
                    {
                        ownership.Suspend();
                        health.Set("ownership", "standby", TimeSpan.FromSeconds(20));
                    }
                    else
                    {
                        if (ready != priorReady)
                        {
                            BotLog.Acquired(logger, database.Owner, ready.Value);
                            priorReady = ready;
                        }

                        if (ready <= time.GetUtcNow())
                        {
                            ownership.Activate();
                        }
                        else
                        {
                            ownership.Suspend();
                        }

                        health.Set("ownership", ready > time.GetUtcNow() ? "deployment drain" : "lease renewed", TimeSpan.FromSeconds(25));
                    }
                }
                catch (Exception exception) when (database.IsTransientDatabaseException(exception) || exception is TimeoutException)
                {
                    // If we cannot prove ownership, stop admitting work immediately. Do not
                    // keep sending based on a lease that might expire while the database is down.
                    ownership.Suspend();
                    health.Set("ownership", "database recovery", TimeSpan.FromSeconds(25), "database");
                    BotLog.Recovering(logger, "ownership", "database", time.GetUtcNow().AddSeconds(10), exception);
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            BotLog.Stopped(logger, "ownership");
        }
        catch (Exception exception)
        {
            health.Fail("ownership", exception.Message);
            BotLog.Failed(logger, "ownership", exception.Message, exception);
            throw;
        }
        finally
        {
            // Release is best-effort with its own deadline. If it cannot reach storage,
            // lease expiry still allows a replacement to take over after the safety drain.
            ownership.Suspend();

            using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(5));

            try
            {
                await database.ReleaseAsync(cleanup.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (database.IsDatabaseException(exception) ||
                exception is OperationCanceledException or TimeoutException)
            {
                BotLog.Recovering(logger, "ownership", "lease release; the lease will expire", time.GetUtcNow().AddSeconds(90));
            }
        }
    }
}
