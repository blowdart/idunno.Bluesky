// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Labeler.UI;

/// <summary>Refreshes bounded public-identity lookup batches outside storage transactions, retaining progress across restarts.</summary>
internal sealed partial class HandleSearchIndexer(
    ILabelStore store,
    IDidHandleResolver resolver,
    TimeProvider clock,
    ILogger<HandleSearchIndexer> logger) : BackgroundService
{
    /// <summary>Refreshes expired observations every fifteen seconds without interpreting lookup failures as verified handles.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (store is not ILabelSearchStore index)
        {
            IndexUnavailable(logger, "This provider does not support a handle search index.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            LabelServiceResult<LabelHandleIndexBatch> pending = await index.ReadHandleIndexBatchAsync(stoppingToken).ConfigureAwait(false);
            if (pending.Succeeded)
            {
                LabelHandleIndexEntry[] entries = pending.Result.Entries.ToArray();
                await Parallel.ForEachAsync(
                    source: Enumerable.Range(0, entries.Length),
                    parallelOptions: new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = stoppingToken },
                    body: async (position, token) =>
                    {
                        Handle handle = await resolver.ResolveHandleAsync(new Did(entries[position].Did), token).ConfigureAwait(false);
                        entries[position] = entries[position] with { Handle = handle == Handle.Invalid ? null : handle.Value };
                    }).ConfigureAwait(false);
                if (entries.Length > 0)
                {
                    LabelServiceResult<LabelHandleIndexBatch> saved = await index.SaveHandleIndexBatchAsync(
                        new(pending.Result.ObservedAt, entries), stoppingToken).ConfigureAwait(false);
                    if (!saved.Succeeded)
                    {
                        IndexUnavailable(logger, saved.Error.Message);
                    }
                }
            }
            else
            {
                IndexUnavailable(logger, pending.Error.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(15), clock, stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>Surfaces storage/index failures instead of presenting the cache as complete.</summary>
    [LoggerMessage(EventId = 7301, Level = LogLevel.Warning, Message = "Xenon handle search index update failed: {Reason}")]
    private static partial void IndexUnavailable(ILogger logger, string reason);
}
