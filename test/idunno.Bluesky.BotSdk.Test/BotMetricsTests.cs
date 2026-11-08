// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;
using System.Net;
using System.Reflection;

using idunno.AtProto.Jetstream.Archive;
using idunno.Bluesky.BotSdk.DataProviders;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace idunno.Bluesky.BotSdk.Test;

/// <summary>Verifies runtime metric contracts and persistence-sensitive emission boundaries.</summary>
public sealed class BotMetricsTests
{
    /// <summary>Checks the exact counter names, types and units used by collectors.</summary>
    /// <param name="property">The instrument property.</param>
    /// <param name="suffix">The published suffix.</param>
    /// <param name="unit">The instrument unit.</param>
    [Theory]
    [InlineData(nameof(BotMetrics.EventsHandled), "events.handled", "{events}")]
    [InlineData(nameof(BotMetrics.EventsDuplicates), "events.duplicates", "{events}")]
    [InlineData(nameof(BotMetrics.ActionsEnqueued), "actions.enqueued", "{actions}")]
    [InlineData(nameof(BotMetrics.ActionsCompleted), "actions.completed", "{actions}")]
    [InlineData(nameof(BotMetrics.RetriesScheduled), "actions.retries.scheduled", "{retries}")]
    [InlineData(nameof(BotMetrics.RateDeferrals), "rate_limit.deferrals", "{deferrals}")]
    [InlineData(nameof(BotMetrics.OwnershipTransitions), "ownership.transitions", "{transitions}")]
    [InlineData(nameof(BotMetrics.RecoveryEpisodes), "worker.recovery.episodes", "{episodes}")]
    [InlineData(nameof(BotMetrics.WorkerFailures), "worker.failures", "{failures}")]
    public void CounterContracts(string property, string suffix, string unit)
    {
        using Fixture fixture = new();
        using MetricCollector<long> collector = fixture.LongCollector(suffix);
        Counter<long> instrument = Assert.IsType<Counter<long>>(Property(property).GetValue(fixture.Metrics));
        Assert.Equal(BotMetrics.Prefix + suffix, instrument.Name);
        Assert.Equal(unit, instrument.Unit);
        instrument.Add(2);
        Assert.Equal(2, Assert.Single(collector.GetMeasurementSnapshot()).Value);
    }

    /// <summary>Checks histogram names, units, monotonic elapsed time and outcomes.</summary>
    /// <param name="property">The instrument property.</param>
    /// <param name="suffix">The published suffix.</param>
    [Theory]
    [InlineData(nameof(BotMetrics.EventDuration), "event.handling.duration")]
    [InlineData(nameof(BotMetrics.DispatchDuration), "action.dispatch.duration")]
    [InlineData(nameof(BotMetrics.ReconciliationDuration), "action.reconciliation.duration")]
    [InlineData(nameof(BotMetrics.RetryDelay), "action.retry.delay")]
    [InlineData(nameof(BotMetrics.RateWaitDuration), "rate_limit.wait.duration")]
    [InlineData(nameof(BotMetrics.ActionLatency), "action.latency")]
    public void HistogramContracts(string property, string suffix)
    {
        using Fixture fixture = new();
        using MetricCollector<double> collector = fixture.DoubleCollector(suffix);
        Histogram<double> instrument = Assert.IsType<Histogram<double>>(Property(property).GetValue(fixture.Metrics));
        Assert.Equal(BotMetrics.Prefix + suffix, instrument.Name);
        Assert.Equal("s", instrument.Unit);
        long started = fixture.Metrics.Timestamp;
        fixture.Time.Advance(TimeSpan.FromSeconds(3));
        fixture.Metrics.RecordDuration(instrument, started, "success");
        CollectedMeasurement<double> measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal(3, measurement.Value);
        Assert.Equal("success", measurement.Tags["outcome"]);
        Assert.Single(measurement.Tags);
    }

    /// <summary>Counts only actual ownership transitions and separates normal shutdown.</summary>
    [Fact]
    public void OwnershipRenewalsDoNotCountAsTransitions()
    {
        using Fixture fixture = new();
        using MetricCollector<long> collector = fixture.LongCollector("ownership.transitions");
        fixture.Ownership.Suspend();
        fixture.Ownership.Activate();
        fixture.Ownership.Activate();
        fixture.Ownership.Suspend();
        fixture.Ownership.Suspend();
        fixture.Ownership.Activate();
        fixture.Ownership.Suspend("shutdown");
        Assert.Equal(
            ["activated", "lost", "activated", "shutdown"],
            collector.GetMeasurementSnapshot().Select(item => item.Tags["transition"]));
    }

