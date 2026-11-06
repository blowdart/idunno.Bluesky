// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Retries database operations while their host remains available for recovery.</summary>
internal static partial class DatabaseRetry
{
    private static readonly TimeSpan s_startupRetryDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan s_initialDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan s_maximumDelay = TimeSpan.FromSeconds(30);
    private const int MaximumStartupRetries = 5;

    /// <summary>Retries a startup database operation up to five times, with a fixed thirty-second delay.</summary>
    internal static Task<T> ExecuteStartupAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeProvider clock,
        ILogger logger,
        string description,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            operation,
            clock,
            logger,
            description,
            cancellationToken,
            maxRetries: MaximumStartupRetries,
            retryDelay: s_startupRetryDelay);

    /// <summary>Runs a database operation until it succeeds or the host begins shutting down.</summary>
    internal static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeProvider clock,
        ILogger logger,
        string description,
        CancellationToken cancellationToken,
        Action? onRetry = null,
        int? maxRetries = null,
        TimeSpan? retryDelay = null)
    {
        TimeSpan delay = s_initialDelay;
        int retries = 0;
        while (true)
        {
            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (DbException exception) when (!cancellationToken.IsCancellationRequested)
            {
                if (maxRetries is not null && retries >= maxRetries.Value)
                {
                    RetryExhausted(logger, exception, description, retries);
                    throw;
                }

                retries++;
                if (retryDelay is not null)
                {
                    delay = retryDelay.Value;
                }
                RetryScheduled(logger, exception, description, delay);
                onRetry?.Invoke();
                await Task.Delay(delay, clock, cancellationToken).ConfigureAwait(false);
                if (retryDelay is null)
                {
                    delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, s_maximumDelay.Ticks));
                }
            }
        }
    }

    [LoggerMessage(EventId = 7310, Level = LogLevel.Warning, Message = "Database operation {Description} failed; retrying in {Delay}.")]
    private static partial void RetryScheduled(ILogger logger, Exception exception, string description, TimeSpan delay);

    [LoggerMessage(EventId = 7313, Level = LogLevel.Error, Message = "Database operation {Description} remains unavailable after {RetryCount} retries.")]
    private static partial void RetryExhausted(ILogger logger, Exception exception, string description, int retryCount);
}
