// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Globalization;
using System.Net.WebSockets;

using idunno.Bluesky.Labeler;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Labeler;

internal sealed partial class LabelerHost
{
    /// <summary>Admits bounded public subscriptions and pairs each send loop with a cancellable receive drain.</summary>
    internal async Task SubscribeAsync(HttpContext context)
    {
        using IDisposable? admission = await AdmitTrafficAsync(context).ConfigureAwait(false);
        if (admission is null)
        {
            return;
        }

        if (!HttpMethods.IsGet(context.Request.Method) &&
            !(HttpMethods.IsConnect(context.Request.Method) && context.WebSockets.IsWebSocketRequest))
        {
            context.Response.Headers.Allow = "GET, CONNECT";
            await ErrorAsync(
                context: context,
                status: 405,
                error: "InvalidRequest",
                message: "Use a WebSocket handshake for label subscriptions.").ConfigureAwait(false);
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.Headers.Upgrade = "websocket";
            await ErrorAsync(
                context: context,
                status: 426,
                error: "InvalidRequest",
                message: "A WebSocket upgrade is required.").ConfigureAwait(false);
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

        if (Interlocked.Increment(ref _subscriptions) > options.MaximumSubscriptions)
        {
            metrics.Rejection("subscription_limit");
            Interlocked.Decrement(ref _subscriptions);
            context.Response.Headers.RetryAfter = "1";
            await ErrorAsync(
                context: context,
                status: 429,
                error: "RateLimitExceeded",
                message: "The subscription limit has been reached.").ConfigureAwait(false);
            return;
        }

        try
        {
            metrics.Subscription(1);
            using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
            using CancellationTokenSource lifetime = CancellationTokenSource.CreateLinkedTokenSource(
                context.RequestAborted, _lifetime.Token, MaintenanceToken);
            await ServeSocketAsync(context, socket, lifetime).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _subscriptions);
            metrics.Subscription(-1);
        }
    }

    /// <summary>Joins the receive operation before its caller disposes the socket or linked lifetime.</summary>
    private async Task ServeSocketAsync(HttpContext context, WebSocket socket, CancellationTokenSource lifetime)
    {
        Task receive = DrainAsync(socket, lifetime);
        try
        {
            await StreamAsync(context, socket, lifetime.Token).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            metrics.Rejection("subscription_cursor");
            await TryTerminalAsync(socket, "InvalidRequest", lifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!lifetime.IsCancellationRequested)
        {
            Disconnected(logger, "send deadline");
            metrics.Failure("send_deadline");
            await TryTerminalAsync(socket, "ConsumerTooSlow", lifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            Disconnected(logger, "cancelled");
        }
        catch (WebSocketException)
        {
            Disconnected(logger, "transport");
        }
        catch (Exception exception) when (exception is DbException or InvalidDataException or InvalidOperationException)
        {
            Failed(logger, exception, "subscription");
            metrics.Failure("subscription");
            await TryTerminalAsync(socket, "ServiceUnavailable", lifetime.Token).ConfigureAwait(false);
        }
        finally
        {
            await CloseAsync(socket).ConfigureAwait(false);
            await lifetime.CancelAsync().ConfigureAwait(false);
            await receive.ConfigureAwait(false);
        }
    }

    /// <summary>Reads retained snapshots until cancellation, using Ozone's exclusive reconnect position.</summary>
    private async Task StreamAsync(HttpContext context, WebSocket socket, CancellationToken token)
    {
        if (context.Request.QueryString.Value?.Length > 1024)
        {
            throw new ArgumentException("The query exceeds the hosting input limit.");
        }

        string? text = LabelQuery.Scalar(context.Request, "cursor");
        long? cursor = null;
        if (text is not null)
        {
            if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) || parsed > MaximumSequence)
            {
                throw new ArgumentException("The subscription cursor is invalid.");
            }

            cursor = parsed;
        }

        LabelStoreState state = await store.GetStateAsync(token).ConfigureAwait(false);
        CheckState(state);
        if (cursor > state.PublishedThrough)
        {
            metrics.Rejection("subscription_future");
            await SendAsync(socket, LabelFrames.Notice("FutureCursor", "The cursor exceeds the published stream.", true), token).ConfigureAwait(false);
            return;
        }

        long after = cursor ?? state.PublishedThrough;
        while (!token.IsCancellationRequested)
        {
            CheckState(state);
            if (after < state.ReplayFloor)
            {
                if (after != 0)
                {
                    await SendAsync(socket, LabelFrames.Notice("OutdatedCursor", "Replay starts at the oldest retained position.", false), token).ConfigureAwait(false);
                }

                after = state.ReplayFloor;
            }

            long through = state.PublishedThrough;
            if (after < through)
            {
                LabelDistributionPage page;
                try
                {
                    if (BeforeReadAsync is not null)
                    {
                        await BeforeReadAsync(token).ConfigureAwait(false);
                    }

                    page = await store.ReadDistributionAsync(
                        streamId: _streamId,
                        after: after,
                        through: through,
                        uriPatterns: [],
                        sources: [],
                        limit: options.BatchSize,
                        scanLimit: 1000,
                        cancellationToken: token).ConfigureAwait(false);
                }
                catch (ArgumentOutOfRangeException exception) when (exception.ParamName == nameof(after))
                {
                    // A purge raced the captured snapshot. Re-enter the floor check before reading again.
                    state = await store.GetStateAsync(token).ConfigureAwait(false);
                    if (state.ReplayFloor <= after)
                    {
                        throw;
                    }

                    continue;
                }

                foreach (StoredLabel label in page.Labels)
                {
                    await SendAsync(socket, LabelFrames.Labels(label, options.MaximumMessageBytes), token).ConfigureAwait(false);
                }

                after = page.ScannedThrough;
                continue;
            }

            await Task.Delay(options.PollInterval, clock, token).ConfigureAwait(false);
            state = await store.GetStateAsync(token).ConfigureAwait(false);
        }
    }

