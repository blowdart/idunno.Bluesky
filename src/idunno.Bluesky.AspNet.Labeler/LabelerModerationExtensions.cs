// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Moderation;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Installs optional Xenon services without replacing the canonical public distribution host.</summary>
public static class LabelerModerationExtensions
{
    /// <summary>Registers report intake and shared durable policy verification.</summary>
    /// <param name="services">The service collection with one shared relational provider.</param>
    /// <param name="issuer">The initialized labeler issuer.</param>
    /// <param name="httpClient">The caller-owned hardened identity and repository client.</param>
    /// <param name="options">The optional report limits and explicit verification freshness.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An option exceeds its supported range.</exception>
    /// <exception cref="InvalidOperationException">Required shared storage is missing, ambiguous or registered independently.</exception>
    /// <remarks><para>Register before <c>AddLabelerAdministration</c>, and do not separately register an
    /// issuance-policy provider. Before starting the host, initialize the ordinary store and call
    /// <see cref="ILabelPolicyStore.InitializePolicyAsync"/> with the same freshness. This permanently
    /// enables commit-time policy fences on that issuer database; configuration alone cannot bypass them.</para>
    /// <para>Admission limits apply per host process, before expensive authentication or durable audit.
    /// Operators should additionally bound aggregate traffic across replicas at their trusted proxy.</para></remarks>
    public static IServiceCollection AddLabelerModeration(
        this IServiceCollection services,
        Did issuer,
        HttpClient httpClient,
        LabelerModerationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(httpClient);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(LabelerModerationHost) ||
                descriptor.ServiceType == typeof(ILabelIssuancePolicyProvider)) ||
            services.Count(descriptor => descriptor.ServiceType == typeof(ILabelStore)) != 1 ||
            services.Count(descriptor => descriptor.ServiceType == typeof(ILabelPolicyStore)) != 1 ||
            services.Count(descriptor => descriptor.ServiceType == typeof(ILabelReportStore)) != 1 ||
            services.Count(descriptor => descriptor.ServiceType == typeof(ILabelAppealStore)) != 1)
        {
            throw new InvalidOperationException("Register one shared provider, then moderation once before administration or any other issuance policy.");
        }

        LabelerModerationOptions settings = options ?? new();
        settings.Validate();
        services.AddLogging();
        services.AddMetrics();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(settings);
        services.AddSingleton(provider =>
        {
            ILabelStore store = provider.GetRequiredService<ILabelStore>();
            ILabelPolicyStore policies = provider.GetRequiredService<ILabelPolicyStore>();
            if (!ReferenceEquals(store, policies) || !ReferenceEquals(store, provider.GetRequiredService<ILabelReportStore>()) ||
                !ReferenceEquals(store, provider.GetRequiredService<ILabelAppealStore>()))
            {
                throw new InvalidOperationException("Reports, policies and label decisions must use the same provider instance.");
            }

            return new LabelPolicyService(
                issuer: issuer, store: policies, httpClient: httpClient, loggerFactory: provider.GetRequiredService<ILoggerFactory>());
        });
        services.AddSingleton<ILabelIssuancePolicyProvider>(provider => provider.GetRequiredService<LabelPolicyService>());
        services.AddSingleton(provider => new LabelerModerationHost(
            issuer: issuer,
            httpClient: httpClient,
            reports: provider.GetRequiredService<ILabelReportStore>(),
            policies: provider.GetRequiredService<LabelPolicyService>(),
            policyStore: provider.GetRequiredService<ILabelPolicyStore>(),
            settings: settings,
            clock: provider.GetRequiredService<TimeProvider>(),
            logger: provider.GetRequiredService<ILogger<LabelerModerationHost>>(),
            meterFactory: provider.GetRequiredService<IMeterFactory>()));
        services.AddHealthChecks().AddCheck<LabelerModerationHost>("xenon_moderation");
        return services;
    }

    /// <summary>Maps the independent standard reporter endpoint without consulting remote CLI enablement.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint route builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> is <see langword="null"/>.</exception>
    /// <remarks><para>Call only when moderation is explicitly enabled. HTTPS is mandatory; configure
    /// trusted proxy forwarding before mapping. Browser cookies and team membership do not authenticate reports.</para>
    /// <para>The separate <c>/health/labeler/moderation</c> endpoint reports policy health; do not use it
    /// for distribution or administration traffic routing.</para></remarks>
    public static IEndpointRouteBuilder MapLabelerModeration(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        LabelerModerationHost host = endpoints.ServiceProvider.GetRequiredService<LabelerModerationHost>();
        endpoints.MapPost("/xrpc/com.atproto.moderation.createReport", (RequestDelegate)host.ReportAsync).AllowAnonymous();
        endpoints.MapHealthChecks("/health/labeler/moderation", new HealthCheckOptions
        {
            Predicate = registration => registration.Name == "xenon_moderation"
        }).AllowAnonymous();
        return endpoints;
    }
}
