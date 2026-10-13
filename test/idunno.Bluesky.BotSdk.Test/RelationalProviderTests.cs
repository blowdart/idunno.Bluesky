// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Net;
using System.Reflection;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Jetstream;
using idunno.AtProto.Jetstream.Archive;
using idunno.AtProto.Repo;
using idunno.Bluesky;
using idunno.Bluesky.BotSdk.DataProviders;
using idunno.Bluesky.Feed;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

using MySqlConnector;

using Npgsql;

using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace idunno.Bluesky.BotSdk.Test;

/// <summary>Supplies a MySQL test case, explicitly skipped when no disposable server connection is configured.</summary>
public sealed class MySqlDataAttribute : DataAttribute
{
    /// <summary>Initializes a new instance of the <see cref="MySqlDataAttribute"/> class.</summary>
    public MySqlDataAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BOTSDK_TEST_MYSQL_CONNECTION")))
        {
            Skip = "Set BOTSDK_TEST_MYSQL_CONNECTION to a disposable MySQL server to run MySQL provider tests.";
        }
    }

    /// <inheritdoc/>
    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(
        MethodInfo testMethod,
        DisposalTracker disposalTracker)
    {
        TheoryDataRow<string> row = new("MySql") { Skip = Skip };
        return ValueTask.FromResult<IReadOnlyCollection<ITheoryDataRow>>([row]);
    }

    /// <inheritdoc/>
    public override bool SupportsDiscoveryEnumeration() => true;
}

/// <summary>Supplies PostgreSQL cases, explicitly skipped when no disposable server connection is configured.</summary>
public sealed class PostgreSqlDataAttribute : DataAttribute
{
    /// <summary>Initializes a new instance of the <see cref="PostgreSqlDataAttribute"/> class.</summary>
    public PostgreSqlDataAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BOTSDK_TEST_POSTGRESQL_CONNECTION")))
        {
            Skip = "Set BOTSDK_TEST_POSTGRESQL_CONNECTION to a disposable PostgreSQL server to run PostgreSQL provider tests.";
        }
    }

    /// <inheritdoc/>
    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(
        MethodInfo testMethod,
        DisposalTracker disposalTracker)
    {
        TheoryDataRow<string> row = new("Postgres") { Skip = Skip };
        return ValueTask.FromResult<IReadOnlyCollection<ITheoryDataRow>>([row]);
    }

    /// <inheritdoc/>
    public override bool SupportsDiscoveryEnumeration() => true;
}

/// <summary>Supplies every rate-budget case for one configured relational provider.</summary>
public sealed class ProviderBudgetDataAttribute : DataAttribute
{
    private readonly string _provider;

    /// <summary>Initializes cases for the named provider and skips them when its disposable connection is absent.</summary>
    /// <param name="provider">The provider whose rate budgets are tested.</param>
    public ProviderBudgetDataAttribute(string provider)
    {
        _provider = provider;
        string variable = provider switch
        {
            "Postgres" => "BOTSDK_TEST_POSTGRESQL_CONNECTION",
            "MySql" => "BOTSDK_TEST_MYSQL_CONNECTION",
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable)))
        {
            Skip = $"Set {variable} to a disposable {provider} server to run provider tests.";
        }
    }

    /// <inheritdoc/>
    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(
        MethodInfo testMethod,
        DisposalTracker disposalTracker)
    {
        ITheoryDataRow[] rows = new[] { "hour", "day", "requests", "sessions-day", "sessions-five-minutes" }
            .Select(budget => (ITheoryDataRow)new TheoryDataRow<string, string>(_provider, budget) { Skip = Skip })
            .ToArray();

        return ValueTask.FromResult<IReadOnlyCollection<ITheoryDataRow>>(rows);
    }

    /// <inheritdoc/>
    public override bool SupportsDiscoveryEnumeration() => true;
}