    /// <summary>Counts recovery episodes rather than repeated attempts and excludes ownership standby.</summary>
    [Fact]
    public void RecoveryRequiresSuccessfulResumptionBeforeAnotherEpisode()
    {
        using Fixture fixture = new();
        using MetricCollector<long> collector = fixture.LongCollector("worker.recovery.episodes");
        fixture.Health.Set("sender", "standby", TimeSpan.FromSeconds(10), "database/ownership");
        Assert.Empty(collector.GetMeasurementSnapshot());
        fixture.Health.Set("sender", "retry", TimeSpan.FromSeconds(10), "database");
        fixture.Health.Set("sender", "attempt", TimeSpan.FromSeconds(10));
        fixture.Health.Set("sender", "retry", TimeSpan.FromSeconds(10), "database");
        Assert.Single(collector.GetMeasurementSnapshot());
        Assert.True(fixture.Health.IsRecovering("sender"));
        fixture.Health.Recovered("sender");
        fixture.Health.Set("sender", "retry", TimeSpan.FromSeconds(10), "database");
        Assert.Equal(2, collector.GetMeasurementSnapshot().Count);
    }

    /// <summary>Isolates hosts with separate meter factories rather than adding per-action identity tags.</summary>
    [Fact]
    public void HostMeterFactoriesRemainIsolated()
    {
        using Fixture first = new();
        using Fixture second = new();
        using MetricCollector<long> firstCollector = first.LongCollector("actions.completed");
        using MetricCollector<long> secondCollector = second.LongCollector("actions.completed");
        first.Metrics.ActionsCompleted.Add(1, BotMetrics.Tag("status", "sent"));
        second.Metrics.ActionsCompleted.Add(2, BotMetrics.Tag("status", "simulated"));
        Assert.Equal(1, Assert.Single(firstCollector.GetMeasurementSnapshot()).Value);
        Assert.Equal(2, Assert.Single(secondCollector.GetMeasurementSnapshot()).Value);
    }

    /// <summary>Publishes deadline-based health and unrecoverable failures without treating standby as recovery.</summary>
    [Fact]
    public void WorkerGaugesUseDeadlinesAndFailuresCountOnce()
    {
        using Fixture fixture = new();
        using MetricCollector<int> healthy = new(fixture.Factory, BotMetrics.MeterName, BotMetrics.Prefix + "worker.healthy");
        using MetricCollector<int> recovering = new(fixture.Factory, BotMetrics.MeterName, BotMetrics.Prefix + "worker.recovering");
        using MetricCollector<long> failures = fixture.LongCollector("worker.failures");
        fixture.Health.Set("sender", "standby", TimeSpan.FromSeconds(10), "database/ownership");
        fixture.Time.Advance(TimeSpan.FromSeconds(11));
        healthy.RecordObservableInstruments();
        recovering.RecordObservableInstruments();
        Assert.Equal(0, healthy.GetMeasurementSnapshot().Last(item => (string?)item.Tags["worker"] == "sender").Value);
        Assert.Equal(0, recovering.GetMeasurementSnapshot().Last(item => (string?)item.Tags["worker"] == "sender").Value);
        fixture.Health.Fail("sender");
        fixture.Health.Fail("sender");
        Assert.Equal("sender", Assert.Single(failures.GetMeasurementSnapshot()).Tags["worker"]);
    }

