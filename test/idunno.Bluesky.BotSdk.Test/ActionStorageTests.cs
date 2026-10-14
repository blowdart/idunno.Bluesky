// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto.Jetstream.Archive;
using idunno.Bluesky.BotSdk.DataProviders;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

using MySqlConnector;

using Npgsql;

using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace idunno.Bluesky.BotSdk.Test;

/// <summary>Supplies PostgreSQL cases only when an isolated-schema test connection is configured.</summary>
public sealed class ActionPostgresDataAttribute : DataAttribute
{
    /// <summary>Initializes a new instance of the <see cref="ActionPostgresDataAttribute"/> class.</summary>
    public ActionPostgresDataAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BOTSDK_TEST_POSTGRESQL_CONNECTION")))
        {
            Skip = "Set BOTSDK_TEST_POSTGRESQL_CONNECTION to run isolated PostgreSQL action tests.";
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

/// <summary>Checks generalized action identity, atomicity, migration, and recovery across storage providers.</summary>
public sealed class ActionStorageTests
{
    /// <summary>Marks existing and newly enqueued deleted targets without broadening reply-root suppression.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [ActionPostgresData]
    public async Task SameEventDeletionMarksNewActions(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        await VerifySameEventDeletionAsync(store.Database);
    }

    internal static async Task VerifySameEventDeletionAsync(IDatabaseProvider database)
    {
        BotAction original = TestRecords.Work();
        await database.HandleAsync(11, [original], null, CancellationToken.None);
        await database.MarkAttemptAsync(original, CancellationToken.None);
        BotAction uncertain = original with { Status = "uncertain", Attempts = 1 };
        string[] kinds = ["reply", "like", "repost", "bookmark", "list-item", "custom"];
        List<BotAction> actions = [.. kinds.Select(kind => original with
        {
            ActionKind = kind,
            ActionKey = "new",
            RecordKey = kind,
            SourceSequence = 12
        })];
        BotAction rootReply = original with
        {
            SourceUri = original.SourceUri + "-child",
            SourceSequence = 12,
            RecordKey = "child"
        };
        BotAction rootCustom = rootReply with { ActionKind = "custom", RecordKey = "custom-child" };
        BotAction unrelated = rootReply with { ActionKind = "like", RecordKey = "unrelated" };
        actions.AddRange([rootReply, rootCustom, unrelated]);
        Assert.Equal(9, await database.HandleAsync(12, actions, null, CancellationToken.None, original.SourceUri));
        Assert.Equal(12, (await database.LoadStateAsync(CancellationToken.None)).ProcessedThrough);

        Dictionary<string, BotAction> remaining = actions.Append(uncertain).ToDictionary(action => action.RecordKey);
        while (remaining.Count > 0)
        {
            BotAction saved = Assert.IsType<BotAction>(await database.NextWorkAsync(CancellationToken.None));
            BotAction expected = remaining[saved.RecordKey];
            bool deleted = expected.SourceUri == original.SourceUri || expected.ActionKind == "reply";
            Assert.Equal(expected with { TargetDeleted = deleted, RecordJson = saved.RecordJson }, saved);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected.RecordJson), JsonNode.Parse(saved.RecordJson)));
            Assert.True(remaining.Remove(saved.RecordKey));
            await database.FinishAsync(saved, "not-sent", null, "test completed", CancellationToken.None);
        }
        Assert.Null(await database.NextWorkAsync(CancellationToken.None));
    }

    /// <summary>Preserves arbitrary custom JSON and applies root deletion only to replies.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [ActionPostgresData]
    public async Task CustomPayloadsDoNotInheritReplyDeletionSemantics(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        BotAction reply = TestRecords.Work();
        string[] payloads =
        [
            """{"reply":"text"}""",
            "[]",
            "\"saved text\"",
            JsonSerializer.Serialize(new { reply = new { root = new { uri = reply.SourceUri.ToString() } } })
        ];
        List<BotAction> actions = [];
        for (int index = 0; index < payloads.Length; index++)
        {
            actions.Add(Action("custom", $"payload{index}", $"custom{index}") with { RecordJson = payloads[index] });
        }

        actions.Add(reply);
        Assert.Equal(5, await store.Database.HandleAsync(11, actions, null, CancellationToken.None));
        if (provider != "Postgres")
        {
            // Older versions also extracted root_uri from custom payloads.
            await store.SqlAsync($"UPDATE reply_work SET root_uri = '{reply.SourceUri}' WHERE action_kind = 'custom'");
        }

        await store.Database.HandleAsync(12, [], null, CancellationToken.None, reply.SourceUri);
        Assert.Equal(1, await store.CountAsync($"action_kind = 'reply' AND target_deleted = {store.TrueSql}"));
        Assert.Equal(0, await store.CountAsync($"action_kind = 'custom' AND target_deleted = {store.TrueSql}"));
        await store.Database.HandleAsync(13, [], null, CancellationToken.None, actions[0].SourceUri);
        Assert.Equal(4, await store.CountAsync($"action_kind = 'custom' AND target_deleted = {store.TrueSql}"));
    }

    /// <summary>Removes legacy record-key indexes and preserves separate actions sharing a remote membership.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [ActionPostgresData]
    public async Task SharesRemoteKeysWithoutSharingRowMutations(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        BotAction first = Action(
            "list-item",
            "list",
            "shared-key") with
        { PayloadVersion = 2 };
        await store.Database.HandleAsync(
            11,
            [first],
            null,
            CancellationToken.None);
        await store.AddLegacyRecordKeyIndexAsync();
        await store.Database.InitializeAsync(CancellationToken.None);
        await store.Database.InitializeAsync(CancellationToken.None);
        Assert.Equal(
            first,
            await store.Database.NextWorkAsync(CancellationToken.None));
        BotAction second = first with
        {
            SourceUri = first.SourceUri + "-second",
            SourceSequence = 12
        };
        Assert.Equal(
            1,
            await store.Database.HandleAsync(
                12,
                [second],
                null,
                CancellationToken.None));
        Assert.Equal(
            1,
            await store.Database.MarkAttemptAsync(
                first,
                CancellationToken.None));
        Assert.Equal(
            1,
            await store.Database.RetryAsync(
                first,
                DateTimeOffset.UtcNow.AddHours(1),
                "one row only",
                CancellationToken.None));
        Assert.Equal(
            second,
            await store.Database.NextWorkAsync(CancellationToken.None));
        Assert.Equal(
            1,
            await store.Database.FinishAsync(
                second,
                "sent",
                $"at://{TestRecords.BotDid}/app.bsky.graph.listitem/shared-key",
                "reconciled",
                CancellationToken.None));
        Assert.Equal(
            1,
            await store.CountAsync("status = 'uncertain' AND attempts = 1"));
        Assert.Equal(
            1,
            await store.CountAsync("status = 'sent' AND attempts = 0"));
    }

    /// <summary>Restores complete actions and changes only the addressed kind and key.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [ActionPostgresData]
    public async Task StoresAndUpdatesIndependentIdentities(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        BotAction reply = Action(
            "reply",
            "default",
            "record1");
        BotAction like = Action(
            "like",
            "default",
            "record2");
        BotAction secondLike = Action(
            "like",
            "second",
            "record3") with
        { PayloadVersion = 2 };
        Assert.Equal(
            3,
            await store.Database.HandleAsync(
            11,
            [reply, like, secondLike],
            null,
            CancellationToken.None));
        Assert.Equal(
            0,
            await store.Database.HandleAsync(
            12,
            [reply with { RecordKey = "ignored" }, like, secondLike],
            null,
            CancellationToken.None));
        Assert.Equal(
            1,
            await store.Database.MarkAttemptAsync(
            like,
            CancellationToken.None));
        Assert.Equal(
            1,
            await store.Database.RetryAsync(
            like,
            DateTimeOffset.UtcNow.AddHours(1),
            "retry only this like",
            CancellationToken.None));
        Assert.Equal(
            1,
            await store.Database.FinishAsync(
            reply,
            "sent",
            $"at://{TestRecords.BotDid}/app.bsky.feed.post/reply",
            "confirmed",
            CancellationToken.None));
        BotAction durable = (await store.Database.NextWorkAsync(CancellationToken.None))!;
        Assert.Equal(
            secondLike,
            durable);
        await store.Database.ReleaseAsync(CancellationToken.None);
        IDatabaseProvider restarted = store.NewProvider();
        Assert.NotNull(await restarted.AcquireOrRenewAsync(CancellationToken.None));
        await store.MakeReadyAsync();
        Assert.Equal(
            secondLike,
            await restarted.NextWorkAsync(CancellationToken.None));
        Assert.Equal(
            0,
            await restarted.HandleAsync(
            13,
            [],
            null,
            CancellationToken.None,
            secondLike.SourceUri));
        durable = (await restarted.NextWorkAsync(CancellationToken.None))!;
        Assert.True(durable.TargetDeleted);
        Assert.Equal(
            0,
            await restarted.MarkAttemptAsync(
            durable,
            CancellationToken.None));
        Assert.Equal(
            1,
            await restarted.FinishAsync(
            durable,
            "not-sent",
            null,
            "target deleted",
            CancellationToken.None));
        Assert.Equal(
            1,
            await store.CountAsync("status = 'sent' AND target_deleted = " + store.FalseSql));
        Assert.Equal(
            1,
            await store.CountAsync("status = 'uncertain' AND attempts = 1 AND target_deleted = " + store.TrueSql));
        Assert.Equal(
            1,
            await store.CountAsync("status = 'not-sent' AND action_key = 'second'"));
        await store.MakeRetriesDueAsync();
        durable = (await restarted.NextWorkAsync(CancellationToken.None))!;
        Assert.Equal(
            like with { Status = "uncertain", Attempts = 1, TargetDeleted = true },
            durable);
    }

    /// <summary>Rolls back earlier inserts, deletion flags, audits, and progress when a later action fails.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [ActionPostgresData]
    public async Task RollsBackCompleteBatch(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        BotAction original = Action(
            "reply",
            "default",
            "reserved");
        await store.Database.HandleAsync(
            11,
            [original],
            null,
            CancellationToken.None);
        BotAction first = Action(
            "like",
            "default",
            "new") with
        { SourceSequence = 12 };
        BotAction failing = Action(
            "repost",
            "default",
            original.RecordKey) with
        { SourceSequence = 12, RecordJson = "invalid-json" };
        Exception failure = await Assert.ThrowsAnyAsync<Exception>(() => store.Database.HandleAsync(
            12,
            [first, failing],
            "must roll back",
            CancellationToken.None,
            original.SourceUri));
        Assert.True(failure is DbException or JsonException);
        Assert.Equal(
            11,
            (await store.Database.LoadStateAsync(CancellationToken.None)).ProcessedThrough);
        Assert.Equal(
            1,
            await store.CountAsync("1 = 1"));
        Assert.False((await store.Database.NextWorkAsync(CancellationToken.None))!.TargetDeleted);
        Assert.Equal(
            0,
            await store.ScalarAsync("SELECT count(*) FROM skipped_data"));
        Assert.Equal(
            2,
            await store.Database.HandleAsync(
            12,
            [first, failing with { RecordJson = "{}" }],
            null,
            CancellationToken.None));
    }

    /// <summary>Rejects invalid new action metadata without advancing progress or retaining earlier inserts.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [ActionPostgresData]
    public async Task ValidatesNewActionsAtomically(string provider)
    {
        await using Store store = await Store.CreateAsync(provider);
        BotAction first = Action(
            "reply",
            "default",
            "first");
        BotAction next = Action(
            "like",
            "default",
            "next");
        BotAction[] invalid =
        [
            next with { ActionKind = "" },
            next with { ActionKind = new string(
                'a',
                129) },
            next with { ActionKey = "" },
            next with { ActionKey = new string(
                'a',
                129) },
            next with { PayloadVersion = 0 },
            next with { SourceSequence = 12 }
        ];
        foreach (BotAction action in invalid)
        {
            await Assert.ThrowsAsync<ArgumentException>(() => store.Database.HandleAsync(
                11,
                [first, action],
                null,
                CancellationToken.None));
            Assert.Equal(
                10,
                (await store.Database.LoadStateAsync(CancellationToken.None)).ProcessedThrough);
            Assert.Equal(
                0,
                await store.CountAsync("1 = 1"));
        }
    }

    /// <summary>Migrates queued legacy replies in place and permits other actions on their source afterward.</summary>
    /// <param name="provider">The provider under test.</param>
    [Theory]
    [InlineData("Sqlite")]
    [MySqlData]
    [ActionPostgresData]
    public async Task MigratesQueuedLegacyReplies(string provider)
    {
        await using Store store = await Store.CreateAsync(
            provider,
            legacy: true);
        BotAction durable = (await store.Database.NextWorkAsync(CancellationToken.None))!;
        Assert.Equal(
            Action(
                "reply",
                "default",
                "legacy") with
            {
                Status = "uncertain",
                Attempts = 2,
                TargetDeleted = true
            },
            durable);
        BotAction bookmark = Action(
            "bookmark",
            "default",
            "legacy");
        Assert.Equal(
            1,
            await store.Database.HandleAsync(
            11,
            [bookmark],
            null,
            CancellationToken.None));
        Assert.Equal(
            2,
            await store.CountAsync("1 = 1"));
        Assert.Equal(
            1,
            await store.Database.FinishAsync(
            durable,
            "not-sent",
            null,
            "legacy target deleted",
            CancellationToken.None));
        Assert.Equal(
            "bookmark",
            (await store.Database.NextWorkAsync(CancellationToken.None))!.ActionKind);
    }

    private static BotAction Action(
        string kind,
        string key,
        string recordKey) => new(
            $"at://{TestRecords.AuthorDid}/app.bsky.feed.post/source",
            11,
            TestRecords.AuthorDid,
            recordKey,
            "{}",
            "pending",
            0,
            ActionKind: kind,
            ActionKey: key);

    private sealed class Store : IAsyncDisposable
    {
        private readonly string _provider;
        private readonly string _name = "action_test_" + Guid.NewGuid().ToString("N");
        private readonly string _connectionString;
        private readonly string? _admin;
        private readonly NpgsqlDataSource? _source;
        private readonly BotOptions _options = TestRecords.Options();

        public IDatabaseProvider Database { get; }
        public string TrueSql => _provider == "Postgres" ? "true" : "1";
        public string FalseSql => _provider == "Postgres" ? "false" : "0";

        private Store(string provider)
        {
            _provider = provider;
            if (provider == "Sqlite")
            {
                _connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        _name + ".db"),
                    Pooling = false
                }.ConnectionString;
            }
            else if (provider == "MySql")
            {
                _admin = Environment.GetEnvironmentVariable("BOTSDK_TEST_MYSQL_CONNECTION")
                    ?? throw new InvalidOperationException("Missing disposable MySQL connection.");
                _connectionString = new MySqlConnectionStringBuilder(_admin)
                {
                    Database = _name,
                    Pooling = false
                }.ConnectionString;
            }
            else
            {
                _admin = Environment.GetEnvironmentVariable("BOTSDK_TEST_POSTGRESQL_CONNECTION")
                    ?? throw new InvalidOperationException("Missing isolated-schema PostgreSQL connection.");
                _connectionString = new NpgsqlConnectionStringBuilder(_admin)
                {
                    SearchPath = _name,
                    Pooling = false
                }.ConnectionString;
                _source = NpgsqlDataSource.Create(_connectionString);
            }
            Database = NewProvider();
        }

        public IDatabaseProvider NewProvider() => _provider switch
        {
            "Sqlite" => new SqliteDatabaseProvider(
                _connectionString,
                _options,
                TimeProvider.System,
                NullLogger<SqliteDatabaseProvider>.Instance),
            "MySql" => new MySqlDatabaseProvider(
                _connectionString,
                _options,
                TimeProvider.System,
                NullLogger<MySqlDatabaseProvider>.Instance),
            _ => new PostgreSqlDatabaseProvider(
                _source!,
                _options,
                TimeProvider.System,
                NullLogger<PostgreSqlDatabaseProvider>.Instance)
        };

        private DbConnection Connection(bool admin = false) => _provider switch
        {
            "Sqlite" => new SqliteConnection(_connectionString),
            "MySql" => new MySqlConnection(admin ? _admin : _connectionString),
            _ => new NpgsqlConnection(admin ? _admin : _connectionString)
        };

        public static async Task<Store> CreateAsync(
            string provider,
            bool legacy = false)
        {
            Store store = new(provider);
            if (provider != "Sqlite")
            {
                await store.SqlAsync(
                    provider == "MySql"
                        ? $"CREATE DATABASE `{store._name}`"
                        : $"CREATE SCHEMA \"{store._name}\"",
                    admin: true);
            }
            if (legacy)
            {
                await store.CreateLegacyAsync();
            }
            await store.Database.InitializeAsync(CancellationToken.None);
            await store.Database.InitializeAsync(CancellationToken.None);
            Assert.NotNull(await store.Database.AcquireOrRenewAsync(CancellationToken.None));
            await store.MakeReadyAsync();
            await store.Database.LoadStateAsync(CancellationToken.None);
            await store.Database.SaveRequestAsync(
                new SnapshotRequest { AfterSeq = 10 },
                CancellationToken.None);

            return store;
        }

        private async Task CreateLegacyAsync()
        {
            string sql = _provider == "Postgres"
                ? """
                CREATE TABLE reply_work (
                    source_uri text PRIMARY KEY, source_sequence bigint NOT NULL, source_did text NOT NULL,
                    record_key text NOT NULL UNIQUE, record jsonb NOT NULL, status text NOT NULL DEFAULT 'pending',
                    attempts integer NOT NULL DEFAULT 0, next_retry timestamptz NOT NULL DEFAULT now(),
                    outcome text, reply_uri text, completed_at timestamptz,
                    target_deleted boolean NOT NULL DEFAULT false)
                """
                : $"""
                CREATE TABLE reply_work (
                    source_hash {(_provider == "MySql" ? "BINARY(32)" : "BLOB")} PRIMARY KEY,
                    source_uri {(_provider == "MySql" ? "LONGTEXT" : "TEXT")} NOT NULL,
                    source_sequence BIGINT NOT NULL, source_did TEXT NOT NULL,
                    record_key VARCHAR(128) NOT NULL UNIQUE,
                    record {(_provider == "MySql" ? "JSON" : "TEXT")} NOT NULL,
                    root_uri TEXT, status VARCHAR(16) NOT NULL DEFAULT 'pending',
                    attempts INTEGER NOT NULL DEFAULT 0, next_retry BIGINT NOT NULL,
                    outcome TEXT, reply_uri TEXT, completed_at BIGINT, target_deleted INTEGER NOT NULL DEFAULT 0)
                """;
            await SqlAsync(sql);
            BotAction action = Action(
                "reply",
                "default",
                "legacy");
            await using DbConnection connection = Connection();
            await connection.OpenAsync();
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = _provider == "Postgres"
                ? """
                INSERT INTO reply_work(source_uri, source_sequence, source_did, record_key, record,
                    status, attempts, next_retry, target_deleted)
                VALUES(@uri, 11, @did, 'legacy', CAST(@record AS jsonb), 'uncertain', 2, now(), true)
                """
                : """
                INSERT INTO reply_work(source_hash, source_uri, source_sequence, source_did, record_key,
                    record, status, attempts, next_retry, target_deleted)
                VALUES(@hash, @uri, 11, @did, 'legacy', @record, 'uncertain', 2, 0, 1)
                """;
            foreach ((string name, object value) in new (string, object)[]
            {
                ("@uri", action.SourceUri.ToString()),
                ("@did", action.SourceDid),
                ("@record", action.RecordJson)
            })
            {
                DbParameter parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }
            if (_provider != "Postgres")
            {
                DbParameter hash = command.CreateParameter();
                hash.ParameterName = "@hash";
                hash.Value = SHA256.HashData(Encoding.UTF8.GetBytes(action.SourceUri.ToString()));
                command.Parameters.Add(hash);
            }
            await command.ExecuteNonQueryAsync();
        }

        public Task MakeReadyAsync() => SqlAsync(_provider == "Postgres"
            ? "UPDATE bot_ownership SET ready_at = now()"
            : "UPDATE bot_ownership SET ready_at = 0");

        public Task AddLegacyRecordKeyIndexAsync() =>
            SqlAsync("CREATE UNIQUE INDEX legacy_record_key ON reply_work(record_key)");

        public Task MakeRetriesDueAsync() => SqlAsync(_provider == "Postgres"
            ? "UPDATE reply_work SET next_retry = now()"
            : "UPDATE reply_work SET next_retry = 0");

        public Task<long> CountAsync(string condition) =>
            ScalarAsync($"SELECT count(*) FROM reply_work WHERE {condition}");

        public async Task<long> ScalarAsync(string sql)
        {
            await using DbConnection connection = Connection();
            await connection.OpenAsync();
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }

        public async Task SqlAsync(
            string sql,
            bool admin = false)
        {
            await using DbConnection connection = Connection(admin);
            await connection.OpenAsync();
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_provider == "Sqlite")
            {
                string path = new SqliteConnectionStringBuilder(_connectionString).DataSource;
                File.Delete(path);
                File.Delete(path + "-wal");
                File.Delete(path + "-shm");
            }
            else
            {
                await SqlAsync(
                    _provider == "MySql"
                        ? $"DROP DATABASE `{_name}`"
                        : $"DROP SCHEMA \"{_name}\" CASCADE",
                    admin: true);
                if (_source is not null)
                {
                    await _source.DisposeAsync();
                }
            }
        }
    }
}