/// <summary>Checks shared durability and lease guarantees across PostgreSQL, SQLite, and MySQL.</summary>
public sealed partial class RelationalProviderTests
{
    /// <summary>Reports durable action states and verifies connectivity without mutating state.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [PostgreSqlData]
    public async Task ReadsOperationalSnapshot(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        IBotDatabaseDiagnostics diagnostics = Assert.IsType<IBotDatabaseDiagnostics>(store.Database, exactMatch: false);
        await diagnostics.CheckConnectionAsync(CancellationToken.None);

        BotAction source = TestRecords.Work();
        BotAction pending = source with
        {
            ActionKind = "like",
            ActionKey = "pending",
            RecordKey = "pending-key"
        };
        BotAction rejected = source with
        {
            ActionKind = "like",
            ActionKey = "rejected",
            RecordKey = "rejected-key"
        };
        BotAction simulated = source with
        {
            ActionKind = "like",
            ActionKey = "simulated",
            RecordKey = "simulated-key"
        };

        Assert.Equal(4, await store.Database.HandleAsync(
            11,
            [source, pending, rejected, simulated],
            null,
            CancellationToken.None));
        Assert.Equal(1, await store.Database.MarkAttemptAsync(source, CancellationToken.None));
        Assert.Equal(1, await store.Database.FinishAsync(source, "sent", $"at://{TestRecords.BotDid}/app.bsky.feed.post/sent", "confirmed", CancellationToken.None));
        Assert.Equal(1, await store.Database.FinishAsync(rejected, "not-sent", null, "rejected", CancellationToken.None));
        Assert.Equal(1, await store.Database.FinishAsync(simulated, "simulated", null, "simulation", CancellationToken.None));

        BotOperationalSnapshot snapshot = await diagnostics.GetOperationalSnapshotAsync(CancellationToken.None);
        Assert.Equal(11, snapshot.ProcessedThrough);
        Assert.Equal(1, snapshot.PendingActions);
        Assert.Equal(0, snapshot.UncertainActions);
        Assert.Equal(1, snapshot.SentActions);
        Assert.Equal(1, snapshot.RejectedActions);
        Assert.Equal(1, snapshot.SimulatedActions);
    }

    /// <summary>Preserves complete replies, JSON, credentials, and progress across provider recreation and safe pruning.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [PostgreSqlData]
    public async Task RestoresAndDeduplicates(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        BotAction work = TestRecords.Work();
        Assert.True(await store.Database.HandleAsync(11, [work], null, CancellationToken.None) > 0);
        Assert.Equal(0, await store.Database.HandleAsync(12, [work with { RecordKey = "different" }], null, CancellationToken.None));
        BotAction durable = (await store.Database.NextWorkAsync(CancellationToken.None))!;
        Assert.Equal(work.RecordKey, durable.RecordKey);
        Assert.True(ReplyRecords.SameRecord(TestRecords.Post(work), TestRecords.Post(durable)));
        byte[] credentials = [0, 1, 255];
        await store.Database.SaveCredentialsAsync(credentials, CancellationToken.None);
        Assert.Equal(credentials, await store.Database.LoadCredentialsAsync(CancellationToken.None));
        await store.Database.SaveCredentialsAsync(null, CancellationToken.None);
        Assert.Null(await store.Database.LoadCredentialsAsync(CancellationToken.None));
        await store.Database.FinishAsync(work, "sent", $"at://{TestRecords.BotDid}/app.bsky.feed.post/test", "confirmed", CancellationToken.None);
        await store.AgeCompletedWorkAsync();
        await store.Database.PruneAsync(CancellationToken.None);
        Assert.Equal(0, await store.CountAsync("reply_work"));
        await store.Database.ReleaseAsync(CancellationToken.None);
        IDatabaseProvider restarted = store.NewProvider();
        Assert.NotNull(await restarted.AcquireOrRenewAsync(CancellationToken.None));
        await store.SetReadyNowAsync();
        Assert.Equal(12, (await restarted.LoadStateAsync(CancellationToken.None)).ProcessedThrough);
        Assert.Equal(0, await restarted.HandleAsync(11, [work], null, CancellationToken.None));
    }

