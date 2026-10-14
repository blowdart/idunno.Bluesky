// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Jetstream.Archive;
using idunno.AtProto.Repo;
using idunno.Bluesky.BotSdk.DataProviders;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

using Xunit;

namespace idunno.Bluesky.BotSdk.Test;

/// <summary>Checks volatile provider registration, warning, lease fencing, replay, and action durability semantics.</summary>
public sealed class InMemoryProviderTests
{
    /// <summary>Suppresses same-event deleted targets while retaining uncertain actions for reconciliation.</summary>
    [Fact]
    public async Task SameEventDeletionMarksNewActions()
    {
        (InMemoryDatabaseProvider database, _) = await CreateProvider();
        await ActionStorageTests.VerifySameEventDeletionAsync(database);
    }

    /// <summary>Does not interpret custom payload properties as reply roots, but retains source deletion.</summary>
    /// <param name="payload">The application-defined JSON payload.</param>
    [Theory]
    [InlineData("""{"reply":"text"}""")]
    [InlineData("[]")]
    [InlineData("\"saved text\"")]
    [InlineData("""{"reply":{"root":{"uri":"at://did:plc:7e6kocyzb77xkncplrkoojej/app.bsky.feed.post/root"}}}""")]
    public async Task CustomPayloadsKeepIndependentDeletionSemantics(string payload)
    {
        (InMemoryDatabaseProvider database, _) = await CreateProvider();
        AtUri root = new($"at://{TestRecords.AuthorDid}/app.bsky.feed.post/root");
        StrongReference rootReference = new(root, new Cid(TestRecords.CidValue));
        BotAction reply = TestRecords.Work(new ReplyReferences(rootReference, rootReference));
        BotAction custom = reply with
        {
            ActionKind = "custom",
            SourceUri = new AtUri($"at://{TestRecords.AuthorDid}/app.bsky.feed.post/custom-source"),
            RecordKey = "custom",
            RecordJson = payload
        };
        await database.HandleAsync(11, [custom, reply], null, CancellationToken.None);
        await database.HandleAsync(12, [], null, CancellationToken.None, root);
        BotAction work = (await database.NextWorkAsync(CancellationToken.None))!;
        Assert.Equal("custom", work.ActionKind);
        Assert.Equal(payload, work.RecordJson);
        Assert.False(work.TargetDeleted);
        await database.HandleAsync(13, [], null, CancellationToken.None, custom.SourceUri);
        Assert.True((await database.NextWorkAsync(CancellationToken.None))!.TargetDeleted);
        await database.FinishAsync(custom, "sent", null, "custom action completed", CancellationToken.None);
        Assert.True((await database.NextWorkAsync(CancellationToken.None))!.TargetDeleted);
    }

