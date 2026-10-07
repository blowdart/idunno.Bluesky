// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Reflection;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace idunno.AtProto.OAuthCallback.Test;

[Collection("CallbackServer")]
public class CallbackServerMetricsTests
{
    [Theory]
    [InlineData("idunno.atproto.oauthcallback.callbacks.total", false)]
    [InlineData("idunno.atproto.oauthcallback.callbacks.total", true)]
    [InlineData("idunno.atproto.oauthcallback.callback.wait.duration", false)]
    [InlineData("idunno.atproto.oauthcallback.callback.wait.duration", true)]
    [InlineData("idunno.atproto.oauthcallback.requests.rejected.total", false)]
    [InlineData("idunno.atproto.oauthcallback.requests.rejected.total", true)]
    public async Task MetricListenerFailuresDoNotInterruptResponsesOrDisposal(string instrumentName, bool disposeWhileWaiting)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        InvalidOperationException failure = new("Deliberate metric listener failure.");
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == CallbackServerMetrics.MeterName && instrument.Name == instrumentName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) => throw failure);
        listener.SetMeasurementEventCallback<double>((_, _, _, _) => throw failure);
        listener.Start();
        using MetricFailureLoggerProvider logProvider = new();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logProvider));
        await using CallbackServer server = await CallbackServerFactory.CreateAsync(loggerFactory: loggerFactory);
        WebApplication application = (WebApplication)typeof(CallbackServer)
            .GetField("_listener", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(server)!;
        IHostApplicationLifetime lifetime = application.Services.GetRequiredService<IHostApplicationLifetime>();
        Task<string> callback = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken);
        using HttpClient client = new();

        using HttpResponseMessage badRequest = await client.GetAsync(server.Uri, cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, badRequest.StatusCode);
        Assert.Equal("<h1>Invalid request.</h1>", await badRequest.Content.ReadAsStringAsync(cancellationToken));
        using HttpResponseMessage wrongMethod = await client.PostAsync(server.Uri, null, cancellationToken);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, wrongMethod.StatusCode);
        Assert.Equal("<h1>Method Not Allowed.</h1>", await wrongMethod.Content.ReadAsStringAsync(cancellationToken));

        if (disposeWhileWaiting)
        {
            await server.DisposeAsync();
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await callback);
        }
        else
        {
            using HttpResponseMessage response = await client.GetAsync(new Uri($"{server.Uri}?code=abc&state=xyz"), cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("?code=abc&state=xyz", await callback.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken));
            await server.DisposeAsync();
        }

        Assert.True(lifetime.ApplicationStopped.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => application.Services.GetRequiredService<IHostApplicationLifetime>());
        Assert.NotEmpty(logProvider.Failures);
        Assert.All(logProvider.Failures, exception => Assert.Same(failure, exception));
    }

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

        CallbackServer? faultedServer = null;
        Task<string>? faultedCallback = null;
        InvalidOperationException listenerFailure = new("Deliberate listener startup failure.");
        using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(new ListenerFailureLoggerProvider(() =>
        {
            Assert.NotNull(faultedServer);
            faultedCallback = faultedServer.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken);
            throw listenerFailure;
        })));

        AggregateException startupFailure = await Assert.ThrowsAsync<AggregateException>(() =>
            CallbackServerFactory.CreateAsync(loggerFactory: loggerFactory, configure: server => faultedServer = server));
        Assert.Same(listenerFailure, Assert.Single(startupFailure.InnerExceptions));
        Assert.NotNull(faultedCallback);
        Assert.Same(startupFailure, await Assert.ThrowsAsync<AggregateException>(async () => await faultedCallback));

        Assert.True(provider.ForceFlush());

        AssertCounter(exporter, "idunno.atproto.oauthcallback.callbacks.total", "outcome", "success");
        AssertCounter(exporter, "idunno.atproto.oauthcallback.callbacks.total", "outcome", "oauth_error");
        AssertCounter(exporter, "idunno.atproto.oauthcallback.callbacks.total", "outcome", "timeout");
        AssertCounter(exporter, "idunno.atproto.oauthcallback.callbacks.total", "outcome", "cancelled");
        AssertCounter(exporter, "idunno.atproto.oauthcallback.callbacks.total", "outcome", "disposed");
        AssertCounter(exporter, "idunno.atproto.oauthcallback.callbacks.total", "outcome", "listener_error");
        AssertCounter(exporter, "idunno.atproto.oauthcallback.requests.rejected.total", "reason", "bad_request");
        AssertCounter(exporter, "idunno.atproto.oauthcallback.requests.rejected.total", "reason", "method_not_allowed");

        AssertHistogram(exporter, "success");
        AssertHistogram(exporter, "oauth_error");
        AssertHistogram(exporter, "timeout");
        AssertHistogram(exporter, "cancelled");
        AssertHistogram(exporter, "disposed");
        AssertHistogram(exporter, "listener_error");
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

    private sealed class ListenerFailureLoggerProvider(Action onListening) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new ListenerFailureLogger(categoryName, onListening);

        public void Dispose()
        {
        }

        private sealed class ListenerFailureLogger(string categoryName, Action onListening) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (categoryName == typeof(CallbackServer).FullName && eventId.Id == 1)
                {
                    onListening();
                }
            }
        }
    }

    private sealed class MetricFailureLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<Exception?> Failures { get; } = new();

        public ILogger CreateLogger(string categoryName) => new MetricFailureLogger(this);

        public void Dispose()
        {
        }

        private sealed class MetricFailureLogger(MetricFailureLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (eventId.Id == 16 && logLevel == LogLevel.Warning)
                {
                    provider.Failures.Enqueue(exception);
                }
            }
        }
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
