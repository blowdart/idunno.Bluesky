// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;

using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Signing;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Continues explicitly authorized bounded retirement work independently of browser or CLI lifetime.</summary>
internal sealed partial class RetirementWorker(
    ILabelStore store,
    ILabelSigningKeyRing? keys,
    LabelerHost? host,
    TimeProvider clock,
    ILogger<RetirementWorker> logger) : BackgroundService
{
    private readonly SemaphoreSlim _advanceGate = new(1, 1);
    private readonly TaskCompletionSource _initialIteration = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly LabelRetirementService? _service = store is ILabelRetirementStore retirement
        ? new(store, retirement, keys) : null;
    private LabelRetirementCheckpoint? _pendingPause;

    /// <summary>Gets completion of the initial checkpoint read and registration of the next hosted iteration.</summary>
    internal Task InitialIteration => _initialIteration.Task;

    /// <summary>Releases the execution gate after hosted execution has stopped.</summary>
    public override void Dispose()
    {
        base.Dispose();
        _advanceGate.Dispose();
    }

    /// <summary>Rejects unsafe keyless administrative hosting before background execution begins.</summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (keys is null)
        {
            LabelStoreState state = await store.GetStateAsync(cancellationToken).ConfigureAwait(false);
            LabelMaintenanceState maintenance = await ((ILabelMaintenanceStore)store).GetMaintenanceStateAsync(cancellationToken).ConfigureAwait(false);
            if (state.Phase is not (LabelerLifecyclePhase.Retired or LabelerLifecyclePhase.Purging or LabelerLifecyclePhase.Purged) ||
                state.IssuedThrough != state.PublishedThrough || maintenance.IsActive)
            {
                throw new InvalidOperationException("Keyless administration requires terminal retirement with no unpublished labels or active signing-key rotation.");
            }
        }

        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reloads provider checkpoints before each batch and never blindly retries uncertain signatures.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_service is null)
        {
            RetirementPaused(logger, "The selected provider does not implement durable retirement; retirement commands are unavailable.");
            _initialIteration.TrySetResult();
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            using (IDisposable? admission = host?.TryAdmitAdministrativeIssuance())
            {
                if (host is null || admission is not null)
                {
                    await AdvanceAsync(stoppingToken).ConfigureAwait(false);
                }
            }

            Task delay = Task.Delay(TimeSpan.FromSeconds(1), clock, stoppingToken);
            _initialIteration.TrySetResult();
            await delay.ConfigureAwait(false);
        }
    }

    /// <summary>Serializes manual and hosted work, retaining failed checkpoints until their pause is durably recorded.</summary>
    internal async Task<LabelServiceError?> AdvanceAsync(CancellationToken token)
    {
        await _advanceGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_service is null || store is not ILabelRetirementStore retirement)
            {
                return new(LabelServiceErrorCode.StorageUnavailable, "The provider does not implement durable retirement.");
            }

            if (_pendingPause is { } pending)
            {
                await PersistPauseAsync(retirement, pending, token).ConfigureAwait(false);
                _pendingPause = null;
            }

            LabelServiceError? error;
            try
            {
                error = await _service.AdvanceAsync(token).ConfigureAwait(false);
                if (error is null || error.Code == LabelServiceErrorCode.StaleDecision)
                {
                    return error;
                }

                RetirementPaused(logger, error.Code.ToString());
            }
            catch (Exception exception) when (exception is DbException or InvalidDataException or InvalidOperationException or OverflowException)
            {
                RetirementFailed(logger, exception);
                error = new(
                    code: LabelServiceErrorCode.StorageUnavailable,
                    message: "Retirement failed; inspect durable progress and explicitly resume.",
                    exception: exception);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                _pendingPause = _service.RecoveryCheckpoint;
                throw;
            }

            _pendingPause = _service.RecoveryCheckpoint;
            if (_pendingPause is { } checkpoint)
            {
                // Cancellation leaves the checkpoint owned by this host, not by the disconnected request.
                await PersistPauseAsync(retirement, checkpoint, token).ConfigureAwait(false);
                _pendingPause = null;
            }

            return error;
        }
        finally
        {
            _advanceGate.Release();
        }
    }

    /// <summary>Keeps this worker faulted until its pause is durably recorded, even through a database outage.</summary>
    private async Task PersistPauseAsync(ILabelRetirementStore retirement, LabelRetirementCheckpoint checkpoint, CancellationToken token)
    {
        while (true)
        {
            try
            {
                await retirement.PauseRetirementCheckpointAsync(checkpoint, "StorageUnavailable: worker interrupted; inspect durable progress and explicitly resume.", token).ConfigureAwait(false);
                return;
            }
            catch (Exception exception) when (exception is DbException or InvalidOperationException or InvalidDataException)
            {
                RetirementFailed(logger, exception);
                await Task.Delay(TimeSpan.FromSeconds(5), clock, token).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Surfaces classified recovery requirements without private credential diagnostics.</summary>
    [LoggerMessage(EventId = 7305, Level = LogLevel.Warning, Message = "Retirement worker paused: {Reason}")]
    private static partial void RetirementPaused(ILogger logger, string reason);

    /// <summary>Preserves the operational exception for a failed provider operation.</summary>
    [LoggerMessage(EventId = 7306, Level = LogLevel.Error, Message = "Retirement worker failed; explicit recovery is required.")]
    private static partial void RetirementFailed(ILogger logger, Exception exception);
}