    /// <summary>Reports unknown observations as absent, preserves clock skew, and resets on new ownership.</summary>
    [Fact]
    public void ObservableProgressUsesKnownEventsAndMonotonicAge()
    {
        using Fixture fixture = new();
        using MetricCollector<double> witnessed = fixture.DoubleCollector("replay.witnessed.age");
        using MetricCollector<double> observed = fixture.DoubleCollector("replay.observation.age");
        witnessed.RecordObservableInstruments();
        observed.RecordObservableInstruments();
        Assert.Empty(witnessed.GetMeasurementSnapshot());
        Assert.Empty(observed.GetMeasurementSnapshot());
        fixture.Progress.Observe(11, fixture.Time.GetUtcNow().AddSeconds(10), fixture.Time.GetUtcNow());
        fixture.Time.Advance(TimeSpan.FromSeconds(3));
        witnessed.RecordObservableInstruments();
        observed.RecordObservableInstruments();
        Assert.Equal(-7, witnessed.GetMeasurementSnapshot()[^1].Value);
        Assert.Equal(3, observed.GetMeasurementSnapshot()[^1].Value);
        using CancellationTokenSource ownership = new();
        fixture.Progress.BeginOwnership(ownership.Token);
        Assert.Null(fixture.Progress.GetObservedTimestamp());
        int before = observed.GetMeasurementSnapshot().Count;
        observed.RecordObservableInstruments();
        Assert.Equal(before, observed.GetMeasurementSnapshot().Count);
    }

    /// <summary>Does not leave observable callbacks or storage subscriptions active after host disposal.</summary>
    [Fact]
    public void HostDisposalStopsObservationsAndStorageTiming()
    {
        using Fixture fixture = new();
        using MetricCollector<double> collector = fixture.DoubleCollector("action.latency");
        using MetricCollector<int> available = new(fixture.Factory, BotMetrics.MeterName, BotMetrics.Prefix + "replay.observation.available");
        int priorObservations = available.GetMeasurementSnapshot().Count;
        fixture.Dispose();
        ((IBotActionTimingSource)fixture.Database).Timing.Record("sent", TestRecords.Start, TestRecords.Start.AddSeconds(4));
        available.RecordObservableInstruments();
        Assert.Empty(collector.GetMeasurementSnapshot());
        Assert.Equal(priorObservations, available.GetMeasurementSnapshot().Count);
    }

    /// <summary>Removes event subscriptions without claiming ownership of factory-managed observable lifetime.</summary>
    [Fact]
    public void MetricsDisposalUnsubscribesBeforeFactoryDisposal()
    {
        using Fixture fixture = new();
        using MetricCollector<double> latency = fixture.DoubleCollector("action.latency");
        using MetricCollector<long> transitions = fixture.LongCollector("ownership.transitions");
        using MetricCollector<long> recoveries = fixture.LongCollector("worker.recovery.episodes");
        using MetricCollector<long> failures = fixture.LongCollector("worker.failures");
        using MetricCollector<int> available = new(fixture.Factory, BotMetrics.MeterName, BotMetrics.Prefix + "replay.observation.available");
        fixture.Metrics.Dispose();
        ((IBotActionTimingSource)fixture.Database).Timing.Record("sent", TestRecords.Start, TestRecords.Start.AddSeconds(4));
        fixture.Ownership.Activate();
        fixture.Health.Set("sender", "recovering", TimeSpan.FromSeconds(10), "database");
        fixture.Health.Fail("sender");
        Assert.Empty(latency.GetMeasurementSnapshot());
        Assert.Empty(transitions.GetMeasurementSnapshot());
        Assert.Empty(recoveries.GetMeasurementSnapshot());
        Assert.Empty(failures.GetMeasurementSnapshot());
        available.RecordObservableInstruments();
        Assert.Equal(0, Assert.Single(available.GetMeasurementSnapshot()).Value);
    }

    /// <summary>Counts simulated completions and latency only for a newly committed terminal row.</summary>
    [Fact]
    public async Task SimulationAndNoOpCompletionHaveDistinctMetrics()
    {
        using Fixture fixture = new();
        await fixture.InitializeAsync();
        using MetricCollector<long> completed = fixture.LongCollector("actions.completed");
        using MetricCollector<double> latency = fixture.DoubleCollector("action.latency");
        fixture.Options.DeveloperMode = true;
        BotAction action = TestRecords.Work();
        await fixture.Database.HandleAsync(11, [action], null, CancellationToken.None);
        fixture.Time.Advance(TimeSpan.FromSeconds(5));
        ActionDispatcher dispatcher = new(
            database: fixture.Database,
            options: fixture.Options,
            logger: NullLogger<ActionDispatcher>.Instance,
            statistics: new(TestRecords.Start),
            handlers: TestRecords.Handlers(),
            metrics: fixture.Metrics);
        await dispatcher.SendAsync(action, null, CancellationToken.None);
        await dispatcher.SendAsync(action, null, CancellationToken.None);
        Assert.Equal("simulated", Assert.Single(completed.GetMeasurementSnapshot()).Tags["status"]);
        Assert.Equal(5, Assert.Single(latency.GetMeasurementSnapshot()).Value);
    }

