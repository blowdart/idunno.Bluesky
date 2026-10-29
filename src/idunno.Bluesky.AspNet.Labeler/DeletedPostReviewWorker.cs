// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Moderation;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Runs restart-resumed public PDS checks independently of browser sessions and CLI enablement.</summary>
internal sealed partial class DeletedPostReviewWorker(
    DeletedPostReviewService reviews,
    TimeProvider clock,
    ILogger<DeletedPostReviewWorker> logger) : BackgroundService
{
    /// <summary>Checks a bounded batch each minute; per-case database timestamps enforce the five-minute recheck interval.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            LabelServiceResult<LabelDeletedPostBatch> result = await reviews.RunBatchAsync(stoppingToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                ReviewUnavailable(logger, result.Error.Message);
            }

            await Task.Delay(TimeSpan.FromMinutes(1), clock, stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>Reports batch failures without changing pending report or appeal state.</summary>
    [LoggerMessage(EventId = 7303, Level = LogLevel.Warning, Message = "Deleted-post review could not read its next batch: {Reason}")]
    private static partial void ReviewUnavailable(ILogger logger, string reason);
}
