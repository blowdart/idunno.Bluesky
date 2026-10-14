// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Reflection;

using idunno.AtProto.Jetstream;
using idunno.AtProto.Jetstream.Archive;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Xunit;

namespace idunno.Bluesky.BotSdk.Test;

/// <summary>Checks replay diagnostics and the production cleanup sequence without network or database access.</summary>
public sealed class ReplayDiagnosticsTests
{
    /// <summary>Omits inapplicable HTTP cooldowns and retains an actual deadline as structured timestamp data.</summary>
    /// <param name="hasCooldown">Whether the response advises a future cooldown.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HttpOutcomeOnlyShowsActualCooldown(bool hasCooldown)
    {
        DiagnosticLogger logger = new();
        DateTimeOffset deadline = TestRecords.Start.AddMinutes(5);
        BotLog.HttpResult(logger, "com.atproto.server.getSession", 200, hasCooldown ? deadline : null);
        DiagnosticEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(200, entry.Properties["StatusCode"]);
        Assert.DoesNotContain("(null)", entry.Message);
        if (hasCooldown)
        {
            Assert.Equal(deadline, entry.Properties["CooldownUntil"]);
            Assert.Contains(deadline.ToString("O", CultureInfo.InvariantCulture), entry.Message);
        }
        else
        {
            Assert.Equal("Outbound com.atproto.server.getSession returned HTTP 200.", entry.Message);
            Assert.False(entry.Properties.ContainsKey("CooldownUntil"));
        }
    }

    /// <summary>Distinguishes absent observations, unbounded requests, and unsaved checkpoint positions.</summary>
    [Fact]
    public void DescribesEveryMissingLogValue()
    {
        DiagnosticLogger logger = new();
        InvalidOperationException failure = new("original failure");
        BotLog.Skipped(logger, "record", null, "malformed");
        BotLog.Outcome(logger, "reply", "default", "at://example/source", "rejected", null, null);
        BotLog.ReplayWaiting(logger, null, null, TimeSpan.FromSeconds(15));
        BotLog.EnumerationStarting(
            logger,
            Guid.Empty,
            null,
            null,
            "wss://example.test",
            null,
            null,
            "",
            "",
            "",
            0,
            null,
            null,
            null);
        ReplayDiagnostics diagnostics = new(new DiagnosticTime(), logger);
        diagnostics.Report(final: true);
        BotLog.EnumerationFailure(logger, Guid.Empty, "restoring", "failed", null, null, null, null, null, failure);
        BotLog.EnumerationCancelled(logger, Guid.Empty, "shutdown", "restoring", null, null);
        BotLog.EnumerationRecovery(logger, Guid.Empty, "retry", "restoring", null, null, null, null, null, failure);

        Assert.All(logger.Entries, entry =>
        {
            Assert.DoesNotContain("(null)", entry.Message);
            Assert.All(entry.Properties, property => Assert.NotNull(property.Value));
        });
        Assert.Contains("unavailable in malformed data", logger.Entries.Single(entry => entry.Id == 6).Message);
        Assert.Contains("not applicable; no remote URI", logger.Entries.Single(entry => entry.Id == 9).Message);
        Assert.Contains("not yet observed", logger.Entries.Single(entry => entry.Id == 18).Message);
        Assert.Contains("bounds (unbounded, unbounded]", logger.Entries.Single(entry => entry.Id == 21).Message);
        DiagnosticEntry summary = logger.Entries.Single(entry => entry.Id == 22);
        Assert.Contains("not yet established", summary.Message);
        Assert.Contains("not yet saved", summary.Message);
        Assert.Contains("no saved archive segment", summary.Message);
        Assert.Contains("no saved archive position", summary.Message);
        Assert.Contains("unavailable; no witnessed event time", summary.Message);
        Assert.Same(failure, logger.Entries.Single(entry => entry.Id == 23).Exception);
        Assert.Same(failure, logger.Entries.Single(entry => entry.Id == 26).Exception);
    }