    /// <summary>Rolls back enqueue failures and coalesced progress when checkpoints are ahead of durable handling.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [PostgreSqlData]
    public async Task RollsBackAndPreservesCheckpoints(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        BotAction work = TestRecords.Work();
        await AssertInvalidActionFailsAsync(provider, () =>
            store.Database.HandleAsync(11, [work with { RecordJson = "not-json" }], null, CancellationToken.None));
        Assert.Equal(10, (await store.Database.LoadStateAsync(CancellationToken.None)).ProcessedThrough);
        Assert.Equal(0, await store.CountAsync("reply_work"));
        Assert.True(await store.Database.HandleAsync(11, [work], null, CancellationToken.None) > 0);
        BotAction different = work with { SourceUri = work.SourceUri + "other", RecordJson = "not-json", SourceSequence = 12 };
        await AssertInvalidActionFailsAsync(provider, () =>
            store.Database.HandleAsync(12, [different], null, CancellationToken.None));
        Assert.Equal(11, (await store.Database.LoadStateAsync(CancellationToken.None)).ProcessedThrough);
        SnapshotCheckpoint checkpoint = new()
        {
            RequestFingerprint = "original",
            SealedTipSeq = 10,
            PlanAfterSeq = 10,
            ReplayAfterSeq = 9,
            LiveAfterSeq = 5000,
            SegmentName = "segment",
            SegmentChecksum = "checksum",
            NextBlockIndex = 7,
            NextByteOffset = 123456
        };

        SnapshotCheckpoint durablyHandledCheckpoint = checkpoint with { LiveAfterSeq = 11 };
        store.Database.SaveCheckpoint(durablyHandledCheckpoint, CancellationToken.None);
        ReplayState state = await store.Database.LoadStateAsync(CancellationToken.None);
        Assert.Equal(11, state.ProcessedThrough);
        Assert.Equal(durablyHandledCheckpoint, JsonSerializer.Deserialize(state.CheckpointJson!, BotJson.SnapshotCheckpoint));

        Assert.Throws<InvalidOperationException>(() =>
            store.Database.SaveCheckpoint(checkpoint with { LiveAfterSeq = 12 }, CancellationToken.None));
        state = await store.Database.LoadStateAsync(CancellationToken.None);
        Assert.Equal(11, state.ProcessedThrough);
        Assert.Equal(durablyHandledCheckpoint, JsonSerializer.Deserialize(state.CheckpointJson!, BotJson.SnapshotCheckpoint));

        store.Database.SaveCheckpoint(checkpoint, CancellationToken.None, 5000);
        state = await store.Database.LoadStateAsync(CancellationToken.None);
        Assert.Equal(checkpoint, JsonSerializer.Deserialize(state.CheckpointJson!, BotJson.SnapshotCheckpoint));
        Assert.Throws<InvalidOperationException>(() =>
            store.Database.SaveCheckpoint(checkpoint with { LiveAfterSeq = 6000 }, CancellationToken.None, 5500));
        state = await store.Database.LoadStateAsync(CancellationToken.None);
        Assert.Equal(5000, state.ProcessedThrough);
        Assert.Equal(checkpoint, JsonSerializer.Deserialize(state.CheckpointJson!, BotJson.SnapshotCheckpoint));
    }

