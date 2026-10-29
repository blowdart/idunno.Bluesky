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
    ILabelReportStore reports,
    LabelPolicyService policies,
    LabelerPolicyEmailService notifications,
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
                    LabelServiceResult<LabelPolicyState>? verification = await VerifyPolicyAsync(linked.Token, stoppingToken).ConfigureAwait(false);
                    if (verification is { } result && !result.Succeeded)
                    {
                        VerificationFailed(logger, result.Error.Code.ToString());
                        await RecordFailureNotificationAsync(result.Error.Code.ToString(), stoppingToken).ConfigureAwait(false);
                    }
                    else if (verification is { Succeeded: true } verified)
                    {
                        await RecordSuccessNotificationAsync(stoppingToken).ConfigureAwait(false);
                        while (!linked.IsCancellationRequested)
                        {
                            LabelServiceResult<LabelPendingReportBatchResult> batch = await reports.ProcessPendingReportsAsync(
                                expectedPolicyRevision: verified.Result.Revision,
                                limit: 100,
                                cancellationToken: linked.Token).ConfigureAwait(false);
                            if (!batch.Succeeded)
                            {
                                VerificationFailed(logger, "pending-report-" + batch.Error.Code);
                                break;
                            }

                            if (!batch.Result.HasMore)
                            {
                                break;
                            }
                        }
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

    /// <summary>Runs independent policy verification and records only failures from that operation as incidents.</summary>
    private async Task<LabelServiceResult<LabelPolicyState>?> VerifyPolicyAsync(
        CancellationToken verificationToken,
        CancellationToken stoppingToken)
    {
        try
        {
            return await policies.VerifyActivePolicyAsync(verificationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            VerificationFailed(logger, "deadline");
            await RecordFailureNotificationAsync("deadline", stoppingToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception exception) when (exception is DbException or InvalidDataException)
        {
            VerificationFailed(logger, exception.GetType().Name);
            await RecordFailureNotificationAsync(exception.GetType().Name, stoppingToken).ConfigureAwait(false);
            return null;
        }
    }

    /// <summary>Persists a verification incident without allowing a notification-store outage to stop verification.</summary>
    private async Task RecordFailureNotificationAsync(string category, CancellationToken cancellationToken)
    {
        try
        {
            await notifications.VerificationFailedAsync(category, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is DbException or InvalidDataException)
        {
            VerificationFailed(logger, "alert-store-" + exception.GetType().Name);
        }
    }

    /// <summary>Closes an incident without allowing notification persistence to stop report recovery.</summary>
    private async Task RecordSuccessNotificationAsync(CancellationToken cancellationToken)
    {
        try
        {
            await notifications.VerificationSucceededAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is DbException or InvalidDataException)
        {
            VerificationFailed(logger, "alert-store-" + exception.GetType().Name);
        }
    }

    /// <summary>Reports verification failures without extending freshness or logging remote response contents.</summary>
    [LoggerMessage(EventId = 7305, Level = LogLevel.Warning, Message = "Scheduled active policy verification failed ({Category}); the previous verification deadline is unchanged.")]
    private static partial void VerificationFailed(ILogger logger, string category);
}
