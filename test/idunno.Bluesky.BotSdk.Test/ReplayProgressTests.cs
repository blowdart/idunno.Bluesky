// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto.Jetstream;
using idunno.Bluesky.BotSdk.DataProviders;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace idunno.Bluesky.BotSdk.Test;

/// <summary>Checks handled replay observations independently of durability, logs, and liveness.</summary>
public sealed class ReplayProgressTests
{
    /// <summary>Registers one read-only progress service without constructing storage or starting workers.</summary>
    [Fact]
    public void RegistrationProvidesUnknownSingleton()
    {
        ServiceCollection services = new();
        services.AddBlueskyBot<TestPolicy>(TestRecords.Options(), "progress-test/1.0");
        using ServiceProvider provider = services.BuildServiceProvider();
        BotReplayProgress progress = provider.GetRequiredService<BotReplayProgress>();
        Assert.Same(progress, provider.GetRequiredService<BotReplayProgress>());
        AssertUnknown(progress.Snapshot);
    }

    /// <summary>Publishes exact metadata only after handled work, including ignored and audited malformed events.</summary>
    /// <param name="kind">The policy's handling outcome.</param>
    [Theory]
    [InlineData("action")]
    [InlineData("ignored")]
    [InlineData("json")]
    [InlineData("malformed")]
    [InlineData("delete")]
    public async Task SuccessfulHandlingPublishesExactObservation(string kind)
    {
        using ReaderFixture fixture = await ReaderFixture.CreateAsync();
        fixture.Policy.EvaluateEvent = _ => kind switch
        {
            "action" => [TestRecords.Work()],
            "json" => throw new JsonException("Malformed source JSON."),
            "malformed" => throw new MalformedEventException("Malformed source record."),
            _ => []
        };
        JetstreamCommitEvent item = TestRecords.Event();
        if (kind == "delete")
        {
            item = item with { Commit = item.Commit with { Operation = JetstreamCommitOperation.Delete } };
        }

        AssertUnknown(fixture.Observations.Snapshot);
        DateTimeOffset observedAt = fixture.Time.GetUtcNow();
        Assert.Equal(11, await fixture.HandleAsync(item));
        ReplayProgressSnapshot snapshot = fixture.Observations.Snapshot;
        AssertObservation(snapshot, 11, TestRecords.Start, observedAt);

        ReplayState durable = await fixture.Database.LoadStateAsync(CancellationToken.None);
        if (kind == "ignored")
        {
            Assert.Equal(10, durable.ProcessedThrough);
            Assert.Equal(10, fixture.Diagnostics.DurableThrough);
        }
        else
        {
            Assert.Equal(11, durable.ProcessedThrough);
            Assert.Equal(11, fixture.Diagnostics.DurableThrough);
        }

        fixture.Time.Advance(TimeSpan.FromSeconds(1));
        await fixture.HandleAsync(TestRecords.Event(sequence: 12));
        AssertObservation(snapshot, 11, TestRecords.Start, observedAt);
        Assert.Equal(12, fixture.Observations.Snapshot.Sequence);
    }

    /// <summary>Updates observations on every handled event while retaining the existing report throttle.</summary>
    /// <param name="loggingEnabled">Whether progress logs are enabled.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObservationsDoNotDependOnLogEmission(bool loggingEnabled)
    {
        FakeTimeProvider time = new(TestRecords.Start);
        DiagnosticLogger logger = new();
        BotReplayProgress observations = new();
        ReplayProgress progress = new(time, loggingEnabled ? logger : NullLogger.Instance, observations);
        for (int sequence = 1; sequence <= 10; sequence++)
        {
            time.Advance(TimeSpan.FromSeconds(1));
            progress.Observe(sequence, TestRecords.Start);
            AssertObservation(observations.Snapshot, sequence, TestRecords.Start, time.GetUtcNow());
        }

        Assert.Equal(loggingEnabled ? 1 : 0, logger.Entries.Count);
        time.Advance(TimeSpan.FromSeconds(6));
        progress.Observe(11, TestRecords.Start);
        Assert.Equal(loggingEnabled ? 2 : 0, logger.Entries.Count);
        AssertObservation(observations.Snapshot, 11, TestRecords.Start, time.GetUtcNow());
    }