    /// <summary>Fences competing owners, preserves renewal readiness, and prevents stale release and writes.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [PostgreSqlData]
    public async Task FencesOwners(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        Assert.NotNull(await store.Database.AcquireOrRenewAsync(CancellationToken.None));
        await store.SetReadyAsync();
        IDatabaseProvider other = store.NewProvider();
        Assert.Null(await other.AcquireOrRenewAsync(CancellationToken.None));
        await store.ExpireLeaseAsync();
        DateTimeOffset? ready = await other.AcquireOrRenewAsync(CancellationToken.None);
        Assert.True(ready > DateTimeOffset.UtcNow.AddSeconds(35));
        await Assert.ThrowsAsync<OwnershipLostException>(() => other.LoadStateAsync(CancellationToken.None));
        await store.SetReadyAsync();
        await Assert.ThrowsAsync<OwnershipLostException>(() =>
            store.Database.HandleAsync(11, [TestRecords.Work()], null, CancellationToken.None));
        await Assert.ThrowsAsync<OwnershipLostException>(() =>
            store.Database.ReserveRequestAsync(3, false, CancellationToken.None));
        Assert.Throws<OwnershipLostException>(() => store.Database.RecordSkip(null, "block", "test"));
        Assert.Throws<OwnershipLostException>(() =>
            store.Database.SaveCheckpoint(new SnapshotCheckpoint { PlanAfterSeq = 10, SealedTipSeq = 10 }, CancellationToken.None));
        await store.Database.ReleaseAsync(CancellationToken.None);
        Assert.True(await other.HandleAsync(11, [TestRecords.Work()], null, CancellationToken.None) > 0);
    }

    /// <summary>Serializes concurrent first-owner acquisition and prevents two active leases.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [PostgreSqlData]
    public async Task SerializesCompetingAcquisition(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        await store.SqlAsync("DELETE FROM bot_ownership");
        IDatabaseProvider first = store.NewProvider();
        IDatabaseProvider second = store.NewProvider();
        DateTimeOffset?[] acquired = await Task.WhenAll(
            Task.Run(() => first.AcquireOrRenewAsync(CancellationToken.None)),
            Task.Run(() => second.AcquireOrRenewAsync(CancellationToken.None)));
        Assert.Single(acquired, value => value is not null);
    }

    /// <summary>Preserves original filters, queued uncertain work, and explicit data-loss audits during lookback.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [PostgreSqlData]
    public async Task PreservesLookbackAndAudits(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        BotAction work = TestRecords.Work();
        await store.Database.HandleAsync(11, [work], null, CancellationToken.None);
        await store.Database.MarkAttemptAsync(work, CancellationToken.None);
        ReplayState before = await store.Database.LoadStateAsync(CancellationToken.None);
        DateTimeOffset cutoff = before.StartedAt.AddMinutes(1);
        Assert.Equal(100, await store.Database.OverrideLookbackAsync(cutoff, new SnapshotRequest
        {
            AfterSeq = 100,
            Collections = [new("app.bsky.feed.post")]
        }, CancellationToken.None));
        ReplayState after = await store.Database.LoadStateAsync(CancellationToken.None);
        Assert.Equal(cutoff, after.StartedAt);
        Assert.Null(after.CheckpointJson);
        Assert.Equal("app.bsky.feed.post", JsonSerializer.Deserialize(after.RequestJson!, BotJson.SnapshotRequest)!.Collections!.Single().ToString());
        Assert.Equal("uncertain", (await store.Database.NextWorkAsync(CancellationToken.None))!.Status);
        Assert.Equal(100, await store.Database.OverrideLookbackAsync(cutoff.AddMinutes(1), new SnapshotRequest
        {
            AfterSeq = 50,
            Collections = [new("app.bsky.feed.post")]
        }, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.Database.OverrideLookbackAsync(cutoff, new SnapshotRequest { AfterSeq = 200 }, CancellationToken.None));
        store.Database.RecordSkip(null, "archive block", "bad data");
        await store.Database.HandleAsync(101, [], "malformed post", CancellationToken.None);
        Assert.Equal(4, await store.CountAsync("skipped_data"));
        store.Options.JetstreamService = "wss://other.example";
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.Database.LoadStateAsync(CancellationToken.None));
    }

