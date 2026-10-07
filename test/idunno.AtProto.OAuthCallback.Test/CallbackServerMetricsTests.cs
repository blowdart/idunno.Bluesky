// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;
using System.Net;

using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace idunno.AtProto.OAuthCallback.Test;

[Collection("CallbackServer")]
public class CallbackServerMetricsTests
{
    [Fact]
    public async Task CallbackServerMetricsCanBeRegisteredAndReportCallbackOutcomes()
    {
        using RecordingMetricExporter exporter = new();
        using PeriodicExportingMetricReader reader = new(exporter);
        using MeterProvider provider = Sdk.CreateMeterProviderBuilder()
            .AddAtProtoOAuthCallbackMetrics()
            .AddReader(reader)
            .Build();

        await using CallbackServer server = await CallbackServerFactory.CreateAsync();
        Task<string> callback = server.WaitForCallbackAsync(
            timeoutInSeconds: 300,
            cancellationToken: TestContext.Current.CancellationToken);

        using HttpClient client = new();
        using HttpResponseMessage rejectedResponse = await client.GetAsync(server.Uri, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, rejectedResponse.StatusCode);

        using HttpResponseMessage callbackResponse = await client.GetAsync(
            new Uri($"{server.Uri}?code=abc&state=xyz"),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, callbackResponse.StatusCode);
        Assert.Equal("?code=abc&state=xyz", await callback);

        Assert.True(provider.ForceFlush());
        Assert.Contains("idunno.atproto.oauthcallback.callbacks.total", exporter.MetricNames);
        Assert.Contains("idunno.atproto.oauthcallback.callback.wait.duration", exporter.MetricNames);
        Assert.Contains("idunno.atproto.oauthcallback.requests.rejected.total", exporter.MetricNames);
    }

    private sealed class RecordingMetricExporter : BaseExporter<Metric>
    {
        public HashSet<string> MetricNames { get; } = [];

        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (Metric metric in batch)
            {
                MetricNames.Add(metric.Name);
            }

            return ExportResult.Success;
        }
    }
}
