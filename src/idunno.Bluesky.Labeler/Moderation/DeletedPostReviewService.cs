// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;

using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Confirms deletion at the authoritative PDS before asking the narrow host-only store path to close review work.</summary>
internal sealed partial class DeletedPostReviewService(
    ILabelStore store,
    HttpClient httpClient,
    ILoggerFactory loggerFactory,
    TimeProvider clock)
{
    /// <summary>Checks one bounded persisted batch; missing AppView previews and generic HTTP errors never count as deletion.</summary>
    internal async Task<LabelServiceResult<LabelDeletedPostBatch>> RunBatchAsync(CancellationToken token)
    {
        if (store is not ILabelDeletedPostStore reviews)
        {
            return LabelServiceResult<LabelDeletedPostBatch>.Failure(new(
                LabelServiceErrorCode.TargetNotAllowed, "This provider does not support deleted-post review."));
        }

        LabelServiceResult<LabelDeletedPostBatch> batch = await reviews.ReadDeletedPostBatchAsync(token).ConfigureAwait(false);
        if (!batch.Succeeded)
        {
            return batch;
        }

        LabelTargetResolver resolver = new(httpClient, loggerFactory);
        ILogger logger = loggerFactory.CreateLogger<DeletedPostReviewService>();
        foreach (LabelDeletedPostCandidate candidate in batch.Result.Entries)
        {
            bool deleted = false;
            using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(20), clock);
            using CancellationTokenSource lookupCancellation = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
            try
            {
                LabelServiceResult<LabelTarget> lookup = await resolver.ResolveAsync(
                    LabelTarget.Record(candidate.Uri), lookupCancellation.Token).ConfigureAwait(false);
                if (!lookup.Succeeded)
                {
                    deleted = lookup.Error.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound &&
                        lookup.Error.AtErrorDetail?.Error == "RecordNotFound";
                    if (!deleted)
                    {
                        CheckUnavailable(logger, candidate.CaseId, lookup.Error.Message);
                    }
                }
            }
            catch (JsonException exception)
            {
                CheckUnavailable(logger, candidate.CaseId, "The PDS response was invalid: " + exception.Message);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested && deadline.IsCancellationRequested)
            {
                CheckUnavailable(logger, candidate.CaseId, "The authoritative PDS check timed out.");
            }

            LabelServiceResult<LabelDeletedPostCheck> completed = await reviews.CompleteDeletedPostCheckAsync(
                candidate: candidate, observedAt: batch.Result.ObservedAt, deleted: deleted, cancellationToken: token).ConfigureAwait(false);
            if (!completed.Succeeded)
            {
                CheckUnavailable(logger, candidate.CaseId, completed.Error.Message);
            }
        }

        return batch;
    }

    /// <summary>Leaves unresolved work visible and surfaces inconclusive checks without fabricating a deletion outcome.</summary>
    [LoggerMessage(EventId = 7302, Level = LogLevel.Warning, Message = "Deleted-post review check for case {CaseId} was not completed: {Reason}")]
    private static partial void CheckUnavailable(ILogger logger, long caseId, string reason);
}
