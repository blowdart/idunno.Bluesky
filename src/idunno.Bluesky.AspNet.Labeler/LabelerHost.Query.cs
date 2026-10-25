// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;

using idunno.Bluesky.Labeler.Storage;

using Microsoft.AspNetCore.Http;

namespace idunno.Bluesky.AspNet.Labeler;

internal sealed partial class LabelerHost
{
    /// <summary>Serves bounded retained-slot queries with stable pagination and explicit XRPC failures.</summary>
    internal async Task QueryAsync(HttpContext context)
    {
        using CancellationTokenSource deadline = new(options.QueryTimeout, clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, deadline.Token, _lifetime.Token);
        long started = clock.GetTimestamp();
        try
        {
            using IDisposable? admission = await AdmitTrafficAsync(context, linked.Token).ConfigureAwait(false);
            if (admission is null)
            {
                return;
            }

            if (!HttpMethods.IsGet(context.Request.Method))
            {
                context.Response.Headers.Allow = "GET";
                await ErrorAsync(
                    context: context,
                    status: 405,
                    error: "InvalidRequest",
                    message: "Use GET for label queries.").ConfigureAwait(false);
                return;
            }

            if (!_ready)
            {
                await ErrorAsync(
                    context: context,
                    status: 503,
                    error: "ServiceUnavailable",
                    message: "Label publication is not available.").ConfigureAwait(false);
                return;
            }

            LabelQuery query = LabelQuery.Parse(context.Request);
            LabelStoreState state = await store.GetStateAsync(linked.Token).ConfigureAwait(false);
            CheckState(state);
            (long after, long through) = query.Position(state);
            if (BeforeReadAsync is not null)
            {
                await BeforeReadAsync(linked.Token).ConfigureAwait(false);
            }

            LabelDistributionPage page = await store.ReadDistributionAsync(
                streamId: state.StreamId,
                after: after,
                through: through,
                uriPatterns: query.Patterns,
                sources: query.Sources,
                limit: query.Limit,
                scanLimit: 1000,
                cancellationToken: linked.Token).ConfigureAwait(false);
            byte[] json = query.Encode(
                page: page,
                streamId: state.StreamId,
                through: through,
                maximumBytes: options.MaximumMessageBytes);
            linked.Token.ThrowIfCancellationRequested();
            context.Response.ContentType = "application/json";
            await context.Response.Body.WriteAsync(json, context.RequestAborted).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            metrics.Rejection("query");
            await ErrorAsync(
                context: context,
                status: 400,
                error: "InvalidRequest",
                message: "Invalid label query or cursor.").ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {
            metrics.Failure("query_deadline");
            await ErrorAsync(
                context: context,
                status: 503,
                error: "ServiceUnavailable",
                message: "The label query deadline or host lifetime ended.").ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is DbException or InvalidDataException or InvalidOperationException)
        {
            metrics.Failure("query");
            Failed(logger, exception, "query");
            await ErrorAsync(
                context: context,
                status: 503,
                error: "ServiceUnavailable",
                message: "Label storage is unavailable or its retained history changed.").ConfigureAwait(false);
        }
        finally
        {
            metrics.Query(clock.GetElapsedTime(started));
        }
    }
}
