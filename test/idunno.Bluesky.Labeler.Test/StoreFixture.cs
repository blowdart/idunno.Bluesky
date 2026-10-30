// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Diagnostics.Metrics;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

using MySqlConnector;

using Npgsql;

namespace idunno.Bluesky.Labeler.Test;

internal sealed class StoreFixture : IAsyncDisposable
{
    internal static readonly Did s_issuer = new("did:plc:label-issuer");
    private readonly string _database = "label_test_" + Guid.NewGuid().ToString("N");
    private readonly List<ServiceProvider> _providers = [];
    private string _admin = string.Empty;
    internal string Engine { get; }
    internal string ConnectionString { get; private set; } = string.Empty;
    internal ILabelStore Store { get; private set; } = null!;
    internal ServiceProvider Services => _providers[0];
    /// <summary>Gets the shared database-time replacement used by explicitly controlled moderation fixtures.</summary>
    internal FakeTimeProvider? DatabaseClock { get; private set; }
    private StoreFixture(string engine) => Engine = engine;

    /// <summary>Creates a moderation fixture whose original and restarted stores share one fixed database clock.</summary>
    internal static Task<StoreFixture> CreateModerationAsync(string engine) =>
        CreateAsync(
            engine: engine,
            databaseClock: new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));

    internal static async Task<StoreFixture> CreateAsync(
        string engine,
        bool initialize = true,
        FakeTimeProvider? databaseClock = null)
    {
        StoreFixture fixture = new(engine) { DatabaseClock = databaseClock };
        if (engine == "sqlite")
        {
            fixture.ConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(Path.GetTempPath(), fixture._database + ".db"),
                Pooling = false
            }.ConnectionString;
        }
        else
        {
            string variable = engine == "mysql" ? "LABELER_TEST_MYSQL_CONNECTION" : "LABELER_TEST_POSTGRESQL_CONNECTION";
            fixture._admin = Environment.GetEnvironmentVariable(variable)
                ?? throw new InvalidOperationException($"Set {variable} to an administrative connection on a disposable Docker database server. Real-engine conformance never skips.");
            if (engine == "postgresql")
            {
                fixture._admin = new NpgsqlConnectionStringBuilder(fixture._admin)
                {
                    GssEncryptionMode = GssEncryptionMode.Disable
                }.ConnectionString;
            }
            await using DbConnection connection = fixture.AdminConnection();
            await connection.OpenAsync();
            await using DbCommand command = connection.CreateCommand();
#pragma warning disable CA2100 // The identifier contains only a fixed prefix and generated hexadecimal digits.
            command.CommandText = $"CREATE DATABASE {fixture._database}";
#pragma warning restore CA2100
            await command.ExecuteNonQueryAsync();
            fixture.ConnectionString = engine == "mysql"
                ? new MySqlConnectionStringBuilder(fixture._admin) { Database = fixture._database, Pooling = false }.ConnectionString
                : new NpgsqlConnectionStringBuilder(fixture._admin) { Database = fixture._database, Pooling = false }.ConnectionString;
        }

        fixture.Store = fixture.NewStore();
        try
        {
            if (initialize)
            {
                await fixture.Store.InitializeAsync(s_issuer);
            }

            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    internal ILabelStore NewStore(FakeTimeProvider? databaseClock = null)
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddLogging(builder => builder.AddFakeLogging(options => options.CollectRecordsForDisabledLogLevels = true));
        switch (Engine)
        {
            case "sqlite":
                services.AddSqliteLabelStorage(ConnectionString);
                break;
            case "mysql":
                services.AddMySqlLabelStorage(ConnectionString);
                break;
            case "postgresql":
                services.AddPostgreSqlLabelStorage(ConnectionString);
                break;
            default:
                throw new InvalidOperationException("Unknown test engine.");
        }

        ServiceProvider provider = services.BuildServiceProvider();
        _providers.Add(provider);
        ILabelStore store = provider.GetRequiredService<ILabelStore>();
        switch (store)
        {
            case SqliteLabelStore sqlite:
                sqlite.DatabaseClock = databaseClock ?? DatabaseClock;
                break;
            case PostgreSqlLabelStore postgresql:
                postgresql.DatabaseClock = databaseClock ?? DatabaseClock;
                break;
            case MySqlLabelStore mysql:
                mysql.DatabaseClock = databaseClock ?? DatabaseClock;
                break;
        }

        return store;
    }

    internal IMeterFactory MeterFactory => Services.GetRequiredService<IMeterFactory>();

    /// <summary>Drains a known publication prefix using explicit database time and the production budgets.</summary>
    internal async Task<LabelStoreState> PublishThroughAsync(long? through = null)
    {
        FakeTimeProvider clock = DatabaseClock ?? throw new InvalidOperationException("Publication draining requires a controlled database clock.");
        CancellationToken token = TestContext.Current.CancellationToken;
        LabelStoreState state = await Store.GetStateAsync(token);
        long target = through ?? state.IssuedThrough;
        while (state.PublishedThrough < target)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            state = await Store.PublishAsync((int)Math.Min(250, target - state.PublishedThrough), token);
        }

        return state;
    }

    internal void SetCommitHooks(
        Func<DbConnection, DbTransaction, CancellationToken, Task>? beforeCommit = null,
        Func<DbConnection, CancellationToken, Task>? afterCommit = null)
    {
        switch (Store)
        {
            case SqliteLabelStore sqlite:
                sqlite.BeforeCommitAsync = beforeCommit;
                sqlite.AfterCommitAsync = afterCommit;
                break;
            case PostgreSqlLabelStore postgresql:
                postgresql.BeforeCommitAsync = beforeCommit;
                postgresql.AfterCommitAsync = afterCommit;
                break;
            case MySqlLabelStore mysql:
                mysql.BeforeCommitAsync = beforeCommit;
                mysql.AfterCommitAsync = afterCommit;
                break;
            default:
                throw new InvalidOperationException("Unknown test store.");
        }
    }

    private DbConnection AdminConnection() => Engine == "mysql"
        ? new MySqlConnection(_admin) : new NpgsqlConnection(_admin);

    internal DbConnection Connection() => Engine switch
    {
        "mysql" => new MySqlConnection(ConnectionString),
        "postgresql" => new NpgsqlConnection(ConnectionString),
        _ => new SqliteConnection(ConnectionString)
    };

    internal async Task ExecuteAsync(string sql)
    {
        await using DbConnection connection = Connection();
        await connection.OpenAsync();
        await using DbCommand command = connection.CreateCommand();
#pragma warning disable CA2100 // Tests supply fixed fixture-owned SQL only.
        command.CommandText = sql;
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (ServiceProvider provider in _providers)
        {
            await provider.DisposeAsync();
        }

        if (Engine == "sqlite")
        {
            string path = new SqliteConnectionStringBuilder(ConnectionString).DataSource;
            foreach (string suffix in new[] { string.Empty, "-wal", "-shm" })
            {
                File.Delete(path + suffix);
            }
        }
        else
        {
            await using DbConnection connection = AdminConnection();
            await connection.OpenAsync();
            await using DbCommand command = connection.CreateCommand();
#pragma warning disable CA2100 // The fixture owns this generated database name.
            command.CommandText = $"DROP DATABASE {_database}";
#pragma warning restore CA2100
            await command.ExecuteNonQueryAsync();
        }
    }
}