    /// <summary>Records ownership failure without publishing a completion when the terminal update is fenced out.</summary>
    [Fact]
    public async Task FailedTerminalPersistenceDoesNotCountCompletion()
    {
        using Fixture fixture = new();
        await fixture.InitializeAsync();
        using MetricCollector<long> completed = fixture.LongCollector("actions.completed");
        using MetricCollector<double> latency = fixture.DoubleCollector("action.latency");
        BotAction action = TestRecords.Work();
        await fixture.Database.HandleAsync(11, [action], null, CancellationToken.None);
        fixture.Time.Advance(TimeSpan.FromMinutes(2));
        fixture.Options.DeveloperMode = true;
        ActionDispatcher dispatcher = new(
            database: fixture.Database,
            options: fixture.Options,
            logger: NullLogger<ActionDispatcher>.Instance,
            statistics: new(TestRecords.Start),
            handlers: TestRecords.Handlers(),
            metrics: fixture.Metrics);
        await Assert.ThrowsAsync<OwnershipLostException>(() => dispatcher.SendAsync(action, null, CancellationToken.None));
        Assert.Empty(completed.GetMeasurementSnapshot());
        Assert.Empty(latency.GetMeasurementSnapshot());
    }

    /// <summary>Records retries only after successful persistence and never reports negative scheduled delays.</summary>
    /// <param name="state">The storage state at retry persistence.</param>
    /// <param name="delay">The requested delay in seconds.</param>
    [Theory]
    [InlineData("unfinished", 15)]
    [InlineData("unfinished", -3)]
    [InlineData("terminal", 15)]
    [InlineData("fenced", 15)]
    public async Task RetryMetricsFollowPersistedSchedules(string state, int delay)
    {
        using Fixture fixture = new();
        await fixture.InitializeAsync();
        BotAction action = TestRecords.Work();
        await fixture.Database.HandleAsync(11, [action], null, CancellationToken.None);
        if (state == "terminal")
        {
            await fixture.Database.FinishAsync(action, "not-sent", null, "rejected", CancellationToken.None);
        }
        else if (state == "fenced")
        {
            fixture.Time.Advance(TimeSpan.FromMinutes(2));
        }
        using MetricCollector<long> retries = fixture.LongCollector("actions.retries.scheduled");
        using MetricCollector<double> delays = fixture.DoubleCollector("action.retry.delay");
        using SenderWorker sender = fixture.CreateSender();
        await sender.ScheduleRetryAsync(
            work: action,
            retryAt: fixture.Time.GetUtcNow().AddSeconds(delay),
            reason: "transport timeout",
            cancellationToken: CancellationToken.None,
            classification: "transport");
        if (state == "unfinished")
        {
            Assert.Equal("transport", Assert.Single(retries.GetMeasurementSnapshot()).Tags["reason"]);
            Assert.Equal(Math.Max(0, delay), Assert.Single(delays.GetMeasurementSnapshot()).Value);
        }
        else
        {
            Assert.Empty(retries.GetMeasurementSnapshot());
            Assert.Empty(delays.GetMeasurementSnapshot());
        }
    }

    /// <summary>Measures elapsed rate backoff, including partial cancellation, after synchronous timer registration.</summary>
    /// <param name="rateLimit">Whether the wait is rate-limit backoff rather than ordinary idleness.</param>
    /// <param name="cancel">Whether cancellation ends the registered wait.</param>
    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task SenderWaitMeasuresActualElapsedTime(bool rateLimit, bool cancel)
    {
        using Fixture fixture = new();
        using MetricCollector<double> waits = fixture.DoubleCollector("rate_limit.wait.duration");
        using SenderWorker sender = fixture.CreateSender();
        using CancellationTokenSource cancellation = new();
        string phase = rateLimit ? "rate-limit wait" : "idle / scheduled work";
        Task wait = sender.WaitAsync(
            phase: phase,
            delay: TimeSpan.FromSeconds(5),
            dependency: null,
            cancellationToken: cancellation.Token);
        Assert.Equal(phase, fixture.Health.Snapshot().Workers["sender"].Phase);
        Assert.False(wait.IsCompleted);
        fixture.Time.Advance(TimeSpan.FromSeconds(cancel ? 2 : 5));
        if (cancel)
        {
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        }
        else
        {
            await wait;
        }
        if (rateLimit)
        {
            CollectedMeasurement<double> measurement = Assert.Single(waits.GetMeasurementSnapshot());
            Assert.Equal(cancel ? 2 : 5, measurement.Value);
            Assert.Equal(cancel ? "cancelled" : "success", measurement.Tags["outcome"]);
            Assert.Equal("sender", measurement.Tags["location"]);
            Assert.Equal(2, measurement.Tags.Count);
        }
        else
        {
            Assert.Empty(waits.GetMeasurementSnapshot());
        }
    }