    /// <summary>Keeps available optional values typed and distinguishes valid zero positions from missing data.</summary>
    [Fact]
    public void PreservesTypedOptionalMeasurements()
    {
        DiagnosticLogger logger = new();
        BotLog.ReplayWaiting(logger, 0, TestRecords.Start, TimeSpan.Zero);
        DiagnosticEntry waiting = Assert.Single(logger.Entries);
        Assert.Equal(0L, waiting.Properties["Sequence"]);
        Assert.Equal(TestRecords.Start, waiting.Properties["WitnessedAt"]);
        Assert.Contains(TestRecords.Start.ToString("O", CultureInfo.InvariantCulture), waiting.Message);
        ReplayDiagnostics diagnostics = new(new DiagnosticTime(), logger);
        diagnostics.Start("wss://example.test", new() { AfterSeq = 0, BeforeSeq = 100 }, 0, Checkpoint());
        diagnostics.Processed(11, TestRecords.Start);
        diagnostics.Report(final: true);
        DiagnosticEntry summary = logger.Entries.Single(entry => entry.Id == 22);
        Assert.Equal(10L, summary.Properties["PlanAfterSeq"]);
        Assert.Equal(2, summary.Properties["NextBlockIndex"]);
        Assert.Equal(1024L, summary.Properties["NextByteOffset"]);
        Assert.Equal(TestRecords.Start, summary.Properties["WitnessedAt"]);
        Assert.IsType<TimeSpan>(summary.Properties["WitnessedLag"]);
        Assert.Equal(0L, logger.Entries.Single(entry => entry.Id == 21).Properties["AfterSequence"]);
    }

    /// <summary>Describes absent checkpoint cursors without displaying error-looking null values.</summary>
    [Fact]
    public void DescribesUnsavedReplayCursors()
    {
        DiagnosticLogger logger = new();
        BotLog.ReplayRestored(logger, 100, null, null);
        DiagnosticEntry entry = Assert.Single(logger.Entries);
        Assert.Contains("not yet saved; using the saved request boundary", entry.Message);
        Assert.Contains("not yet saved; replay will establish live progress", entry.Message);
        Assert.DoesNotContain("(null)", entry.Message);

        BotLog.ReplayRestored(logger, 100, 90, 100);
        Assert.Contains("saved archive cursor 90, saved live cursor 100", logger.Entries.Last().Message);
    }

    /// <summary>Includes the source AT URI and original exception in malformed-post warnings.</summary>
    [Fact]
    public void LogsSkippedPostIdentityAndException()
    {
        DiagnosticLogger logger = new();
        string uri = $"at://{TestRecords.AuthorDid}/app.bsky.feed.post/3mjfhkjsshs2q";
        ArgumentNullException exception = new("text");
        BotLog.PostSkipped(logger, uri, 11, exception.Message, exception);

        DiagnosticEntry entry = Assert.Single(logger.Entries);
        Assert.Contains(uri, entry.Message);
        Assert.Equal(uri, entry.Properties["SourceUri"]);
        Assert.Equal(11L, entry.Properties["Sequence"]);
        Assert.Same(exception, entry.Exception);
    }

    /// <summary>Aggregates cumulative work and interval rates with monotonic timing, separately from read waits.</summary>
    [Fact]
    public async Task AggregatesWorkWithoutPerEventLogs()
    {
        DiagnosticTime time = new();
        DiagnosticLogger logger = new();
        ReplayDiagnostics diagnostics = new(time, logger);
        SnapshotCheckpoint checkpoint = Checkpoint();
        diagnostics.Start("wss://example.test", new() { AfterSeq = 10 }, 10, checkpoint);
        diagnostics.BeginRead();
        time.Advance(TimeSpan.FromSeconds(20));

        SnapshotCheckpoint saved = checkpoint with { LiveAfterSeq = 10 };
        diagnostics.SaveCheckpoint(saved, 10, () => time.Advance(TimeSpan.FromSeconds(2)));
        time.Advance(TimeSpan.FromSeconds(3));
        diagnostics.EndRead();
        diagnostics.Consumed();

        long handlingStarted = time.GetTimestamp();
        await diagnostics.DatabaseAsync(() =>
        {
            time.Advance(TimeSpan.FromSeconds(1));

            return Task.FromResult(true);
        });

        diagnostics.DurableEvent(11);
        diagnostics.Processed(11, time.GetUtcNow());
        time.Advance(TimeSpan.FromSeconds(1));
        diagnostics.Handled(handlingStarted);
        diagnostics.Report();
        Assert.DoesNotContain(logger.Entries, entry => entry.Id == 22);
        diagnostics.Consumed(); // A replay duplicate still counts as consumed, not relevant.
        time.Advance(TimeSpan.FromSeconds(3));
        time.UtcNow = time.UtcNow.AddHours(-1); // Wall-clock changes cannot change work timings.
        ReplayDiagnosticSummary summary = diagnostics.Snapshot();
        Assert.Equal(30, summary.Elapsed.TotalSeconds);
        Assert.Equal(2, summary.EventsConsumed);
        Assert.Equal(1, summary.RelevantEvents);
        Assert.Equal(2d / 30, summary.EventsPerSecond);
        Assert.Equal(1, summary.HandlingCount);
        Assert.Equal(TimeSpan.FromSeconds(2), summary.HandlingTotal);
        Assert.Equal(summary.HandlingTotal, summary.HandlingMaximum);
        Assert.Equal(2, summary.DatabaseCount);
        Assert.Equal(TimeSpan.FromSeconds(3), summary.DatabaseTotal);
        Assert.Equal(TimeSpan.FromSeconds(2), summary.DatabaseMaximum);
        Assert.Equal(1, summary.CheckpointCount);
        Assert.Equal(TimeSpan.FromSeconds(2), summary.CheckpointTotal);
        Assert.Equal(summary.CheckpointTotal, summary.CheckpointMaximum);
        Assert.Equal(TimeSpan.FromSeconds(23), summary.ReadWait);
        diagnostics.Report();
        diagnostics.Report();

        DiagnosticEntry entry = Assert.Single(logger.Entries, entry => entry.Id == 22);
        Assert.Equal(diagnostics.EnumerationId, entry.Properties["ReplayEnumerationId"]);
        Assert.Equal(11L, entry.Properties["ProcessedThrough"]);
        Assert.Equal(11L, entry.Properties["DurableThrough"]);
        Assert.Equal(10L, entry.Properties["LiveAfterSeq"]);
        time.Advance(TimeSpan.FromSeconds(30));
        diagnostics.Report();
        Assert.Equal(0d, logger.Entries.Last().Properties["EventsPerSecond"]);
        diagnostics.Report(final: true);
        Assert.Equal(true, logger.Entries.Last().Properties["Final"]);
    }

