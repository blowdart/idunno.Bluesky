// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Signing;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Registers and maps the standalone labeller's public distribution endpoints.</summary>
public static class LabelerHostingExtensions
{
    /// <summary>Registers durable publication and public query/subscription hosting.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="issuer">The issuer already bound to the database.</param>
    /// <param name="options">The immutable hosting limits, or <see langword="null"/> for defaults.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">A required dependency is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An option is outside its supported range.</exception>
    /// <exception cref="ArgumentException"><paramref name="options"/> contains an invalid public endpoint.</exception>
    /// <exception cref="InvalidOperationException">Hosting is already registered or the store registration is ambiguous.</exception>
    /// <remarks><para>Register exactly one initialized label store. No keys, credentials, database
    /// migrations, identity changes or administrative mutation endpoints are installed.</para></remarks>
    public static IServiceCollection AddBlueskyLabeler(
        this IServiceCollection services,
        Did issuer,
        LabelerHostingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(issuer);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(LabelerHost)) ||
            services.Count(descriptor => descriptor.ServiceType == typeof(ILabelStore)) != 1 ||
            services.Count(descriptor => descriptor.ServiceType == typeof(ILabelMaintenanceStore)) != 1)
        {
            throw new InvalidOperationException("Register one label store and maintenance store, and register hosting only once.");
        }

        LabelerHostingOptions settings = options ?? new();
        settings.Validate(nameof(options));
        services.AddSingleton(settings);
        services.AddLogging();
        services.AddMetrics();
        services.AddSingleton(provider => new HostingMetrics(provider.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>()));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(provider =>
        {
            ILabelStore store = provider.GetRequiredService<ILabelStore>();
            ILabelMaintenanceStore maintenanceStore = provider.GetRequiredService<ILabelMaintenanceStore>();
            if (!ReferenceEquals(store, maintenanceStore))
            {
                throw new InvalidOperationException("Maintenance coordination and label distribution must use the same provider instance.");
            }

            return new LabelerHost(
                issuer: issuer,
                store: store,
                maintenanceStore: maintenanceStore,
                signingKeyRing: provider.GetService<ILabelSigningKeyRing>(),
                options: settings,
                clock: provider.GetRequiredService<TimeProvider>(),
                logger: provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LabelerHost>>(),
                metrics: provider.GetRequiredService<HostingMetrics>(),
                applicationLifetime: provider.GetRequiredService<IHostApplicationLifetime>());
        });
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<LabelerHost>());
        services.AddHealthChecks().AddCheck<LabelerHost>("bluesky_labeler");
        return services;
    }

    /// <summary>Maps public distribution routes and labeler liveness/readiness health checks.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is <see langword="null"/>.</exception>
    /// <remarks><para>Call <c>UseWebSockets</c> before mapping endpoints. Both routes are public;
    /// authorization for administrative issuance is a separate responsibility. Use TLS in production.</para>
    /// <para>Health checks are mapped at <c>/health/labeler/live</c> and <c>/health/labeler/ready</c>.
    /// Liveness checks the running application; readiness also checks the publisher and durable storage.
    /// Responses contain only aggregate health status, not exception or database details.</para></remarks>
    public static IEndpointRouteBuilder MapBlueskyLabeler(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        LabelerHost host = endpoints.ServiceProvider.GetRequiredService<LabelerHost>();
        endpoints.Map("/xrpc/com.atproto.label.queryLabels", (RequestDelegate)host.QueryAsync).AllowAnonymous();
        endpoints.Map("/xrpc/com.atproto.label.subscribeLabels", (RequestDelegate)host.SubscribeAsync).AllowAnonymous();
        endpoints.MapHealthChecks("/health/labeler/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        endpoints.MapHealthChecks("/health/labeler/ready", new HealthCheckOptions { Predicate = check => check.Name == "bluesky_labeler" }).AllowAnonymous();
        return endpoints;
    }
}
