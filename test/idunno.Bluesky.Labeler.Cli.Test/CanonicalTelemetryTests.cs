// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;
using System.Net;
using System.Text;

using idunno.Bluesky.Labeler.Host;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenTelemetry.Metrics;

namespace idunno.Bluesky.Labeler.Cli.Test;

/// <summary>Verifies opt-in export, listener isolation and the canonical host's collected meters.</summary>
public class CanonicalTelemetryTests
{
    [Fact]
    public void ExportersAreDisabledByDefault()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        CanonicalLabelerTelemetry.AddServices(builder);
        Assert.DoesNotContain(builder.Services, descriptor => descriptor.ServiceType == typeof(MeterProvider));
        Assert.DoesNotContain(builder.Services, descriptor => descriptor.ServiceType == typeof(PrometheusListenerSettings));
    }

    [Theory]
    [InlineData("Prometheus:Address", "localhost")]
    [InlineData("Prometheus:Port", "0")]
    [InlineData("Prometheus:Port", "65536")]
    [InlineData("Otlp:Endpoint", "file:///tmp/metrics")]
    [InlineData("Otlp:Endpoint", "https://user:password@collector.example")]
    [InlineData("Otlp:Protocol", "http/json")]
    public void EnabledExportersRejectInvalidConfiguration(string key, string value)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Configuration["LabelerTelemetry:Prometheus:Enabled"] = "true";
        builder.Configuration["LabelerTelemetry:Otlp:Enabled"] = "true";
        builder.Configuration["LabelerTelemetry:" + key] = value;
        Assert.Throws<InvalidOperationException>(() => CanonicalLabelerTelemetry.AddServices(builder));
    }

    [Fact]
    public async Task PrometheusServesAllCanonicalMetersOnlyOnItsDedicatedListener()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        WebApplicationBuilder builder = CreateBuilder();
        builder.Configuration["LabelerTelemetry:Prometheus:Enabled"] = "true";
        CanonicalLabelerTelemetry.AddServices(builder);
        builder.Services.AddSingleton(new PrometheusListenerSettings(IPAddress.Loopback, 0));
        await using WebApplication app = builder.Build();
        app.MapGet("/", () => "public");
        await app.StartAsync(token);
        PrometheusListener listener = Assert.Single(app.Services.GetServices<IHostedService>().OfType<PrometheusListener>());
        using HttpClient scrape = new() { BaseAddress = Address(listener.Application) };
        using HttpClient publicClient = new() { BaseAddress = Address(app) };
        IMeterFactory factory = app.Services.GetRequiredService<IMeterFactory>();
        using MetricCollector<double> requestDuration = new(factory, "Microsoft.AspNetCore.Hosting", "http.server.request.duration");
        using HttpResponseMessage publicMetrics = await publicClient.GetAsync("/metrics", token);
        Assert.Equal(HttpStatusCode.NotFound, publicMetrics.StatusCode);
        using HttpResponseMessage privateRoot = await scrape.GetAsync("/", token);
        Assert.Equal(HttpStatusCode.NotFound, privateRoot.StatusCode);
        using HttpResponseMessage privateReadiness = await scrape.GetAsync("/health/ready", token);
        Assert.Equal(HttpStatusCode.NotFound, privateReadiness.StatusCode);
        // Kestrel can flush the response before request completion records the duration.
        using CancellationTokenSource watchdog = CancellationTokenSource.CreateLinkedTokenSource(token);
        watchdog.CancelAfter(TimeSpan.FromSeconds(30));
        await requestDuration.WaitForMeasurementsAsync(1, watchdog.Token);
        Assert.NotEmpty(requestDuration.GetMeasurementSnapshot());

        string[] meters =
        [
            LabelPersistenceMetrics.MeterName,
            "idunno.Bluesky.AspNet.Labeler",
            "idunno.Bluesky.AspNet.Labeler.Moderation",
            "idunno.Bluesky.AspNet.Authentication",
            "idunno.AtProto.AtProtoHttpClient",
            "idunno.AtProto.Directory",
            "idunno.AtProto.DidHandleCache"
        ];
        for (int index = 0; index < meters.Length; index++)
        {
            Meter meter = factory.Create(meters[index]);
            using MetricCollector<long> measurements = new(factory, meters[index], $"canonical_export_{index}");
            meter.CreateCounter<long>($"canonical_export_{index}").Add(index + 1);
            Assert.Equal(index + 1, Assert.Single(measurements.GetMeasurementSnapshot()).Value);
        }
        using HttpResponseMessage metrics = await scrape.GetAsync("/metrics", token);
        Assert.Equal(HttpStatusCode.OK, metrics.StatusCode);
        Assert.Equal("text/plain", metrics.Content.Headers.ContentType?.MediaType);
        string body = await metrics.Content.ReadAsStringAsync(token);
        for (int index = 0; index < meters.Length; index++)
        {
            Assert.Contains($"canonical_export_{index}_total", body, StringComparison.Ordinal);
        }
        Assert.Contains("http_server_request_duration", body, StringComparison.Ordinal);
        await app.StopAsync(token);
        await Assert.ThrowsAsync<HttpRequestException>(() => scrape.GetAsync("/metrics", token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OtlpExportsProtobufWithOrWithoutPrometheus(bool prometheus)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TaskCompletionSource<byte[]> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        WebApplicationBuilder collectorBuilder = CreateBuilder();
        await using WebApplication collector = collectorBuilder.Build();
        collector.MapPost("/v1/metrics", async (HttpContext context) =>
        {
            Assert.Equal("application/x-protobuf", context.Request.ContentType);
            using MemoryStream payload = new();
            await context.Request.Body.CopyToAsync(payload, context.RequestAborted);
            received.TrySetResult(payload.ToArray());
            return Results.Bytes([], "application/x-protobuf");
        });
        await collector.StartAsync(token);

        WebApplicationBuilder builder = CreateBuilder();
        builder.Configuration["LabelerTelemetry:Otlp:Enabled"] = "true";
        builder.Configuration["LabelerTelemetry:Otlp:Endpoint"] = new Uri(Address(collector), "/v1/metrics").AbsoluteUri;
        builder.Configuration["LabelerTelemetry:Otlp:Protocol"] = "http/protobuf";
        builder.Configuration["LabelerTelemetry:Prometheus:Enabled"] = prometheus.ToString();
        CanonicalLabelerTelemetry.AddServices(builder);
        if (prometheus)
        {
            builder.Services.AddSingleton(new PrometheusListenerSettings(IPAddress.Loopback, 0));
        }
        builder.Configuration["LabelerTelemetry:Otlp:Endpoint"] = "file:///changed";
        builder.Configuration["LabelerTelemetry:Otlp:Protocol"] = "invalid";
        await using WebApplication app = builder.Build();
        await app.StartAsync(token);
        Assert.Equal(prometheus ? 1 : 0, app.Services.GetServices<IHostedService>().OfType<PrometheusListener>().Count());
        IMeterFactory factory = app.Services.GetRequiredService<IMeterFactory>();
        Meter meter = factory.Create(LabelPersistenceMetrics.MeterName);
        using MetricCollector<long> measurements = new(factory, LabelPersistenceMetrics.MeterName, "canonical_otlp_export");
        meter.CreateCounter<long>("canonical_otlp_export").Add(1);
        Assert.Equal(1, Assert.Single(measurements.GetMeasurementSnapshot()).Value);
        // Pull readers do not flush; verify the actual OTLP payload rather than the aggregate flush result.
        app.Services.GetRequiredService<MeterProvider>().ForceFlush();
        byte[] body = await received.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
        Assert.Contains("canonical_otlp_export", Encoding.UTF8.GetString(body), StringComparison.Ordinal);
        await app.StopAsync(token);
        await collector.StopAsync(token);
    }

    [Fact]
    public async Task OccupiedScrapePortFailsParentStartup()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        WebApplicationBuilder occupiedBuilder = CreateBuilder();
        await using WebApplication occupied = occupiedBuilder.Build();
        await occupied.StartAsync(token);
        WebApplicationBuilder builder = CreateBuilder();
        builder.Configuration["LabelerTelemetry:Prometheus:Enabled"] = "true";
        builder.Configuration["LabelerTelemetry:Prometheus:Port"] = Address(occupied).Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        CanonicalLabelerTelemetry.AddServices(builder);
        await using WebApplication app = builder.Build();
        await Assert.ThrowsAnyAsync<IOException>(() => app.StartAsync(token));
        await occupied.StopAsync(token);
    }

    /// <summary>Creates an isolated loopback application with an operating-system-assigned port.</summary>
    private static WebApplicationBuilder CreateBuilder()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        return builder;
    }

    /// <summary>Returns the actual bound address after server startup.</summary>
    private static Uri Address(WebApplication app) =>
        new(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
}
