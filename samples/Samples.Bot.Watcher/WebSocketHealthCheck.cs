// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Samples.Bot.Watcher
{
    /// <summary>
    /// Provides health check information so Docker can monitor and restart the application as appropriate.
    /// </summary>
    internal sealed class WebSocketHealthCheck(IFaultWatcher faultWatcher) : IHealthCheck
    {
        public IFaultWatcher _faultWatcher = faultWatcher;

        /// <inheritdoc/>
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            Console.WriteLine("Checking health");

            if (_faultWatcher.Count > 8)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(":("));
            }
            else if (_faultWatcher.Count > 5)
            {
                return Task.FromResult(HealthCheckResult.Degraded(":|"));
            }

            return Task.FromResult(HealthCheckResult.Healthy(":)"));
        }
    }
}