    /// <summary>Retains pending, uncertain, and uncovered terminal rows and respects retry deadlines.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [PostgreSqlData]
    public async Task RetainsUnsafeWorkAndRetries(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        BotAction work = TestRecords.Work();
        await store.Database.HandleAsync(11, [work], null, CancellationToken.None);
        await store.Database.MarkAttemptAsync(work, CancellationToken.None);
        await store.AgeCompletedWorkAsync();
        await store.Database.PruneAsync(CancellationToken.None);
        Assert.Equal(1, await store.CountAsync("reply_work"));
        await store.Database.RetryAsync(work, DateTimeOffset.UtcNow.AddMinutes(10), "wait", CancellationToken.None);
        Assert.Null(await store.Database.NextWorkAsync(CancellationToken.None));
        await store.SetRetryReadyAsync();
        Assert.Equal(1, (await store.Database.NextWorkAsync(CancellationToken.None))!.Attempts);
        await store.Database.FinishAsync(work, "sent", null, "test", CancellationToken.None);
        await store.SqlAsync("UPDATE reply_work SET source_sequence = 999");
        await store.AgeCompletedWorkAsync();
        await store.Database.PruneAsync(CancellationToken.None);
        Assert.Equal(1, await store.CountAsync("reply_work"));
    }

    /// <summary>Marks root deletions without admitting a new write or changing uncertain records.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [PostgreSqlData]
    public async Task HonorsDeletion(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        BotAction work = TestRecords.Work();
        await store.Database.HandleAsync(11, [work], null, CancellationToken.None);
        string root = TestRecords.Post(work).Reply!.Root.Uri.ToString();
        await store.Database.HandleAsync(12, [], null, CancellationToken.None, root);
        BotAction deleted = (await store.Database.NextWorkAsync(CancellationToken.None))!;
        Assert.True(deleted.TargetDeleted);
        Assert.Equal(0, await store.Database.MarkAttemptAsync(deleted, CancellationToken.None));
        Assert.Equal("pending", (await store.Database.NextWorkAsync(CancellationToken.None))!.Status);
    }

    /// <summary>Rejects replay state when the bot identity, mode, or Jetstream service changes.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [PostgreSqlData]
    public async Task BindsReplayStateToConfiguration(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        store.Options.DeveloperMode = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.Database.LoadStateAsync(CancellationToken.None));

