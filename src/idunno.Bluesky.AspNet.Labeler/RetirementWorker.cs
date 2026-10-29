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
        if (store is not ILabelRetirementStore retirement)
        {
            RetirementPaused(logger, "The selected provider does not implement durable retirement; retirement commands are unavailable.");
            return;
        }

        LabelRetirementService service = new(store, retirement, keys);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IDisposable? admission = host?.TryAdmitAdministrativeIssuance();
                if (host is not null && admission is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), clock, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                LabelServiceError? error = await service.AdvanceAsync(stoppingToken).ConfigureAwait(false);
                if (error is not null && error.Code != LabelServiceErrorCode.StaleDecision)
                {
                    RetirementPaused(logger, error.Code.ToString());
                    if (service.ObservedGeneration is long generation)
                    {
                        await retirement.PauseRetirementAsync(generation, error.Code + ": inspect progress and explicitly resume.", stoppingToken).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception exception) when (exception is DbException or InvalidDataException or InvalidOperationException or OverflowException)
            {
                RetirementFailed(logger, exception);
                // Retry only persistence of the pause, never the failed signing or deletion operation.
                if (service.ObservedGeneration is long generation)
                {
                    await PersistPauseAsync(retirement, generation, stoppingToken).ConfigureAwait(false);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(1), clock, stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>Keeps this worker faulted until its pause is durably recorded, even through a database outage.</summary>
    private async Task PersistPauseAsync(ILabelRetirementStore retirement, long generation, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await retirement.PauseRetirementAsync(generation, "StorageUnavailable: worker interrupted; inspect durable progress and explicitly resume.", token).ConfigureAwait(false);
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
