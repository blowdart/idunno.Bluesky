// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Resumes durable policy cleanup independently of browser lifetime and remote administration enablement.</summary>
internal sealed partial class PolicyWithdrawalWorker(
    AdministrationDispatcher dispatcher,
    TimeProvider clock,
    ILogger<PolicyWithdrawalWorker> logger) : BackgroundService
{
    /// <summary>Attempts one bounded batch every five seconds; shared provider checkpoints serialize multiple hosts.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            LabelServiceError? error = await dispatcher.AdvancePolicyWithdrawalAsync(stoppingToken).ConfigureAwait(false);
            if (error is not null)
            {
                WithdrawalPaused(logger, error.Message);
            }
            await Task.Delay(TimeSpan.FromSeconds(5), clock, stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>Surfaces blocked cleanup without treating it as completed publication.</summary>
    [LoggerMessage(EventId = 7304, Level = LogLevel.Warning, Message = "Policy label withdrawal could not advance: {Reason}")]
    private static partial void WithdrawalPaused(ILogger logger, string reason);
}