    /// <summary>Preserves the thirty-second diagnostic boundary and explicit clock-skew reporting.</summary>
    [Fact]
    public void RetainsLagDiagnosticSemantics()
    {
        FakeTimeProvider time = new(TestRecords.Start);
        DiagnosticLogger logger = new();
        ReplayProgress progress = new(time, logger, new BotReplayProgress());
        progress.Observe(1, TestRecords.Start.AddSeconds(-31));
        progress.Observe(2, TestRecords.Start.AddSeconds(-30));
        progress.Observe(3, TestRecords.Start.AddSeconds(1));
        Assert.Contains("catching up", logger.Entries[0].Message);
        Assert.Contains("near current time (within 30 seconds)", logger.Entries[1].Message);
        Assert.Equal(2, logger.Entries.Count);
        time.Advance(TimeSpan.FromSeconds(15));
        progress.Observe(4, time.GetUtcNow().AddSeconds(1));
        Assert.Contains("ahead of the local clock", logger.Entries[2].Message);
    }

    /// <summary>Does not publish during policy execution, and only advances when execution succeeds.</summary>
    /// <param name="fails">Whether the in-flight policy fails.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InFlightAndFailedHandlingDoNotAdvance(bool fails)
    {
        using ReaderFixture fixture = await ReaderFixture.CreateAsync();
        await fixture.HandleAsync(TestRecords.Event());
        ReplayProgressSnapshot before = fixture.Observations.Snapshot;
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new();
        fixture.Policy.EvaluateEvent = _ =>
        {
            entered.SetResult();
            release.Wait(TestContext.Current.CancellationToken);
            if (fails)
            {
                throw new InvalidOperationException("Policy failed.");
            }

            return [];
        };

        Task<long> handling = Task.Run(
            () => fixture.HandleAsync(TestRecords.Event(sequence: 12), 11), TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            fixture.Time.Advance(TimeSpan.FromSeconds(2));
            AssertObservation(fixture.Observations.Snapshot, before.Sequence!.Value, before.WitnessedAt!.Value, before.ObservedAt!.Value);
        }
        finally
        {
            release.Set();
        }

        if (fails)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => handling);
            Assert.Equal(before, fixture.Observations.Snapshot);
        }
        else
        {
            Assert.Equal(12, await handling);
            AssertObservation(fixture.Observations.Snapshot, 12, TestRecords.Start, fixture.Time.GetUtcNow());
        }
    }

    /// <summary>Does not publish an action or malformed skip whose storage transaction failed.</summary>
    /// <param name="malformed">Whether handling deliberately skips malformed data.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedPersistenceDoesNotAdvance(bool malformed)
    {
        using ReaderFixture fixture = await ReaderFixture.CreateAsync();
        await fixture.HandleAsync(TestRecords.Event());
        ReplayProgressSnapshot before = fixture.Observations.Snapshot;
        fixture.Policy.EvaluateEvent = _ => malformed
            ? throw new MalformedEventException("Malformed source record.")
            : [TestRecords.Work() with { SourceSequence = 12 }];
        await fixture.Database.ReleaseAsync(CancellationToken.None);
        fixture.Time.Advance(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<OwnershipLostException>(() => fixture.HandleAsync(TestRecords.Event(sequence: 12), 11));
        Assert.Equal(before, fixture.Observations.Snapshot);
    }

    /// <summary>Preserves observations and their age across quiet waits, restoration, and same-period re-enumeration.</summary>
    [Fact]
    public async Task RecoveryRetainsObservationButNewOwnershipResetsIt()
    {
        using ReaderFixture fixture = await ReaderFixture.CreateAsync();
        using Ownership ownership = new();
        WorkerHealth health = new(fixture.Time);
        ownership.Activate();
        CancellationToken period = await ownership.WaitAsync("reader", health, CancellationToken.None);
        fixture.Observations.BeginOwnership(period);
        fixture.Progress.Waiting();
        AssertUnknown(fixture.Observations.Snapshot);
        await fixture.HandleAsync(TestRecords.Event());
        ReplayProgressSnapshot before = fixture.Observations.Snapshot;

        fixture.Time.Advance(TimeSpan.FromHours(2));
        fixture.Progress.Waiting();
        fixture.Observations.BeginOwnership(await ownership.WaitAsync("reader", health, CancellationToken.None));
        ReplayDiagnostics diagnostics = new(fixture.Time, NullLogger.Instance);
        diagnostics.Restored(11);
        ReplayProgress restarted = new(fixture.Time, NullLogger.Instance, fixture.Observations);
        restarted.Waiting();
        await fixture.Reader.HandleEventAsync(
            TestRecords.Event(), TestRecords.Start, 11, diagnostics, restarted, CancellationToken.None);
        Assert.Equal(before, fixture.Observations.Snapshot);
        ownership.Suspend();
        Assert.True(period.IsCancellationRequested);
        Assert.Equal(before, fixture.Observations.Snapshot);

        ownership.Activate();
        fixture.Observations.BeginOwnership(await ownership.WaitAsync("reader", health, CancellationToken.None));
        AssertUnknown(fixture.Observations.Snapshot);
        await fixture.Reader.HandleEventAsync(
            TestRecords.Event(), TestRecords.Start, 11, diagnostics, restarted, CancellationToken.None);
        AssertUnknown(fixture.Observations.Snapshot);
        await fixture.Reader.HandleEventAsync(
            TestRecords.Event(sequence: 12), TestRecords.Start, 11, diagnostics, restarted, CancellationToken.None);
        AssertObservation(fixture.Observations.Snapshot, 12, TestRecords.Start, fixture.Time.GetUtcNow());
    }

    /// <summary>Rejects incomplete positions and invalid policy output before observations can advance.</summary>
    /// <param name="kind">The invalid event or policy output.</param>
    [Theory]
    [InlineData("sequence")]
    [InlineData("witnessed")]
    [InlineData("action")]
    public async Task InvalidHandlingLeavesProgressUnknown(string kind)
    {
        using ReaderFixture fixture = await ReaderFixture.CreateAsync();
        JetstreamCommitEvent item = TestRecords.Event() with
        {
            Sequence = kind == "sequence" ? null : 11,
            WitnessedAt = kind == "witnessed" ? null : TestRecords.Start
        };
        if (kind == "action")
        {
            fixture.Policy.EvaluateEvent = _ => [TestRecords.Work() with { ActionKind = "unregistered" }];
        }

        await Assert.ThrowsAnyAsync<Exception>(() => fixture.HandleAsync(item));
        AssertUnknown(fixture.Observations.Snapshot);
    }

    /// <summary>Returns whole observations under simultaneous readers and a sequential writer.</summary>
    [Fact]
    public async Task ConcurrentReadsRemainInternallyConsistent()
    {
        FakeTimeProvider time = new(TestRecords.Start);
        BotReplayProgress observations = new();
        ReplayProgress progress = new(time, NullLogger.Instance, observations);
        using Barrier start = new(5);
        Task writer = Task.Run(() =>
        {
            for (int sequence = 1; sequence <= 1000; sequence++)
            {
                start.SignalAndWait(TestContext.Current.CancellationToken);
                time.SetUtcNow(TestRecords.Start.AddTicks(sequence));
                progress.Observe(sequence, TestRecords.Start.AddSeconds(sequence));
            }
        }, TestContext.Current.CancellationToken);
        Task[] readers = [.. Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            try
            {
                for (int iteration = 1; iteration <= 1000; iteration++)
                {
                    start.SignalAndWait(TestContext.Current.CancellationToken);
                    ReplayProgressSnapshot snapshot = observations.Snapshot;
                    if (snapshot.Sequence is long sequence)
                    {
                        AssertObservation(snapshot, sequence, TestRecords.Start.AddSeconds(sequence), TestRecords.Start.AddTicks(sequence));
                    }
                    else
                    {
                        AssertUnknown(snapshot);
                    }
                }
            }
            finally
            {
                start.RemoveParticipant();
            }
        }, TestContext.Current.CancellationToken))];

        await Task.WhenAll(readers.Append(writer));
        Assert.Equal(1000, observations.Snapshot.Sequence);
    }

    private static void AssertUnknown(ReplayProgressSnapshot snapshot)
    {
        Assert.Null(snapshot.Sequence);
        Assert.Null(snapshot.WitnessedAt);
        Assert.Null(snapshot.ObservedAt);
    }

    private static void AssertObservation(ReplayProgressSnapshot snapshot, long sequence, DateTimeOffset witnessedAt, DateTimeOffset observedAt)
    {
        Assert.Equal(sequence, snapshot.Sequence);
        Assert.Equal(witnessedAt, snapshot.WitnessedAt);
        Assert.Equal(observedAt, snapshot.ObservedAt);
    }

    private sealed class TestPolicy : IBotPolicy
    {
        public Func<JetstreamEvent, IReadOnlyList<BotAction>> EvaluateEvent { get; set; } = _ => [];

        public IReadOnlyList<BotAction> Evaluate(JetstreamEvent item, DateTimeOffset startedAt) => EvaluateEvent(item);
    }

    private sealed class ReaderFixture : IDisposable
    {
        private readonly ServiceProvider _provider;

        private ReaderFixture(ServiceProvider provider, FakeTimeProvider time, TestPolicy policy)
        {
            _provider = provider;
            Time = time;
            Policy = policy;
            Observations = provider.GetRequiredService<BotReplayProgress>();
            Database = provider.GetRequiredService<IDatabaseProvider>();
            Reader = provider.GetServices<IHostedService>().OfType<ReaderWorker>().Single();
            Diagnostics = new(time, NullLogger.Instance);
            Diagnostics.Restored(10);
            Progress = new(time, NullLogger.Instance, Observations);
        }

        public FakeTimeProvider Time { get; }
        public TestPolicy Policy { get; }
        public BotReplayProgress Observations { get; }
        public IDatabaseProvider Database { get; }
        public ReaderWorker Reader { get; }
        public ReplayDiagnostics Diagnostics { get; }
        public ReplayProgress Progress { get; }

        public static async Task<ReaderFixture> CreateAsync()
        {
            FakeTimeProvider time = new(TestRecords.Start);
            TestPolicy policy = new();
            ServiceCollection services = new();
            services.AddLogging();
            services.AddSingleton<TimeProvider>(time);
            services.AddBlueskyBot<TestPolicy>(TestRecords.Options(), "progress-test/1.0").WithInMemoryStorage();
            services.AddSingleton<IBotPolicy>(policy);
            ReaderFixture fixture = new(services.BuildServiceProvider(), time, policy);
            await fixture.Database.InitializeAsync(CancellationToken.None);
            await fixture.Database.AcquireOrRenewAsync(CancellationToken.None);
            time.Advance(TimeSpan.FromSeconds(40));
            await fixture.Database.LoadStateAsync(CancellationToken.None);
            await fixture.Database.SaveRequestAsync(new() { AfterSeq = 10 }, CancellationToken.None);
            return fixture;
        }

        public Task<long> HandleAsync(JetstreamEvent item, long processed = 10) =>
            Reader.HandleEventAsync(item, TestRecords.Start, processed, Diagnostics, Progress, CancellationToken.None).AsTask();

        public void Dispose() => _provider.Dispose();
    }
}
