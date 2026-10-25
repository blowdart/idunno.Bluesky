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
/// <param name="maintenanceStore">The durable store for coordinated host maintenance.</param>
/// <param name="signingKeyRing">The optional signing-key ring used during key rotation.</param>
/// <param name="options">The validated hosting limits.</param>
/// <param name="clock">The shared timer clock.</param>
/// <param name="logger">The operational logger.</param>
/// <param name="metrics">The process-local hosting instruments.</param>
/// <param name="applicationLifetime">The application shutdown signal, raised before the web server drains requests.</param>
internal sealed partial class LabelerHost(
    Did issuer,
    ILabelStore store,
    ILabelMaintenanceStore maintenanceStore,
    ILabelSigningKeyRing? signingKeyRing,
    LabelerHostingOptions options,
    TimeProvider clock,
    ILogger<LabelerHost> logger,
    HostingMetrics metrics,
    IHostApplicationLifetime applicationLifetime) : BackgroundService, Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    internal const long MaximumSequence = 9_007_199_254_740_991;
    private readonly Guid _instanceId = Guid.NewGuid();
    private readonly object _admissionLock = new();
    private readonly SemaphoreSlim _maintenanceRefreshGate = new(1, 1);
    private CancellationTokenSource _maintenanceLifetime = new();
    private TaskCompletionSource? _drained;
    private CancellationTokenRegistration _stoppingRegistration;
    private int _subscriptions;
    private int _activeRequests;
    private int _disposed;
    private bool _registered;
    private Guid _streamId;
    private bool _maintenanceActive;
    private bool _shuttingDown;
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
        LabelMaintenanceState maintenance = await maintenanceStore.RegisterHostAsync(_instanceId, cancellationToken).ConfigureAwait(false);
        _registered = true;
        ApplyMaintenanceState(maintenance);
        _stoppingRegistration = applicationLifetime.ApplicationStopping.Register(BeginLocalDrain);
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Promotes pending history in bounded transactions and resumes from the durable publication boundary.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                LabelMaintenanceState maintenance = await RefreshMaintenanceStateAsync(stoppingToken).ConfigureAwait(false);

                LabelStoreState state = maintenance.IsActive
                    ? await store.GetStateAsync(stoppingToken).ConfigureAwait(false)
                    : await store.PublishAsync(options.BatchSize, stoppingToken).ConfigureAwait(false);
                CheckState(state);
                metrics.Pending(state.IssuedThrough - state.PublishedThrough);
                if (maintenance.IsActive)
                {
                    _ready = false;
                    if (maintenance.Phase == LabelMaintenancePhase.Draining && state.PublishedThrough < state.IssuedThrough)
                    {
                        state = await store.PublishAsync(options.BatchSize, stoppingToken).ConfigureAwait(false);
                        CheckState(state);
                    }

                    LabelMaintenanceHost? participant = maintenance.Hosts.FirstOrDefault(host => host.InstanceId == _instanceId);
                    if (state.PublishedThrough == state.IssuedThrough && ActiveRequestCount == 0 && participant is { Acknowledged: false })
                    {
                        try
                        {
                            await maintenanceStore.AcknowledgeHostDrainedAsync(
                                operationId: maintenance.OperationId!,
                                instanceId: _instanceId,
                                cancellationToken: stoppingToken).ConfigureAwait(false);
                        }
                        catch (InvalidOperationException)
                        {
                            LabelMaintenanceState refreshed = await RefreshMaintenanceStateAsync(stoppingToken).ConfigureAwait(false);
                            if (refreshed.OperationId == maintenance.OperationId && refreshed.IsActive &&
                                refreshed.Hosts.FirstOrDefault(host => host.InstanceId == _instanceId) is { Acknowledged: false })
                            {
                                throw;
                            }
                        }
                    }

                    await Task.Delay(options.PollInterval, clock, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                _ready = true;
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
        BeginLocalDrain();
        await _lifetime.CancelAsync().ConfigureAwait(false);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await WaitForDrainAsync(cancellationToken).ConfigureAwait(false);
        if (_registered)
        {
            try
            {
                await maintenanceStore.StopHostAsync(_instanceId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is DbException or InvalidDataException or InvalidOperationException)
            {
                metrics.Failure("maintenance_stop");
                Failed(logger, exception, "maintenance stop acknowledgement");
            }
            finally
            {
                _registered = false;
            }
        }
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
        _stoppingRegistration.Dispose();
        _maintenanceRefreshGate.Dispose();
        lock (_admissionLock)
        {
            _maintenanceLifetime.Cancel();
            _maintenanceLifetime.Dispose();
        }
        base.Dispose();
    }

    /// <summary>Refreshes durable host presence before admitting public distribution work.</summary>
    private async Task<IDisposable?> AdmitTrafficAsync(HttpContext context)
    {
        try
        {
            LabelMaintenanceState state = await RefreshMaintenanceStateAsync(context.RequestAborted).ConfigureAwait(false);
            if (!state.IsActive && TryEnterRequest())
            {
                return new AdmissionLease(this);
            }

            await WriteMaintenanceUnavailableAsync(context).ConfigureAwait(false);
            return null;
        }
        catch (Exception exception) when (exception is DbException or InvalidDataException or InvalidOperationException)
        {
            metrics.Failure("maintenance_admission");
            Failed(logger, exception, "maintenance admission");
            await WriteMaintenanceUnavailableAsync(context).ConfigureAwait(false);
            return null;
        }
    }

    /// <summary>Closes admission atomically with request accounting so a drain acknowledgement cannot race a new request.</summary>
    private bool TryEnterRequest()
    {
        lock (_admissionLock)
        {
            if (_maintenanceActive || _shuttingDown)
            {
                return false;
            }

            if (_activeRequests == 0)
            {
                _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            _activeRequests++;
            return true;
        }
    }

    /// <summary>Tracks authorized label mutations so rotation cannot drain while an issuer is resolving a target.</summary>
    internal IDisposable? TryAdmitAdministrativeIssuance() =>
        TryEnterRequest() ? new AdmissionLease(this) : null;

    /// <summary>Releases one admitted query or subscription and signals an outstanding drain when the count reaches zero.</summary>
    private void ExitRequest()
    {
        lock (_admissionLock)
        {
            _activeRequests--;
            if (_activeRequests == 0)
            {
                _drained?.TrySetResult();
                _drained = null;
            }
        }
    }

    /// <summary>Gets the number of admitted requests that have not yet drained.</summary>
    private int ActiveRequestCount
    {
        get
        {
            lock (_admissionLock)
            {
                return _activeRequests;
            }
        }
    }

    /// <summary>Gets a token cancelled when maintenance or graceful shutdown closes public admission.</summary>
    private CancellationToken MaintenanceToken
    {
        get
        {
            lock (_admissionLock)
            {
                return _maintenanceLifetime.Token;
            }
        }
    }

    /// <summary>Gets whether maintenance or graceful shutdown is closing public admission.</summary>
    private bool IsDraining
    {
        get
        {
            lock (_admissionLock)
            {
                return _maintenanceActive || _shuttingDown;
            }
        }
    }

    /// <summary>Applies shared maintenance state and cancels established subscriptions when draining begins.</summary>
    private void ApplyMaintenanceState(LabelMaintenanceState state)
    {
        lock (_admissionLock)
        {
            bool active = state.IsActive || _shuttingDown;
            if (active && !_maintenanceActive)
            {
                _maintenanceActive = true;
                _ready = false;
                _maintenanceLifetime.Cancel();
            }
            else if (!active && _maintenanceActive)
            {
                _maintenanceLifetime.Dispose();
                _maintenanceLifetime = new();
                _maintenanceActive = false;
            }
        }
    }

    /// <summary>Activates a verified key on this process before acknowledging or reopening it.</summary>
    private async Task<LabelMaintenanceState> EnsureRotationKeyAsync(
        LabelMaintenanceState state,
        CancellationToken cancellationToken)
    {
        if (state.Phase is not (LabelMaintenancePhase.ReadyToReopen or LabelMaintenancePhase.Completed) ||
            string.IsNullOrWhiteSpace(state.ReplacementKeyId))
        {
            return state;
        }

        if (signingKeyRing is null)
        {
            throw new InvalidOperationException("This host has no signing-key ring for the completed rotation.");
        }

        LabelMaintenanceHost? participant = state.Hosts.FirstOrDefault(host => host.InstanceId == _instanceId);
        if (state.Phase == LabelMaintenancePhase.ReadyToReopen && !state.AllHostsDrained)
        {
            return state;
        }

        if (!string.Equals(
            signingKeyRing.GetPublicKeyMultibase(state.ReplacementKeyId),
            state.ReplacementPublicKey,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The locally installed replacement key does not match the verified published key.");
        }

        if (!string.Equals(signingKeyRing.ActiveKeyId, state.ReplacementKeyId, StringComparison.Ordinal))
        {
            await signingKeyRing.ActivateAsync(state.ReplacementKeyId, cancellationToken).ConfigureAwait(false);
        }

        if (state.Phase == LabelMaintenancePhase.ReadyToReopen && participant is { KeyActivated: false })
        {
            try
            {
                state = await maintenanceStore.AcknowledgeHostKeyActivatedAsync(
                    operationId: state.OperationId!,
                    instanceId: _instanceId,
                    keyId: state.ReplacementKeyId,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                LabelMaintenanceState refreshed = await maintenanceStore.GetMaintenanceStateAsync(cancellationToken).ConfigureAwait(false);
                LabelMaintenanceHost? latest = refreshed.Hosts.FirstOrDefault(host => host.InstanceId == _instanceId);
                if (refreshed.OperationId == state.OperationId &&
                    refreshed.Phase != LabelMaintenancePhase.Completed &&
                    refreshed.AllHostsDrained &&
                    latest is not { KeyActivated: true })
                {
                    throw;
                }

                state = refreshed;
            }
        }

        return state;
    }

    /// <summary>Serializes durable maintenance refreshes with their local admission-state updates.</summary>
    private async Task<LabelMaintenanceState> RefreshMaintenanceStateAsync(CancellationToken cancellationToken)
    {
        await _maintenanceRefreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LabelMaintenanceState state = await maintenanceStore.HeartbeatHostAsync(
                instanceId: _instanceId,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (state.IsActive)
            {
                ApplyMaintenanceState(state);
            }

            state = await EnsureRotationKeyAsync(state, cancellationToken).ConfigureAwait(false);
            ApplyMaintenanceState(state);
            return state;
        }
        finally
        {
            _maintenanceRefreshGate.Release();
        }
    }

    /// <summary>Stops local admission and closes active subscriptions for either rotation or process shutdown.</summary>
    private void BeginLocalDrain()
    {
        lock (_admissionLock)
        {
            _shuttingDown = true;
            _maintenanceActive = true;
            _ready = false;
            _maintenanceLifetime.Cancel();
        }
    }

    /// <summary>Waits for admitted requests to release their leases before this process acknowledges shutdown.</summary>
    private Task WaitForDrainAsync(CancellationToken cancellationToken)
    {
        lock (_admissionLock)
        {
            return _activeRequests == 0
                ? Task.CompletedTask
                : _drained!.Task.WaitAsync(cancellationToken);
        }
    }

    /// <summary>Writes the explicit retryable response required while maintenance blocks distribution.</summary>
    private static async Task WriteMaintenanceUnavailableAsync(HttpContext context)
    {
        context.Response.Headers.RetryAfter = "1";
        await ErrorAsync(
            context: context,
            status: StatusCodes.Status503ServiceUnavailable,
            error: "ServiceUnavailable",
            message: "The labeler is in maintenance; retry the request.").ConfigureAwait(false);
    }

    /// <summary>Releases an admitted request exactly once when its endpoint exits.</summary>
    private sealed class AdmissionLease(LabelerHost host) : IDisposable
    {
        private int _disposed;

        /// <summary>Releases the host's active request count.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                host.ExitRequest();
            }
        }
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