    /// <summary>Warns once at initialization and resolves the provider from the main BotSdk registration.</summary>
    [Fact]
    public async Task InitializationWarnsThatStorageIsNotProductionDurable()
    {
        using FakeLoggerProvider loggerProvider = new();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
        FakeTimeProvider timeProvider = new(TestRecords.Start);
        InMemoryDatabaseProvider database = new(
            TestRecords.Options(),
            timeProvider,
            loggerFactory.CreateLogger<InMemoryDatabaseProvider>());

        await database.InitializeAsync(CancellationToken.None);
        await database.InitializeAsync(CancellationToken.None);

        FakeLogRecord warning = Assert.Single(
            loggerProvider.Collector.GetSnapshot(),
            record => record.Level == LogLevel.Warning);
        Assert.Equal(420, warning.Id.Id);
        Assert.Contains("not suitable for production", warning.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lost when the process exits", warning.Message, StringComparison.Ordinal);

        ServiceCollection services = new();
        services.AddLogging();
        services.AddInMemoryBotStorage();
        services.AddBlueskyBot<BotHostingTests.NoReplyPolicy>(TestRecords.Options(), "in-memory-test/1.0");
        Assert.Throws<InvalidOperationException>(() => services.AddInMemoryBotStorage());

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.IsType<InMemoryDatabaseProvider>(provider.GetRequiredService<IDatabaseProvider>());
    }

    /// <summary>Stores the full original request and checkpoint and rejects progress ahead of handled events.</summary>
    [Fact]
    public async Task RequestAndCheckpointPreserveTheirCompleteShapesAndFenceProgress()
    {
        (InMemoryDatabaseProvider database, _) = await CreateProvider();
        SnapshotRequest request = new()
        {
            AfterSeq = 10,
            BeforeSeq = 100,
            Kinds = [idunno.AtProto.Jetstream.JetStreamEventKind.Commit]
        };
        ReplayState state = await database.LoadStateAsync(CancellationToken.None);

        Assert.Equal(1, await database.SaveRequestAsync(request, CancellationToken.None));
        state = await database.LoadStateAsync(CancellationToken.None);
        Assert.Equal(JsonSerializer.Serialize(request, BotJson.SnapshotRequest), state.RequestJson);
        Assert.Equal(10, state.ProcessedThrough);

        SnapshotCheckpoint checkpoint = new()
        {
            RequestFingerprint = "request-fingerprint",
            SealedTipSeq = 20,
            PlanAfterSeq = 10,
            SegmentName = "2026-10-07/0",
            SegmentChecksum = "segment-checksum",
            NextBlockIndex = 2,
            NextByteOffset = 128,
            LiveAfterSeq = 11
        };
        Assert.Throws<InvalidOperationException>(() =>
            database.SaveCheckpoint(checkpoint, CancellationToken.None));

        await database.HandleAsync(11, [], null, CancellationToken.None);
        database.SaveCheckpoint(checkpoint, CancellationToken.None, consumedThrough: 11);
        state = await database.LoadStateAsync(CancellationToken.None);

        Assert.Equal(11, state.ProcessedThrough);
        Assert.Equal(
            JsonSerializer.Serialize(checkpoint, BotJson.SnapshotCheckpoint),
            state.CheckpointJson);
        Assert.Equal(
            checkpoint,
            JsonSerializer.Deserialize(state.CheckpointJson!, BotJson.SnapshotCheckpoint));
    }

    /// <summary>Commits a saved action and event progress together and suppresses a replayed event.</summary>
    [Fact]
    public async Task HandlingPersistsTheOriginalActionAndSuppressesDuplicateEvents()
    {
        (InMemoryDatabaseProvider database, FakeTimeProvider timeProvider) = await CreateProvider();
        await database.SaveRequestAsync(new SnapshotRequest { AfterSeq = 10 }, CancellationToken.None);
        StrongReference source = new(
            new AtUri($"at://{TestRecords.AuthorDid}/app.bsky.feed.post/3mjfhkjsshs2q"),
            new Cid(TestRecords.CidValue));
        BotAction action = BotActions.Like(source, 11, timeProvider.GetUtcNow());

        Assert.Equal(1, await database.HandleAsync(11, [action], null, CancellationToken.None));
        Assert.Equal(0, await database.HandleAsync(
            11,
            [action with { RecordJson = """{"createdAt":"regenerated"}""", RecordKey = "changed" }],
            null,
            CancellationToken.None));
        Assert.Equal(action, await database.NextWorkAsync(CancellationToken.None));

        BotOperationalSnapshot snapshot =
            await ((IBotDatabaseDiagnostics)database).GetOperationalSnapshotAsync(CancellationToken.None);
        Assert.Equal(11, snapshot.ProcessedThrough);
        Assert.Equal(1, snapshot.PendingActions);
    }

    /// <summary>Rejects fenced operations after the process releases or loses its lease.</summary>
    [Fact]
    public async Task ReleaseAndLeaseExpiryFenceProviderOperations()
    {
        (InMemoryDatabaseProvider database, FakeTimeProvider timeProvider) = await CreateProvider();

        await database.ReleaseAsync(CancellationToken.None);
        await Assert.ThrowsAsync<OwnershipLostException>(() => database.LoadStateAsync(CancellationToken.None));

        (database, timeProvider) = await CreateProvider();
        timeProvider.Advance(TimeSpan.FromSeconds(55));
        await Assert.ThrowsAsync<OwnershipLostException>(() => database.LoadStateAsync(CancellationToken.None));
    }

    private static async Task<(InMemoryDatabaseProvider Database, FakeTimeProvider TimeProvider)> CreateProvider()
    {
        FakeTimeProvider timeProvider = new(TestRecords.Start);
        InMemoryDatabaseProvider database = new(
            TestRecords.Options(),
            timeProvider,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryDatabaseProvider>.Instance);
        await database.InitializeAsync(CancellationToken.None);
        Assert.Equal(TestRecords.Start + TimeSpan.FromSeconds(40), await database.AcquireOrRenewAsync(CancellationToken.None));
        timeProvider.Advance(TimeSpan.FromSeconds(40));
        await database.LoadStateAsync(CancellationToken.None);
        return (database, timeProvider);
    }
}