    /// <summary>Counts HTTP 429 independently of cooldown persistence and then counts durable budget admission.</summary>
    /// <param name="loseOwnership">Whether the response arrives after the storage fence expires.</param>
    /// <param name="shortWait">Whether admission first waits on a registered fake-clock timer.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Http429AndBudgetDeferralsUseRealTransport(bool loseOwnership, bool shortWait)
    {
        using Fixture fixture = new();
        await fixture.InitializeAsync();
        using MetricCollector<long> deferrals = fixture.LongCollector("rate_limit.deferrals");
        using MetricCollector<double> waits = fixture.DoubleCollector("rate_limit.wait.duration");
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using WebApplication app = builder.Build();
        int requests = 0;
        app.MapGet("/xrpc/com.atproto.repo.getRecord", (HttpContext context) =>
        {
            requests++;
            if (loseOwnership)
            {
                fixture.Time.Advance(TimeSpan.FromMinutes(2));
            }
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        using OutboundHandler handler = new(
            database: fixture.Database,
            options: fixture.Options,
            time: fixture.Time,
            health: fixture.Health,
            logger: NullLogger<OutboundHandler>.Instance,
            metrics: fixture.Metrics)
        {
            InnerHandler = new HttpClientHandler()
        };
        using HttpClient client = new(handler);
        string endpoint = app.Urls.Single() + "/xrpc/com.atproto.repo.getRecord";
        if (shortWait)
        {
            await fixture.Database.CooldownAsync(fixture.Time.GetUtcNow().AddSeconds(3), CancellationToken.None);
        }
        Task<HttpResponseMessage> request = client.GetAsync(endpoint, TestContext.Current.CancellationToken);
        if (shortWait)
        {
            Assert.Equal("outbound pacing", fixture.Health.Snapshot().Workers["sender"].Phase);
            fixture.Time.Advance(TimeSpan.FromSeconds(3));
        }
        if (loseOwnership)
        {
            await Assert.ThrowsAsync<OwnershipLostException>(() => request);
        }
        else
        {
            await Assert.ThrowsAsync<RateWaitException>(() => request);
            await Assert.ThrowsAsync<RateWaitException>(() => client.GetAsync(endpoint, TestContext.Current.CancellationToken));
        }
        Assert.Equal(1, requests);
        string[] expected = shortWait ? ["budget", "http_429", "budget"] : loseOwnership ? ["http_429"] : ["http_429", "budget"];
        Assert.Equal(expected, deferrals.GetMeasurementSnapshot().Select(item => (string?)item.Tags["reason"]));
        Assert.All(deferrals.GetMeasurementSnapshot(), item => Assert.Single(item.Tags));
        if (shortWait)
        {
            CollectedMeasurement<double> measurement = Assert.Single(waits.GetMeasurementSnapshot());
            Assert.Equal(3, measurement.Value);
            Assert.Equal("success", measurement.Tags["outcome"]);
            Assert.Equal("outbound", measurement.Tags["location"]);
        }
        else
        {
            Assert.Empty(waits.GetMeasurementSnapshot());
        }
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Records only actual elapsed pacing when cancellation interrupts a short budget wait.</summary>
    [Fact]
    public async Task CancelledOutboundPacingRecordsPartialWait()
    {
        using Fixture fixture = new();
        await fixture.InitializeAsync();
        await fixture.Database.CooldownAsync(fixture.Time.GetUtcNow().AddSeconds(3), CancellationToken.None);
        using MetricCollector<double> waits = fixture.DoubleCollector("rate_limit.wait.duration");
        using OutboundHandler handler = new(
            database: fixture.Database,
            options: fixture.Options,
            time: fixture.Time,
            health: fixture.Health,
            logger: NullLogger<OutboundHandler>.Instance,
            metrics: fixture.Metrics)
        {
            InnerHandler = new HttpClientHandler()
        };
        using HttpClient client = new(handler);
        using CancellationTokenSource cancellation = new();
        Task<HttpResponseMessage> request = client.GetAsync("https://pds.example.test/xrpc/com.atproto.repo.getRecord", cancellation.Token);
        Assert.Equal("outbound pacing", fixture.Health.Snapshot().Workers["sender"].Phase);
        fixture.Time.Advance(TimeSpan.FromSeconds(2));
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        CollectedMeasurement<double> measurement = Assert.Single(waits.GetMeasurementSnapshot());
        Assert.Equal(2, measurement.Value);
        Assert.Equal("cancelled", measurement.Tags["outcome"]);
        Assert.Equal("outbound", measurement.Tags["location"]);
    }

    /// <summary>Finds an internal instrument property for contract verification.</summary>
    private static PropertyInfo Property(string name) =>
        typeof(BotMetrics).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>Owns isolated meters and a fake-clock storage provider without starting background workers.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider _services;
        internal FakeTimeProvider Time { get; } = new(TestRecords.Start);
        internal BotOptions Options { get; } = TestRecords.Options();
        internal Ownership Ownership { get; } = new();
        internal WorkerHealth Health { get; }
        internal BotReplayProgress Progress { get; }
        internal InMemoryDatabaseProvider Database { get; }
        internal IMeterFactory Factory { get; }
        internal BotMetrics Metrics { get; }

        /// <summary>Initializes a new instance of the <see cref="Fixture"/> class.</summary>
        internal Fixture()
        {
            ServiceCollection services = new();
            services.AddMetrics();
            services.AddHttpClient();
            _services = services.BuildServiceProvider();
            Factory = _services.GetRequiredService<IMeterFactory>();
            Health = new(Time);
            Progress = new(Time);
            Database = new(Options, Time, NullLogger<InMemoryDatabaseProvider>.Instance);
            Metrics = new(
                factory: Factory,
                time: Time,
                database: Database,
                ownership: Ownership,
                health: Health,
                progress: Progress);
        }

        /// <summary>Initializes storage and advances the deterministic ownership drain.</summary>
        internal async Task InitializeAsync()
        {
            await Database.InitializeAsync(CancellationToken.None);
            await Database.AcquireOrRenewAsync(CancellationToken.None);
            Time.Advance(TimeSpan.FromSeconds(40));
            await Database.LoadStateAsync(CancellationToken.None);
            await Database.SaveRequestAsync(new SnapshotRequest { AfterSeq = 10 }, CancellationToken.None);
        }

        /// <summary>Creates a counter collector scoped to this fixture's meter factory.</summary>
        internal MetricCollector<long> LongCollector(string suffix) => new(Factory, BotMetrics.MeterName, BotMetrics.Prefix + suffix);

        /// <summary>Creates a duration collector scoped to this fixture's meter factory.</summary>
        internal MetricCollector<double> DoubleCollector(string suffix) => new(Factory, BotMetrics.MeterName, BotMetrics.Prefix + suffix);

        /// <summary>Creates the sender with real storage, host services and instrument collectors.</summary>
        internal SenderWorker CreateSender() => new(
            database: Database,
            ownership: Ownership,
            options: Options,
            dispatcher: new(
                database: Database,
                options: Options,
                logger: NullLogger<ActionDispatcher>.Instance,
                statistics: new(TestRecords.Start),
                handlers: TestRecords.Handlers(),
                metrics: Metrics),
            health: Health,
            httpClientFactory: new(_services.GetRequiredService<IHttpClientFactory>()),
            vault: new(Options),
            loggerFactory: NullLoggerFactory.Instance,
            logger: NullLogger<SenderWorker>.Instance,
            time: Time,
            metrics: Metrics);

        /// <inheritdoc/>
        public void Dispose()
        {
            Metrics.Dispose();
            Ownership.Dispose();
            _services.Dispose();
        }
    }
}
