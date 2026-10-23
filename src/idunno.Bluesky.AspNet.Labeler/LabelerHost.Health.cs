// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace idunno.Bluesky.AspNet.Labeler;

internal sealed partial class LabelerHost
{
    /// <summary>Checks publication lifetime and durable stream compatibility without mutating or migrating storage.</summary>
    /// <param name="context">The health registration being evaluated.</param>
    /// <param name="cancellationToken">The health request cancellation token.</param>
    /// <returns>The aggregate readiness status without private storage details.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_ready)
        {
            return HealthCheckResult.Unhealthy("The label publisher is not accepting traffic.");
        }

        using CancellationTokenSource deadline = new(options.QueryTimeout, clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token, _lifetime.Token);
        try
        {
            CheckState(await store.GetStateAsync(linked.Token).ConfigureAwait(false));
            linked.Token.ThrowIfCancellationRequested();
            return _ready
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("The label publisher stopped.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            metrics.Failure("health");
            return HealthCheckResult.Unhealthy("The label storage check deadline or host lifetime ended.");
        }
        catch (Exception exception) when (exception is DbException or InvalidDataException or InvalidOperationException)
        {
            metrics.Failure("health");
            Failed(logger, exception, "health");
            return HealthCheckResult.Unhealthy("Label storage is unavailable or incompatible.");
        }
    }
}