    /// <summary>A pending read appears in the summary without being counted as slow event handling.</summary>
    [Fact]
    public void ReportsQuietReadSeparately()
    {
        DiagnosticTime time = new();
        DiagnosticLogger logger = new();
        ReplayDiagnostics diagnostics = new(time, logger);
        diagnostics.BeginRead();
        time.Advance(TimeSpan.FromSeconds(30));
        diagnostics.Report();
        Assert.Equal(TimeSpan.FromSeconds(30), diagnostics.Snapshot().ReadWait);
        Assert.Equal(0, diagnostics.Snapshot().HandlingCount);
        Assert.Equal(0, diagnostics.Snapshot().EventsConsumed);
        Assert.Contains("not evidence of slow consumption", logger.Entries.Single().Message);
    }

    /// <summary>Publishes only successful complete checkpoints after the persistence action returns; failed saves propagate.</summary>
    [Fact]
    public void CheckpointDiagnosticsDoNotAdvanceBeforeCommit()
    {
        DiagnosticTime time = new();
        DiagnosticLogger logger = new();
        ReplayDiagnostics diagnostics = new(time, logger);
        SnapshotCheckpoint original = Checkpoint();
        SnapshotCheckpoint next = original with { LiveAfterSeq = 12, NextBlockIndex = 4, NextByteOffset = 2048 };
        diagnostics.Start("wss://example.test", new() { AfterSeq = 10 }, 10, original);
        diagnostics.Processed(12, time.GetUtcNow()); // Coalesced progress is not yet durable.
        List<string> ordering = [];
        diagnostics.SaveCheckpoint(next, 12, () =>
        {
            Assert.Same(original, diagnostics.SavedCheckpoint);
            Assert.Equal(10, diagnostics.DurableThrough);
            ordering.Add("commit complete checkpoint");
            time.Advance(TimeSpan.FromMilliseconds(50));
        });

        ordering.Add("diagnostic progress published");
        Assert.Equal(["commit complete checkpoint", "diagnostic progress published"], ordering);
        Assert.Same(next, diagnostics.SavedCheckpoint);
        Assert.Equal(12, diagnostics.DurableThrough);

        InvalidOperationException failure = new("database failed");
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() =>
            diagnostics.SaveCheckpoint(next with { LiveAfterSeq = 14 }, 14, () => throw failure)));
        Assert.Same(next, diagnostics.SavedCheckpoint);
        Assert.Equal(12, diagnostics.DurableThrough);
        Assert.Equal(1, diagnostics.Snapshot().CheckpointSaves);
        Assert.Equal(1, diagnostics.Snapshot().CheckpointFailures);
        Assert.Equal(2, diagnostics.Snapshot().CheckpointCount);
        Assert.Same(failure, logger.Entries.Last().Exception);
    }

    /// <summary>Classifies only intentional cancellation signals, with shutdown taking precedence over ownership.</summary>
    [Theory]
    [InlineData(true, true, true, "host shutdown")]
    [InlineData(false, true, false, "existing recovery action: ownership loss")]
    [InlineData(false, false, true, "read-slice timeout")]
    [InlineData(false, false, false, "unexpected cancellation")]
    public void ClassifiesCancellationReasons(bool host, bool ownership, bool slice, string expected) =>
        Assert.Equal(expected, ReplayDiagnostics.CancellationReason(host, ownership, slice));

    /// <summary>Requests cancellation and waits for the outstanding read before enumerator disposal.</summary>
    [Fact]
    public async Task CleanupDoesNotDisposeConcurrentlyWithRead()
    {
        DiagnosticTime time = new();
        DiagnosticLogger logger = new();
        ReplayDiagnostics diagnostics = new(time, logger) { ExitReason = "read-slice timeout" };
        using CancellationTokenSource cancellation = new();
        List<string> ordering = [];
        TaskCompletionSource<bool> cancellationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellation.Token.Register(() =>
        {
            ordering.Add("cancel");
            cancellationObserved.TrySetResult(true);
        });
        TaskCompletionSource<bool> read = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task cleanup = ReplayCleanup.CloseAsync(cancellation, read.Task, () =>
        {
            Assert.True(read.Task.IsCompleted);
            ordering.Add("dispose");

            return ValueTask.CompletedTask;
        }, diagnostics, time);
        await cancellationObserved.Task;
        Assert.Equal(["cancel"], ordering);
        Assert.False(cleanup.IsCompleted);
        time.Advance(TimeSpan.FromMilliseconds(250));
        read.SetCanceled(cancellation.Token);
        await cleanup;
        Assert.Equal(["cancel", "dispose"], ordering);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("outstanding read observed enumeration cancellation", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("completed", StringComparison.Ordinal));
        Assert.Equal(TimeSpan.FromMilliseconds(250), logger.Entries.Last().Properties["Elapsed"]);
    }

    /// <summary>Reports an outstanding-read fault separately, propagates it, and never races disposal against that wait.</summary>
    [Fact]
    public async Task CleanupReportsOutstandingReadFailure()
    {
        DiagnosticTime time = new();
        DiagnosticLogger logger = new();
        ReplayDiagnostics diagnostics = new(time, logger);
        using CancellationTokenSource cancellation = new();
        TaskCompletionSource<bool> read = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool disposed = false;
        Task cleanup = ReplayCleanup.CloseAsync(cancellation, read.Task, () =>
        {
            disposed = true;

            return ValueTask.CompletedTask;
        }, diagnostics, time);

        InvalidOperationException failure = new("pending read failure");
        read.SetException(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => cleanup));
        Assert.False(disposed);

        DiagnosticEntry entry = logger.Entries.Single(entry => entry.Id == 23);
        Assert.Same(failure, entry.Exception);
        Assert.Equal("cleanup awaiting outstanding MoveNextAsync", entry.Properties["Phase"]);
    }

    /// <summary>Reports disposal faults with the exception object and preserves propagation.</summary>
    [Fact]
    public async Task CleanupReportsDisposalFailure()
    {
        DiagnosticLogger logger = new();
        DiagnosticTime time = new();
        ReplayDiagnostics diagnostics = new(time, logger);
        using CancellationTokenSource cancellation = new();
        InvalidOperationException failure = new("dispose failure");
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ReplayCleanup.CloseAsync(cancellation, Task.FromResult(true),
                () => ValueTask.FromException(failure), diagnostics, time)));

        DiagnosticEntry entry = logger.Entries.Single(entry => entry.Id == 23);
        Assert.Same(failure, entry.Exception);
        Assert.Equal("cleanup disposing enumerator", entry.Properties["Phase"]);
    }

    /// <summary>Logs SDK disposal separately from enumerator disposal, retaining its failure and elapsed time.</summary>
    [Fact]
    public async Task ReportsSdkDisposalFailure()
    {
        DiagnosticLogger logger = new();
        DiagnosticTime time = new();
        ReplayDiagnostics diagnostics = new(time, logger);
        InvalidOperationException failure = new("SDK dispose failure");
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ReplayCleanup.DisposeSdkAsync(() =>
            {
                time.Advance(TimeSpan.FromMilliseconds(100));

                return ValueTask.FromException(failure);
            }, diagnostics, time)));

        DiagnosticEntry entry = Assert.Single(logger.Entries, entry => entry.Id == 23);
        Assert.Same(failure, entry.Exception);
        Assert.Equal("SDK asynchronous disposal", entry.Properties["Phase"]);
        Assert.Equal(TimeSpan.FromMilliseconds(100), logger.Entries.Last().Properties["Elapsed"]);
    }

    /// <summary>A cancellation callback failure prevents the later cleanup steps and propagates with its phase.</summary>
    [Fact]
    public async Task CleanupReportsCancellationFailure()
    {
        DiagnosticLogger logger = new();
        DiagnosticTime time = new();
        ReplayDiagnostics diagnostics = new(time, logger);
        using CancellationTokenSource enumeration = new();
        InvalidOperationException failure = new("cancellation callback failed");
        using CancellationTokenRegistration registration = enumeration.Token.Register(() => throw failure);
        bool disposed = false;
        AggregateException propagated = await Assert.ThrowsAsync<AggregateException>(() =>
            ReplayCleanup.CloseAsync(enumeration, null, () =>
            {
                disposed = true;

                return ValueTask.CompletedTask;
            }, diagnostics, time));
        Assert.Same(failure, Assert.Single(propagated.InnerExceptions));
        Assert.False(disposed);

        DiagnosticEntry entry = Assert.Single(logger.Entries, entry => entry.Id == 23);
        Assert.Same(propagated, entry.Exception);
        Assert.Equal("cleanup cancellation", entry.Properties["Phase"]);
    }

    /// <summary>Reports an unverified cleanup cancellation without changing the existing cancellation filter.</summary>
    [Fact]
    public async Task CleanupDoesNotClaimUnrelatedCancellationIsExpected()
    {
        DiagnosticLogger logger = new();
        DiagnosticTime time = new();
        ReplayDiagnostics diagnostics = new(time, logger);
        using CancellationTokenSource enumeration = new();
        TaskCompletionSource<bool> read = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task cleanup = ReplayCleanup.CloseAsync(enumeration, read.Task, () => ValueTask.CompletedTask, diagnostics, time);
        OperationCanceledException failure = new("unrelated cancellation");
        read.SetException(failure);
        await cleanup;

        DiagnosticEntry entry = Assert.Single(logger.Entries, entry => entry.Exception is not null);
        Assert.Same(failure, entry.Exception);
        Assert.Contains("cancellation token not verified", entry.Message);
    }

    /// <summary>Logs the loaded SDK version, request filters and safe host authority without URI secrets.</summary>
    [Fact]
    public void StartReportsActualSdkAndSafeRequestDetails()
    {
        DiagnosticLogger logger = new();
        ReplayDiagnostics diagnostics = new(new DiagnosticTime(), logger);
        diagnostics.Start("wss://name:secret@example.test/path?apiKey=hidden", new()
        {
            AfterSeq = 10,
            BeforeSeq = 100,
            Collections = [new("app.bsky.feed.post")],
            Kinds = [JetStreamEventKind.Commit]
        }, 12, Checkpoint());

        DiagnosticEntry entry = Assert.Single(logger.Entries);
        Assembly assembly = typeof(AtProtoJetstream).Assembly;
        Assert.Equal(assembly.GetName().Version?.ToString(), entry.Properties["AssemblyVersion"]);
        Assert.Equal(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            entry.Properties["InformationalVersion"]);
        Assert.Equal("wss://example.test:443", entry.Properties["Host"]);
        Assert.Equal("app.bsky.feed.post", entry.Properties["Collections"]);
        Assert.Equal("Commit", entry.Properties["Kinds"]);
        Assert.Equal(10L, entry.Properties["AfterSequence"]);
        Assert.Equal(100L, entry.Properties["BeforeSequence"]);
        Assert.Equal(12L, entry.Properties["ProcessedThrough"]);
        Assert.DoesNotContain("secret", entry.Message);
        Assert.DoesNotContain("hidden", entry.Message);
    }

    private static SnapshotCheckpoint Checkpoint() => new()
    {
        SealedTipSeq = 100,
        PlanAfterSeq = 10,
        ReplayAfterSeq = 8,
        LiveAfterSeq = 10,
        RequestFingerprint = "fingerprint",
        SegmentName = "segment",
        SegmentChecksum = "checksum",
        NextBlockIndex = 2,
        NextByteOffset = 1024
    };
}