        store.Options.DeveloperMode = true;
        store.Options.JetstreamService = "wss://different.example";
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.Database.LoadStateAsync(CancellationToken.None));

        store.Options.JetstreamService = TestRecords.Options().JetstreamService;
        store.Options.Did = "did:plc:different";
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.Database.LoadStateAsync(CancellationToken.None));
    }

    /// <summary>Persists hourly, daily, request, session, spacing, and monotonic cooldown limits.</summary>
    /// <param name="provider">The provider under test.</param>
    /// <param name="budget">The budget to exhaust.</param>
    [Theory]
    [InlineData("Sqlite", "hour")]
    [InlineData("Sqlite", "day")]
    [InlineData("Sqlite", "requests")]
    [InlineData("Sqlite", "sessions-day")]
    [InlineData("Sqlite", "sessions-five-minutes")]
    [ProviderBudgetData("Postgres")]
    [ProviderBudgetData("MySql")]
    public async Task PersistsRateBudgets(string provider, string budget)
    {
        await CheckBudgetAsync(provider, budget);
    }

    private static async Task CheckBudgetAsync(string provider, string budget)
    {
        await using Store store = await Store.CreateAsync(provider);
        switch (budget)
        {
            case "hour": store.Options.HourlyWritePoints = 3; break;
            case "day": store.Options.DailyWritePoints = 3; break;
            case "requests": store.Options.RequestsPerFiveMinutes = 1; break;
            case "sessions-day": store.Options.SessionsPerDay = 1; break;
            case "sessions-five-minutes": store.Options.SessionsPerFiveMinutes = 1; break;
        }
        bool session = budget.StartsWith("sessions", StringComparison.Ordinal);
        Assert.Null(await store.Database.ReserveRequestAsync(3, session, CancellationToken.None));
        Assert.True(await store.Database.ReserveRequestAsync(0, false, CancellationToken.None) > DateTimeOffset.UtcNow);
        await store.SetRateRequestReadyAsync();
        DateTimeOffset? wait = await store.Database.ReserveRequestAsync(3, session, CancellationToken.None);
        double minimumMinutes = budget switch
        {
            "hour" => 55,
            "requests" or "sessions-five-minutes" => 4,
            _ => 1400
        };
        Assert.True(wait > DateTimeOffset.UtcNow.AddMinutes(minimumMinutes));
        Assert.Equal(1, await store.CountAsync("rate_accounting"));
        DateTimeOffset until = DateTimeOffset.UtcNow.AddDays(2);
        await store.Database.CooldownAsync(until, CancellationToken.None);
        await store.Database.CooldownAsync(until.AddMinutes(-1), CancellationToken.None);
        Assert.True(await store.Database.ReserveRequestAsync(0, false, CancellationToken.None) >= until.AddMilliseconds(-1));
        await store.Database.SaveArchiveCooldownAsync(until, CancellationToken.None);
        await store.Database.SaveArchiveCooldownAsync(until.AddMinutes(-1), CancellationToken.None);
        Assert.True(await store.Database.ArchiveCooldownAsync(CancellationToken.None) >= until.AddMilliseconds(-1));
    }

    private static async Task AssertInvalidActionFailsAsync(string provider, Func<Task> action)
    {
        if (provider == "Postgres")
        {
            await Assert.ThrowsAsync<PostgresException>(action);
        }
        else
        {
            await Assert.ThrowsAnyAsync<JsonException>(action);
        }
    }

    private sealed class Store : IAsyncDisposable
    {
        private readonly string _provider;
        private readonly string _name = "test_" + Guid.NewGuid().ToString("N");
        private readonly string _connectionString;
        private readonly string? _admin;
        private NpgsqlDataSource? _postgresSource;
        public BotOptions Options { get; } = TestRecords.Options();
        public IDatabaseProvider Database { get; private set; } = null!;

        private Store(string provider)
        {
            _provider = provider;
            if (provider == "Sqlite")
            {
                _connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = Path.Combine(Path.GetTempPath(), _name + ".db"),
                    Pooling = false
                }.ConnectionString;
            }
            else if (provider == "Postgres")
            {
                _admin = Environment.GetEnvironmentVariable("BOTSDK_TEST_POSTGRESQL_CONNECTION")
                    ?? throw new InvalidOperationException("Missing disposable PostgreSQL connection.");
                NpgsqlConnectionStringBuilder builder = new(_admin)
                {
                    SearchPath = _name,
                    Pooling = false,
                    CommandTimeout = 15
                };
                _connectionString = builder.ConnectionString;
            }
            else
            {
                _admin = Environment.GetEnvironmentVariable("BOTSDK_TEST_MYSQL_CONNECTION")
                    ?? throw new InvalidOperationException("Missing disposable MySQL connection.");
                MySqlConnectionStringBuilder builder = new(_admin) { Database = _name, Pooling = false };
                _connectionString = builder.ConnectionString;
            }
        }

        public IDatabaseProvider NewProvider() => _provider switch
        {
            "Sqlite" => new SqliteDatabaseProvider(
                _connectionString,
                Options,
                TimeProvider.System,
                NullLogger<SqliteDatabaseProvider>.Instance),
            "Postgres" => new PostgreSqlDatabaseProvider(
                _postgresSource!,
                Options,
                TimeProvider.System,
                NullLogger<PostgreSqlDatabaseProvider>.Instance),
            _ => new MySqlDatabaseProvider(
                _connectionString,
                Options,
                TimeProvider.System,
                NullLogger<MySqlDatabaseProvider>.Instance)
        };

        public static async Task<Store> CreateAsync(string provider)
        {
            Store store = new(provider);
            if (provider == "Postgres")
            {
                await using NpgsqlConnection admin = new(store._admin);
                await admin.OpenAsync();
                await using NpgsqlCommand create = new($"CREATE SCHEMA \"{store._name}\"", admin);
                await create.ExecuteNonQueryAsync();
                store._postgresSource = NpgsqlDataSource.Create(store._connectionString);
            }
            else if (provider == "MySql")
            {
                await using MySqlConnection admin = new(store._admin);
                await admin.OpenAsync();
                await using MySqlCommand create = new($"CREATE DATABASE `{store._name}`", admin);
                await create.ExecuteNonQueryAsync();
            }
            store.Database = store.NewProvider();
            await store.Database.InitializeAsync(CancellationToken.None);
            Assert.NotNull(await store.Database.AcquireOrRenewAsync(CancellationToken.None));
            await store.SetReadyAsync();
            await store.Database.LoadStateAsync(CancellationToken.None);
            await store.Database.SaveRequestAsync(new SnapshotRequest
            {
                AfterSeq = 10,
                Collections = [new CollectionSelector("app.bsky.feed.post")]
            }, CancellationToken.None);

            return store;
        }

        private DbConnection Connection() => _provider switch
        {
            "Sqlite" => new SqliteConnection(_connectionString),
            "Postgres" => new NpgsqlConnection(_connectionString),
            _ => new MySqlConnection(_connectionString)
        };

        public Task SetReadyAsync() => SqlAsync(_provider == "Postgres"
            ? "UPDATE bot_ownership SET ready_at = 'epoch'"
            : "UPDATE bot_ownership SET ready_at = 0");

        public Task SetReadyNowAsync() => SqlAsync(_provider == "Postgres"
            ? "UPDATE bot_ownership SET ready_at = now()"
            : "UPDATE bot_ownership SET ready_at = 0");

        public Task ExpireLeaseAsync() => SqlAsync(_provider == "Postgres"
            ? "UPDATE bot_ownership SET lease_until = now() - interval '1 second'"
            : "UPDATE bot_ownership SET lease_until = 0");

        public Task AgeCompletedWorkAsync() => SqlAsync(_provider == "Postgres"
            ? "UPDATE reply_work SET completed_at = now() - interval '8 days'"
            : "UPDATE reply_work SET completed_at = 0");

        public Task SetRetryReadyAsync() => SqlAsync(_provider == "Postgres"
            ? "UPDATE reply_work SET next_retry = now() - interval '1 second'"
            : "UPDATE reply_work SET next_retry = 0");

        public Task SetRateRequestReadyAsync() => SqlAsync(_provider == "Postgres"
            ? "UPDATE rate_state SET next_request = '-infinity'"
            : "UPDATE rate_state SET next_request = 0");

        public async Task SqlAsync(string sql)
        {
            await using DbConnection connection = Connection();
            await connection.OpenAsync();
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        public async Task<long> CountAsync(string table)
        {
            await using DbConnection connection = Connection();
            await connection.OpenAsync();
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT count(*) FROM {table}";

            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        public async ValueTask DisposeAsync()
        {
            if (_provider == "Postgres")
            {
                await _postgresSource!.DisposeAsync();
                await using NpgsqlConnection admin = new(_admin);
                await admin.OpenAsync();
                await using NpgsqlCommand drop = new($"DROP SCHEMA \"{_name}\" CASCADE", admin);
                await drop.ExecuteNonQueryAsync();
            }
            else if (_provider == "MySql")
            {
                await using MySqlConnection admin = new(_admin);
                await admin.OpenAsync();
                await using MySqlCommand drop = new($"DROP DATABASE `{_name}`", admin);
                await drop.ExecuteNonQueryAsync();
            }
            else
            {
                string path = new SqliteConnectionStringBuilder(_connectionString).DataSource;
                File.Delete(path);
                File.Delete(path + "-wal");
                File.Delete(path + "-shm");
            }
        }
    }
}