    /// <summary>Bounds one socket write without retaining a transaction or an unbounded subscriber queue.</summary>
    private async Task SendAsync(WebSocket socket, byte[] frame, CancellationToken token)
    {
        using CancellationTokenSource deadline = new(options.SendTimeout, clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
        if (BeforeSendAsync is not null)
        {
            await BeforeSendAsync(linked.Token).ConfigureAwait(false);
        }

        await socket.SendAsync(
            buffer: frame.AsMemory(),
            messageType: WebSocketMessageType.Binary,
            endOfMessage: true,
            cancellationToken: linked.Token).ConfigureAwait(false);
        metrics.Frame();
    }

    /// <summary>Attempts terminal protocol delivery under a short deadline, aborting an unusable transport.</summary>
    private async Task TryTerminalAsync(WebSocket socket, string error, CancellationToken token)
    {
        if (socket.State != WebSocketState.Open || token.IsCancellationRequested)
        {
            return;
        }

        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(1), clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
        try
        {
            await socket.SendAsync(
                buffer: LabelFrames.Notice(error, "The subscription cannot continue.", true).AsMemory(),
                messageType: WebSocketMessageType.Binary,
                endOfMessage: true,
                cancellationToken: linked.Token).ConfigureAwait(false);
            metrics.Frame();
        }
        catch (Exception exception) when (exception is OperationCanceledException or WebSocketException)
        {
            Disconnected(logger, "terminal delivery");
            socket.Abort();
        }
    }

    /// <summary>Ignores inbound application frames in fixed memory and observes remote close while sends are idle.</summary>
    private static async Task DrainAsync(WebSocket socket, CancellationTokenSource lifetime)
    {
        byte[] buffer = new byte[1024];
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                ValueWebSocketReceiveResult result = await socket.ReceiveAsync(buffer.AsMemory(), lifetime.Token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            // The send loop or host has already selected the terminal outcome.
        }
        catch (WebSocketException)
        {
            // Remote transport loss is propagated to the send loop by lifetime cancellation.
        }
        finally
        {
            await lifetime.CancelAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Closes the server half without waiting indefinitely for a peer handshake.</summary>
    private async Task CloseAsync(WebSocket socket)
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            return;
        }

        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(1), clock);
        try
        {
            WebSocketCloseStatus closeStatus = IsDraining ? (WebSocketCloseStatus)1012 : WebSocketCloseStatus.NormalClosure;
            string description = IsDraining ? "Service maintenance; reconnect and retry." : "Subscription ended.";
            await socket.CloseOutputAsync(closeStatus, description, deadline.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException or WebSocketException)
        {
            Disconnected(logger, "close deadline or transport");
            socket.Abort();
        }
    }

    /// <summary>Records expected disconnect causes without logging client-supplied subjects or cursors.</summary>
    [LoggerMessage(2, LogLevel.Debug, "Label subscription disconnected: {Reason}.")]
    private static partial void Disconnected(ILogger logger, string reason);
}
