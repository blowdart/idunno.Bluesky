// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using idunno.Bluesky;


namespace Samples.ConfigurationBinding
{
    internal sealed class Program
    {
        static async Task Main(string[] args)
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddBlueskyAgentOptions();

            builder.Services.AddHostedService<HostedService>();

            using IHost host = builder.Build();
            await host.RunAsync();
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Example code, inline for clarity")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Example code, inline for clarity")]
    internal sealed class HostedService(
        IOptions<BlueskyAgentOptions> options,
        ILogger<HostedService> logger,
        IHostApplicationLifetime lifeTime) : BackgroundService
    {
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("1. Starting");

            logger.LogInformation($"1.1 options.EnableBackgroundTokenRefresh = {options.Value.EnableBackgroundTokenRefresh}");

            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("3. Stopping");

            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var agent = new BlueskyAgent(options.Value);
                var resolvedDid = await agent.ResolveHandle("blowdart.me", cancellationToken: stoppingToken);
                if (resolvedDid is not null)
                {
                    logger.LogInformation($"2. Resolved DID to {resolvedDid}");
                }

                // Now stop the application programmatically.
                lifeTime.StopApplication();
            }
        }
    }
}
