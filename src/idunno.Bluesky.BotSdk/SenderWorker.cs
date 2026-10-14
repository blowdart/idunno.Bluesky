// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Security.Cryptography;

using idunno.Bluesky.BotSdk.DataProviders;

namespace idunno.Bluesky.BotSdk;

// ReaderWorker has already saved complete action intents in the chosen database.
// This worker can pause for rate limits without stopping the reader. It executes one saved
// action at a time (or reconciles an uncertain attempt) and saves the outcome.
// Unfinished actions stay in the database for the next run if this process stops.
// Like the reader, we wait for OwnershipWorker to give this process permission to work.
/// <summary>Executes saved actions independently of ingestion and retains uncertain outcomes for recovery.</summary>
/// <param name="database">The chosen durable storage provider.</param>
/// <param name="ownership">The local signal that this process holds the database lease.</param>
/// <param name="options">The validated runtime configuration.</param>
/// <param name="dispatcher">The action dispatcher and reconciliation boundary.</param>
/// <param name="health">The in-memory worker deadline tracker.</param>
/// <param name="httpClientFactory">The factory using the SDK's protected HTTP transport.</param>
/// <param name="vault">The encryption helper for saved refresh credentials.</param>
/// <param name="loggerFactory">The factory for SDK and session diagnostics.</param>
/// <param name="logger">The sender diagnostic logger.</param>
/// <param name="time">The clock used for retry scheduling and worker deadlines.</param>
internal sealed class SenderWorker(
    IDatabaseProvider database,
    Ownership ownership,
    BotOptions options,
    ActionDispatcher dispatcher,
    WorkerHealth health,
    BotHttpClientFactory httpClientFactory,
    CredentialVault vault,
    ILoggerFactory loggerFactory,
    ILogger<SenderWorker> logger,
    TimeProvider time) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Keep one agent for the worker's lifetime, not one login per action. Manual refresh avoids
        // a background token exchange racing the sender or running while this process is standby.
        using BlueskyAgent agent = new(
    httpClientFactory,
    new BlueskyAgentOptions
    {
        LoggerFactory = loggerFactory,
        EnableBackgroundTokenRefresh = false
    });
        using BlueskySession session = new(
            agent,
            database,
            vault,
            options,
            loggerFactory.CreateLogger<BlueskySession>());

        IBotActionRemote remote = new BlueskyActionRemote(
            session,
            options,
            httpClientFactory);
        DateTimeOffset nextPrune = time.GetUtcNow().AddHours(1);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                CancellationToken leaseToken = await ownership.WaitAsync("sender", health, stoppingToken).ConfigureAwait(false);
                using CancellationTokenSource active = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, leaseToken);
                BotAction? work = null;

                try
                {
                    health.Set("sender", "selecting durable work", TimeSpan.FromSeconds(60));
                    await database.LoadStateAsync(active.Token).ConfigureAwait(false);

                    if (time.GetUtcNow() >= nextPrune)
                    {
                        await database.PruneAsync(active.Token).ConfigureAwait(false);
                        nextPrune = time.GetUtcNow().AddHours(1);
                    }

                    work = await database.NextWorkAsync(active.Token).ConfigureAwait(false);

                    if (work is not null)
                    {
                        health.Set("sender", "authenticating / sending / reconciling", TimeSpan.FromMinutes(3));

                        if (!options.DeveloperMode)
                        {
                            await session.AuthenticateAsync(active.Token).ConfigureAwait(false);
                        }

                        await dispatcher.SendAsync(work, options.DeveloperMode ? null : remote, active.Token).ConfigureAwait(false);
                    }

                    await WaitAsync("idle / scheduled work", TimeSpan.FromSeconds(2), null, stoppingToken).ConfigureAwait(false);
                }
                catch (RateWaitException exception)
                {
                    // Persist long waits on the item instead of holding a network operation open.
                    // The reader keeps ingesting and this schedule survives a process restart.
                    if (work is not null)
                    {
                        await ScheduleRetryAsync(work, exception.Until, "durable rate budget or server cooldown", active.Token).ConfigureAwait(false);
                    }

                    await WaitAsync("rate-limit wait", TimeSpan.FromSeconds(5), null, stoppingToken).ConfigureAwait(false);
                }
                catch (RemoteFailureException exception) when (exception.Retryable && work is not null)
                {
                    session.RefreshRequired |= exception.Status == HttpStatusCode.Unauthorized;
                    await ScheduleRetryAsync(work, RetryAt(work.Attempts), exception.Message, active.Token).ConfigureAwait(false);
                    await WaitAsync("retry wait", TimeSpan.FromSeconds(2), "PDS/AppView", stoppingToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is HttpRequestException or TimeoutException ||
                    (exception is OperationCanceledException && !stoppingToken.IsCancellationRequested && !leaseToken.IsCancellationRequested))
                {
                    // Do not label a timeout "not sent": the server may have accepted the write.
                    // MarkAttemptAsync has already left it uncertain if the create was admitted.
                    BotLog.Recovering(logger, "sender", "remote transport", time.GetUtcNow().AddSeconds(10), exception);

                    if (work is not null)
                    {
                        await ScheduleRetryAsync(work, RetryAt(work.Attempts), "network timeout or unknown remote outcome", active.Token).ConfigureAwait(false);
                    }

                    await WaitAsync("remote recovery", TimeSpan.FromSeconds(10), "PDS", stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (leaseToken.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
                {
                    await WaitAsync("ownership recovery", TimeSpan.FromSeconds(2), "database/ownership", stoppingToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is OwnershipLostException ||
                    database.IsTransientDatabaseException(exception))
                {
                    BotLog.Recovering(logger, "sender", "database/ownership", time.GetUtcNow().AddSeconds(10), exception);
                    await WaitAsync("database recovery", TimeSpan.FromSeconds(10), "database", stoppingToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            BotLog.Stopped(logger, "sender");
        }
        catch (Exception exception)
        {
            // Invariant failures and unexpected errors are not ordinary dependency outages.
            // Fail liveness and let the host stop rather than silently abandoning the sender.
            health.Fail("sender");
            BotLog.Failed(logger, "sender", exception.Message, exception);
            throw;
        }
    }

    /// <summary>Saves the next attempt time without erasing uncertainty when retry persistence is unavailable.</summary>
    /// <param name="work">The existing durable intent, including its original key and payload.</param>
    /// <param name="retryAt">The earliest time another attempt may start.</param>
    /// <param name="reason">The diagnostic retry reason.</param>
    /// <param name="cancellationToken">The active ownership/shutdown cancellation token.</param>
    private async Task ScheduleRetryAsync(
        BotAction work,
        DateTimeOffset retryAt,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            await database.RetryAsync(work, retryAt, reason, cancellationToken).ConfigureAwait(false);
            BotLog.Retrying(logger, work.SourceUri.ToString(), work.Attempts, retryAt, reason);
        }
        catch (Exception exception) when (exception is OwnershipLostException or OperationCanceledException ||
            database.IsTransientDatabaseException(exception))
        {
            // A failed retry update does not delete the item or revert its uncertain status.
            // Leave recovery to the next owner/iteration and explicitly report the storage failure.
            BotLog.Recovering(
                logger,
                "sender",
                "retry persistence unavailable; durable pending/uncertain item retained",
                time.GetUtcNow().AddSeconds(10));
        }
    }

    /// <summary>Chooses a capped exponential retry delay with jitter to avoid synchronized reconnects.</summary>
    /// <param name="attempts">The saved number of attempts already admitted.</param>
    /// <returns>The next attempt's UTC deadline.</returns>
    private DateTimeOffset RetryAt(int attempts) =>

        // Cap exponential backoff and add jitter so transient outages do not cause synchronized retries.
        time.GetUtcNow().AddSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Min(attempts, 7))) + RandomNumberGenerator.GetInt32(0, 10));

    /// <summary>Reports a bounded sender wait so pacing and recovery do not look like a stalled worker.</summary>
    /// <param name="phase">The worker activity displayed in health diagnostics.</param>
    /// <param name="delay">The wait duration.</param>
    /// <param name="dependency">The recovering dependency, or <see langword="null"/> for a healthy idle wait.</param>
    /// <param name="cancellationToken">The host shutdown token.</param>
    private async Task WaitAsync(
        string phase,
        TimeSpan delay,
        string? dependency,
        CancellationToken cancellationToken)
    {
        health.Set("sender", phase, delay + TimeSpan.FromSeconds(15), dependency);
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
    }
}