/// <summary>Serializes the console-output capture so other tests cannot interfere with the process-wide writer.</summary>
[CollectionDefinition("Console diagnostics", DisableParallelization = true)]
public sealed class ConsoleDiagnosticsCollection;

/// <summary>Verifies the actual console sink and category filters, including SDK scope propagation and full exceptions.</summary>
[Collection("Console diagnostics")]
public sealed class ReplayConsoleTests
{
    /// <summary>Retains Jetstream Debug, correlation, exception stacks and inner exceptions without enabling other Debug categories.</summary>
    [Fact]
    public void ConsoleRetainsSdkDebugScopeAndFullExceptions()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:LogLevel:Default"] = "Information",
                ["Logging:LogLevel:idunno.AtProto.Jetstream"] = "Debug",
                ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",
                ["Logging:LogLevel:System.Net.Http.HttpClient"] = "Warning"
            })
            .Build();

        TextWriter previous = Console.Out;
        using StringWriter output = new();
        Exception failure = CreateFailure();
        Guid enumerationId = Guid.Empty;

        try
        {
            Console.SetOut(output);
            using (ILoggerFactory factory = LoggerFactory.Create(logging =>
            {
                logging.AddConfiguration(configuration.GetSection("Logging"));
                BotLog.ConfigureConsole(logging);
            }))
            {
                ILogger sdk = factory.CreateLogger("idunno.AtProto.Jetstream.AtProtoJetstream");
                ILogger other = factory.CreateLogger("Unrelated");
                Assert.True(sdk.IsEnabled(LogLevel.Debug));
                Assert.False(sdk.IsEnabled(LogLevel.Trace));
                Assert.False(other.IsEnabled(LogLevel.Debug));
                Assert.True(other.IsEnabled(LogLevel.Information));
                Assert.False(factory.CreateLogger("Microsoft.AspNetCore").IsEnabled(LogLevel.Information));
                Assert.False(factory.CreateLogger("System.Net.Http.HttpClient").IsEnabled(LogLevel.Information));

                ReplayDiagnostics diagnostics = new(new DiagnosticTime(), factory.CreateLogger("MaddyBot.ReaderWorker"));
                enumerationId = diagnostics.EnumerationId;
                diagnostics.Restored(42);

                using IDisposable? scope = diagnostics.BeginScope();
                sdk.LogDebug("SDK reconnect diagnostic");
                diagnostics.Failure("unexpected cancellation must not be swallowed", failure);
                diagnostics.Report(final: true);

                // Dispose flushes the console provider before we inspect its asynchronously written output.
            }
        }
        finally
        {
            Console.SetOut(previous);
        }

        string text = output.ToString();
        Assert.Contains("SDK reconnect diagnostic", text);

        // The SDK entry is the first output, before our reader logs containing explicit IDs.
        // Verify its scope, rather than accidentally finding the ID in a later reader message.
        string sdkEntry = text[..text.IndexOf("SDK reconnect diagnostic", StringComparison.Ordinal)];
        Assert.Contains($"ReplayEnumerationId={enumerationId}", sdkEntry);

        foreach (string line in failure.ToString().Split(Environment.NewLine))
        {
            Assert.Contains(line.Trim(), text);
        }

        Assert.Contains(nameof(CreateFailure), text);
        Assert.Contains("inner failure", text);
        Assert.Contains("in-memory processed 42, durable processed 42", text);
        Assert.Contains("final=True", text);
    }

    private static OperationCanceledException CreateFailure()
    {
        try
        {
            throw new OperationCanceledException("unrequested cancellation", new InvalidOperationException("inner failure"));
        }
        catch (OperationCanceledException exception)
        {
            return exception;
        }
    }
}

internal sealed class DiagnosticTime : TimeProvider
{
    private long _timestamp;
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.Parse("2026-10-08T00:00:00Z", CultureInfo.InvariantCulture);
    public override DateTimeOffset GetUtcNow() => UtcNow;
    public override long GetTimestamp() => _timestamp;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public void Advance(TimeSpan elapsed)
    {
        _timestamp += elapsed.Ticks;
        UtcNow += elapsed;
    }
}

internal sealed record DiagnosticEntry(int Id, string Message, Exception? Exception, Dictionary<string, object?> Properties);

internal sealed class DiagnosticLogger : ILogger
{
    public List<DiagnosticEntry> Entries { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Entries.Add(new(eventId.Id, formatter(state, exception), exception,
            ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(pair => pair.Key, pair => pair.Value)));
}
