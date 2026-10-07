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
    public async Task CallbackServerMetricsReportOutcomesAndRejectedRequests()
    {
        using RecordingMetricExporter exporter = new();
        using PeriodicExportingMetricReader reader = new(exporter);
        using MeterProvider provider = Sdk.CreateMeterProviderBuilder()
            .AddAtProtoOAuthCallbackMetrics()
            .AddReader(reader)
            .Build();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using HttpClient client = new();

        await using (CallbackServer server = await CallbackServerFactory.CreateAsync())
        {
            Task<string> callback = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken);

            using HttpResponseMessage rejected = await client.GetAsync(server.Uri, cancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

            using HttpResponseMessage completed = await client.GetAsync(
                new Uri($"{server.Uri}?code=abc&state=xyz"),
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
            Assert.Equal("?code=abc&state=xyz", await callback);
        }

        await using (CallbackServer server = await CallbackServerFactory.CreateAsync())
        {
            Task<string> callback = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken);
            using HttpResponseMessage response = await client.GetAsync(
                new Uri($"{server.Uri}?error=access_denied&state=xyz"),
                cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("?error=access_denied&state=xyz", await callback);
        }

        await using (CallbackServer server = await CallbackServerFactory.CreateAsync())
        {
            Task<string> callback = server.WaitForCallbackAsync(timeoutInSeconds: 1, cancellationToken);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await callback);
        }

        await using (CallbackServer server = await CallbackServerFactory.CreateAsync())
        using (CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            Task<string> callback = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellation.Token);
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await callback);
        }

        CallbackServer disposedServer = await CallbackServerFactory.CreateAsync();
        Task<string> disposedCallback = disposedServer.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken);
        await disposedServer.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await disposedCallback);

        await using (CallbackServer server = await CallbackServerFactory.CreateAsync())
        {
            Task<string> callback = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken);

            using HttpRequestMessage request = new(HttpMethod.Post, server.Uri);
            using HttpResponseMessage rejected = await client.SendAsync(request, cancellationToken);
            Assert.Equal(HttpStatusCode.MethodNotAllowed, rejected.StatusCode);

            using HttpResponseMessage completed = await client.GetAsync(new Uri($"{server.Uri}?code=method&state=xyz"), cancellationToken);
            Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
            _ = await callback;
        }

        Assert.True(provider.ForceFlush());

        AssertCounter(exporter, "idunno.atproto.oauthcallback.callbacks.total", "outcome", "success");
        AssertCounter(exporter, "idunno.atproto.oauthcallback.callbacks.total", "outcome", "oauth_error");
        AssertCounter(exporter, "idunno.atproto.oauthcallback.callbacks.total", "outcome", "timeout");
        AssertCounter(exporter, "idunno.atproto.oauthcallback.callbacks.total", "outcome", "cancelled");
        AssertCounter(exporter, "idunno.atproto.oauthcallback.callbacks.total", "outcome", "disposed");
        AssertCounter(exporter, "idunno.atproto.oauthcallback.requests.rejected.total", "reason", "bad_request");
        AssertCounter(exporter, "idunno.atproto.oauthcallback.requests.rejected.total", "reason", "method_not_allowed");

        AssertHistogram(exporter, "success");
        AssertHistogram(exporter, "oauth_error");
        AssertHistogram(exporter, "timeout");
        AssertHistogram(exporter, "cancelled");
        AssertHistogram(exporter, "disposed");
    }

    private static void AssertCounter(RecordingMetricExporter exporter, string metricName, string tagName, string tagValue)
    {
        RecordedMetric metric = Assert.Single(exporter.Measurements, measurement =>
            measurement.Name == metricName &&
            measurement.Tags.TryGetValue(tagName, out object? value) &&
            Equals(value, tagValue));

        Assert.True(metric.Sum is > 0);
    }

    private static void AssertHistogram(RecordingMetricExporter exporter, string outcome)
    {
        RecordedMetric metric = Assert.Single(exporter.Measurements, measurement =>
            measurement.Name == "idunno.atproto.oauthcallback.callback.wait.duration" &&
            measurement.Tags.TryGetValue("outcome", out object? value) &&
            Equals(value, outcome));

        Assert.True(metric.Count is > 0);
    }

    private sealed record RecordedMetric(
        string Name,
        long? Sum,
        long? Count,
        IReadOnlyDictionary<string, object?> Tags);

    private sealed class RecordingMetricExporter : BaseExporter<Metric>
    {
        public List<RecordedMetric> Measurements { get; } = [];

        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (Metric metric in batch)
            {
                foreach (ref readonly MetricPoint point in metric.GetMetricPoints())
                {
                    Dictionary<string, object?> tags = [];

                    foreach (KeyValuePair<string, object?> tag in point.Tags)
                    {
                        tags[tag.Key] = tag.Value;
                    }

                    long? sum = metric.MetricType == MetricType.LongSum ? point.GetSumLong() : null;
                    long? count = metric.MetricType == MetricType.Histogram ? point.GetHistogramCount() : null;
                    Measurements.Add(new RecordedMetric(metric.Name, sum, count, tags));
                }
            }

            return ExportResult.Success;
        }
    }
}
