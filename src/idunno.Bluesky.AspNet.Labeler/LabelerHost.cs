// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Labeler;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Owns publication and socket admission; all network delivery occurs outside store transactions.</summary>
/// <param name="issuer">The expected persisted issuer.</param>
/// <param name="store">The caller-initialized durable store.</param>
/// <param name="options">The validated hosting limits.</param>
/// <param name="clock">The shared timer clock.</param>
/// <param name="logger">The operational logger.</param>
/// <param name="metrics">The process-local hosting instruments.</param>
/// <param name="applicationLifetime">The application shutdown signal, raised before the web server drains requests.</param>
internal sealed partial class LabelerHost(
    Did issuer,
    ILabelStore store,
    LabelerHostingOptions options,
    TimeProvider clock,
    ILogger<LabelerHost> logger,
    HostingMetrics metrics,
    IHostApplicationLifetime applicationLifetime) : BackgroundService, Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    internal const long MaximumSequence = 9_007_199_254_740_991;
    private int _subscriptions;
    private int _disposed;
    private Guid _streamId;
    private volatile bool _ready;
    private readonly CancellationTokenSource _lifetime = CancellationTokenSource.CreateLinkedTokenSource(applicationLifetime.ApplicationStopping);

    /// <summary>Gets or sets an internal phase barrier for deterministic read, handoff and cancellation tests.</summary>
    internal Func<CancellationToken, Task>? BeforeReadAsync { get; set; }

    /// <summary>Gets or sets an internal phase barrier for backpressure tests; it shares the actual send deadline.</summary>
    internal Func<CancellationToken, Task>? BeforeSendAsync { get; set; }

    /// <summary>Validates initialized storage before admitting traffic; never migrates under serving credentials.</summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        LabelStoreState state = await store.GetStateAsync(cancellationToken).ConfigureAwait(false);
        if (state.Issuer != issuer.Value || state.IssuedThrough > MaximumSequence)
        {
            throw new InvalidOperationException("The initialized store issuer or sequence range is incompatible with hosting.");
        }

        _streamId = state.StreamId;
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Promotes pending history in bounded transactions and resumes from the durable publication boundary.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                LabelStoreState state = await store.PublishAsync(options.BatchSize, stoppingToken).ConfigureAwait(false);
                CheckState(state);
                _ready = true;
                metrics.Pending(state.IssuedThrough - state.PublishedThrough);
                if (state.PublishedThrough == state.IssuedThrough)
                {
                    await Task.Delay(options.PollInterval, clock, stoppingToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown is not a publication failure.
        }
        catch (Exception exception) when (exception is DbException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            metrics.Failure("publication");
            Failed(logger, exception, "publication");
            throw;
        }
        finally
        {
            _ready = false;
        }
    }

    /// <summary>Stops admission before cancelling publication and all active subscription loops.</summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _ready = false;
        await _lifetime.CancelAsync().ConfigureAwait(false);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Releases the host-owned cancellation source after stopping all admitted operations.</summary>
    public override void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _lifetime.Cancel();
        _lifetime.Dispose();
        base.Dispose();
    }

    /// <summary>Rejects database replacement rather than silently moving existing clients to a different stream.</summary>
    private void CheckState(LabelStoreState state)
    {
        if (state.StreamId != _streamId || state.Issuer != issuer.Value || state.IssuedThrough > MaximumSequence)
        {
            throw new InvalidOperationException("The bound label stream changed or exhausted its wire sequence range.");
        }
    }

    /// <summary>Writes standard XRPC errors without reflecting internal exception details.</summary>
    private static async Task ErrorAsync(HttpContext context, int status, string error, string message)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("error", error);
            writer.WriteString("message", message);
            writer.WriteEndObject();
        }

        await context.Response.Body.WriteAsync(buffer.ToArray(), context.RequestAborted).ConfigureAwait(false);
    }

    /// <summary>Logs safe operation names and original failures without copying query inputs into structured tags.</summary>
    [LoggerMessage(1, LogLevel.Error, "Label hosting {Operation} failed.")]
    private static partial void Failed(ILogger logger, Exception exception, string operation);
}
