// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;

using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Moderation;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Refreshes the active policy independently of traffic without completing pending publication or recovery.</summary>
internal sealed partial class PolicyVerificationWorker(
    ILabelPolicyStore store,
    LabelPolicyService policies,
    TimeProvider clock,
    ILogger<PolicyVerificationWorker> logger) : BackgroundService
{
    /// <summary>Checks at startup and every five minutes, with bounded remote requests.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = TimeSpan.FromMinutes(5);
        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan timeout = TimeSpan.FromTicks(Math.Min(TimeSpan.FromSeconds(30).Ticks, Math.Max(TimeSpan.TicksPerMillisecond, interval.Ticks / 2)));
            using CancellationTokenSource deadline = new(timeout, clock);
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, deadline.Token);
            try
            {
                LabelPolicyState? state = await store.ReadPolicyStateAsync(linked.Token).ConfigureAwait(false);
                if (state?.Phase == LabelPolicyPhase.VerifiedActive)
                {
                    LabelServiceResult<LabelPolicyState> result = await policies.VerifyActivePolicyAsync(linked.Token).ConfigureAwait(false);
                    if (!result.Succeeded)
                    {
                        VerificationFailed(logger, result.Error.Code.ToString());
                    }
                }
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                VerificationFailed(logger, "deadline");
            }
            catch (Exception exception) when (exception is DbException or InvalidDataException)
            {
                VerificationFailed(logger, exception.GetType().Name);
            }
            await Task.Delay(interval, clock, stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>Reports verification failures without extending freshness or logging remote response contents.</summary>
    [LoggerMessage(EventId = 7305, Level = LogLevel.Warning, Message = "Scheduled active policy verification failed ({Category}); the previous verification deadline is unchanged.")]
    private static partial void VerificationFailed(ILogger logger, string category);
}
