// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using idunno.Bluesky.Labeler.Storage;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace idunno.Bluesky.Labeler.Host;

/// <summary>Registers explicitly enabled metrics exporters shared by both canonical providers.</summary>
internal static class CanonicalLabelerTelemetry
{
    /// <summary>Captures validated listener configuration and registers the host's instrumentation and exporters.</summary>
    internal static void AddServices(WebApplicationBuilder builder)
    {
        IConfigurationSection configuration = builder.Configuration.GetSection("LabelerTelemetry");
        bool prometheus = configuration.GetValue<bool>("Prometheus:Enabled");
        bool otlp = configuration.GetValue<bool>("Otlp:Enabled");
        if (!prometheus && !otlp)
        {
            return;
        }

        if (prometheus)
        {
            string address = configuration["Prometheus:Address"] ?? "127.0.0.1";
            int port = configuration.GetValue("Prometheus:Port", 9464);
            if (!IPAddress.TryParse(address, out IPAddress? ip) || port is < 1 or > 65535)
            {
                throw new InvalidOperationException("Configure a literal IP address and a port between 1 and 65535 for LabelerTelemetry:Prometheus.");
            }
            builder.Services.AddSingleton(new PrometheusListenerSettings(ip, port));
            builder.Services.AddHostedService<PrometheusListener>();
        }

        Uri? endpoint = null;
        OtlpExportProtocol? exportProtocol = null;
        if (otlp &&
            configuration["Otlp:Endpoint"] is string configuredEndpoint &&
            (!Uri.TryCreate(configuredEndpoint, UriKind.Absolute, out endpoint) ||
            endpoint.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(endpoint.UserInfo) ||
            !string.IsNullOrEmpty(endpoint.Fragment)))
        {
            throw new InvalidOperationException("LabelerTelemetry:Otlp:Endpoint must be an absolute HTTP or HTTPS collector URL without user information or a fragment.");
        }
        if (otlp && configuration["Otlp:Protocol"] is string protocol)
        {
            exportProtocol = protocol switch
            {
                "grpc" => OtlpExportProtocol.Grpc,
                "http/protobuf" => OtlpExportProtocol.HttpProtobuf,
                _ => throw new InvalidOperationException("LabelerTelemetry:Otlp:Protocol must be grpc or http/protobuf.")
            };
        }

        string serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "idunno.Bluesky.Labeler.Host";
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithMetrics(metrics =>
            {
                metrics.AddBlueskyLabelerMetrics()
                    .AddMeter(LabelPersistenceMetrics.MeterName)
                    .AddBlueskyAuthenticationMetrics()
                    .AddAtProtoHttpClientMetrics()
                    .AddAtProtoDirectoryMetrics()
                    .AddAtProtoDidHandleCacheMetrics()
                    .AddMeter("Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Server.Kestrel", "System.Net.Http");
                if (prometheus)
                {
                    metrics.AddPrometheusExporter();
                }
                if (otlp)
                {
                    metrics.AddOtlpExporter(options =>
                    {
                        if (endpoint is not null)
                        {
                            options.Endpoint = endpoint;
                        }
                        if (exportProtocol is not null)
                        {
                            options.Protocol = exportProtocol.Value;
                        }
                    });
                }
            });
    }
}

/// <summary>Captures the dedicated scrape listener's bind address, separate from public application URLs.</summary>
/// <param name="Address">The explicitly selected bind address.</param>
/// <param name="Port">The listener port.</param>
internal sealed record PrometheusListenerSettings(IPAddress Address, int Port);

/// <summary>Owns an isolated Kestrel application serving only metrics, with the parent host's meter provider.</summary>
/// <param name="settings">The captured listener binding.</param>
/// <param name="meterProvider">The parent-owned instrumentation and exporters.</param>
/// <param name="loggerFactory">The parent-owned logging configuration.</param>
internal sealed class PrometheusListener(
    PrometheusListenerSettings settings,
    MeterProvider meterProvider,
    ILoggerFactory loggerFactory) : IHostedService, IAsyncDisposable
{
    private WebApplication? _application;

    /// <summary>Gets the listener application for internal lifecycle and isolation verification.</summary>
    internal WebApplication Application => _application ?? throw new InvalidOperationException("The metrics listener has not started.");

    /// <summary>Starts the dedicated listener, failing parent startup if its binding is unavailable.</summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [] });
        builder.Configuration.Sources.Clear();
        builder.Services.AddSingleton(loggerFactory);
        builder.WebHost.ConfigureKestrel(options => options.Listen(settings.Address, settings.Port));
        WebApplication application = builder.Build();
        application.MapPrometheusScrapingEndpoint(
            path: "/metrics",
            meterProvider: meterProvider,
            configureBranchedPipeline: null,
            optionsName: null);
        _application = application;
        await application.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Stops accepting scrapes when the parent application stops.</summary>
    public Task StopAsync(CancellationToken cancellationToken) =>
        _application?.StopAsync(cancellationToken) ?? Task.CompletedTask;

    /// <summary>Releases the dedicated server without disposing the parent's meter provider.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_application is not null)
        {
            await _application.DisposeAsync().ConfigureAwait(false);
        }
    }
}
